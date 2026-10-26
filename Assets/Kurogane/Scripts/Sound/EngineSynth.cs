using System.Threading;
using UnityEngine;

namespace Kurogane.Sound
{
    public enum EngineType { Cuatro, Rotativo, Seis }

    /// <summary>
    /// Sintetizador de motor por código (sin muestras de audio): armónicos de la frecuencia de explosiones,
    /// ruido de combustión, silbido del turbo, válvula de descarga, petardeo, chirrido de ruedas y golpes.
    /// Los parámetros los escribe el hilo principal (<see cref="CarAudio"/>) y los lee el hilo de audio.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class EngineSynth : MonoBehaviour
    {
        public EngineType type;

        // ---- Parámetros (hilo principal → hilo de audio) ----
        [System.NonSerialized] public volatile float rpm = 1000;
        [System.NonSerialized] public volatile float throttle;
        [System.NonSerialized] public volatile float boost;
        [System.NonSerialized] public volatile float squeal;
        [System.NonSerialized] public volatile float volume;
        [System.NonSerialized] public volatile float pan;

        int popRequests, blowOffRequests;
        volatile float impactRequest;

        public void Pops() => Interlocked.Increment(ref popRequests);
        public void BlowOff() => Interlocked.Increment(ref blowOffRequests);
        public void Impact(float strength) => impactRequest = Mathf.Max(impactRequest, Mathf.Clamp01(strength));

        // Peso de cada armónico de la frecuencia de explosiones y explosiones por vuelta según el motor
        static readonly float[] Inline4 = { 1, 0.5f, 0.35f, 0.2f, 0.15f, 0.1f, 0.06f, 0.04f };
        static readonly float[] Rotary = { 1, 0.12f, 0.6f, 0.1f, 0.42f, 0.08f, 0.3f, 0.06f };
        static readonly float[] Inline6 = { 1, 0.4f, 0.25f, 0.15f, 0.1f, 0.06f, 0.04f, 0.02f };

        // ---- Estado del hilo de audio ----
        float sampleRate = 48000;
        double phase, subPhase, whistlePhase, squealPhase, vibratoPhase, thudPhase;
        float curRpm = 1000, curThrottle, curBoost, curSqueal, curVolume;
        float lowpass, noiseLow, popLow;
        float blowEnv, popEnv, popGap, impactEnv, impactStrength;
        int popsLeft;
        uint rng = 0x9E3779B9u;

        void Awake()
        {
            sampleRate = AudioSettings.outputSampleRate;
            var src = GetComponent<AudioSource>();
            // Clip de silencio en bucle: garantiza que la fuente esté sonando y Unity llame a OnAudioFilterRead
            int rate = Mathf.Max(8000, (int)sampleRate);
            src.clip = AudioClip.Create("Silencio", rate, 1, rate, false);
            src.playOnAwake = true;
            src.loop = true;
            src.spatialBlend = 0;
            src.dopplerLevel = 0;
        }

        void OnEnable() => GetComponent<AudioSource>().Play();

        float Noise()
        {
            // xorshift: UnityEngine.Random no se puede usar fuera del hilo principal
            rng ^= rng << 13;
            rng ^= rng >> 17;
            rng ^= rng << 5;
            return rng / (float)uint.MaxValue * 2 - 1;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            float[] weights = type == EngineType.Rotativo ? Rotary : type == EngineType.Seis ? Inline6 : Inline4;
            float firesPerRev = type == EngineType.Seis ? 3 : 2;
            float sub = type == EngineType.Cuatro ? 0.16f : type == EngineType.Rotativo ? 0.05f : 0.07f;
            float roughness = type == EngineType.Rotativo ? 0.5f : 0.3f;

            float targetRpm = rpm, targetThrottle = throttle, targetBoost = boost, targetSqueal = squeal, targetVolume = volume;
            float p = Mathf.Clamp(pan, -1, 1);
            float gainL = Mathf.Sqrt(0.5f * (1 - p)), gainR = Mathf.Sqrt(0.5f * (1 + p));

            int pops = Interlocked.Exchange(ref popRequests, 0);
            if (pops > 0) { popsLeft = 3 + (int)((Noise() + 1) * 1.5f); popGap = 0; }
            if (Interlocked.Exchange(ref blowOffRequests, 0) > 0) blowEnv = 1;
            float imp = impactRequest;
            if (imp > 0) { impactRequest = 0; impactEnv = 1; impactStrength = imp; thudPhase = 0; }

            float dt = 1f / sampleRate;
            float smooth = 1 - Mathf.Exp(-dt * 30);
            float blowDecay = Mathf.Exp(-dt / 0.14f), popDecay = Mathf.Exp(-dt / 0.018f), impactDecay = Mathf.Exp(-dt / 0.22f);
            const double TwoPi = System.Math.PI * 2;

            for (int i = 0; i < data.Length; i += channels)
            {
                curRpm += (targetRpm - curRpm) * smooth;
                curThrottle += (targetThrottle - curThrottle) * smooth;
                curBoost += (targetBoost - curBoost) * smooth;
                curSqueal += (targetSqueal - curSqueal) * smooth;
                curVolume += (targetVolume - curVolume) * smooth;

                // Motor: armónicos de la frecuencia de explosiones, más brillantes con el gas pisado
                float fire = curRpm / 60f * firesPerRev;
                phase += fire * dt;
                if (phase > 1) phase -= 1;
                subPhase += fire * 0.5f * dt;
                if (subPhase > 1) subPhase -= 1;
                float engine = 0;
                for (int k = 0; k < weights.Length; k++)
                {
                    float bright = k == 0 ? 1 : 0.45f + curThrottle * 0.9f;
                    engine += weights[k] * bright * (float)System.Math.Sin(TwoPi * (k + 1) * phase);
                }
                engine += sub * (float)System.Math.Sin(TwoPi * subPhase);

                // Combustión: ruido que golpea al principio de cada explosión
                float n = Noise();
                noiseLow += (n - noiseLow) * 0.2f;
                float pulse = 1 - (float)phase;
                engine += noiseLow * pulse * pulse * pulse * (roughness + curThrottle * 0.6f);

                float load = 0.3f + 0.5f * curThrottle + 0.2f * Mathf.Clamp01((curRpm - 1000) / 7000);
                float cutoff = 0.08f + 0.25f * curThrottle + 0.15f * Mathf.Clamp01(curRpm / 8000);
                lowpass += (engine * load - lowpass) * cutoff;
                float s = lowpass * 0.55f;

                // Turbo: silbido que sube con la presión y descarga al soltar el gas
                if (curBoost > 0.01f)
                {
                    whistlePhase += (1800 + 3600 * curBoost) * dt;
                    if (whistlePhase > 1) whistlePhase -= 1;
                    s += 0.05f * curBoost * curBoost * (float)System.Math.Sin(TwoPi * whistlePhase);
                }
                if (blowEnv > 0.001f)
                {
                    s += (n - noiseLow) * 0.22f * blowEnv;
                    blowEnv *= blowDecay;
                }

                // Petardeo por el escape
                if (popsLeft > 0 || popEnv > 0.001f)
                {
                    popGap -= dt;
                    if (popGap <= 0 && popsLeft > 0)
                    {
                        popEnv = 1;
                        popsLeft--;
                        popGap = 0.04f + (Noise() + 1) * 0.045f;
                    }
                    popLow += (n - popLow) * 0.35f;
                    s += popLow * 0.9f * popEnv;
                    popEnv *= popDecay;
                }

                // Chirrido de ruedas: tono con vibrato y algo de ruido
                if (curSqueal > 0.01f)
                {
                    vibratoPhase += 6.5f * dt;
                    if (vibratoPhase > 1) vibratoPhase -= 1;
                    float f = 880 + 110 * (float)System.Math.Sin(TwoPi * vibratoPhase);
                    squealPhase += f * dt;
                    if (squealPhase > 1) squealPhase -= 1;
                    s += curSqueal * (0.09f * (float)System.Math.Sin(TwoPi * squealPhase) + 0.03f * n);
                }

                // Golpe contra el muro: tono grave y ruido sordo
                if (impactEnv > 0.001f)
                {
                    thudPhase += 55 * dt;
                    s += impactStrength * impactEnv * (0.6f * (float)System.Math.Sin(TwoPi * thudPhase) + 0.35f * noiseLow);
                    impactEnv *= impactDecay;
                }

                s *= curVolume;
                s /= 1 + Mathf.Abs(s); // saturación suave, nunca pasa de 1

                if (channels >= 2)
                {
                    data[i] = s * gainL;
                    data[i + 1] = s * gainR;
                    for (int c = 2; c < channels; c++) data[i + c] = 0;
                }
                else data[i] = s;
            }
        }
    }
}

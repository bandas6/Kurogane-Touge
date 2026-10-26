using System.Threading;
using Kurogane.Sound;
using UnityEngine;

namespace Kurogane.Atmosphere
{
    /// <summary>
    /// Sonido de lluvia por código: ruido filtrado con algún golpe de gota y truenos lejanos. Más fuerte en la vista
    /// de capó (dentro del coche) y casi apagado en el túnel. Lo regula WeatherSystem.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class RainAudio : MonoBehaviour
    {
        [System.NonSerialized] public volatile float volume;

        int thunderRequests;
        float sampleRate = 48000;
        float low, band, cur, thunderEnv, thunderLow, thunderDelay;
        uint rng = 0x2545F491u;

        public void Thunder() => Interlocked.Increment(ref thunderRequests);

        void Awake()
        {
            sampleRate = AudioSettings.outputSampleRate;
            var src = GetComponent<AudioSource>();
            int rate = Mathf.Max(8000, (int)sampleRate);
            src.clip = AudioClip.Create("Silencio", rate, 1, rate, false);
            src.loop = true;
            src.playOnAwake = true;
            src.spatialBlend = 0;
        }

        void OnEnable() => GetComponent<AudioSource>().Play();

        float Noise()
        {
            rng ^= rng << 13;
            rng ^= rng >> 17;
            rng ^= rng << 5;
            return rng / (float)uint.MaxValue * 2 - 1;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            float target = volume * CarAudio.MasterVolume;
            if (Interlocked.Exchange(ref thunderRequests, 0) > 0) thunderDelay = 0.6f + (Noise() + 1) * 1.2f;
            float dt = 1f / sampleRate, smooth = 1 - Mathf.Exp(-dt * 4);
            for (int i = 0; i < data.Length; i += channels)
            {
                cur += (target - cur) * smooth;
                float n = Noise();
                low += (n - low) * 0.08f;     // rumor grave
                band += (n - band) * 0.5f;    // siseo
                float s = (low * 1.2f + (n - band) * 0.35f) * 0.35f;
                if (Noise() > 0.9996f) s += Noise() * 0.6f; // gota suelta cerca
                if (thunderDelay > 0)
                {
                    thunderDelay -= dt;
                    if (thunderDelay <= 0) thunderEnv = 1;
                }
                if (thunderEnv > 0.001f)
                {
                    thunderLow += (n - thunderLow) * 0.015f;
                    s += thunderLow * 4f * thunderEnv;
                    thunderEnv *= 1 - dt / 1.6f;
                }
                s *= cur;
                s /= 1 + Mathf.Abs(s);
                for (int c = 0; c < channels; c++) data[i + c] = s * (c % 2 == 0 ? 1 : 0.9f);
            }
        }
    }
}

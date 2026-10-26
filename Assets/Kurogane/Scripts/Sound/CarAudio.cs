using Kurogane.Cars;
using UnityEngine;

namespace Kurogane.Sound
{
    /// <summary>
    /// Conecta un coche (jugador o rival) con su <see cref="EngineSynth"/>: vueltas, gas, turbo, derrape y golpes.
    /// El rival suena según la distancia y el lado respecto al oyente.
    /// </summary>
    [RequireComponent(typeof(EngineSynth))]
    public class CarAudio : MonoBehaviour
    {
        /// <summary>Volumen general de motores y efectos (0-1), lo fija el menú de opciones.</summary>
        public static float MasterVolume = 0.8f;

        [Tooltip("Metros a los que el rival suena a la mitad de volumen.")]
        public float rivalHalfDistance = 14;

        EngineSynth synth;
        CarController player;
        RivalAI rival;
        AudioListener listener;
        float boost;

        void Awake()
        {
            synth = GetComponent<EngineSynth>();
            player = GetComponent<CarController>();
            rival = GetComponent<RivalAI>();
            if (player != null)
            {
                player.Upshift += OnUpshift;
                player.LiftOff += OnLiftOff;
                player.WallHit += OnWallHit;
            }
        }

        void OnDestroy()
        {
            if (player == null) return;
            player.Upshift -= OnUpshift;
            player.LiftOff -= OnLiftOff;
            player.WallHit -= OnWallHit;
        }

        CarSpec Spec => player != null ? player.spec : rival != null ? rival.spec : null;

        static EngineType EngineFor(CarModel model) =>
            model == CarModel.Coupe ? EngineType.Rotativo : model == CarModel.Sedan ? EngineType.Seis : EngineType.Cuatro;

        void Update()
        {
            var spec = Spec;
            if (spec == null || Time.timeScale == 0) { synth.volume = 0; return; }
            synth.type = EngineFor(spec.model);

            float rpm, gas, slip, speed;
            if (player != null)
            {
                rpm = player.Rpm;
                gas = player.GasInput;
                slip = Mathf.Abs(player.Slip);
                speed = player.Speed;
            }
            else
            {
                speed = rival.Speed;
                rpm = RivalRpm(spec, speed);
                gas = rival.Braking ? 0.1f : 0.85f;
                slip = Mathf.Abs(rival.YawOffset);
            }

            // El turbo carga con gas y vueltas, y se vacía rápido al soltar
            float target = spec.turbo && gas > 0.5f ? Mathf.InverseLerp(3500, 7500, rpm) : 0;
            boost = Mathf.MoveTowards(boost, target, Time.deltaTime * (target > boost ? 1.2f : 4));

            synth.rpm = rpm;
            synth.throttle = gas;
            synth.boost = boost;
            synth.squeal = speed > 8 ? Mathf.InverseLerp(0.18f, 0.5f, slip) : 0;

            if (player != null)
            {
                synth.volume = MasterVolume;
                synth.pan = 0;
                return;
            }
            if (listener == null) listener = FindAnyObjectByType<AudioListener>();
            if (listener == null) { synth.volume = 0; return; }
            var to = transform.position - listener.transform.position;
            float d = to.magnitude;
            synth.volume = MasterVolume * 0.85f / (1 + d / rivalHalfDistance);
            synth.pan = d > 0.1f ? Vector3.Dot(listener.transform.right, to / d) * 0.7f : 0;
        }

        /// <summary>El rival no simula la caja: se estima la marcha y las vueltas a partir de la velocidad.</summary>
        static float RivalRpm(CarSpec spec, float speed)
        {
            for (int g = 0; g < spec.Gears; g++)
                if (speed <= spec.gearTopSpeed[g] * 0.92f || g == spec.Gears - 1)
                    return 1000 + 7200 * Mathf.Clamp01(speed / spec.gearTopSpeed[g]);
            return 1000;
        }

        void OnUpshift()
        {
            if (Spec != null && Spec.turbo && boost > 0.4f) synth.BlowOff();
        }

        void OnLiftOff()
        {
            synth.Pops();
            if (Spec != null && Spec.turbo && boost > 0.3f) synth.BlowOff();
        }

        void OnWallHit(float impact, Vector3 point) => synth.Impact(impact / 15f);
    }
}

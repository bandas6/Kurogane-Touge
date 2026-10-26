using Kurogane.Track;
using UnityEngine;

namespace Kurogane.Cars
{
    public enum RivalSkill { Facil, Normal, Leyenda }

    /// <summary>
    /// Rival que sigue la trazada precalculada con el perfil de velocidad de la pista.
    /// Se ajusta un poco a la distancia con el jugador para que la batalla no se rompa.
    /// </summary>
    public class RivalAI : MonoBehaviour, ICarObstacle
    {
        bool ICarObstacle.Active => isActiveAndEnabled;
        Vector3 ICarObstacle.Position => transform.position;

        public CarSpec spec;
        public RivalSkill skill = RivalSkill.Normal;

        public TrackData Track { get; private set; }
        public CarController Player { get; set; }
        public bool Running { get; set; }
        /// <summary>Metros recorridos sobre la línea central.</summary>
        public float Distance { get; private set; }
        public float Speed { get; private set; }
        public int Index { get; private set; }
        public float Heading { get; private set; }
        public float YawOffset { get; private set; }
        public bool Finished { get; private set; }
        public float FinishTime { get; private set; } = float.PositiveInfinity;
        public bool Braking { get; private set; }
        public Vector3 Velocity => new Vector3(Mathf.Sin(Heading), 0, Mathf.Cos(Heading)) * Speed;
        /// <summary>Lo usa el RaceManager para fijar el tiempo de llegada.</summary>
        public System.Func<float> Clock { get; set; }
        /// <summary>Agarre del clima: la velocidad en curva se escala por su raíz, para que en mojado no vaya imposible.</summary>
        public float GripScale { get; set; } = 1;

        static float SkillFactor(RivalSkill s) => s == RivalSkill.Facil ? 0.83f : s == RivalSkill.Normal ? 0.92f : 1.0f;

        /// <summary>Carril donde aparca tras la meta (derecha), lejos del carril izquierdo por el que llega el jugador.</summary>
        const float ParkLane = 3.6f;
        float park;

        public void Init(TrackData track, CarSpec carSpec, float startDistance)
        {
            Track = track;
            spec = carSpec;
            Distance = startDistance;
            Speed = 0;
            Finished = false;
            FinishTime = float.PositiveInfinity;
            YawOffset = 0;
            park = 0;
            Place();
        }

        public void Bump(float deltaSpeed) => Speed = Mathf.Max(0, Speed + deltaSpeed);

        void Update()
        {
            if (Track == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (Running)
            {
                int i = Mathf.Clamp(Mathf.FloorToInt(Distance / Track.Step), 0, Track.N - 1);
                float sk = SkillFactor(skill);
                if (Player != null)
                {
                    float gap = Distance - Player.Index * Track.Step;
                    if (gap > 90) sk -= 0.07f;
                    else if (gap < -10) sk += 0.05f;
                }
                // Tras la meta sigue despacio (hacia el pueblo, o hacia la cima subiendo) y se para a 100 m, apartado a la derecha
                float target = Finished
                    ? (Distance < Track.TimedLength + 100 ? Mathf.Min(9, Track.VMax[Mathf.Min(Track.N - 1, i + 3)]) : 0)
                    : Mathf.Min(spec.gearTopSpeed[spec.Gears - 1] * 0.97f,
                        Track.VMax[Mathf.Min(Track.N - 1, i + 3)] * sk * (spec.grip > 9 ? 1.03f : 1) * Mathf.Sqrt(GripScale));
                // Subiendo, la pendiente le resta aceleración (Track.RivalAccel); bajando es 1
                float accUp = (Speed < 12 ? 6 : 4.2f) * spec.gearAccel[0] / 10f * Track.RivalAccel;
                Braking = target < Speed - 0.5f;
                Speed += Mathf.Clamp(target - Speed, -10 * dt, accUp * dt);
                Distance += Speed * dt;
                if (Distance >= Track.TimedLength - 6 && !Finished)
                {
                    Finished = true;
                    FinishTime = Clock != null ? Clock() : Time.time;
                }
                Distance = Mathf.Min(Distance, Track.Length - 4);
                if (Finished) park = Mathf.MoveTowards(park, 1, dt * 0.5f);
            }
            Place();
        }

        void Place()
        {
            var d = Track;
            float fi = Mathf.Clamp(Distance / d.Step, 0, d.N - 1.001f);
            int i = Mathf.FloorToInt(fi);
            float f = fi - i;
            var a = d.P[i];
            var b = d.P[Mathf.Min(d.N - 1, i + 1)];
            float lane = Mathf.Lerp(d.Lane[i], d.Lane[Mathf.Min(d.N - 1, i + 1)], f);
            lane = Mathf.Lerp(lane, ParkLane, park);
            var pos = Vector3.Lerp(a.pos, b.pos, f) + a.right * lane;
            float k = 0;
            for (int j = 0; j < 8; j++) k += d.P[Mathf.Clamp(i + j, 0, d.N - 1)].k;
            k /= 8;
            // ángulo de derrape visual: la trompa apunta hacia el interior de la curva
            float driftMul = spec.grip > 9 ? 0.4f : 1;
            float want = Mathf.Sign(k) * Mathf.Min(0.42f, Mathf.Abs(k) * Speed * 1.1f) * driftMul;
            YawOffset = Mathf.Lerp(YawOffset, want, Mathf.Min(1, Time.deltaTime * 3));
            Heading = Mathf.LerpAngle(a.heading * Mathf.Rad2Deg, b.heading * Mathf.Rad2Deg, f) * Mathf.Deg2Rad + YawOffset;
            Index = i;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0, Heading * Mathf.Rad2Deg, 0));
        }
    }
}

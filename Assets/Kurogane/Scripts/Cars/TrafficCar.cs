using Kurogane.Track;
using UnityEngine;

namespace Kurogane.Cars
{
    /// <summary>
    /// Coche del tráfico (bloque D): sigue la línea central con un desplazamiento lateral fijo y una velocidad baja,
    /// frena en las curvas cerradas y se para donde le diga <see cref="StopZone"/> (paso a nivel). Se puede chocar
    /// con él como con el rival.
    /// </summary>
    public class TrafficCar : MonoBehaviour, ICarObstacle
    {
        public TrackData Track { get; private set; }
        /// <summary>Metros sobre la línea central.</summary>
        public float Distance { get; private set; }
        /// <summary>+1 baja (como el jugador), -1 sube en sentido contrario.</summary>
        public int Direction { get; private set; } = -1;
        public float Lane { get; private set; } = 2.6f;
        public float CruiseSpeed { get; private set; } = 12;
        public int Index => Track == null ? 0 : Mathf.Clamp(Mathf.FloorToInt(Distance / Track.Step), 0, Track.N - 1);
        /// <summary>Devuelve true si a esta distancia (m) el coche debe estar parado.</summary>
        public System.Func<float, bool> StopZone { get; set; }

        float speed;
        Vector3 velocity;

        public bool Active => isActiveAndEnabled;
        public Vector3 Position => transform.position;
        public Vector3 Velocity => velocity;
        public void Bump(float deltaSpeed) => speed = Mathf.Max(0, speed + deltaSpeed);

        public void Init(TrackData track, float distance, int direction, float lane, float cruise)
        {
            Track = track;
            Distance = distance;
            Direction = direction;
            Lane = lane;
            CruiseSpeed = cruise;
            speed = cruise;
            Place(0);
        }

        void Update()
        {
            if (Track == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            int ahead = Mathf.Clamp(Index + Direction * 6, 0, Track.N - 1);
            float target = Mathf.Min(CruiseSpeed, Track.VMax[ahead] * 0.6f);
            // Para antes de la zona de parada (mira 10 m por delante)
            if (StopZone != null && StopZone(Distance + Direction * 10)) target = 0;
            speed = Mathf.MoveTowards(speed, target, dt * (target > speed ? 2 : 6));
            Distance = Mathf.Clamp(Distance + Direction * speed * dt, 0, Track.Length - 1);
            Place(dt);
        }

        void Place(float dt)
        {
            var d = Track;
            float fi = Mathf.Clamp(Distance / d.Step, 0, d.N - 1.001f);
            int i = Mathf.FloorToInt(fi);
            float f = fi - i;
            var a = d.P[i];
            var b = d.P[Mathf.Min(d.N - 1, i + 1)];
            var pos = Vector3.Lerp(a.pos, b.pos, f) + a.right * Lane;
            float heading = Mathf.LerpAngle(a.heading * Mathf.Rad2Deg, b.heading * Mathf.Rad2Deg, f) + (Direction < 0 ? 180 : 0);
            if (dt > 0) velocity = (pos - transform.position) / dt;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0, heading, 0));
        }
    }
}

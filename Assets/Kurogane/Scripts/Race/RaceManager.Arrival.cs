using UnityEngine;

namespace Kurogane.Race
{
    /// <summary>
    /// Llegada al pueblo (bloque B): al cruzar la meta el coche sigue solo por el carril izquierdo hasta unos
    /// 40 km/h y frena frente a la tienda de tofu, mientras la cámara corta entre tres planos fijos. Después
    /// salen los resultados. Confirmar la salta.
    /// </summary>
    public partial class RaceManager
    {
        [Header("Llegada")]
        [Tooltip("Velocidad del piloto automático por el pueblo (m/s). 11 ≈ 40 km/h.")]
        public float arrivalSpeed = 11;
        [Tooltip("Carril del coche en el pueblo (negativo = izquierda; en Japón se circula por la izquierda).")]
        public float arrivalLane = -2.4f;

        public bool ArrivalDone { get; private set; } = true;
        /// <summary>El HUD se oculta durante los planos de la llegada (tras el GOAL).</summary>
        public bool HideHud => State == RaceState.Finished && !ArrivalDone && finishTimer > 1.2f;

        int shot;
        float shotTimer, stoppedTime;

        void BeginArrival()
        {
            ArrivalDone = false;
            shot = -1;
            shotTimer = 0;
            stoppedTime = 0;
            player.manualGearbox = false;
            player.AutoBrake = false;
            player.ControlsEnabled = true;
            player.Autopilot = Autopilot;
        }

        void ResetArrival()
        {
            ArrivalDone = true;
            player.Autopilot = null;
            chaseCamera.cinematic = false;
        }

        void SkipArrival()
        {
            ArrivalDone = true;
            finishTimer = Mathf.Max(finishTimer, 1.5f);
        }

        void UpdateArrival(float dt)
        {
            // El primer segundo, cámara normal con el GOAL; luego los planos fijos
            if (finishTimer < 1.2f) return;
            shotTimer -= dt;
            if (shot < 0 || (shotTimer <= 0 && shot < 2)) NextShot();
            if (player.Speed < 0.5f) stoppedTime += dt;
            if ((shot == 2 && stoppedTime > 1.2f) || finishTimer > 30) ArrivalDone = true;
        }

        void NextShot()
        {
            shot++;
            var d = track.Data;
            int i = player.Index;
            Vector3 pos;
            switch (shot)
            {
                case 0: // lateral bajo, por delante: el coche pasa junto a la cámara al final del plano
                {
                    var p = d.P[Mathf.Min(d.N - 1, i + 14)];
                    pos = p.pos + p.right * (d.W + 1.6f) + Vector3.up * 0.8f;
                    chaseCamera.shotFov = 34;
                    shotTimer = 2.8f;
                    break;
                }
                case 1: // picado desde un poste del lado izquierdo
                {
                    var p = d.P[Mathf.Min(d.N - 1, i + 22)];
                    pos = p.pos - p.right * (d.W + 2) + Vector3.up * 8.5f;
                    chaseCamera.shotFov = 44;
                    shotTimer = 3.2f;
                    break;
                }
                default: // frontal, delante de la tienda de tofu
                {
                    var p = d.P[Mathf.Min(d.N - 1, d.StopIndex + 9)];
                    pos = p.pos - p.right * 1.2f + Vector3.up * 1.3f;
                    chaseCamera.shotFov = 36;
                    shotTimer = float.PositiveInfinity;
                    break;
                }
            }
            chaseCamera.cinematic = true;
            chaseCamera.shotPosition = pos;
            if (retro != null) retro.ResetHistory(); // corte de cámara: sin estela del plano anterior
        }

        /// <summary>Sigue la calle por el carril izquierdo y frena para parar en StopIndex.</summary>
        (float gas, float brake, float steer) Autopilot()
        {
            var d = track.Data;
            int i = player.Index;
            int look = Mathf.Min(d.N - 1, i + 5 + Mathf.RoundToInt(player.Speed * 0.35f));
            float lane = Mathf.Lerp(0, arrivalLane, Mathf.InverseLerp(d.FinishIndex, d.TownStart, look));
            var p = d.P[look];
            var to = p.pos + p.right * lane - player.transform.position;
            float want = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            float steer = Mathf.Clamp(Mathf.DeltaAngle(player.Heading * Mathf.Rad2Deg, want) / 30f, -1, 1);

            // Perfil de frenada de 4 m/s² hasta la tienda; los últimos 1,5 m, freno a fondo para no arrastrarse
            float dist = (d.StopIndex - i) * d.Step;
            float target = dist <= 1.5f ? 0 : Mathf.Min(arrivalSpeed, Mathf.Sqrt(2 * 4f * dist));
            float v = player.Speed;
            float gas = v < target - 0.5f ? 0.6f : 0;
            float brake = target <= 0 ? 1 : v > target + 0.4f ? Mathf.Clamp01((v - target) * 0.35f) : 0;
            return (gas, brake, steer);
        }
    }
}

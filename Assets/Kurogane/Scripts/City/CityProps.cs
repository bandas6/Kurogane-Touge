using UnityEngine;

namespace Kurogane.City
{
    /// <summary>Celdas del atlas del pueblo (ver ProceduralTextures.TownAtlas).</summary>
    public enum TownCell
    {
        WallCream, WallWhite, WallBeige, WallBlue, Wood, Roof, WindowLit, WindowDark,
        Shutter, KonbiniWall, KonbiniStripe, KonbiniGlass, Concrete, Asphalt, Sidewalk, MetalDark,
        Noren, VendingRed, VendingBlue, VendingWhite, SignRed, SignBlue, CanopyLight, Paddy,
        Dyke, RailTie, RailSteel, Stripes, Housing, Door, Balcony, AcUnit,
        PhoneBooth, Cedar, Trunk, Ballast, Paint, SignWood, Lantern, StreetLamp,
    }

    /// <summary>
    /// Paso a nivel (bloque D): cada <see cref="period"/> segundos se cierra durante <see cref="closedTime"/>:
    /// se encienden las luces rojas alternas, a los pocos segundos bajan las barreras y al final suben. Mientras
    /// está cerrado (<see cref="Down"/>), el tráfico del pueblo se para antes de la vía. No se cierra mientras
    /// <see cref="allowed"/> diga que no (el jugador cruzando el pueblo en la llegada).
    /// </summary>
    public class CrossingController : MonoBehaviour
    {
        public Transform[] arms;
        [Tooltip("Rotación de cada barrera bajada (subida = vertical).")]
        public Quaternion[] lowered;
        public Blinker lights;
        [Tooltip("Metros sobre la línea central de la pista donde está la vía.")]
        public float trackDistance;
        public float period = 50, closedTime = 14, warning = 3;
        public System.Func<bool> allowed;

        public bool Down { get; private set; }
        float timer = 30, blend;

        void Update()
        {
            timer += Time.deltaTime;
            float phase = timer % period, start = period - closedTime;
            bool closed = phase > start && (allowed == null || allowed());
            Down = closed;
            if (lights != null) lights.active = closed;
            float target = closed && phase > start + warning && phase < period - 2 ? 1 : 0;
            blend = Mathf.MoveTowards(blend, target, Time.deltaTime * 0.4f);
            if (arms == null) return;
            for (int i = 0; i < arms.Length; i++)
                if (arms[i] != null && lowered != null && i < lowered.Length)
                    arms[i].rotation = Quaternion.Slerp(Quaternion.identity, lowered[i], blend);
        }
    }

    /// <summary>
    /// Enciende y apaga unas luces a ritmo fijo: el semáforo en ámbar intermitente, las luces rojas alternas del
    /// paso a nivel (las activa CrossingController) y los intermitentes de emergencia de los coches aparcados.
    /// </summary>
    public class Blinker : MonoBehaviour
    {
        public Renderer[] lamps;
        [Tooltip("Segundos de un ciclo completo.")]
        public float period = 1.2f;
        [Tooltip("Las luces se alternan (paso a nivel) en lugar de encenderse a la vez.")]
        public bool alternate;
        public bool active = true;

        void Update()
        {
            if (lamps == null) return;
            bool firstHalf = Time.time % period < period * 0.5f;
            for (int i = 0; i < lamps.Length; i++)
            {
                if (lamps[i] == null) continue;
                bool on = active && (alternate ? (i % 2 == 0) == firstHalf : firstHalf);
                if (lamps[i].enabled != on) lamps[i].enabled = on;
            }
        }
    }
}

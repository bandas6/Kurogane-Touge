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
    /// Enciende y apaga unas luces a ritmo fijo: el semáforo en ámbar intermitente y, si se activa, las luces
    /// rojas alternas del paso a nivel (el bloque D las activará al bajar la barrera).
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

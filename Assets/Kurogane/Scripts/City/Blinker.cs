using UnityEngine;

namespace Kurogane.City
{
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

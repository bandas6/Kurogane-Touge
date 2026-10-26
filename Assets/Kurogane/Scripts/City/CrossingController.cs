using UnityEngine;

namespace Kurogane.City
{
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
}

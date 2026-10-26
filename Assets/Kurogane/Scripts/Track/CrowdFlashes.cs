using UnityEngine;

namespace Kurogane.Track
{
    /// <summary>Flashes de cámara del público: enciende un instante, al azar, los quads que crea TrackBuilder.</summary>
    public class CrowdFlashes : MonoBehaviour
    {
        public Renderer[] flashes;
        [Tooltip("Flashes por segundo de cada espectador con cámara.")]
        public float perSecond = 0.6f;
        public float duration = 0.07f;

        float[] timers;
        readonly System.Random rng = new System.Random();

        void Start()
        {
            if (flashes == null) flashes = new Renderer[0];
            timers = new float[flashes.Length];
            foreach (var f in flashes) if (f != null) f.enabled = false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < flashes.Length; i++)
            {
                var f = flashes[i];
                if (f == null) continue;
                if (timers[i] > 0)
                {
                    timers[i] -= dt;
                    if (timers[i] <= 0) f.enabled = false;
                }
                else if (rng.NextDouble() < perSecond * dt)
                {
                    timers[i] = duration;
                    f.enabled = true;
                }
            }
        }
    }
}

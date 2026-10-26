using System.Collections.Generic;
using UnityEngine;

namespace Kurogane.Core
{
    /// <summary>
    /// Apaga por distancia a la cámara los objetos pequeños (postes, catadióptricos, ojos de gato) y las luces de
    /// los faroles. La niebla ya los tapa de lejos, pero sin esto se siguen dibujando e iluminando.
    /// Revisa unas pocas veces por segundo: con unos cientos de elementos el coste es despreciable.
    /// </summary>
    public class DetailCulling : MonoBehaviour
    {
        [Tooltip("Segundos entre revisiones.")]
        public float interval = 0.2f;
        [Tooltip("Multiplica todas las distancias (calidad baja < 1).")]
        public float distanceScale = 1;

        struct Item
        {
            public Vector3 center;
            public float maxDistance;
            public Renderer renderer;
            public Light light;
            public bool on;
        }

        readonly List<Item> items = new List<Item>();
        Transform cameraTransform;
        float timer;

        public void Add(Renderer r, float maxDistance) =>
            items.Add(new Item { center = r.bounds.center, maxDistance = maxDistance + r.bounds.extents.magnitude, renderer = r, on = true });

        public void Add(Light l, float maxDistance) =>
            items.Add(new Item { center = l.transform.position, maxDistance = maxDistance + l.range, light = l, on = true });

        void LateUpdate()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0) return;
            timer = interval;
            if (cameraTransform == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                cameraTransform = cam.transform;
            }
            var eye = cameraTransform.position;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                float d = it.maxDistance * distanceScale;
                bool on = (it.center - eye).sqrMagnitude < d * d;
                if (on == it.on) continue;
                it.on = on;
                if (it.renderer != null) it.renderer.enabled = on;
                if (it.light != null) it.light.enabled = on;
                items[i] = it;
            }
        }
    }
}

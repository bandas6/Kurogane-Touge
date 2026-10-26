using System.Collections.Generic;
using Kurogane.Core;
using UnityEngine;

namespace Kurogane.Cars
{
    public enum TrafficModel { Kei, Furgoneta, Taxi, Camion }

    /// <summary>
    /// Coches decorativos (bloque D): camioneta kei, furgoneta de reparto, taxi y camión pequeño, hechos con cajas
    /// y fusionados en una sola malla por coche. Los materiales se comparten entre todos los coches del tráfico.
    /// Ejes locales: +Z delante, +X derecha.
    /// </summary>
    public static class TrafficModels
    {
        public static readonly Color[] Paints =
        {
            new Color(0.92f, 0.92f, 0.9f), new Color(0.12f, 0.12f, 0.14f), new Color(0.2f, 0.3f, 0.55f),
            new Color(0.55f, 0.12f, 0.12f), new Color(0.75f, 0.72f, 0.62f), new Color(0.25f, 0.42f, 0.3f),
        };

        const string HazardName = "Emergencia";
        static readonly Dictionary<Color, Material> paints = new Dictionary<Color, Material>();
        static Material black, glass, lens, tail, amber, tyre, taxiSign, stripe;
        static Mesh cube, cylinder;

        static Material Paint(Color c)
        {
            if (!paints.TryGetValue(c, out var m) || m == null) paints[c] = m = MaterialFactory.Lit(c, null, 0.55f);
            return m;
        }

        static void EnsureShared()
        {
            if (black == null) black = MaterialFactory.Lit(new Color(0.05f, 0.05f, 0.055f), null, 0.1f);
            if (glass == null) glass = MaterialFactory.Lit(new Color(0.06f, 0.09f, 0.14f), null, 0.9f);
            if (lens == null) lens = MaterialFactory.Emissive(new Color(1f, 0.96f, 0.85f), new Color(1.4f, 1.3f, 1.1f));
            if (tail == null) tail = MaterialFactory.Emissive(new Color(0.35f, 0, 0.03f), new Color(0.9f, 0.08f, 0.12f));
            if (amber == null) amber = MaterialFactory.Emissive(new Color(1f, 0.6f, 0.1f), new Color(2.4f, 1.3f, 0.2f));
            if (tyre == null) tyre = MaterialFactory.Lit(new Color(0.07f, 0.07f, 0.07f), null, 0.05f);
            if (taxiSign == null) taxiSign = MaterialFactory.Emissive(new Color(1f, 0.85f, 0.3f), new Color(1.6f, 1.2f, 0.35f));
            if (stripe == null) stripe = MaterialFactory.Lit(new Color(0.15f, 0.45f, 0.25f), null, 0.4f);
            if (cube == null) cube = Primitive(PrimitiveType.Cube);
            if (cylinder == null) cylinder = Primitive(PrimitiveType.Cylinder);
        }

        static Mesh Primitive(PrimitiveType t)
        {
            var tmp = GameObject.CreatePrimitive(t);
            var m = tmp.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Object.Destroy(tmp); else Object.DestroyImmediate(tmp);
            return m;
        }

        /// <summary>
        /// Construye el coche como hijo de <paramref name="parent"/> y devuelve la raíz. Los intermitentes de
        /// emergencia (apagados) quedan en <paramref name="hazards"/> para encenderlos con un Blinker.
        /// </summary>
        public static GameObject Build(Transform parent, TrafficModel model, Color paint, out Renderer[] hazards)
        {
            EnsureShared();
            var root = new GameObject("Trafico " + model).transform;
            root.SetParent(parent, false);
            var body = Paint(paint);
            var lights = new List<Renderer>();
            float halfW, front, rear, wheelR, axle;

            switch (model)
            {
                case TrafficModel.Kei: // camioneta kei: cabina corta y caja abierta
                    halfW = 0.7f; front = 1.7f; rear = -1.7f; wheelR = 0.27f; axle = 1.15f;
                    Box(root, body, new Vector3(0, 0.95f, 1.05f), new Vector3(1.4f, 1.25f, 1.3f));
                    Box(root, glass, new Vector3(0, 1.2f, 1.71f), new Vector3(1.25f, 0.55f, 0.03f));
                    Box(root, body, new Vector3(0, 0.55f, -0.65f), new Vector3(1.4f, 0.12f, 2.1f));
                    foreach (int s in new[] { -1, 1 })
                        Box(root, body, new Vector3(s * 0.68f, 0.78f, -0.65f), new Vector3(0.05f, 0.35f, 2.1f));
                    Box(root, body, new Vector3(0, 0.78f, -1.68f), new Vector3(1.4f, 0.35f, 0.05f));
                    break;
                case TrafficModel.Furgoneta: // furgoneta de reparto alta con franja
                    halfW = 0.85f; front = 2.3f; rear = -2.3f; wheelR = 0.32f; axle = 1.5f;
                    Box(root, body, new Vector3(0, 1.25f, 0), new Vector3(1.7f, 1.9f, 4.6f));
                    Box(root, glass, new Vector3(0, 1.6f, 2.31f), new Vector3(1.5f, 0.7f, 0.03f));
                    foreach (int s in new[] { -1, 1 })
                    {
                        Box(root, stripe, new Vector3(s * 0.86f, 1.0f, -0.3f), new Vector3(0.02f, 0.3f, 3.6f));
                        Box(root, glass, new Vector3(s * 0.86f, 1.6f, 1.6f), new Vector3(0.02f, 0.55f, 0.9f));
                    }
                    break;
                case TrafficModel.Taxi: // berlina con luminoso de taxi
                    halfW = 0.85f; front = 2.3f; rear = -2.3f; wheelR = 0.31f; axle = 1.4f;
                    Box(root, body, new Vector3(0, 0.62f, 0), new Vector3(1.7f, 0.65f, 4.6f));
                    Box(root, body, new Vector3(0, 1.17f, -0.25f), new Vector3(1.5f, 0.5f, 2.3f));
                    Box(root, glass, new Vector3(0, 1.17f, 0.91f), new Vector3(1.4f, 0.42f, 0.03f));
                    foreach (int s in new[] { -1, 1 })
                        Box(root, glass, new Vector3(s * 0.76f, 1.17f, -0.25f), new Vector3(0.02f, 0.38f, 2.0f));
                    Box(root, taxiSign, new Vector3(0, 1.52f, -0.1f), new Vector3(0.5f, 0.2f, 0.2f));
                    break;
                default: // camión pequeño: cabina alta y caja cerrada
                    halfW = 0.95f; front = 2.9f; rear = -3.1f; wheelR = 0.38f; axle = 1.9f;
                    Box(root, body, new Vector3(0, 1.35f, 2.05f), new Vector3(1.9f, 2.0f, 1.7f));
                    Box(root, glass, new Vector3(0, 1.75f, 2.91f), new Vector3(1.7f, 0.75f, 0.03f));
                    Box(root, Paint(Paints[0]), new Vector3(0, 1.6f, -0.85f), new Vector3(2.1f, 2.3f, 4.5f));
                    break;
            }

            // Ruedas, paragolpes y luces
            float frontAxle = axle, rearAxle = model == TrafficModel.Camion ? -1.9f : -axle;
            foreach (float z in new[] { frontAxle, rearAxle })
                foreach (int s in new[] { -1, 1 })
                {
                    var w = Box(root, tyre, new Vector3(s * (halfW - 0.05f), wheelR, z), new Vector3(wheelR * 2, 0.12f, wheelR * 2), cylinder);
                    w.localRotation = Quaternion.Euler(0, 0, 90);
                }
            Box(root, black, new Vector3(0, 0.35f, front - 0.05f), new Vector3(halfW * 2 + 0.04f, 0.2f, 0.15f));
            Box(root, black, new Vector3(0, 0.35f, rear + 0.05f), new Vector3(halfW * 2 + 0.04f, 0.2f, 0.15f));
            foreach (int s in new[] { -1, 1 })
            {
                Box(root, lens, new Vector3(s * (halfW - 0.25f), 0.65f, front), new Vector3(0.3f, 0.14f, 0.04f));
                Box(root, tail, new Vector3(s * (halfW - 0.2f), 0.7f, rear), new Vector3(0.25f, 0.16f, 0.04f));
                foreach (float z in new[] { front + 0.01f, rear - 0.01f })
                {
                    var h = Box(root, amber, new Vector3(s * (halfW - 0.06f), 0.52f, z), new Vector3(0.14f, 0.09f, 0.04f));
                    h.name = HazardName;
                    var r = h.GetComponent<MeshRenderer>();
                    r.enabled = false;
                    lights.Add(r);
                }
            }

            MeshKit.MergeToSingle(root, "Carroceria", new[] { HazardName });
            hazards = lights.ToArray();
            return root.gameObject;
        }

        static Transform Box(Transform parent, Material mat, Vector3 pos, Vector3 size, Mesh mesh = null)
        {
            var go = new GameObject("Pieza");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = mesh != null ? mesh : cube;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }
    }
}

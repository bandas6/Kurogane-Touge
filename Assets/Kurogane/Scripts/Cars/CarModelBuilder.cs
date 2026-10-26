using System.Collections.Generic;
using Kurogane.Core;
using UnityEngine;

namespace Kurogane.Cars
{
    /// <summary>Referencias a las piezas móviles de un coche generado.</summary>
    public class CarRig
    {
        public Transform root, body;
        public Transform[] wheelSpin = new Transform[4]; // DI, DD, TI, TD
        public Transform[] steerPivot = new Transform[2];
        public Material tailLights;
        public Light headlight;
        public GameObject flame;
        public float wheelBase;
        /// <summary>Mallas y materiales creados para este coche; se destruyen al cambiar de coche.</summary>
        public readonly List<Mesh> ownedMeshes = new List<Mesh>();
        public readonly List<Material> ownedMaterials = new List<Material>();
    }

    /// <summary>
    /// Coches low-poly generados por código: perfil lateral extruido con pasos de rueda recortados,
    /// como en el prototipo web. Es provisional hasta tener los modelos de Blender (Fase 3).
    /// Ejes locales: +Z delante, +X derecha. El conductor va a la derecha (coche japonés).
    /// </summary>
    public static class CarModelBuilder
    {
        class Model
        {
            public float wb, rear, front, cap, popY, popZ;
            public Vector2[] top, gh;
            public string tail, spoiler;
            public bool fixedLights;
        }

        static Model Get(CarModel m)
        {
            switch (m)
            {
                case CarModel.Coupe:
                    return new Model
                    {
                        wb = 1.22f, rear = -2.2f, front = 2.18f, cap = 0.4f, popY = 0.78f, popZ = 1.66f, tail = "round2", spoiler = "wing",
                        top = V(2.22f, 0.42f, 2.12f, 0.62f, 1.35f, 0.76f, 0.2f, 0.82f, -1.6f, 0.88f, -2.15f, 0.86f, -2.24f, 0.5f),
                        gh = V(1.05f, 0.77f, 0.3f, 1.15f, -0.55f, 1.17f, -1.6f, 0.87f)
                    };
                case CarModel.Sedan:
                    return new Model
                    {
                        wb = 1.32f, rear = -2.24f, front = 2.24f, cap = 0.42f, tail = "round4", spoiler = "lip", fixedLights = true,
                        top = V(2.26f, 0.5f, 2.24f, 0.76f, 1.35f, 0.86f, -1.5f, 0.88f, -2.24f, 0.92f, -2.28f, 0.5f),
                        gh = V(1.25f, 0.86f, 0.5f, 1.31f, -0.9f, 1.32f, -1.55f, 0.89f)
                    };
                default:
                    return new Model
                    {
                        wb = 1.2f, rear = -2.12f, front = 2.1f, cap = 0.6f, popY = 0.86f, popZ = 1.72f, tail = "band", spoiler = "hatch",
                        top = V(2.12f, 0.5f, 2.1f, 0.72f, 1.25f, 0.85f, -1.92f, 0.91f, -2.12f, 0.84f, -2.14f, 0.5f),
                        gh = V(1.18f, 0.84f, 0.38f, 1.29f, -1.05f, 1.31f, -1.92f, 0.9f)
                    };
            }
        }

        static Vector2[] V(params float[] f)
        {
            var r = new Vector2[f.Length / 2];
            for (int i = 0; i < r.Length; i++) r[i] = new Vector2(f[i * 2], f[i * 2 + 1]);
            return r;
        }

        const float YB = 0.3f, AR = 0.39f;

        static List<Vector2> BodyOutline(Model md)
        {
            var pts = new List<Vector2> { new Vector2(md.rear, YB) };
            MeshKit.Arc(pts, new Vector2(-md.wb, YB), AR, Mathf.PI, 0, 8);
            MeshKit.Arc(pts, new Vector2(md.wb, YB), AR, Mathf.PI, 0, 8);
            pts.Add(new Vector2(md.front, YB));
            pts.AddRange(md.top);
            return pts;
        }

        static List<Vector2> Quad(float z0, float z1, float y0, float y1) =>
            new List<Vector2> { new Vector2(z0, y0), new Vector2(z1, y0), new Vector2(z1, y1), new Vector2(z0, y1) };

        public static CarRig Build(Transform parent, CarSpec spec, bool withLights)
        {
            var md = Get(spec.model);
            var rig = new CarRig { wheelBase = md.wb };
            rig.root = new GameObject("Model").transform;
            rig.root.SetParent(parent, false);
            rig.body = new GameObject("Body").transform;
            rig.body.SetParent(rig.root, false);

            var paint = MaterialFactory.Lit(spec.paint, null, 0.65f);
            var band = MaterialFactory.Lit(spec.band, null, 0.35f);
            var black = MaterialFactory.Lit(new Color(0.05f, 0.05f, 0.055f), null, 0.1f);
            var glass = MaterialFactory.Lit(new Color(0.06f, 0.09f, 0.14f), null, 0.92f);
            var chrome = MaterialFactory.Lit(new Color(0.72f, 0.74f, 0.77f), null, 0.85f);
            var seat = MaterialFactory.Lit(new Color(0.11f, 0.11f, 0.13f));
            var lens = MaterialFactory.Emissive(new Color(1f, 0.96f, 0.85f), new Color(1.6f, 1.5f, 1.2f));
            var amber = MaterialFactory.Emissive(new Color(1f, 0.69f, 0.19f), new Color(0.9f, 0.5f, 0.1f));
            rig.tailLights = MaterialFactory.Emissive(new Color(0.33f, 0, 0.03f), new Color(1f, 0.1f, 0.16f) * 0.5f);

            float front = Mathf.Max(md.front, md.top[0].x), rear = Mathf.Min(md.rear, md.top[md.top.Length - 1].x);
            float rearTop = md.top[md.top.Length - 2].y;

            // Carrocería
            AddMesh(rig, "Shell", MeshKit.Extrude(BodyOutline(md), 1.64f), paint);
            // Franja inferior bicolor / faldones, partida alrededor de los pasos de rueda
            var bandSpans = new[] { md.rear + 0.02f, -md.wb - AR - 0.02f, -md.wb + AR + 0.02f, md.wb - AR - 0.02f, md.wb + AR + 0.02f, md.front - 0.02f };
            for (int b = 0; b < bandSpans.Length; b += 2)
                AddMesh(rig, "Band", MeshKit.Extrude(Quad(bandSpans[b], bandSpans[b + 1], YB, md.cap), 1.68f), band);
            // Habitáculo de cristal y techo
            AddMesh(rig, "Glass", MeshKit.Extrude(new List<Vector2>(md.gh), 1.4f), glass);
            float rz0 = md.gh[1].x, rz1 = md.gh[2].x, ry = Mathf.Max(md.gh[1].y, md.gh[2].y);
            Box(rig.body, paint, new Vector3(0, ry + 0.03f, (rz0 + rz1) / 2), new Vector3(1.42f, 0.05f, rz0 - rz1 + 0.1f));
            foreach (int sx in new[] { -1, 1 })
                Box(rig.body, paint, new Vector3(sx * 0.71f, (ry + md.gh[0].y) / 2, (rz0 + rz1) / 2 - 0.15f), new Vector3(0.05f, (ry - md.gh[3].y) * 0.9f, 0.12f));

            // Interior: asientos y conductor a la derecha
            Box(rig.body, seat, new Vector3(0.34f, 1.0f, -0.55f), new Vector3(0.42f, 0.5f, 0.12f));
            Box(rig.body, seat, new Vector3(-0.34f, 1.0f, -0.55f), new Vector3(0.42f, 0.5f, 0.12f));
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Strip(head);
            head.transform.SetParent(rig.body, false);
            head.transform.localPosition = new Vector3(0.34f, 1.18f, -0.35f);
            head.transform.localScale = Vector3.one * 0.26f;
            head.GetComponent<MeshRenderer>().sharedMaterial = MaterialFactory.Lit(new Color(0.16f, 0.11f, 0.09f));

            // Paragolpes, rejilla, espejos, matrículas, escape
            Box(rig.body, black, new Vector3(0, 0.36f, front - 0.05f), new Vector3(1.66f, 0.2f, 0.18f));
            Box(rig.body, black, new Vector3(0, 0.38f, rear + 0.05f), new Vector3(1.66f, 0.2f, 0.18f));
            Box(rig.body, black, new Vector3(0, 0.5f, front), new Vector3(0.9f, 0.08f, 0.03f));
            foreach (int sx in new[] { -1, 1 })
            {
                float mz = md.gh[0].x - 0.15f, my = md.gh[0].y + 0.06f;
                Box(rig.body, paint, new Vector3(sx * 0.88f, my, mz), new Vector3(0.18f, 0.1f, 0.06f));
                Box(rig.body, black, new Vector3(sx * 0.8f, my - 0.03f, mz), new Vector3(0.05f, 0.05f, 0.05f));
            }
            var plate = MaterialFactory.Lit(new Color(0.95f, 0.95f, 0.92f));
            Box(rig.body, plate, new Vector3(0, 0.38f, front + 0.05f), new Vector3(0.34f, 0.17f, 0.01f));
            Box(rig.body, plate, new Vector3(0, 0.52f, rear - 0.05f), new Vector3(0.34f, 0.17f, 0.01f));
            Pipe(rig.body, chrome, new Vector3(0.5f, 0.3f, rear + 0.02f));
            if (spec.model != CarModel.Panda) Pipe(rig.body, chrome, new Vector3(0.36f, 0.3f, rear + 0.02f));

            // Faros: escamoteables (Panda y Coupé) o fijos (Sedán)
            if (md.fixedLights)
            {
                foreach (int sx in new[] { -1, 1 })
                    Box(rig.body, lens, new Vector3(sx * 0.55f, md.top[0].y + 0.08f, front - 0.02f), new Vector3(0.42f, 0.13f, 0.04f));
            }
            else
            {
                foreach (int sx in new[] { -1, 1 })
                {
                    Box(rig.body, paint, new Vector3(sx * 0.5f, md.popY, md.popZ), new Vector3(0.44f, 0.18f, 0.1f));
                    Box(rig.body, lens, new Vector3(sx * 0.5f, md.popY, md.popZ + 0.055f), new Vector3(0.38f, 0.13f, 0.02f));
                }
            }
            foreach (int sx in new[] { -1, 1 })
                Box(rig.body, amber, new Vector3(sx * 0.6f, 0.46f, front + 0.04f), new Vector3(0.24f, 0.07f, 0.03f));

            // Pilotos traseros
            float tz = rear - 0.01f;
            if (md.tail == "band")
            {
                Box(rig.body, MaterialFactory.Lit(new Color(0.08f, 0.015f, 0.02f)), new Vector3(0, 0.72f, tz), new Vector3(1.52f, 0.16f, 0.04f));
                foreach (int sx in new[] { -1, 1 })
                    Box(rig.body, rig.tailLights, new Vector3(sx * 0.5f, 0.72f, tz - 0.005f), new Vector3(0.5f, 0.13f, 0.05f));
            }
            else
            {
                float ty = (0.5f + rearTop) / 2 + 0.05f;
                var xs = md.tail == "round4" ? new[] { 0.4f, 0.6f } : new[] { 0.45f, 0.64f };
                foreach (int sx in new[] { -1, 1 })
                    foreach (float x in xs)
                    {
                        var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        Strip(c);
                        c.transform.SetParent(rig.body, false);
                        c.transform.localPosition = new Vector3(sx * x, ty, tz);
                        c.transform.localRotation = Quaternion.Euler(90, 0, 0);
                        c.transform.localScale = new Vector3(0.17f, 0.025f, 0.17f);
                        c.GetComponent<MeshRenderer>().sharedMaterial = rig.tailLights;
                    }
            }

            // Alerones
            if (md.spoiler == "wing")
            {
                float wy = rearTop + 0.12f;
                Box(rig.body, paint, new Vector3(0, wy, md.rear + 0.22f), new Vector3(1.56f, 0.05f, 0.34f));
                foreach (int sx in new[] { -1, 1 })
                {
                    Box(rig.body, paint, new Vector3(sx * 0.62f, wy - 0.1f, md.rear + 0.22f), new Vector3(0.06f, 0.2f, 0.12f));
                    Box(rig.body, paint, new Vector3(sx * 0.78f, wy + 0.02f, md.rear + 0.22f), new Vector3(0.03f, 0.16f, 0.36f));
                }
            }
            else if (md.spoiler == "hatch")
                Box(rig.body, band, new Vector3(0, md.gh[3].y + 0.4f, md.gh[3].x + 0.12f), new Vector3(1.36f, 0.05f, 0.24f)).localRotation = Quaternion.Euler(14, 0, 0);
            else
                Box(rig.body, paint, new Vector3(0, rearTop + 0.04f, md.rear + 0.14f), new Vector3(1.4f, 0.05f, 0.2f));

            // Ruedas: neumático, llanta y cinco radios dentro de un pivote de dirección
            var tyre = MaterialFactory.Lit(new Color(0.08f, 0.08f, 0.08f), null, 0.05f);
            var rim = MaterialFactory.Lit(spec.model == CarModel.Sedan ? new Color(0.25f, 0.26f, 0.28f) : new Color(0.75f, 0.76f, 0.78f), null, 0.7f);
            var wheelPos = new[] { new Vector3(-0.74f, 0.31f, md.wb), new Vector3(0.74f, 0.31f, md.wb), new Vector3(-0.74f, 0.31f, -md.wb), new Vector3(0.74f, 0.31f, -md.wb) };
            for (int w = 0; w < 4; w++)
            {
                var pivot = new GameObject("WheelPivot").transform;
                pivot.SetParent(rig.root, false);
                pivot.localPosition = wheelPos[w];
                var spin = new GameObject("Wheel").transform;
                spin.SetParent(pivot, false);
                Cyl(spin, tyre, Vector3.zero, new Vector3(0.62f, 0.12f, 0.62f));
                Cyl(spin, rim, Vector3.zero, new Vector3(0.4f, 0.125f, 0.4f));
                float side = Mathf.Sign(wheelPos[w].x);
                for (int k = 0; k < 5; k++)
                {
                    var sp = Box(spin, rim, new Vector3(side * 0.13f, 0, 0), new Vector3(0.03f, 0.36f, 0.05f));
                    sp.localRotation = Quaternion.Euler(k * 72, 0, 0);
                }
                rig.wheelSpin[w] = spin;
                if (w < 2) rig.steerPivot[w] = pivot;
            }

            // Llama del escape (petardeo)
            rig.flame = Box(rig.root, MaterialFactory.Emissive(new Color(1f, 0.6f, 0.2f), new Color(4f, 1.8f, 0.4f)),
                new Vector3(0.5f, 0.3f, rear - 0.22f), new Vector3(0.16f, 0.16f, 0.3f)).gameObject;
            rig.flame.SetActive(false);

            if (withLights)
            {
                var l = new GameObject("Headlight").AddComponent<Light>();
                l.transform.SetParent(rig.root, false);
                l.transform.localPosition = new Vector3(0, 0.95f, 1.9f);
                l.transform.localRotation = Quaternion.Euler(7, 0, 0);
                l.type = LightType.Spot;
                l.color = new Color(1f, 0.94f, 0.78f);
                l.range = 90;
                l.spotAngle = 62;
                l.innerSpotAngle = 30;
                l.intensity = 4f;
                l.shadows = LightShadows.None;
                rig.headlight = l;
            }

            // Una malla por material en la carrocería y en cada rueda: de ~45 dibujos por coche a ~20
            MeshKit.MergeByMaterial(rig.body, rig.ownedMeshes);
            foreach (var spin in rig.wheelSpin) MeshKit.MergeByMaterial(spin, rig.ownedMeshes);
            // Todos los materiales del coche se crean aquí con MaterialFactory
            var mats = new HashSet<Material>();
            foreach (var mr in rig.root.GetComponentsInChildren<MeshRenderer>(true))
                foreach (var m in mr.sharedMaterials) if (m != null) mats.Add(m);
            rig.ownedMaterials.AddRange(mats);
            return rig;
        }

        static void Strip(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c == null) return;
            if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c);
        }

        static void AddMesh(CarRig rig, string name, Mesh mesh, Material mat)
        {
            rig.ownedMeshes.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(rig.body, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static Transform Box(Transform parent, Material mat, Vector3 pos, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Strip(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        static void Cyl(Transform parent, Material mat, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Strip(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, 0, 90);
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static void Pipe(Transform parent, Material mat, Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Strip(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(90, 0, 0);
            go.transform.localScale = new Vector3(0.1f, 0.125f, 0.1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}

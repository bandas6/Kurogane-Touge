using System.Collections.Generic;
using Kurogane.Core;
using Kurogane.Track;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kurogane.Atmosphere
{
    /// <summary>
    /// Cielo y horizonte lejano, como en el prototipo web: cúpula con degradado, estrellas, luna con halo, dos
    /// anillos de cordilleras, luces de ciudad en el valle y bruma baja. Todo va en un grupo que sigue a la
    /// cámara (sin rotar), dentro de una esfera menor que su plano lejano, con materiales sin niebla dibujados
    /// primero (cola Background). No hace falta ampliar el plano lejano ni una segunda cámara.
    /// El horizonte toma el color de la niebla, y el fondo de la cámara también, para que no se note la unión.
    /// Namespace Atmosphere y no Environment: este último ocultaría System.Environment.
    /// </summary>
    [DefaultExecutionOrder(1000)] // después de ChaseCamera, que mueve la cámara en LateUpdate
    public class SkyDome : MonoBehaviour
    {
        public Shader shader;
        [Tooltip("Radio de la cúpula. Se reduce solo si el plano lejano de la cámara es más corto.")]
        public float radius = 230;
        [Tooltip("El horizonte usa el color de la niebla (RenderSettings.fogColor).")]
        public bool matchFog = true;
        public Color horizon = new Color(0.07f, 0.1f, 0.23f);
        public Color zenith = new Color(0.008f, 0.01f, 0.03f);
        public Color ground = new Color(0.03f, 0.04f, 0.09f);
        [Range(0, 3000)] public int stars = 800;
        [Range(0, 4000)] public int valleyLights = 1800;
        public int seed = 1986;
        [Tooltip("La geometría ya está horneada en la escena (Kurogane > Hornear pista).")]
        public bool baked;

        const string RootName = "Generated";

        public Transform Root => root;

        Transform root;
        Camera cam;
        System.Random rng;

        void Awake()
        {
            var existing = transform.Find(RootName);
            if (baked && existing != null) root = existing;
            else Build();
        }

        /// <summary>Genera la cúpula desde cero (también en el editor, para hornearla).</summary>
        public void Build()
        {
            var existing = transform.Find(RootName);
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject); else DestroyImmediate(existing.gameObject);
            }
            baked = false;
            rng = new System.Random(seed);
            root = new GameObject(RootName).transform;
            root.SetParent(transform, false);
            if (shader == null) shader = Shader.Find("Kurogane/Sky");
            if (shader == null) { Debug.LogWarning("Kurogane: no se encontró el shader Kurogane/Sky; no habrá cielo."); return; }

            if (matchFog)
            {
                horizon = RenderSettings.fogColor;
                ground = horizon * 0.4f;
            }
            float valley = ValleyAzimuth();
            // Textura blanca propia (no Texture2D.whiteTexture, que es interna de Unity y no se puede hornear)
            var white = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "Blanco" };
            white.SetPixel(0, 0, Color.white);
            white.Apply();
            var soft = ProceduralTextures.SoftDot();

            Add("Cúpula", Dome(), Material(white, false, 1000));
            Add("Cordillera lejana", Ridge(0.97f, 9, 0.35f, valley, Color.Lerp(horizon, new Color(0.04f, 0.045f, 0.08f), 0.55f)), Material(white, false, 1001));
            Add("Ciudad lejana", Skyline(valley), Material(white, false, 1002));
            Add("Cordillera cercana", Ridge(0.93f, 6, 0.6f, valley, new Color(0.018f, 0.022f, 0.04f)), Material(white, false, 1003));
            Add("Estrellas y valle", Points(valley), Material(white, true, 1004));
            Add("Bruma", Mist(valley), Material(soft, true, 1005));
            BuildMoon(soft);
        }

        void LateUpdate()
        {
            if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
            if (cam == null) return;
            transform.SetPositionAndRotation(cam.transform.position, Quaternion.identity);
            // Siempre dentro del plano lejano, con margen
            float s = Mathf.Min(1, cam.farClipPlane * 0.9f / radius);
            transform.localScale = Vector3.one * s;
            if (matchFog) cam.backgroundColor = RenderSettings.fogColor;
        }

        // ---------- Piezas ----------

        /// <summary>Esfera con degradado por vértice: suelo oscuro, horizonte (color de la niebla) y cenit casi negro.</summary>
        Mesh Dome()
        {
            const int rings = 18, segs = 32;
            var v = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            for (int i = 0; i <= rings; i++)
            {
                // más anillos cerca del horizonte, donde cambia el color
                float u = i / (float)rings * 2 - 1;
                float elev = Mathf.Sign(u) * Mathf.Pow(Mathf.Abs(u), 1.6f) * 90;
                for (int j = 0; j <= segs; j++)
                {
                    v.Add(Dir(j / (float)segs * 360, elev) * radius);
                    c.Add(SkyColor(elev));
                }
            }
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < segs; j++)
                {
                    int a = i * (segs + 1) + j, b = a + segs + 1;
                    t.Add(a); t.Add(b); t.Add(a + 1);
                    t.Add(a + 1); t.Add(b); t.Add(b + 1);
                }
            return Finish(v, c, null, t);
        }

        Color SkyColor(float elev)
        {
            // Bajo el horizonte el valle oscurece rápido; si no, parece un lago que refleja el cielo
            if (elev < 0) return Color.Lerp(horizon, ground, Mathf.Clamp01(-elev / 7));
            return Color.Lerp(horizon, zenith, Mathf.Pow(Mathf.Clamp01(elev / 90), 0.45f));
        }

        /// <summary>
        /// Anillo de montañas en silueta: perfil de senos al azar, más bajo en la dirección del valle para que se
        /// vean las luces de la ciudad. La base queda justo bajo el horizonte.
        /// </summary>
        Mesh Ridge(float radiusScale, float maxElev, float roughness, float valley, Color color)
        {
            const int segs = 160;
            var phases = new float[5];
            for (int k = 0; k < phases.Length; k++) phases[k] = (float)rng.NextDouble() * 360;
            var v = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            float r = radius * radiusScale;
            for (int j = 0; j <= segs; j++)
            {
                float az = j / (float)segs * 360;
                float h = 0.55f + 0.25f * Mathf.Sin((az * 2 + phases[0]) * Mathf.Deg2Rad)
                          + 0.15f * Mathf.Sin((az * 5 + phases[1]) * Mathf.Deg2Rad)
                          + roughness * 0.12f * Mathf.Sin((az * 13 + phases[2]) * Mathf.Deg2Rad)
                          + roughness * 0.08f * Mathf.Sin((az * 29 + phases[3]) * Mathf.Deg2Rad);
                float open = Mathf.Pow(Mathf.Max(0, Mathf.Cos(Mathf.DeltaAngle(az, valley) * Mathf.Deg2Rad)), 3);
                float elev = Mathf.Lerp(maxElev * Mathf.Clamp01(h), 0.6f, open * 0.85f);
                v.Add(Dir(az, elev) * r);
                v.Add(Dir(az, -2.5f) * r);
                c.Add(color);
                c.Add(color);
                if (j < segs)
                {
                    int a = j * 2;
                    t.Add(a); t.Add(a + 2); t.Add(a + 1);
                    t.Add(a + 1); t.Add(a + 2); t.Add(a + 3);
                }
            }
            return Finish(v, c, null, t);
        }

        // Edificios de la silueta lejana: (azimut inicial, azimut final, elevación del tejado)
        readonly List<Vector3> skyline = new List<Vector3>();

        /// <summary>
        /// Silueta de la ciudad lejana en el valle (bloque B): una franja de bloques algo más claros que el suelo,
        /// más altos en el centro. Sus ventanas se añaden con las luces del valle.
        /// </summary>
        Mesh Skyline(float valley)
        {
            skyline.Clear();
            var v = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            var color = Color.Lerp(ground, horizon, 0.45f);
            float r = radius * 0.95f;
            for (float az = valley - 30; az < valley + 30;)
            {
                float w = Mathf.Lerp(0.5f, 1.8f, (float)rng.NextDouble());
                float centre = 1 - Mathf.Abs(Mathf.DeltaAngle(az, valley)) / 30;
                float top = Mathf.Lerp(-0.8f, 0.4f + 1.6f * centre, (float)rng.NextDouble() * 0.5f + 0.5f * centre);
                skyline.Add(new Vector3(az, az + w, top));
                int o = v.Count;
                v.Add(Dir(az, -2) * r); v.Add(Dir(az + w, -2) * r); v.Add(Dir(az + w, top) * r); v.Add(Dir(az, top) * r);
                for (int k = 0; k < 4; k++) c.Add(color);
                t.Add(o); t.Add(o + 3); t.Add(o + 2);
                t.Add(o); t.Add(o + 2); t.Add(o + 1);
                az += w + Mathf.Lerp(0, 0.3f, (float)rng.NextDouble());
            }
            return Finish(v, c, null, t);
        }

        /// <summary>Estrellas en el cielo y luces de ciudad bajo el horizonte hacia el valle, como quads mirando al centro.</summary>
        Mesh Points(float valley)
        {
            var v = new List<Vector3>();
            var c = new List<Color>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            float r = radius * 0.99f;

            for (int i = 0; i < stars; i++)
            {
                float az = (float)rng.NextDouble() * 360;
                // más estrellas lejos del horizonte, donde la bruma no las tapa
                float elev = Mathf.Lerp(6, 88, Mathf.Sqrt((float)rng.NextDouble()));
                float b = Mathf.Lerp(0.25f, 1f, Mathf.Pow((float)rng.NextDouble(), 3));
                var tint = rng.Next(6) == 0 ? new Color(1f, 0.85f, 0.7f) : rng.Next(5) == 0 ? new Color(0.75f, 0.85f, 1f) : Color.white;
                Quad(v, c, uv, t, Dir(az, elev) * r, Mathf.Lerp(0.35f, 0.75f, b), tint * b * Mathf.Clamp01(elev / 18));
            }

            // Pueblos del valle: grupos de luces cálidas, alguna fría, más densos y cercanos al centro del valle
            int clusters = Mathf.Max(1, valleyLights / 70);
            var centers = new List<Vector2>();
            for (int k = 0; k < clusters; k++)
            {
                float spread = (float)(rng.NextDouble() * 2 - 1);
                centers.Add(new Vector2(valley + spread * Mathf.Abs(spread) * 70, -Mathf.Lerp(1.5f, 11, (float)rng.NextDouble())));
            }
            for (int i = 0; i < valleyLights; i++)
            {
                var ctr = centers[rng.Next(centers.Count)];
                bool scattered = rng.Next(5) == 0;
                float az = ctr.x + Gauss() * (scattered ? 12 : 3.5f);
                float elev = Mathf.Min(-0.8f, ctr.y + Gauss() * (scattered ? 2.5f : 0.7f));
                var warm = new Color(1f, Mathf.Lerp(0.6f, 0.82f, (float)rng.NextDouble()), Mathf.Lerp(0.3f, 0.5f, (float)rng.NextDouble()));
                var cold = new Color(0.72f, 0.85f, 1f);
                float b = Mathf.Lerp(0.35f, 1f, (float)rng.NextDouble()) * Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(-elev / 14));
                Quad(v, c, uv, t, Dir(az, elev) * r, Mathf.Lerp(0.3f, 0.6f, (float)rng.NextDouble()), (rng.Next(7) == 0 ? cold : warm) * b);
            }

            // Ventanas encendidas en la silueta de la ciudad lejana
            foreach (var b in skyline)
                for (float az = b.x + 0.12f; az < b.y - 0.08f; az += 0.22f)
                    for (float el = -1.7f; el < b.z - 0.1f; el += 0.28f)
                        if (rng.Next(3) == 0)
                            Quad(v, c, uv, t, Dir(az, el) * radius * 0.945f, 0.28f, new Color(1f, 0.8f, 0.5f) * Mathf.Lerp(0.3f, 0.8f, (float)rng.NextDouble()));
            return Finish(v, c, uv, t);
        }

        /// <summary>Bancos de bruma baja sobre el valle, aditivos y muy tenues.</summary>
        Mesh Mist(float valley)
        {
            var v = new List<Vector3>();
            var c = new List<Color>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            var tint = Color.Lerp(horizon, new Color(0.45f, 0.45f, 0.6f), 0.5f) * 0.04f;
            for (int i = 0; i < 14; i++)
            {
                float az = valley + Gauss() * 45;
                float elev = -Mathf.Lerp(1, 7, (float)rng.NextDouble());
                Quad(v, c, uv, t, Dir(az, elev) * radius * 0.95f, Mathf.Lerp(18, 40, (float)rng.NextDouble()), tint, 0.35f);
            }
            return Finish(v, c, uv, t);
        }

        /// <summary>Luna con halo, en el rumbo de la luz direccional de la escena pero baja, para verla desde la carretera.</summary>
        void BuildMoon(Texture2D soft)
        {
            float az = 150, elev = 24;
            var sun = RenderSettings.sun;
            if (sun == null)
                foreach (var l in FindObjectsByType<Light>())
                    if (l.type == LightType.Directional) { sun = l; break; }
            if (sun != null)
            {
                var toward = -sun.transform.forward; // la luz llega desde la luna
                az = Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg;
            }
            var pos = Dir(az, elev) * radius * 0.96f;
            var v = new List<Vector3>(); var c = new List<Color>(); var uv = new List<Vector2>(); var t = new List<int>();
            Quad(v, c, uv, t, pos, 26, new Color(0.55f, 0.6f, 0.8f) * 0.22f);
            Add("Halo", Finish(v, c, uv, t), Material(soft, true, 1006));
            v.Clear(); c.Clear(); uv.Clear(); t.Clear();
            Quad(v, c, uv, t, pos * 0.995f, 5.5f, new Color(1f, 0.98f, 0.9f));
            Add("Luna", Finish(v, c, uv, t), Material(ProceduralTextures.Moon(), true, 1007));
        }

        // ---------- Utilidades ----------

        float ValleyAzimuth()
        {
            // El valle está hacia donde baja la carretera: de la salida a la meta
            var builder = FindAnyObjectByType<TrackBuilder>();
            TrackData d = builder != null ? builder.Data : null;
            if (d == null && builder != null && builder.definition != null) d = new TrackData(builder.definition);
            if (d == null) return 0;
            var dir = d.P[d.N - 1].pos - d.P[0].pos;
            return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        float Gauss()
        {
            double u1 = 1 - rng.NextDouble(), u2 = rng.NextDouble();
            return (float)(System.Math.Sqrt(-2 * System.Math.Log(u1)) * System.Math.Cos(2 * System.Math.PI * u2));
        }

        static Vector3 Dir(float azimuthDeg, float elevDeg)
        {
            float a = azimuthDeg * Mathf.Deg2Rad, e = elevDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e));
        }

        /// <summary>Quad centrado en <paramref name="center"/> y perpendicular a la dirección desde el centro de la cúpula.</summary>
        static void Quad(List<Vector3> v, List<Color> c, List<Vector2> uv, List<int> t, Vector3 center, float size, Color color, float aspect = 1)
        {
            var n = center.normalized;
            var right = Vector3.Cross(Vector3.up, n);
            if (right.sqrMagnitude < 1e-4f) right = Vector3.right;
            right.Normalize();
            var up = Vector3.Cross(n, right);
            right *= size * 0.5f;
            up *= size * 0.5f * aspect;
            int o = v.Count;
            v.Add(center - right - up); v.Add(center + right - up); v.Add(center + right + up); v.Add(center - right + up);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
            for (int k = 0; k < 4; k++) c.Add(color);
            t.Add(o); t.Add(o + 2); t.Add(o + 1);
            t.Add(o); t.Add(o + 3); t.Add(o + 2);
        }

        static Mesh Finish(List<Vector3> v, List<Color> c, List<Vector2> uv, List<int> t)
        {
            var m = new Mesh { indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(v);
            m.SetColors(c);
            if (uv != null) m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        Material Material(Texture tex, bool additive, int queue)
        {
            var m = new Material(shader) { renderQueue = queue };
            m.SetTexture("_MainTex", tex);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.Zero));
            return m;
        }

        void Add(string name, Mesh mesh, Material mat)
        {
            mesh.name = name;
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }
    }
}

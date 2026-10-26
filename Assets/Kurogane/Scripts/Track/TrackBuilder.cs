using System.Collections.Generic;
using Kurogane.Core;
using UnityEngine;

namespace Kurogane.Track
{
    /// <summary>
    /// Construye en tiempo de ejecución la carretera, laderas, muros, guardarraíles, túnel, faroles y bosque
    /// a partir de una <see cref="TrackDefinition"/>. En la Fase 3 esto pasa a una herramienta de editor que
    /// hornea las mallas como assets.
    /// Rendimiento (Fase 6): las cintas van en tramos y los objetos repetidos en celdas, para que se descarten
    /// fuera de cámara; postes y luces se apagan por distancia con <see cref="DetailCulling"/>.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public partial class TrackBuilder : MonoBehaviour
    {
        public TrackDefinition definition;
        [Tooltip("La geometría ya está horneada en la escena (Kurogane > Hornear pista): al arrancar solo se calcula la pista.")]
        public bool baked;
        [Tooltip("Paleta, densidad y semilla del pueblo de la meta. Vacío = valores por defecto.")]
        public Kurogane.City.CityStyle cityStyle;
        [Tooltip("Semilla para la posición de árboles.")]
        public int seed = 86;
        [Range(0, 1200)] public int treeCount = 760;

        [Header("Rendimiento")]
        [Tooltip("Puntos de pista por tramo de malla (cada punto son 2 m). Tramos cortos se descartan mejor fuera de cámara.")]
        [Range(10, 200)] public int chunkPoints = 40;
        [Tooltip("Lado en metros de las celdas en que se agrupan árboles, postes y faroles.")]
        public float cellSize = 60;
        [Tooltip("Distancia a la que se apagan postes, catadióptricos y ojos de gato.")]
        public float detailDistance = 130;
        [Tooltip("Distancia a la que se apagan las luces de faroles y túnel (más su alcance).")]
        public float lightDistance = 90;

        /// <summary>Pista de bajada: con ella se construyen las mallas, el pueblo y el tráfico.</summary>
        public TrackData Geometry { get; private set; }
        /// <summary>Sentido en que se corre. Lo fija el RaceManager; no cambia la geometría.</summary>
        public TrackDirection Direction { get; set; }
        /// <summary>Pista en el sentido de la carrera: la usan el coche, el rival, la cámara y la carrera.</summary>
        public TrackData Data => Geometry != null && Direction == TrackDirection.Subida && definition.allowUphill ? Geometry.Reversed() : Geometry;
        /// <summary>Ambiente de la pista (o null: el del Monte Kurogane).</summary>
        public EnvironmentTheme Theme => definition != null ? definition.theme : null;

        public const string StartBanner = "PancartaSalida", GoalBanner = "PancartaMeta", UphillGoalBanner = "PancartaMetaSubida";

        Transform root;
        System.Random rng;
        DetailCulling culling;

        void Awake() => Build();

        const string RootName = "Generated";

        /// <summary>Raíz de la geometría generada (o horneada).</summary>
        public Transform Root => root;

        public void Build()
        {
            if (Geometry != null) return;
            if (definition == null) definition = ScriptableObject.CreateInstance<TrackDefinition>();
            Geometry = new TrackData(definition);
            culling = GetComponent<DetailCulling>();
            if (culling == null) culling = gameObject.AddComponent<DetailCulling>();

            var existing = transform.Find(RootName);
            if (baked && existing != null)
            {
                root = existing;
                RegisterBakedDetail();
                ApplyDirection();
                return;
            }
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject); else DestroyImmediate(existing.gameObject);
            }

            rng = new System.Random(seed);
            root = new GameObject(RootName).transform;
            root.SetParent(transform, false);

            var d = Geometry;
            var theme = Theme;

            // Calzada
            // Brillo bajo en seco para que la máscara de charcos no se note; la lluvia lo sube (WeatherSystem)
            var road = MaterialFactory.Lit(Color.white, ProceduralTextures.Asphalt(), 0.1f).SmoothnessFromAlpha();
            AddRibbon("Road", d.N, i =>
            {
                var p = d.P[i];
                return (p.pos - p.right * d.HW[i], p.pos + p.right * d.HW[i], 0, 1, i * d.Step / 16f);
            }, road);

            // Canaletas en el interior de las horquillas
            var grate = MaterialFactory.Lit(Color.white, ProceduralTextures.Grate(), 0.05f);
            foreach (var hp in d.Hairpins)
            {
                int s = (int)Mathf.Sign(hp.angle), a = Mathf.Max(0, hp.start - 3), b = Mathf.Min(d.N - 1, hp.end + 3);
                Add("Gutter", MeshKit.Ribbon(b - a + 1, j =>
                {
                    int i = a + j;
                    var p = d.P[i];
                    var inner = p.pos + p.right * (d.HW[i] - 0.7f) * s + Vector3.up * 0.015f;
                    var outer = p.pos + p.right * (d.HW[i] - 0.05f) * s + Vector3.up * 0.015f;
                    return s > 0 ? (inner, outer, 0, 1, i * d.Step / 2f) : (outer, inner, 0, 1, i * d.Step / 2f);
                }), grate);
            }

            // Arcén, muro de contención y ladera a cada lado (en los puentes no hay muro ni ladera)
            var gravel = MaterialFactory.Lit(Color.white, ProceduralTextures.Gravel(), 0.05f).DoubleSided();
            var wallStyle = theme != null ? theme.walls : WallStyle.Hormigon;
            var wallTex = wallStyle == WallStyle.Roca ? ProceduralTextures.RockWall()
                : wallStyle == WallStyle.Piedra ? ProceduralTextures.StoneWall() : ProceduralTextures.RetainingWall();
            var wall = MaterialFactory.Lit(Color.white, wallTex, 0.05f).DoubleSided();
            var grass = MaterialFactory.Lit(Color.white, ProceduralTextures.Grass(), 0.02f).DoubleSided();
            var rail = MaterialFactory.Lit(theme != null ? theme.railColor : Color.white, ProceduralTextures.GuardRail(), 0.5f);
            bool bridges = System.Array.IndexOf(d.Bridge, true) >= 0;
            System.Func<int, bool> ground = bridges ? i => !d.Bridge[i] : (System.Func<int, bool>)null;
            foreach (int s in new[] { -1, 1 })
            {
                AddRibbon("Shoulder", d.N, SideStrip(s, 0, 1, 1, 3), gravel);
                AddRibbon("Wall", d.N, SideStrip(s, 1, 2, 3, 6), wall, ground);
                AddRibbon("Slope", d.N, SideStrip(s, 2, 3, 6, 6), grass, ground);
                int side = s;
                AddRibbon("GuardRail", d.N, i =>
                {
                    var p = d.P[i];
                    var b = p.pos + p.right * (d.HW[i] + 0.45f) * side;
                    return (b + Vector3.up * 0.45f, b + Vector3.up * 0.82f, 0, 1, 0);
                }, rail, i => d.HasRail(i, side), true);
            }
            if (bridges) BuildBridges();

            BuildPosts();
            BuildLamps();
            BuildTunnel();
            BuildLandmarks();
            BuildForest();
            BuildBanner(StartBanner, 8, new Color(0.72f, 0.07f, 0.16f));
            BuildBanner(GoalBanner, d.FinishIndex + 1, new Color(0.11f, 0.16f, 0.5f));
            if (definition.allowUphill) BuildBanner(UphillGoalBanner, d.UphillGoal - 1, new Color(0.11f, 0.16f, 0.5f));
            BuildSigns();
            BuildCrowd();
            BuildParkedCars();
            var style = theme != null && theme.cityStyle != null ? theme.cityStyle : cityStyle;
            if (d.HasTown) new Kurogane.City.CityBuilder(this, style).Build();
            BuildColliders();
            ApplyDirection();
        }

        /// <summary>
        /// Pancartas según el sentido: la meta de subida solo se ve subiendo. La pancarta de meta de bajada es la
        /// salida de subida. Se llama al construir y cada vez que el RaceManager cambia el sentido.
        /// </summary>
        public void ApplyDirection()
        {
            if (root == null) return;
            bool up = Data != null && Data.Uphill;
            foreach (Transform t in root)
                if (t.name == UphillGoalBanner) t.gameObject.SetActive(up);
        }

        /// <summary>
        /// Tablero de hormigón bajo los tramos de puente (cantos y fondo) y pilares cada 24 m hasta 40 m de profundidad.
        /// La barandilla es el guardarraíl, que en los puentes va a los dos lados.
        /// </summary>
        void BuildBridges()
        {
            var d = Geometry;
            var concrete = MaterialFactory.Lit(new Color(0.5f, 0.49f, 0.46f), null, 0.05f).DoubleSided();
            var cube = Primitive(PrimitiveType.Cube);
            var pillars = new List<Matrix4x4>();
            const float depth = 1.4f;
            System.Func<int, bool> on = i => d.Bridge[i];
            foreach (int s in new[] { -1, 1 })
            {
                int side = s;
                AddRibbon("BridgeDeck", d.N, i =>
                {
                    var p = d.P[i];
                    var e = p.pos + p.right * (d.HW[i] + 0.9f) * side;
                    return (e - Vector3.up * depth, e - Vector3.up * 0.1f, 0, 1, i * d.Step / 4f);
                }, concrete, on, true);
            }
            AddRibbon("BridgeDeck", d.N, i =>
            {
                var p = d.P[i];
                float w = d.HW[i] + 0.9f;
                return (p.pos + p.right * w - Vector3.up * depth, p.pos - p.right * w - Vector3.up * depth, 0, 1, i * d.Step / 4f);
            }, concrete, on, true);
            for (int i = 0; i < d.N; i++)
            {
                if (!d.Bridge[i] || i % 12 != 6) continue;
                var p = d.P[i];
                pillars.Add(Matrix4x4.TRS(p.pos - Vector3.up * (depth + 20), Quaternion.Euler(0, p.heading * Mathf.Rad2Deg, 0),
                    new Vector3(d.HW[i] * 1.2f, 40, 1.6f)));
            }
            AddCombined("BridgePillars", cube, pillars, concrete);
        }

        /// <summary>Vuelve a generar la geometría desde cero (lo usa el horneado en el editor).</summary>
        public void Rebuild()
        {
            Geometry = null;
            baked = false;
            Build();
        }

        /// <summary>
        /// Con la pista horneada, los objetos ya existen: solo se registran en el descarte por distancia, por nombre.
        /// Las luces horneadas en lightmaps no cuestan en ejecución, así que esas no se tocan.
        /// </summary>
        void RegisterBakedDetail()
        {
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                switch (mr.gameObject.name)
                {
                    case "Posts": case "Reflectors": case "CatEyes": case "Chevrons": case "WarningSigns":
                    case "SpeedSigns": case "SignBacks": case "SignPoles": case "KmPost":
                    case Kurogane.City.CityBuilder.DetailName: case "Rotulo": case "Ambar": case "LuzPasoNivel": case "Barrera":
                        culling.Add(mr, detailDistance);
                        break;
                    case "Carroceria":
                        culling.Add(mr, detailDistance * 1.3f);
                        break;
                    case "Crowd":
                        culling.Add(mr, detailDistance * 1.3f);
                        break;
                }
            }
            foreach (var l in root.GetComponentsInChildren<Light>(true))
            {
                var o = l.bakingOutput;
                if (!(o.isBaked && o.lightmapBakeType == LightmapBakeType.Baked)) culling.Add(l, lightDistance);
            }
        }

        System.Func<int, (Vector3, Vector3, float, float, float)> SideStrip(int s, int ra, int rb, float uScale, float vScale)
        {
            var d = Geometry;
            var rows = new Vector2[4];
            return i =>
            {
                d.Rows(i, s, rows);
                Vector2 A = rows[ra], B = rows[rb];
                var pa = d.At(i, s, A.x, A.y);
                var pb = d.At(i, s, B.x, B.y);
                float w = (B.x - A.x) / uScale, v = i * d.Step / vScale;
                return s > 0 ? (pa, pb, 0f, w, v) : (pb, pa, w, 0f, v);
            };
        }

        /// <summary>
        /// Cinta a lo largo de la pista partida en tramos de <see cref="chunkPoints"/> puntos que comparten el punto
        /// de unión: cada tramo tiene sus propios límites, así Unity descarta los que quedan fuera de cámara.
        /// </summary>
        void AddRibbon(string name, int count, System.Func<int, (Vector3, Vector3, float, float, float)> row, Material mat,
            System.Func<int, bool> keep = null, bool doubleSided = false)
        {
            for (int start = 0; start < count - 1; start += chunkPoints)
            {
                int a = start, n = Mathf.Min(chunkPoints, count - 1 - start) + 1;
                if (keep != null)
                {
                    bool any = false;
                    for (int i = a; i < a + n - 1 && !any; i++) any = keep(i) && keep(i + 1);
                    if (!any) continue;
                }
                Add(name, MeshKit.Ribbon(n, j => row(a + j), keep == null ? null : (System.Func<int, bool>)(j => keep(a + j)), doubleSided), mat);
            }
        }

        /// <summary>Descarte por distancia de la pista (lo usa también el pueblo).</summary>
        internal DetailCulling Culling => culling;

        internal GameObject Add(string name, Mesh mesh, Material mat, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>
        /// Combina copias de una malla agrupándolas por celdas de <see cref="cellSize"/> metros, para que cada grupo
        /// se pueda descartar por separado. Con <paramref name="cullDistance"/> &gt; 0 el grupo se apaga de lejos.
        /// </summary>
        void AddCombined(string name, Mesh mesh, List<Matrix4x4> matrices, Material mat, float cullDistance = 0)
        {
            if (matrices.Count == 0) return;
            var cells = new Dictionary<Vector2Int, List<Matrix4x4>>();
            foreach (var m in matrices)
            {
                Vector3 p = m.GetColumn(3);
                var key = new Vector2Int(Mathf.FloorToInt(p.x / cellSize), Mathf.FloorToInt(p.z / cellSize));
                if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<Matrix4x4>();
                list.Add(m);
            }
            foreach (var cell in cells.Values)
                foreach (var m in MeshKit.Combine(mesh, cell))
                {
                    var go = Add(name, m, mat);
                    if (cullDistance > 0) culling.Add(go.GetComponent<MeshRenderer>(), cullDistance);
                }
        }

        static Mesh Primitive(PrimitiveType t)
        {
            var tmp = GameObject.CreatePrimitive(t);
            var m = tmp.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Destroy(tmp); else DestroyImmediate(tmp);
            return m;
        }

        void BuildPosts()
        {
            var d = Geometry;
            var cube = Primitive(PrimitiveType.Cube);
            var posts = new List<Matrix4x4>();
            var reflectors = new List<Matrix4x4>();
            var catEyes = new List<Matrix4x4>();
            for (int i = 0; i < d.N; i += 2)
            {
                var p = d.P[i];
                foreach (int s in new[] { -1, 1 })
                {
                    if (!d.HasRail(i, s)) continue;
                    var b = p.pos + p.right * (d.HW[i] + 0.55f) * s;
                    posts.Add(Matrix4x4.TRS(b + Vector3.up * 0.42f, Quaternion.identity, new Vector3(0.12f, 0.85f, 0.12f)));
                    if (i % 8 == 0) reflectors.Add(Matrix4x4.TRS(b + Vector3.up * 1.0f, Quaternion.identity, new Vector3(0.1f, 0.14f, 0.06f)));
                }
                if (i % 5 == 0 && !d.InTunnel(i) && i < d.TownStart)
                    catEyes.Add(Matrix4x4.TRS(p.pos + Vector3.up * 0.02f, Quaternion.Euler(0, p.heading * Mathf.Rad2Deg, 0), new Vector3(0.16f, 0.04f, 0.12f)));
            }
            AddCombined("Posts", cube, posts, MaterialFactory.Lit(new Color(0.49f, 0.51f, 0.55f)), detailDistance);
            AddCombined("Reflectors", cube, reflectors, MaterialFactory.Unlit(new Color(1f, 0.63f, 0.23f)), detailDistance);
            AddCombined("CatEyes", cube, catEyes, MaterialFactory.Unlit(new Color(1f, 0.82f, 0.48f)), detailDistance);
        }

        void BuildLamps()
        {
            var d = Geometry;
            var cube = Primitive(PrimitiveType.Cube);
            var cyl = Primitive(PrimitiveType.Cylinder);
            var poles = new List<Matrix4x4>();
            var arms = new List<Matrix4x4>();
            var heads = new List<Matrix4x4>();
            int n = 0;
            // Densidad del tema: 0 = sin faroles (solo los faros), 1 = uno cada 52 m, 2 = el doble
            float density = Theme != null ? Theme.lampDensity : 1;
            if (density <= 0.01f) return;
            int spacing = Mathf.Max(6, Mathf.RoundToInt(26 / density));
            for (int i = 12; i < d.N; i += spacing, n++)
            {
                if (d.InTunnel(i) || Mathf.Abs(i - d.TunnelStart) < 10 || Mathf.Abs(i - d.TunnelEnd) < 10) continue;
                if (i >= d.TownStart - 6) continue; // el pueblo tiene sus propios faroles en los postes
                var p = d.P[i];
                float a = d.WallHeight(i, 1), b = d.WallHeight(i, -1);
                int s = a < b ? 1 : b < a ? -1 : (n % 2 == 1 ? 1 : -1);
                var rot = Quaternion.Euler(0, p.heading * Mathf.Rad2Deg + 90, 0);
                poles.Add(Matrix4x4.TRS(d.At(i, s, d.HW[i] + 1.0f, p.pos.y + 3.1f), Quaternion.identity, new Vector3(0.2f, 3.1f, 0.2f)));
                arms.Add(Matrix4x4.TRS(d.At(i, s, d.HW[i], p.pos.y + 6.15f), rot, new Vector3(0.1f, 0.1f, 2.2f)));
                var headPos = d.At(i, s, d.HW[i] - 0.9f, p.pos.y + 6.05f);
                heads.Add(Matrix4x4.TRS(headPos, rot, new Vector3(0.35f, 0.14f, 0.7f)));
                // Luz real solo en algunos faroles: en móvil cada luz adicional cuesta. La Fase 3 hornea lightmaps.
                if (n % 2 == 0)
                {
                    var l = new GameObject("LampLight").AddComponent<Light>();
                    l.transform.SetParent(root, false);
                    l.transform.position = headPos - Vector3.up * 0.3f;
                    l.type = LightType.Point;
                    l.color = new Color(1f, 0.71f, 0.33f);
                    l.range = 16;
                    l.intensity = 2.2f;
                    l.shadows = LightShadows.None;
                    culling.Add(l, lightDistance);
                }
            }
            var metal = MaterialFactory.Lit(new Color(0.29f, 0.31f, 0.36f));
            AddCombined("LampPoles", cyl, poles, metal);
            AddCombined("LampArms", cube, arms, metal);
            AddCombined("LampHeads", cube, heads, MaterialFactory.Emissive(new Color(1f, 0.85f, 0.63f), new Color(2.4f, 1.6f, 0.7f)));
        }

        void BuildTunnel()
        {
            var d = Geometry;
            if (d.TunnelStart < 0) return;
            float W = d.HW[Mathf.Clamp(d.TunnelStart, 0, d.N - 1)];
            var prof = new[]
            {
                new Vector2(-W - 0.3f, 0), new Vector2(-W - 0.3f, 4.0f), new Vector2(-W + 1.4f, 5.7f), new Vector2(0, 6.3f),
                new Vector2(W - 1.4f, 5.7f), new Vector2(W + 0.3f, 4.0f), new Vector2(W + 0.3f, 0)
            };
            int a = d.TunnelStart - 2, b = d.TunnelEnd + 2;
            var shell = MeshKit.Grid(b - a + 1, prof.Length,
                (i, k) => { var p = d.P[a + i]; return p.pos + p.right * prof[k].x + Vector3.up * prof[k].y; },
                (i, k) => new Vector2(k / 2f, (a + i) * d.Step / 4f), true);
            Add("TunnelShell", shell, MaterialFactory.Lit(new Color(0.75f, 0.6f, 0.42f), ProceduralTextures.TunnelPanel(), 0.1f));

            var cube = Primitive(PrimitiveType.Cube);
            var lamps = new List<Matrix4x4>();
            for (int i = d.TunnelStart; i <= d.TunnelEnd; i += 4)
            {
                var p = d.P[i];
                foreach (int s in new[] { -1, 1 })
                    lamps.Add(Matrix4x4.TRS(d.At(i, s, W - 1.0f, p.pos.y + 5.35f), Quaternion.Euler(0, p.heading * Mathf.Rad2Deg, 0), new Vector3(0.25f, 0.12f, 1.2f)));
                if ((i - d.TunnelStart) % 12 == 0)
                {
                    var l = new GameObject("TunnelLight").AddComponent<Light>();
                    l.transform.SetParent(root, false);
                    l.transform.position = p.pos + Vector3.up * 5f;
                    l.type = LightType.Point;
                    l.color = new Color(1f, 0.63f, 0.25f);
                    l.range = 20;
                    l.intensity = 2.5f;
                    l.shadows = LightShadows.None;
                    culling.Add(l, lightDistance);
                }
            }
            AddCombined("TunnelLamps", cube, lamps, MaterialFactory.Emissive(new Color(1f, 0.75f, 0.44f), new Color(3f, 1.8f, 0.6f)));

            // Bocas del túnel
            var concrete = MaterialFactory.Lit(new Color(0.42f, 0.41f, 0.39f));
            for (int e = 0; e < 2; e++)
            {
                int i = e == 0 ? d.TunnelStart - 2 : d.TunnelEnd + 2;
                float face = e == 0 ? 0 : 180;
                var p = d.P[i];
                var portal = new GameObject("TunnelPortal").transform;
                portal.SetParent(root, false);
                portal.SetPositionAndRotation(p.pos, Quaternion.Euler(0, p.heading * Mathf.Rad2Deg + face, 0));
                Block(portal, new Vector3(-(W + 0.3f) - 8, 6, 0), new Vector3(16, 16, 1.4f), concrete);
                Block(portal, new Vector3(W + 0.3f + 8, 6, 0), new Vector3(16, 16, 1.4f), concrete);
                Block(portal, new Vector3(0, 6.3f + 4.8f, 0), new Vector3(2 * W + 0.6f, 9.6f, 1.4f), concrete);
            }
        }

        void Block(Transform parent, Vector3 localPos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Block";
            if (Application.isPlaying) Destroy(go.GetComponent<Collider>()); else DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        void BuildForest()
        {
            var d = Geometry;
            var rows = new Vector2[4];
            var cone = ConeMesh(7);
            var tiers = new[] { new List<Matrix4x4>(), new List<Matrix4x4>(), new List<Matrix4x4>() };
            var crowns = new[] { new List<Matrix4x4>(), new List<Matrix4x4>(), new List<Matrix4x4>() };
            var trunks = new List<Matrix4x4>();
            var theme = Theme;
            var primary = theme != null ? theme.vegetation : Vegetation.Cedros;
            float mix = theme != null ? theme.mix : 0;
            int count = Mathf.RoundToInt(treeCount * (theme != null ? theme.treeDensity : 1));
            int tries = 0, placed = 0;
            while (placed < count && tries++ < count * 6)
            {
                int i = rng.Next(d.N), s = rng.Next(2) == 0 ? -1 : 1;
                if (Mathf.Abs(i - d.TunnelStart) < 4 || d.InTunnel(i) || i >= d.TownStart - 8) continue;
                d.Rows(i, s, rows);
                if (rows[3].x - rows[2].x < 3) continue;
                float lat = Mathf.Lerp(rows[2].x + 1, rows[3].x - 0.5f, (float)rng.NextDouble());
                if (d.Platform(i, s) > 0.3f && lat < d.HW[i] + 10) continue;
                var pos = d.At(i, s, lat, 0);
                bool ok = true;
                for (int j = 0; j < d.N; j += 3)
                {
                    float dx = pos.x - d.P[j].pos.x, dz = pos.z - d.P[j].pos.z;
                    if (dx * dx + dz * dz < (d.HW[j] + 3) * (d.HW[j] + 3)) { ok = false; break; }
                }
                if (!ok || InsideLandmark(pos)) continue;
                pos.y = d.TerrainY(i, s, lat) - 0.2f;
                float sc = Mathf.Lerp(0.75f, 1.5f, (float)rng.NextDouble()), hs = sc * Mathf.Lerp(0.95f, 1.35f, (float)rng.NextDouble());
                var rot = Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0);
                // La mezcla saca otro número al azar solo si hay mezcla: sin tema, el bosque es el mismo de siempre
                var kind = primary;
                if (mix > 0 && rng.NextDouble() < mix) kind = primary == Vegetation.Cedros ? Vegetation.HojaAncha : Vegetation.Cedros;
                if (kind == Vegetation.Cedros)
                {
                    // cedro en tres pisos
                    tiers[0].Add(Matrix4x4.TRS(pos + Vector3.up * 1.1f * hs, rot, new Vector3(3.4f * sc, 4.2f * hs, 3.4f * sc)));
                    tiers[1].Add(Matrix4x4.TRS(pos + Vector3.up * 3.7f * hs, rot, new Vector3(2.5f * sc, 3.4f * hs, 2.5f * sc)));
                    tiers[2].Add(Matrix4x4.TRS(pos + Vector3.up * 6.0f * hs, rot, new Vector3(1.4f * sc, 2.2f * hs, 1.4f * sc)));
                    trunks.Add(Matrix4x4.TRS(pos + Vector3.up * 0.8f * hs, rot, new Vector3(0.4f * sc, 0.8f * hs, 0.4f * sc)));
                }
                else
                {
                    // hoja ancha o arce: copa redonda en dos bolas sobre un tronco más alto
                    var crown = crowns[placed % 3];
                    crown.Add(Matrix4x4.TRS(pos + Vector3.up * 3.6f * hs, rot, new Vector3(4.4f * sc, 3.4f * hs, 4.4f * sc)));
                    crown.Add(Matrix4x4.TRS(pos + Vector3.up * 5.0f * hs + rot * Vector3.right * 0.9f * sc, rot, new Vector3(2.8f * sc, 2.4f * hs, 2.8f * sc)));
                    trunks.Add(Matrix4x4.TRS(pos + Vector3.up * 1.3f * hs, rot, new Vector3(0.38f * sc, 1.3f * hs, 0.38f * sc)));
                }
                placed++;
            }
            var greens = new[] { new Color(0.09f, 0.16f, 0.09f), new Color(0.11f, 0.19f, 0.1f), new Color(0.13f, 0.22f, 0.12f) };
            for (int t = 0; t < 3; t++) AddCombined("Cedars", cone, tiers[t], MaterialFactory.Lit(greens[t], null, 0));
            var leaves = primary == Vegetation.ArcesOtono
                ? new[] { new Color(0.42f, 0.07f, 0.04f), new Color(0.5f, 0.2f, 0.04f), new Color(0.45f, 0.33f, 0.06f) }
                : new[] { new Color(0.12f, 0.2f, 0.08f), new Color(0.15f, 0.24f, 0.09f), new Color(0.1f, 0.17f, 0.08f) };
            // Copa de 48 triángulos (la esfera de Unity tiene 768 y hay dos por árbol: en el móvil costaba más de 1 ms)
            var crownMesh = LowSphere(8, 4);
            for (int t = 0; t < 3; t++) AddCombined("Leafy", crownMesh, crowns[t], MaterialFactory.Lit(leaves[t], null, 0));
            AddCombined("Trunks", Primitive(PrimitiveType.Cylinder), trunks, MaterialFactory.Lit(new Color(0.17f, 0.12f, 0.09f)));
        }

        /// <summary>Esfera de diámetro 1 con pocos lados (sombreado plano), para las copas de los árboles.</summary>
        static Mesh LowSphere(int sides, int rings)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            Vector3 P(int ring, int side)
            {
                float a = side / (float)sides * Mathf.PI * 2, e = Mathf.PI * ring / rings - Mathf.PI / 2;
                return new Vector3(Mathf.Cos(e) * Mathf.Cos(a), Mathf.Sin(e), Mathf.Cos(e) * Mathf.Sin(a)) * 0.5f;
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < sides; s++)
                {
                    var a = P(r, s); var b = P(r, s + 1); var c = P(r + 1, s + 1); var d = P(r + 1, s);
                    int o = v.Count;
                    // Triángulos sueltos: caras planas, como el resto del bosque
                    if (r > 0) { v.Add(a); v.Add(d); v.Add(b); t.Add(o); t.Add(o + 1); t.Add(o + 2); o += 3; }
                    if (r < rings - 1) { v.Add(b); v.Add(d); v.Add(c); t.Add(o); t.Add(o + 1); t.Add(o + 2); }
                }
            // Cada cara mirando hacia fuera (con su centro)
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 p0 = v[t[i]], p1 = v[t[i + 1]], p2 = v[t[i + 2]];
                if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), p0 + p1 + p2) < 0) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; }
            }
            var m = new Mesh();
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Cono de base 1 y altura 1, con la base en y=0.</summary>
        static Mesh ConeMesh(int sides)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2, a1 = (i + 1) / (float)sides * Mathf.PI * 2;
                var p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0, Mathf.Sin(a0) * 0.5f);
                var p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0, Mathf.Sin(a1) * 0.5f);
                int o = v.Count;
                v.Add(p0); v.Add(Vector3.up); v.Add(p1);
                t.Add(o); t.Add(o + 1); t.Add(o + 2);
            }
            var m = new Mesh();
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            // corrige el orden si las caras quedaron hacia dentro
            var n = Vector3.Cross(v[1] - v[0], v[2] - v[0]);
            if (Vector3.Dot(n, v[0]) < 0)
            {
                for (int i = 0; i < t.Count; i += 3) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; }
                m.SetTriangles(t, 0);
            }
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        void BuildBanner(string name, int i, Color color)
        {
            var d = Geometry;
            i = Mathf.Clamp(i, 0, d.N - 1);
            var p = d.P[i];
            var g = new GameObject(name).transform;
            g.SetParent(root, false);
            g.SetPositionAndRotation(p.pos, Quaternion.Euler(0, p.heading * Mathf.Rad2Deg, 0));
            var metal = MaterialFactory.Lit(new Color(0.33f, 0.36f, 0.4f));
            Block(g, new Vector3(0, 5.2f, 0), new Vector3(d.HW[i] * 2 + 1, 1.3f, 0.15f), MaterialFactory.Emissive(color, color * 0.6f));
            Block(g, new Vector3(-(d.HW[i] + 0.5f), 2.9f, 0), new Vector3(0.2f, 5.8f, 0.2f), metal);
            Block(g, new Vector3(d.HW[i] + 0.5f, 2.9f, 0), new Vector3(0.2f, 5.8f, 0.2f), metal);
        }
    }
}

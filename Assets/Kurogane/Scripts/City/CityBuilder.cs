using System.Collections.Generic;
using Kurogane.Core;
using Kurogane.Track;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kurogane.City
{
    /// <summary>
    /// Pueblo de la meta (bloque B): calle mayor con aceras, dos calles transversales, paso a nivel, casas de dos
    /// pisos con tejado de kawara, tiendas, konbini, gasolinera, máquinas expendedoras, cabina, postes eléctricos con
    /// cables, semáforo en ámbar, arrozales, cedros, la tienda de tofu donde termina la llegada y la ciudad media del
    /// valle. Casi todo usa un único material con el atlas del pueblo y se combina por celdas de la pista, así entra
    /// en el troceado, en DetailCulling y en Kurogane > Hornear pista.
    /// Coordenadas locales de la calle: x lateral (negativo = izquierda), z a lo largo desde el inicio de la calle.
    /// </summary>
    public class CityBuilder
    {
        enum Layer { Ground, Buildings, Detail, Water, MidCity }

        // Nombres de los objetos generados: KuroganeBake y TrackBuilder los usan para lightmaps y descarte
        public const string GroundName = "PuebloSuelo", BuildingsName = "Pueblo", DetailName = "PuebloDetalle",
            WaterName = "Arrozales", MidCityName = "CiudadMedia", LightName = "LuzPueblo";
        static readonly string[] LayerNames = { GroundName, BuildingsName, DetailName, WaterName, MidCityName };

        class MeshData
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<Color> c = new List<Color>();
            public readonly List<int> t = new List<int>();
        }

        readonly TrackBuilder tb;
        readonly TrackData d;
        readonly CityStyle style;
        readonly System.Random rng;
        readonly Vector3 origin, fwd, right;
        readonly float length, W;
        readonly float z1, z2, zr, zs;
        readonly Dictionary<(int, int, Layer), MeshData> meshes = new Dictionary<(int, int, Layer), MeshData>();
        readonly List<Renderer> amberLamps = new List<Renderer>(), crossingLamps = new List<Renderer>();
        Font font;

        public CityBuilder(TrackBuilder builder, CityStyle cityStyle)
        {
            tb = builder;
            d = builder.Data;
            style = cityStyle != null ? cityStyle : ScriptableObject.CreateInstance<CityStyle>();
            rng = new System.Random(style.seed);
            var p = d.P[d.TownStart];
            origin = p.pos;
            fwd = p.forward;
            right = p.right;
            W = d.W;
            length = (d.N - 1 - d.TownStart) * d.Step;
            z1 = length * 0.25f;   // primera calle transversal (semáforo)
            zr = length * 0.43f;   // paso a nivel
            z2 = length * 0.62f;   // segunda calle transversal (konbini)
            zs = (d.StopIndex - d.TownStart) * d.Step; // tienda de tofu
        }

        public void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Streets();
            RailCrossing();
            TrafficLight();
            Lots();
            Poles();
            Paddies();
            Cedars();
            MidCity();
            Flush();
        }

        // ---------- Coordenadas ----------

        float StreetY(float z)
        {
            float fi = z / d.Step;
            int i = Mathf.FloorToInt(fi);
            if (i < 0) return origin.y - z * tb.definition.afterGoalSlope;
            int a = d.TownStart + i;
            if (a >= d.N - 1) return d.P[d.N - 1].pos.y - (z - length) * tb.definition.afterGoalSlope;
            return Mathf.Lerp(d.P[a].pos.y, d.P[a + 1].pos.y, fi - i);
        }

        /// <summary>Punto del mundo a x metros de la calle mayor, z metros a lo largo, y metros sobre la calle.</summary>
        Vector3 W3(float x, float z, float y)
        {
            var p = origin + right * x + fwd * z;
            p.y = StreetY(z) + y;
            return p;
        }

        Quaternion Facing(int side) => Quaternion.LookRotation(side < 0 ? right : -right); // de cara a la calle mayor
        Quaternion Street => Quaternion.LookRotation(fwd);

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        bool Chance(float p) => rng.NextDouble() < p;

        // ---------- Primitivas ----------

        MeshData Get(Vector3 at, Layer layer)
        {
            float cell = layer == Layer.MidCity ? tb.cellSize * 6 : tb.cellSize;
            var key = (Mathf.FloorToInt(at.x / cell), Mathf.FloorToInt(at.z / cell), layer);
            if (!meshes.TryGetValue(key, out var m)) meshes[key] = m = new MeshData();
            return m;
        }

        /// <summary>Cuadrilátero visto de frente: abajo-izquierda, abajo-derecha, arriba-derecha, arriba-izquierda.</summary>
        void Quad(Layer layer, Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl, Rect uv, Color? color = null)
        {
            var m = Get((bl + tr) * 0.5f, layer);
            int o = m.v.Count;
            m.v.Add(bl); m.v.Add(br); m.v.Add(tr); m.v.Add(tl);
            m.uv.Add(new Vector2(uv.xMin, uv.yMin)); m.uv.Add(new Vector2(uv.xMax, uv.yMin));
            m.uv.Add(new Vector2(uv.xMax, uv.yMax)); m.uv.Add(new Vector2(uv.xMin, uv.yMax));
            var c = color ?? Color.white;
            for (int k = 0; k < 4; k++) m.c.Add(c);
            m.t.Add(o); m.t.Add(o + 3); m.t.Add(o + 2);
            m.t.Add(o); m.t.Add(o + 2); m.t.Add(o + 1);
        }

        void Quad(Layer layer, Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl, TownCell cell) =>
            Quad(layer, bl, br, tr, tl, ProceduralTextures.TownCell((int)cell));

        // Esquinas de cada cara del cubo unidad vistas desde fuera (abajo-izq, abajo-der, arriba-der, arriba-izq)
        static readonly Vector3[][] Faces =
        {
            new[] { new Vector3(.5f, -.5f, .5f), new Vector3(-.5f, -.5f, .5f), new Vector3(-.5f, .5f, .5f), new Vector3(.5f, .5f, .5f) },     // +Z
            new[] { new Vector3(-.5f, -.5f, -.5f), new Vector3(.5f, -.5f, -.5f), new Vector3(.5f, .5f, -.5f), new Vector3(-.5f, .5f, -.5f) }, // -Z
            new[] { new Vector3(.5f, -.5f, -.5f), new Vector3(.5f, -.5f, .5f), new Vector3(.5f, .5f, .5f), new Vector3(.5f, .5f, -.5f) },     // +X
            new[] { new Vector3(-.5f, -.5f, .5f), new Vector3(-.5f, -.5f, -.5f), new Vector3(-.5f, .5f, -.5f), new Vector3(-.5f, .5f, .5f) }, // -X
            new[] { new Vector3(-.5f, .5f, -.5f), new Vector3(.5f, .5f, -.5f), new Vector3(.5f, .5f, .5f), new Vector3(-.5f, .5f, .5f) },     // +Y
            new[] { new Vector3(.5f, -.5f, -.5f), new Vector3(-.5f, -.5f, -.5f), new Vector3(-.5f, -.5f, .5f), new Vector3(.5f, -.5f, .5f) }, // -Y
        };

        /// <summary>Caja con una celda por cara: frente (+Z local), detrás, lados, techo y, si se pide, suelo.</summary>
        void Box(Layer layer, Vector3 center, Quaternion rot, Vector3 size, TownCell front, TownCell back, TownCell sides,
            TownCell top, TownCell? bottom = null)
        {
            var m = Matrix4x4.TRS(center, rot, size);
            var cells = new[] { front, back, sides, sides, top, bottom ?? top };
            for (int f = 0; f < (bottom.HasValue ? 6 : 5); f++)
            {
                var q = Faces[f];
                Quad(layer, m.MultiplyPoint3x4(q[0]), m.MultiplyPoint3x4(q[1]), m.MultiplyPoint3x4(q[2]), m.MultiplyPoint3x4(q[3]), cells[f]);
            }
        }

        void Box(Layer layer, Vector3 center, Quaternion rot, Vector3 size, TownCell all) => Box(layer, center, rot, size, all, all, all, all);

        /// <summary>Caja con la textura repetida cada <paramref name="tile"/> metros (ciudad media, textura propia).</summary>
        void TiledBox(Layer layer, Vector3 center, Quaternion rot, Vector3 size, float tile, Color tint)
        {
            var m = Matrix4x4.TRS(center, rot, size);
            var faceSize = new[]
            {
                new Vector2(size.x, size.y), new Vector2(size.x, size.y), new Vector2(size.z, size.y), new Vector2(size.z, size.y),
            };
            for (int f = 0; f < 5; f++)
            {
                var q = Faces[f];
                // el techo, oscuro y sin ventanas
                var uv = f < 4 ? new Rect(R(0, 1), 0, faceSize[f].x / tile, faceSize[f].y / tile) : new Rect(0, 0, 0.01f, 0.01f);
                Quad(layer, m.MultiplyPoint3x4(q[0]), m.MultiplyPoint3x4(q[1]), m.MultiplyPoint3x4(q[2]), m.MultiplyPoint3x4(q[3]), uv,
                    f < 4 ? tint : tint * 0.3f);
            }
        }

        /// <summary>Tejado a dos aguas con la cumbrera a lo largo del eje Z local; base a la altura de <paramref name="eave"/>.</summary>
        void GableRoof(Vector3 eave, Quaternion rot, float width, float len, float rise, TownCell roof, TownCell gable)
        {
            var m = Matrix4x4.TRS(eave, rot, Vector3.one);
            float w = width / 2, l = len / 2;
            Vector3 P(float x, float y, float z) => m.MultiplyPoint3x4(new Vector3(x, y, z));
            Quad(Layer.Buildings, P(w, 0, -l), P(w, 0, l), P(0, rise, l), P(0, rise, -l), roof);
            Quad(Layer.Buildings, P(-w, 0, l), P(-w, 0, -l), P(0, rise, -l), P(0, rise, l), roof);
            Tri(P(-w, 0, -l), P(0, rise, -l), P(w, 0, -l), gable);
            Tri(P(w, 0, l), P(0, rise, l), P(-w, 0, l), gable);
        }

        void Tri(Vector3 a, Vector3 b, Vector3 c, TownCell cell)
        {
            var m = Get(a, Layer.Buildings);
            var uv = ProceduralTextures.TownCell((int)cell);
            int o = m.v.Count;
            m.v.Add(a); m.v.Add(b); m.v.Add(c);
            m.uv.Add(new Vector2(uv.xMin, uv.yMin)); m.uv.Add(new Vector2(uv.center.x, uv.yMax)); m.uv.Add(new Vector2(uv.xMax, uv.yMin));
            for (int k = 0; k < 3; k++) m.c.Add(Color.white);
            m.t.Add(o); m.t.Add(o + 1); m.t.Add(o + 2);
        }

        /// <summary>Cilindro vertical de 6 lados desde <paramref name="foot"/> (postes, transformadores, troncos).</summary>
        void Cylinder(Layer layer, Vector3 foot, float radius, float height, TownCell cell, int sides = 6)
        {
            for (int s = 0; s < sides; s++)
            {
                float a0 = s * Mathf.PI * 2 / sides, a1 = (s + 1) * Mathf.PI * 2 / sides;
                var p0 = foot + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius;
                var p1 = foot + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius;
                Quad(layer, p0, p1, p1 + Vector3.up * height, p0 + Vector3.up * height, cell);
            }
        }

        void Cone(Vector3 foot, float radius, float height, TownCell cell)
        {
            const int sides = 7;
            var tip = foot + Vector3.up * height;
            for (int s = 0; s < sides; s++)
            {
                float a0 = s * Mathf.PI * 2 / sides, a1 = (s + 1) * Mathf.PI * 2 / sides;
                Tri(foot + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius, tip,
                    foot + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius, cell);
            }
        }

        /// <summary>Cable colgando en catenaria entre dos puntos: tiras finas de doble cara.</summary>
        void Cable(Vector3 a, Vector3 b, float sag)
        {
            const int segs = 8;
            const float half = 0.02f;
            var prev = a;
            for (int k = 1; k <= segs; k++)
            {
                float t = k / (float)segs;
                var p = Vector3.Lerp(a, b, t) - Vector3.up * sag * 4 * t * (1 - t);
                var up = Vector3.up * half;
                Quad(Layer.Detail, prev - up, p - up, p + up, prev + up, TownCell.MetalDark);
                Quad(Layer.Detail, p - up, prev - up, prev + up, p + up, TownCell.MetalDark);
                prev = p;
            }
        }

        // ---------- Calles ----------

        bool Reserved(float z, float margin) =>
            Mathf.Abs(z - z1) < 4.5f + margin || Mathf.Abs(z - z2) < 4.5f + margin || Mathf.Abs(z - zr) < 2.5f + margin;

        void Streets()
        {
            // Aceras con bordillo a los dos lados de la calle mayor, en tramos que siguen la pendiente
            for (int s = -1; s <= 1; s += 2)
                for (float z = 0; z < length; z += 6)
                {
                    float za = z, zb = Mathf.Min(length, z + 6);
                    if (Reserved((za + zb) / 2, 0)) continue;
                    float x0 = s * W, x1 = s * (W + 2.2f);
                    var bl = W3(s < 0 ? x1 : x0, za, 0.15f);
                    var br = W3(s < 0 ? x0 : x1, za, 0.15f);
                    var tr = W3(s < 0 ? x0 : x1, zb, 0.15f);
                    var tl = W3(s < 0 ? x1 : x0, zb, 0.15f);
                    Quad(Layer.Ground, bl, br, tr, tl, TownCell.Sidewalk);
                    // bordillo hacia la calzada
                    var c0 = W3(x0, za, -0.02f);
                    var c1 = W3(x0, zb, -0.02f);
                    if (s > 0) Quad(Layer.Ground, c1, c0, c0 + Vector3.up * 0.17f, c1 + Vector3.up * 0.17f, TownCell.Concrete);
                    else Quad(Layer.Ground, c0, c1, c1 + Vector3.up * 0.17f, c0 + Vector3.up * 0.17f, TownCell.Concrete);
                }

            // Calles transversales: losas de asfalto a ambos lados, con línea central
            foreach (float zc in new[] { z1, z2 })
                for (int s = -1; s <= 1; s += 2)
                {
                    float x0 = W, x1 = 70, xm = s * (x0 + x1) / 2;
                    Box(Layer.Ground, W3(xm, zc, -0.17f), Street, new Vector3(x1 - x0, 0.38f, 9), TownCell.Concrete, TownCell.Concrete,
                        TownCell.Concrete, TownCell.Asphalt);
                    for (float x = x0 + 2; x < x1 - 2; x += 4)
                    {
                        float xa = s * x, xb = s * (x + 2);
                        Quad(Layer.Ground, W3(Mathf.Min(xa, xb), zc - 0.08f, 0.03f), W3(Mathf.Max(xa, xb), zc - 0.08f, 0.03f),
                            W3(Mathf.Max(xa, xb), zc + 0.08f, 0.03f), W3(Mathf.Min(xa, xb), zc + 0.08f, 0.03f), TownCell.Paint);
                    }
                }
        }

        void RailCrossing()
        {
            // Balasto y traviesas fuera de la calzada; carriles de lado a lado
            for (int s = -1; s <= 1; s += 2)
            {
                float x0 = W, x1 = 75;
                Box(Layer.Ground, W3(s * (x0 + x1) / 2, zr, -0.12f), Street, new Vector3(x1 - x0, 0.3f, 3.2f), TownCell.Ballast);
                for (float x = x0 + 0.4f; x < x1; x += 0.6f)
                    Box(Layer.Buildings, W3(s * x, zr, 0.06f), Street, new Vector3(0.22f, 0.1f, 2.2f), TownCell.RailTie);
            }
            foreach (float dz in new[] { -0.72f, 0.72f })
                Box(Layer.Buildings, W3(0, zr + dz, 0.07f), Street, new Vector3(150, 0.12f, 0.07f), TownCell.RailSteel);

            // Barreras (levantadas), cruces de San Andrés y luces rojas: en cada sentido, antes de la vía y a la
            // izquierda del que llega (se circula por la izquierda)
            var red = MaterialFactory.Emissive(new Color(0.5f, 0.02f, 0.02f), new Color(3f, 0.15f, 0.1f));
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * (W + 0.9f), z = zr + s * 2.6f;
                var facing = s < 0 ? Quaternion.LookRotation(-fwd) : Quaternion.LookRotation(fwd); // hacia el tráfico que llega
                Cylinder(Layer.Detail, W3(x, z, 0), 0.09f, 3.9f, TownCell.Stripes);
                Box(Layer.Detail, W3(x - s * 0.25f, z, 3.2f), Street, new Vector3(0.12f, 4.2f, 0.12f), TownCell.Stripes);
                foreach (float ang in new[] { 35f, -35f })
                    Box(Layer.Detail, W3(x, z, 3.55f), facing * Quaternion.Euler(0, 0, ang), new Vector3(1.25f, 0.18f, 0.04f), TownCell.Stripes);
                for (int k = -1; k <= 1; k += 2)
                {
                    var lamp = W3(x, z, 2.75f) + (facing * Vector3.right) * (k * 0.33f);
                    Box(Layer.Detail, lamp, facing, new Vector3(0.3f, 0.3f, 0.14f), TownCell.Housing);
                    crossingLamps.Add(LampQuad("LuzPasoNivel", lamp + facing * Vector3.forward * 0.08f, facing, 0.2f, red));
                }
            }
        }

        void TrafficLight()
        {
            float x = W + 1.5f, z = z1 - 5.5f;
            Cylinder(Layer.Detail, W3(x, z, 0), 0.11f, 5.6f, TownCell.MetalDark);
            Box(Layer.Detail, W3(x / 2, z, 5.45f), Street, new Vector3(x, 0.12f, 0.12f), TownCell.MetalDark);
            // cabeza mirando al coche que llega (que va hacia +fwd)
            var facing = Quaternion.LookRotation(-fwd);
            var head = W3(W * 0.35f, z, 5.2f);
            Box(Layer.Detail, head, facing, new Vector3(1.15f, 0.42f, 0.32f), TownCell.Housing);
            var amber = MaterialFactory.Emissive(new Color(0.6f, 0.35f, 0.02f), new Color(3f, 1.6f, 0.1f));
            amberLamps.Add(LampQuad("Ambar", head + facing * Vector3.forward * 0.17f, facing, 0.24f, amber));
            var blink = tb.Root.gameObject.AddComponent<Blinker>();
            blink.lamps = amberLamps.ToArray();
            blink.period = 1.2f;
            var crossing = tb.Root.gameObject.AddComponent<Blinker>();
            crossing.lamps = crossingLamps.ToArray();
            crossing.alternate = true;
            crossing.period = 1.0f;
            crossing.active = false; // el bloque D lo enciende al bajar la barrera
        }

        Renderer LampQuad(string name, Vector3 center, Quaternion facing, float size, Material mat)
        {
            var go = tb.Add(name, Primitive.Quad, mat);
            // El Quad de Unity se ve desde -Z: mirando hacia el tráfico, su -Z apunta al coche
            go.transform.SetPositionAndRotation(center, facing * Quaternion.Euler(0, 180, 0));
            go.transform.localScale = Vector3.one * size;
            var r = go.GetComponent<MeshRenderer>();
            tb.Culling.Add(r, tb.detailDistance);
            return r;
        }

        // ---------- Parcelas ----------

        void Lots()
        {
            // Sitios fijos: gasolinera (derecha, al principio), konbini (derecha, tras la segunda transversal),
            // tienda de tofu (izquierda, donde para el coche), máquinas junto al paso a nivel y cabina
            GasStation(1, 8, 32);
            Konbini(1, z2 + 6, z2 + 24);
            TofuShop(-1, zs);
            Vending(-1, zr - 11);
            PhoneBooth(1, z2 + 27);

            for (int s = -1; s <= 1; s += 2)
            {
                int shopNames = 0;
                // Primera fila, de cara a la calle mayor
                for (float z = 3; z < length - 6;)
                {
                    float w = R(8.5f, 11.5f), zc = z + w / 2;
                    if (Blocked(s, zc, w)) { z += 2; continue; }
                    bool shop = Chance(style.shopShare);
                    string sign = shop && style.shopNames.Length > 0 ? style.shopNames[shopNames++ % style.shopNames.Length] : null;
                    House(s, W + 2.6f, zc, w, R(7, 9), shop, sign);
                    z += w + R(0.8f, 2.2f);
                }
                // Segunda fila, detrás
                for (float z = -6; z < length + 6;)
                {
                    float w = R(7.5f, 10.5f), zc = z + w / 2;
                    if (Mathf.Abs(zc - z1) > 8 && Mathf.Abs(zc - z2) > 8 && Mathf.Abs(zc - zr) > 6 && Chance(style.backRowDensity))
                        House(s, R(23, 27), zc, w, R(7, 8.5f), false, null);
                    z += w + R(1.5f, 4);
                }
            }
        }

        readonly List<(int side, float z0, float z1)> special = new List<(int, float, float)>();

        bool Blocked(int side, float zc, float w)
        {
            if (Reserved(zc, w / 2)) return true;
            foreach (var sp in special)
                if (sp.side == side && zc + w / 2 > sp.z0 - 1 && zc - w / 2 < sp.z1 + 1) return true;
            return false;
        }

        /// <summary>Casa de dos pisos (o tienda con persiana) de cara a la calle mayor.</summary>
        void House(int side, float setback, float zc, float w, float depth, bool shop, string sign)
        {
            int floors = 2;
            float h = floors * 2.9f;
            var rot = Facing(side);
            var front = rot * Vector3.forward;
            var baseC = W3(side * (setback + depth / 2), zc, -0.3f);
            var m = Matrix4x4.TRS(baseC, rot, Vector3.one);
            Vector3 L(float x, float y, float z) => m.MultiplyPoint3x4(new Vector3(x, y, z));

            var wall = (TownCell)rng.Next(0, 4);
            Box(Layer.Buildings, L(0, h / 2 - 0.5f, 0), rot, new Vector3(w, h + 1, depth), wall, wall, wall, TownCell.Roof);

            // Tejado de kawara, casi siempre con la cumbrera paralela a la calle
            bool ridgeAlong = Chance(0.7f);
            var roofRot = ridgeAlong ? rot * Quaternion.Euler(0, 90, 0) : rot;
            float rw = ridgeAlong ? depth + 1 : w + 1, rl = ridgeAlong ? w + 1 : depth + 1;
            GableRoof(L(0, h, 0), roofRot, rw, rl, rw * 0.32f, TownCell.Roof, wall);

            // Ventanas del piso de arriba (y del de abajo en las casas)
            int count = Mathf.Max(1, Mathf.FloorToInt(w / 3.2f));
            for (int f = shop ? 1 : 0; f < floors; f++)
                for (int k = 0; k < count; k++)
                {
                    float x = (k + 0.5f) / count * w - w / 2, y = f * 2.9f + 1.5f;
                    if (f == 0 && k == 0) { FaceQuad(L(x, 1.05f, depth / 2 + 0.03f), rot, 0.95f, 2.0f, TownCell.Door); continue; }
                    FaceQuad(L(x, y, depth / 2 + 0.03f), rot, 1.3f, 1.1f, Chance(style.litWindows) ? TownCell.WindowLit : TownCell.WindowDark);
                }
            // Una ventana en cada costado
            foreach (int sx in new[] { -1, 1 })
                FaceQuad(L(sx * (w / 2 + 0.03f), 4.4f, 0), rot * Quaternion.Euler(0, sx * 90, 0), 1.1f, 1.0f,
                    Chance(style.litWindows * 0.7f) ? TownCell.WindowLit : TownCell.WindowDark);

            if (shop)
            {
                bool open = Chance(0.4f);
                FaceQuad(L(0, 1.3f, depth / 2 + 0.03f), rot, w - 1.2f, 2.4f, open ? TownCell.WindowLit : TownCell.Shutter);
                var board = Chance(0.5f) ? TownCell.SignRed : TownCell.SignBlue;
                Box(Layer.Buildings, L(0, 2.85f, depth / 2 + 0.1f), rot, new Vector3(w - 0.6f, 0.7f, 0.15f), board, TownCell.MetalDark, TownCell.MetalDark, TownCell.MetalDark);
                if (!string.IsNullOrEmpty(sign)) Text(sign, L(0, 2.85f, depth / 2 + 0.2f), front, 0.42f, Color.white);
            }
            else if (Chance(0.6f))
            {
                // Balcón con barandilla y aire acondicionado
                float bw = w * 0.7f;
                Box(Layer.Buildings, L(0, 2.9f, depth / 2 + 0.45f), rot, new Vector3(bw, 0.12f, 0.9f), TownCell.Concrete);
                Box(Layer.Buildings, L(0, 3.45f, depth / 2 + 0.88f), rot, new Vector3(bw, 1.0f, 0.06f), TownCell.Balcony);
                Box(Layer.Detail, L(bw / 2 - 0.6f, 3.25f, depth / 2 + 0.35f), rot, new Vector3(0.8f, 0.55f, 0.3f), TownCell.AcUnit, TownCell.WallWhite, TownCell.WallWhite, TownCell.WallWhite);
            }
            if (Chance(0.5f))
                Box(Layer.Detail, L(w / 2 + 0.16f, 0.6f, depth / 4), rot * Quaternion.Euler(0, 90, 0), new Vector3(0.8f, 0.55f, 0.3f), TownCell.AcUnit, TownCell.WallWhite, TownCell.WallWhite, TownCell.WallWhite);
        }

        /// <summary>Panel plano pegado a una fachada que mira hacia rot*forward.</summary>
        void FaceQuad(Vector3 center, Quaternion rot, float w, float h, TownCell cell)
        {
            var r = rot * Vector3.right * (w / 2);
            var u = Vector3.up * (h / 2);
            // visto desde delante (mirando hacia -forward), la derecha del observador es -right
            Quad(Layer.Buildings, center + r - u, center - r - u, center - r + u, center + r + u, cell);
        }

        void Konbini(int side, float za, float zb)
        {
            special.Add((side, za, zb));
            float w = zb - za, depth = 13, h = 4.2f, setback = W + 2.6f + 6, zc = (za + zb) / 2;
            var rot = Facing(side);
            var front = rot * Vector3.forward;
            var baseC = W3(side * (setback + depth / 2), zc, -0.3f);
            var m = Matrix4x4.TRS(baseC, rot, Vector3.one);
            Vector3 L(float x, float y, float z) => m.MultiplyPoint3x4(new Vector3(x, y, z));

            Box(Layer.Buildings, L(0, h / 2 - 0.5f, 0), rot, new Vector3(w, h + 1, depth), TownCell.KonbiniWall, TownCell.KonbiniWall, TownCell.KonbiniWall, TownCell.Concrete);
            Box(Layer.Buildings, L(0, h - 0.4f, 0), rot, new Vector3(w + 0.2f, 0.9f, depth + 0.2f), TownCell.KonbiniStripe, TownCell.KonbiniStripe, TownCell.KonbiniStripe, TownCell.Concrete);
            // Escaparate en paneles de 2 m: la celda del atlas se repite en lugar de estirarse
            float glassW = w - 4.5f;
            int panels = Mathf.Max(1, Mathf.RoundToInt(glassW / 2));
            for (int k = 0; k < panels; k++)
                FaceQuad(L(-1.2f - glassW / 2 + (k + 0.5f) * glassW / panels, 1.45f, depth / 2 + 0.03f), rot, glassW / panels - 0.08f, 2.6f, TownCell.KonbiniGlass);
            FaceQuad(L(w / 2 - 1.8f, 1.2f, depth / 2 + 0.03f), rot, 1.8f, 2.3f, TownCell.KonbiniGlass);
            Text(style.konbiniName, L(0, h - 0.4f, depth / 2 + 0.16f), front, 0.5f, Color.white);

            // Aparcamiento delante, con líneas
            Box(Layer.Ground, W3(side * (W + 2.6f + 3), zc, -0.17f), Street, new Vector3(6, 0.38f, w), TownCell.Concrete, TownCell.Concrete, TownCell.Concrete, TownCell.Asphalt);
            for (float z = za + 2; z < zb - 1; z += 2.6f)
                Quad(Layer.Ground, W3(side * (W + 3.2f), z - 0.06f, 0.04f), W3(side * (W + 8.2f), z - 0.06f, 0.04f),
                    W3(side * (W + 8.2f), z + 0.06f, 0.04f), W3(side * (W + 3.2f), z + 0.06f, 0.04f), TownCell.Paint);

            AddLight(L(0, 3.4f, depth / 2 + 2.5f), new Color(0.85f, 0.93f, 1f), 13, 1.5f);
        }

        void GasStation(int side, float za, float zb)
        {
            special.Add((side, za, zb));
            float w = zb - za, zc = (za + zb) / 2, xc = side * (W + 3 + 6);
            var rot = Facing(side);
            var front = rot * Vector3.forward;
            var baseC = W3(xc, zc, -0.3f);
            var m = Matrix4x4.TRS(baseC, rot, Vector3.one);
            Vector3 L(float x, float y, float z) => m.MultiplyPoint3x4(new Vector3(x, y, z));

            Box(Layer.Ground, W3(xc, zc, -0.17f), Street, new Vector3(12, 0.38f, w + 4), TownCell.Concrete, TownCell.Concrete, TownCell.Concrete, TownCell.Asphalt);
            // Marquesina con el techo iluminado por debajo y frontal rojo
            Box(Layer.Buildings, L(0, 4.9f, 0), rot, new Vector3(w, 0.6f, 11), TownCell.SignRed, TownCell.SignRed, TownCell.SignRed, TownCell.Concrete, TownCell.CanopyLight);
            foreach (float px in new[] { -w / 2 + 2, w / 2 - 2 })
                foreach (float pz in new[] { -3f, 3f })
                    Box(Layer.Buildings, L(px, 2.3f, pz), rot, new Vector3(0.4f, 5.2f, 0.4f), TownCell.Concrete);
            foreach (float px in new[] { -w / 4, w / 4 })
            {
                Box(Layer.Buildings, L(px, 0.1f, 0), rot, new Vector3(3.2f, 0.4f, 1.1f), TownCell.Concrete);
                Box(Layer.Detail, L(px, 1.05f, 0), rot, new Vector3(0.8f, 1.6f, 0.5f), TownCell.VendingWhite, TownCell.Housing, TownCell.Housing, TownCell.Housing);
            }
            // Oficina detrás y cartel de precios
            Box(Layer.Buildings, L(0, 1.4f, -7.5f), rot, new Vector3(7, 3.4f, 4), TownCell.WallWhite);
            FaceQuad(L(0, 1.5f, -5.47f), rot, 4, 1.6f, TownCell.WindowLit);
            Cylinder(Layer.Detail, L(w / 2 + 1, -0.3f, 4.5f), 0.12f, 6, TownCell.MetalDark);
            Box(Layer.Detail, L(w / 2 + 1, 5.6f, 4.5f), rot, new Vector3(2.2f, 1.6f, 0.25f), TownCell.SignRed, TownCell.SignRed, TownCell.MetalDark, TownCell.MetalDark);
            Text("GAS", L(w / 2 + 1, 5.6f, 4.65f), front, 0.7f, Color.white);

            AddLight(L(0, 4.2f, 0), new Color(1f, 0.97f, 0.9f), 16, 1.6f);
        }

        void TofuShop(int side, float zc)
        {
            float w = 10, depth = 9, h = 5.8f;
            special.Add((side, zc - w / 2, zc + w / 2));
            var rot = Facing(side);
            var front = rot * Vector3.forward;
            var baseC = W3(side * (W + 2.6f + depth / 2), zc, -0.3f);
            var m = Matrix4x4.TRS(baseC, rot, Vector3.one);
            Vector3 L(float x, float y, float z) => m.MultiplyPoint3x4(new Vector3(x, y, z));

            Box(Layer.Buildings, L(0, h / 2 - 0.5f, 0), rot, new Vector3(w, h + 1, depth), TownCell.Wood, TownCell.Wood, TownCell.Wood, TownCell.Roof);
            GableRoof(L(0, h, 0), rot * Quaternion.Euler(0, 90, 0), depth + 1.2f, w + 1.2f, 2.6f, TownCell.Roof, TownCell.Wood);
            // Alero sobre la tienda
            Box(Layer.Buildings, L(0, 2.75f, depth / 2 + 0.6f), rot * Quaternion.Euler(-12, 0, 0), new Vector3(w + 0.6f, 0.1f, 1.3f), TownCell.Roof);
            // Tienda abierta e iluminada, con noren
            FaceQuad(L(0, 1.15f, depth / 2 + 0.03f), rot, w - 1.5f, 2.2f, TownCell.WindowLit);
            float nw = (w - 2.4f) / 3;
            for (int k = -1; k <= 1; k++)
                FaceQuad(L(k * (nw + 0.06f), 1.95f, depth / 2 + 0.28f), rot, nw, 0.75f, TownCell.Noren);
            // Rótulo de madera con el nombre, y ventanas arriba
            Box(Layer.Buildings, L(0, 3.35f, depth / 2 + 0.12f), rot, new Vector3(w - 1, 0.85f, 0.15f), TownCell.SignWood, TownCell.Wood, TownCell.Wood, TownCell.Wood);
            Text(style.tofuShopName, L(0, 3.35f, depth / 2 + 0.22f), front, 0.5f, new Color(1f, 0.95f, 0.82f));
            for (int k = -1; k <= 1; k += 2)
                FaceQuad(L(k * 2.6f, 4.5f, depth / 2 + 0.03f), rot, 1.6f, 1.1f, TownCell.WindowLit);
            // Farolillo rojo
            Box(Layer.Detail, L(w / 2 - 0.4f, 2.2f, depth / 2 + 0.5f), rot, new Vector3(0.42f, 0.62f, 0.42f), TownCell.Lantern);
            AddLight(L(0, 2.4f, depth / 2 + 2f), new Color(1f, 0.78f, 0.5f), 10, 1.5f);
        }

        void Vending(int side, float zc)
        {
            special.Add((side, zc - 2, zc + 2));
            var rot = Facing(side);
            for (int k = 0; k < 3; k++)
            {
                var front = (TownCell)((int)TownCell.VendingRed + k);
                Box(Layer.Detail, W3(side * (W + 2.9f), zc + (k - 1) * 1.0f, 1.05f), rot, new Vector3(0.95f, 1.85f, 0.8f), front, TownCell.WallWhite, TownCell.WallWhite, TownCell.MetalDark);
            }
        }

        void PhoneBooth(int side, float zc)
        {
            special.Add((side, zc - 1, zc + 1));
            var rot = Facing(side);
            Box(Layer.Detail, W3(side * (W + 2.9f), zc, 1.25f), rot, new Vector3(1, 2.3f, 1), TownCell.PhoneBooth, TownCell.PhoneBooth, TownCell.PhoneBooth, TownCell.Concrete);
        }

        // ---------- Postes eléctricos (電柱), cables y farolas ----------

        void Poles()
        {
            for (int s = -1; s <= 1; s += 2)
            {
                var tops = new List<(Vector3 pole, bool ok)>();
                int n = 0;
                for (float z = 5; z < length; z += style.poleSpacing, n++)
                {
                    if (Reserved(z, 2)) { tops.Add((Vector3.zero, false)); continue; }
                    float x = s * (W + 1.7f);
                    var foot = W3(x, z, 0.15f);
                    Cylinder(Layer.Detail, foot, 0.17f, 11, TownCell.Concrete);
                    Box(Layer.Detail, foot + Vector3.up * 9.6f, Street, new Vector3(2.3f, 0.12f, 0.12f), TownCell.MetalDark);
                    Box(Layer.Detail, foot + Vector3.up * 8.8f, Street, new Vector3(1.9f, 0.12f, 0.12f), TownCell.MetalDark);
                    if (n % 3 == 1) Cylinder(Layer.Detail, foot + Vector3.up * 7.1f + fwd * 0.4f, 0.34f, 1.1f, TownCell.Housing);
                    if (n % 2 == 0)
                    {
                        // Farola: brazo hacia la calzada y luz
                        var arm = foot + Vector3.up * 6.6f - right * s * 0.8f;
                        Box(Layer.Detail, arm, Street, new Vector3(1.6f, 0.1f, 0.1f), TownCell.MetalDark);
                        var head = foot + Vector3.up * 6.5f - right * s * 1.55f;
                        Box(Layer.Detail, head, Street, new Vector3(0.55f, 0.16f, 0.32f), TownCell.MetalDark, TownCell.MetalDark, TownCell.MetalDark, TownCell.MetalDark, TownCell.StreetLamp);
                        AddLight(head - Vector3.up * 0.3f, new Color(1f, 0.9f, 0.72f), 15, 1.5f);
                    }
                    tops.Add((foot, true));
                }
                // Cables entre postes consecutivos: tres arriba y dos abajo
                for (int k = 1; k < tops.Count; k++)
                {
                    if (!tops[k].ok || !tops[k - 1].ok) continue;
                    var a = tops[k - 1].pole;
                    var b = tops[k].pole;
                    foreach (float off in new[] { -1.0f, 0f, 1.0f }) Cable(a + Vector3.up * 9.66f + right * off, b + Vector3.up * 9.66f + right * off, 0.55f);
                    foreach (float off in new[] { -0.85f, 0.85f }) Cable(a + Vector3.up * 8.86f + right * off, b + Vector3.up * 8.86f + right * off, 0.6f);
                }
            }
        }

        // ---------- Alrededores ----------

        void Paddies()
        {
            for (int s = -1; s <= 1; s += 2)
                for (float z = -12; z < length + 12; z += 24)
                {
                    float x0 = 40, x1 = 68, zc = z + 12;
                    var a = W3(s * x0, z + 0.4f, -0.27f);
                    var b = W3(s * x1, z + 0.4f, -0.27f);
                    var c = W3(s * x1, z + 23.6f, -0.27f);
                    var e = W3(s * x0, z + 23.6f, -0.27f);
                    if (s > 0) Quad(Layer.Water, a, b, c, e, TownCell.Paddy);
                    else Quad(Layer.Water, b, a, e, c, TownCell.Paddy);
                    // caballones
                    Box(Layer.Ground, W3(s * (x0 + x1) / 2, z, -0.2f), Street, new Vector3(x1 - x0, 0.3f, 0.7f), TownCell.Dyke);
                    foreach (float x in new[] { x0, x1 })
                        Box(Layer.Ground, W3(s * x, zc, -0.2f), Street, new Vector3(0.7f, 0.3f, 24), TownCell.Dyke);
                }
        }

        void Cedars()
        {
            for (int s = -1; s <= 1; s += 2)
                for (float z = -10; z < length + 20; z += R(4.5f, 6.5f))
                {
                    var foot = W3(s * R(72, 77), z, -0.3f);
                    float sc = R(0.85f, 1.3f);
                    Cylinder(Layer.Buildings, foot, 0.2f * sc, 1.4f * sc, TownCell.Trunk, 5);
                    Cone(foot + Vector3.up * 0.8f * sc, 1.7f * sc, 4.2f * sc, TownCell.Cedar);
                    Cone(foot + Vector3.up * 3.2f * sc, 1.25f * sc, 3.4f * sc, TownCell.Cedar);
                    Cone(foot + Vector3.up * 5.4f * sc, 0.7f * sc, 2.2f * sc, TownCell.Cedar);
                }
        }

        /// <summary>
        /// Ciudad media del valle, de 300 m a 1,5 km tras el pueblo: bloques con ventanas encendidas, sin niebla.
        /// Solo se dibuja cerca del pueblo, cuando ChaseCamera alarga su plano lejano.
        /// </summary>
        void MidCity()
        {
            float endY = StreetY(length);
            for (int i = 0; i < style.midBuildings; i++)
            {
                float dz = 300 + Mathf.Pow((float)rng.NextDouble(), 0.8f) * 1200;
                float spread = 150 + dz * 0.45f;
                float x = (float)(rng.NextDouble() * 2 - 1) * spread;
                float centre = 1 - Mathf.Abs(x) / spread;
                float w = R(12, 30), dp = R(12, 30), h = 8 + Mathf.Pow((float)rng.NextDouble(), 2) * 40 * (0.4f + 0.6f * centre);
                var foot = origin + right * x + fwd * (length + dz);
                foot.y = endY - 8 - dz * 0.03f;
                var tint = Color.Lerp(new Color(0.8f, 0.8f, 0.85f), Color.white, (float)rng.NextDouble());
                TiledBox(Layer.MidCity, foot + Vector3.up * h / 2, Quaternion.LookRotation(fwd) * Quaternion.Euler(0, rng.Next(4) * 90 + R(-8, 8), 0),
                    new Vector3(w, h, dp), 6, tint);
            }
        }

        // ---------- Luces, rótulos y mallas ----------

        void AddLight(Vector3 pos, Color color, float range, float intensity)
        {
            var l = new GameObject(LightName).AddComponent<Light>();
            l.transform.SetParent(tb.Root, false);
            l.transform.position = pos;
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            tb.Culling.Add(l, tb.lightDistance);
        }

        /// <summary>Rótulo con TextMesh (admite kana y kanji con la fuente del sistema), legible desde la calle.</summary>
        void Text(string text, Vector3 pos, Vector3 facing, float height, Color color)
        {
            var go = new GameObject("Rotulo");
            go.transform.SetParent(tb.Root, false);
            // TextMesh se lee mirando hacia +Z: su +Z apunta en contra de quien lo mira
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-facing));
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = height * 10 / 64f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            tb.Culling.Add(mr, tb.detailDistance);
        }

        void Flush()
        {
            var atlas = ProceduralTextures.TownAtlas(out var emission);
            var town = Atlas(MaterialFactory.Lit(Color.white, atlas, 0.15f), emission);
            var water = Atlas(MaterialFactory.Lit(Color.white, atlas, 0.85f), emission);
            var sky = Shader.Find("Kurogane/Sky");
            Material mid = null;
            if (sky != null)
            {
                mid = new Material(sky) { renderQueue = (int)RenderQueue.Geometry };
                mid.SetTexture("_MainTex", ProceduralTextures.CityWindows());
                mid.SetFloat("_SrcBlend", (float)BlendMode.One);
                mid.SetFloat("_DstBlend", (float)BlendMode.Zero);
                mid.SetFloat("_ZWrite", 1);
            }

            foreach (var kv in meshes)
            {
                var layer = kv.Key.Item3;
                var md = kv.Value;
                if (md.v.Count == 0 || (layer == Layer.MidCity && mid == null)) continue;
                var mesh = new Mesh { indexFormat = md.v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(md.v);
                mesh.SetUVs(0, md.uv);
                mesh.SetColors(md.c);
                mesh.SetTriangles(md.t, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var mat = layer == Layer.Water ? water : layer == Layer.MidCity ? mid : town;
                var go = tb.Add(LayerNames[(int)layer], mesh, mat);
                if (layer == Layer.Detail) tb.Culling.Add(go.GetComponent<MeshRenderer>(), tb.detailDistance);
            }
        }

        static Material Atlas(Material m, Texture2D emission)
        {
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", emission);
            m.SetColor("_EmissionColor", Color.white);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return m;
        }

        static class Primitive
        {
            static Mesh quad;
            public static Mesh Quad
            {
                get
                {
                    if (quad != null) return quad;
                    var tmp = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad = tmp.GetComponent<MeshFilter>().sharedMesh;
                    if (Application.isPlaying) Object.Destroy(tmp); else Object.DestroyImmediate(tmp);
                    return quad;
                }
            }
        }
    }
}

using System.Collections.Generic;
using Kurogane.Core;
using UnityEngine;

namespace Kurogane.Track
{
    /// <summary>
    /// Decorado propio de cada pista (TrackDefinition.landmarks). Por ahora el viaducto de ladrillo de cuatro arcos,
    /// inspirado en el de la carretera vieja de Usui (se recrea a nuestra manera, sin nombres ni carteles reales).
    /// Los árboles no se plantan dentro de la huella de un monumento.
    /// </summary>
    public partial class TrackBuilder
    {
        struct Footprint
        {
            public Vector3 center, forward;
            public float halfLength, halfDepth;

            public bool Contains(Vector3 p, float margin)
            {
                var d = p - center;
                float along = d.x * forward.x + d.z * forward.z, across = d.x * forward.z - d.z * forward.x;
                return Mathf.Abs(along) < halfLength + margin && Mathf.Abs(across) < halfDepth + margin;
            }
        }

        readonly List<Footprint> footprints = new List<Footprint>();

        bool InsideLandmark(Vector3 p)
        {
            foreach (var f in footprints) if (f.Contains(p, 3)) return true;
            return false;
        }

        // Huecos en guardarraíl, postes y bordes (entrada a un hotel): puntos [from, to] de un lado
        readonly List<(int from, int to, int side)> gaps = new List<(int, int, int)>();
        readonly List<(int index, int side, Landmark l)> hotelSites = new List<(int, int, Landmark)>();
        // Lugares de los monumentos para el mapa del pueblo (se añaden al TownMap cuando existe)
        readonly List<Kurogane.City.TownMap.Place> landmarkPlaces = new List<Kurogane.City.TownMap.Place>();

        bool InGap(int i, int side)
        {
            foreach (var g in gaps) if (g.side == side && i >= g.from && i <= g.to) return true;
            return false;
        }

        /// <summary>Antes de construir nada: dónde van los hoteles, para dejar su entrada sin guardarraíl ni borde.</summary>
        void PrepareLandmarks()
        {
            gaps.Clear();
            hotelSites.Clear();
            landmarkPlaces.Clear();
            if (definition.landmarks == null) return;
            var d = Geometry;
            foreach (var l in definition.landmarks)
            {
                if (l.kind != LandmarkKind.Hotel) continue;
                int i = Mathf.Clamp(Mathf.RoundToInt(l.atMeters / d.Step), 8, d.N - 9);
                // Del lado donde el terreno baja (la explanada se apoya en un muro), salvo que se pida otro
                int side = l.side != 0 ? (int)Mathf.Sign(l.side) : d.TerrainY(i, 1, d.HW[i] + 10) <= d.TerrainY(i, -1, d.HW[i] + 10) ? 1 : -1;
                hotelSites.Add((i, side, l));
                gaps.Add((i - 6, i + 6, side));
            }
        }

        void BuildLandmarks()
        {
            footprints.Clear();
            if (definition.landmarks == null) return;
            foreach (var l in definition.landmarks)
                if (l.kind == LandmarkKind.Viaducto) BuildViaduct(l);
            foreach (var h in hotelSites) BuildHotel(h.index, h.side, h.l);
        }

        /// <summary>Lugares de los monumentos al mapa del pueblo (lo llama Build tras construir el pueblo).</summary>
        void RegisterLandmarkPlaces()
        {
            if (landmarkPlaces.Count == 0 || root == null) return;
            var map = root.GetComponentInChildren<Kurogane.City.TownMap>(true);
            if (map != null) map.places.AddRange(landmarkPlaces);
        }

        /// <summary>
        /// Ryokan de dos pisos junto a la carretera de montaña, sobre una explanada con aparcamiento apoyada en un muro.
        /// Destino de los repartos del modo libre: la explanada es suelo y el edificio y sus muretes, obstáculo.
        /// </summary>
        void BuildHotel(int i, int side, Landmark l)
        {
            var d = Geometry;
            var p = d.P[i];
            var holder = new GameObject("Hotel").transform;
            holder.SetParent(root, false);
            var outward = p.right * side;
            holder.SetPositionAndRotation(p.pos, Quaternion.LookRotation(-outward)); // +Z local mira a la carretera
            float w = d.HW[i];
            Vector3 L(float across, float y, float along) => new Vector3(-along * side, y, -across) ; // across: distancia desde la carretera
            var concrete = MaterialFactory.Lit(new Color(0.5f, 0.49f, 0.46f), null, 0.05f);
            var asphalt = MaterialFactory.Lit(new Color(0.18f, 0.18f, 0.2f), null, 0.15f);
            var wall = MaterialFactory.Lit(new Color(0.72f, 0.66f, 0.55f), null, 0.05f);
            var wood = MaterialFactory.Lit(new Color(0.3f, 0.21f, 0.14f), null, 0.1f);
            var roof = MaterialFactory.Lit(new Color(0.2f, 0.22f, 0.27f), null, 0.3f);
            var window = MaterialFactory.Emissive(new Color(1f, 0.8f, 0.5f), new Color(1.6f, 1.1f, 0.5f));
            var white = MaterialFactory.Unlit(new Color(0.85f, 0.85f, 0.8f));

            // Explanada: muro de 25 m hacia abajo con el aparcamiento encima, a ras de carretera
            float a0 = w + 0.9f, a1 = w + 24, half = 13;
            Solid(holder, L((a0 + a1) / 2, -12.55f, 0), new Vector3(2 * half, 25, a1 - a0), concrete, Kurogane.Cars.WorldSurface.GroundLayer);
            Solid(holder, L((a0 + 14) / 2, 0.005f, 0), new Vector3(2 * half - 0.4f, 0.02f, 14 - a0), asphalt, -1);
            for (float s = -half + 3; s < half - 2; s += 2.8f) Solid(holder, L(11, 0.03f, s), new Vector3(0.1f, 0.02f, 4.5f), white, -1);
            // Muretes en los extremos y al fondo
            foreach (int e in new[] { -1, 1 })
                Solid(holder, L((a0 + a1) / 2, 0.45f, e * (half - 0.2f)), new Vector3(0.4f, 0.9f, a1 - a0), concrete, Kurogane.Cars.WorldSurface.ObstacleLayer);
            Solid(holder, L(a1 - 0.2f, 0.45f, 0), new Vector3(2 * half, 0.9f, 0.4f), concrete, Kurogane.Cars.WorldSurface.ObstacleLayer);

            // Edificio: dos pisos, tejado a dos aguas, ventanas encendidas, alero de madera y rótulo
            float bx = 19.5f, bw = 20, bd = 8, bh = 6.4f;
            Solid(holder, L(bx, bh / 2, 0), new Vector3(bw, bh, bd), wall, Kurogane.Cars.WorldSurface.ObstacleLayer);
            Solid(holder, L(bx, bh + 0.9f, 0), new Vector3(bw + 1.2f, 1.8f, bd * 0.55f), roof, -1);
            Solid(holder, L(bx - bd / 2 - 0.6f, 2.9f, 0), new Vector3(bw, 0.15f, 1.4f), wood, -1);
            for (int f = 0; f < 2; f++)
                for (float s = -bw / 2 + 2; s < bw / 2 - 1; s += 3)
                    if (f == 1 || Mathf.Abs(s) > 2.5f)
                        Solid(holder, L(bx - bd / 2 - 0.03f, 1.5f + f * 3, s), new Vector3(1.6f, 1.1f, 0.06f), window, -1);
            Solid(holder, L(bx - bd / 2 - 0.05f, 1.2f, 0), new Vector3(2.4f, 2.4f, 0.06f), window, -1);
            Solid(holder, L(bx - bd / 2 - 0.25f, 4.5f, 0), new Vector3(5, 0.9f, 0.15f), wood, -1);
            var sign = new GameObject("Rotulo").AddComponent<TextMesh>();
            sign.transform.SetParent(holder, false);
            sign.transform.localPosition = L(bx - bd / 2 - 0.35f, 4.5f, 0);
            sign.transform.localRotation = Quaternion.identity * Quaternion.Euler(0, 180, 0);
            sign.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            sign.GetComponent<MeshRenderer>().sharedMaterial = sign.font.material;
            sign.text = string.IsNullOrEmpty(l.name) ? "旅館 峠の湯" : l.name;
            sign.fontSize = 64;
            sign.characterSize = 0.07f;
            sign.anchor = TextAnchor.MiddleCenter;
            sign.color = new Color(1f, 0.95f, 0.82f);
            var lamp = new GameObject("LampLight").AddComponent<Light>();
            lamp.transform.SetParent(holder, false);
            lamp.transform.localPosition = L(bx - bd / 2 - 2, 3.2f, 0);
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.78f, 0.5f);
            lamp.range = 14;
            lamp.intensity = 1.6f;
            lamp.shadows = LightShadows.None;
            culling.Add(lamp, lightDistance);

            footprints.Add(new Footprint { center = p.pos + outward * (a0 + a1) / 2, forward = p.forward, halfLength = half, halfDepth = (a1 - a0) / 2 });
            landmarkPlaces.Add(new Kurogane.City.TownMap.Place
            {
                kind = Kurogane.City.TownMap.PlaceKind.Hotel,
                name = sign.text,
                position = p.pos + outward * 9,
                heading = p.heading * Mathf.Rad2Deg,
            });
        }

        /// <summary>Caja con malla de cubo; con capa ≥ 0 lleva colisionador (suelo u obstáculo de la física del mundo).</summary>
        void Solid(Transform parent, Vector3 localPos, Vector3 size, Material mat, int colliderLayer)
        {
            var go = Add("HotelPieza", Primitive(PrimitiveType.Cube), mat, parent);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            if (colliderLayer < 0) return;
            go.layer = colliderLayer;
            go.AddComponent<BoxCollider>();
        }

        /// <summary>
        /// Viaducto paralelo a la carretera, al otro lado del valle: estribos, tres pilas y cuatro arcos de medio punto
        /// con la clave unos 3 m bajo el pretil. El tablero queda algo por debajo de la carretera y los pies se hunden
        /// en el terreno.
        /// </summary>
        void BuildViaduct(Landmark l)
        {
            var d = Geometry;
            int i = Mathf.Clamp(Mathf.RoundToInt(l.atMeters / d.Step), 2, d.N - 3);
            float lateral = l.lateral > 0 ? l.lateral : 45;
            float length = l.length > 0 ? l.length : 92;
            int side = l.side != 0 ? (int)Mathf.Sign(l.side) : 1;
            // Busca a ±100 m el punto y el lado donde el terreno cae más, sin que la huella toque otro tramo de carretera
            float best = float.MaxValue;
            int bestI = i, bestSide = side;
            for (int j = Mathf.Max(2, i - 50); j <= Mathf.Min(d.N - 3, i + 50); j += 2)
                foreach (int s in l.side != 0 ? new[] { side } : new[] { -1, 1 })
                {
                    var c = d.At(j, s, lateral, 0);
                    var f = d.P[j].forward * (length / 2);
                    bool clear = true;
                    for (int k = 0; k < d.N && clear; k += 3)
                    {
                        var q = d.P[k].pos;
                        foreach (var e in new[] { c, c + f, c - f })
                            if ((new Vector2(q.x - e.x, q.z - e.z)).sqrMagnitude < (d.HW[k] + 10) * (d.HW[k] + 10)) { clear = false; break; }
                    }
                    if (!clear) continue;
                    float drop = d.TerrainY(j, s, lateral) - d.P[j].pos.y;
                    if (drop < best) { best = drop; bestI = j; bestSide = s; }
                }
            i = bestI;
            side = bestSide;
            var p = d.P[i];

            var center = d.At(i, side, lateral, 0);
            float ground = Mathf.Min(d.TerrainY(i, side, lateral), Mathf.Min(
                d.TerrainY(Mathf.Clamp(i - Mathf.RoundToInt(length / 2 / d.Step), 0, d.N - 1), side, lateral),
                d.TerrainY(Mathf.Clamp(i + Mathf.RoundToInt(length / 2 / d.Step), 0, d.N - 1), side, lateral)));
            float deck = p.pos.y - 3;
            float height = Mathf.Max(14, deck - ground + 2);
            center.y = deck - height;

            var mesh = ViaductMesh(length, height, 6.5f, 4);
            var go = Add("Viaducto", mesh, MaterialFactory.Lit(Color.white, ProceduralTextures.Brick(), 0.08f).DoubleSided());
            // La malla va a lo largo de x local: z local = -derecha deja x local = adelante de la carretera
            go.transform.SetPositionAndRotation(center, Quaternion.LookRotation(-p.right));
            footprints.Add(new Footprint { center = center, forward = p.forward, halfLength = length / 2, halfDepth = 3.25f });
        }

        /// <summary>
        /// Malla del viaducto en local: x a lo largo (centrado), y hacia arriba desde el pie, z de grueso (centrado).
        /// Caras delantera y trasera recortadas por los arcos, intradós de cada arco, caras interiores de las pilas,
        /// testeros y tablero.
        /// </summary>
        static Mesh ViaductMesh(float length, float height, float depth, int arches)
        {
            float abutment = length * 0.1f, pier = 4.2f;
            float span = (length - 2 * abutment - (arches - 1) * pier) / arches, r = span / 2;
            float spring = height - 2.8f - r;   // arranque de los arcos
            float x0 = -length / 2, hz = depth / 2, bury = 6;

            // Intradós: altura de la parte baja de la fábrica en cada x (pie de las pilas o curva del arco)
            float Bottom(float x, out bool inArch)
            {
                float u = x - x0 - abutment;
                inArch = false;
                if (u <= 0 || u >= length - 2 * abutment) return -bury;
                float cell = span + pier;
                int k = Mathf.FloorToInt(u / cell);
                float local = u - k * cell;
                if (k >= arches || local > span) return -bury;
                inArch = true;
                float dx = local - r;
                return spring + Mathf.Sqrt(Mathf.Max(0, r * r - dx * dx));
            }

            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 e, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ue)
            {
                int o = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(e);
                uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ue);
                t.Add(o); t.Add(o + 1); t.Add(o + 2);
                t.Add(o); t.Add(o + 2); t.Add(o + 3);
            }
            const float tile = 4;
            Vector2 W(float a, float b) => new Vector2(a / tile, b / tile);

            // Columnas: cada 0,5 m, más los bordes exactos de arcos y pilas
            var xs = new List<float>();
            for (float x = x0; x < -x0; x += 0.5f) xs.Add(x);
            xs.Add(-x0);
            for (int k = 0; k <= arches; k++)
            {
                float a = x0 + abutment + k * (span + pier);
                if (k < arches) { xs.Add(a); xs.Add(a + 0.001f); }
                if (k > 0) { xs.Add(a - pier); xs.Add(a - pier - 0.001f); }
            }
            xs.Sort();

            for (int j = 0; j < xs.Count - 1; j++)
            {
                float xa = xs[j], xb = xs[j + 1];
                if (xb - xa < 1e-4f) continue;
                float ya = Bottom((xa + xb) / 2 - (xb - xa) / 2 + 1e-4f, out bool archA);
                float yb = Bottom((xa + xb) / 2 + (xb - xa) / 2 - 1e-4f, out bool archB);
                // Delante (z = -hz, mira a -z) y detrás (z = +hz)
                Quad(new Vector3(xa, ya, -hz), new Vector3(xa, height, -hz), new Vector3(xb, height, -hz), new Vector3(xb, yb, -hz),
                    W(xa, ya), W(xa, height), W(xb, height), W(xb, yb));
                Quad(new Vector3(xb, yb, hz), new Vector3(xb, height, hz), new Vector3(xa, height, hz), new Vector3(xa, ya, hz),
                    W(-xb, yb), W(-xb, height), W(-xa, height), W(-xa, ya));
                // Intradós del arco (mira hacia abajo)
                if (archA && archB)
                    Quad(new Vector3(xa, ya, -hz), new Vector3(xb, yb, -hz), new Vector3(xb, yb, hz), new Vector3(xa, ya, hz),
                        W(xa, -hz), W(xb, -hz), W(xb, hz), W(xa, hz));
                // Tablero
                Quad(new Vector3(xa, height, -hz), new Vector3(xa, height, hz), new Vector3(xb, height, hz), new Vector3(xb, height, -hz),
                    W(xa, -hz), W(xa, hz), W(xb, hz), W(xb, -hz));
            }
            // Caras interiores de las pilas y estribos, del pie al arranque de cada arco
            for (int k = 0; k < arches; k++)
            {
                float a = x0 + abutment + k * (span + pier), b = a + span;
                Quad(new Vector3(a, -bury, -hz), new Vector3(a, -bury, hz), new Vector3(a, spring, hz), new Vector3(a, spring, -hz),
                    W(-hz, -bury), W(hz, -bury), W(hz, spring), W(-hz, spring));
                Quad(new Vector3(b, -bury, hz), new Vector3(b, -bury, -hz), new Vector3(b, spring, -hz), new Vector3(b, spring, hz),
                    W(hz, -bury), W(-hz, -bury), W(-hz, spring), W(hz, spring));
            }
            // Testeros
            Quad(new Vector3(x0, -bury, hz), new Vector3(x0, height, hz), new Vector3(x0, height, -hz), new Vector3(x0, -bury, -hz),
                W(hz, -bury), W(hz, height), W(-hz, height), W(-hz, -bury));
            Quad(new Vector3(-x0, -bury, -hz), new Vector3(-x0, height, -hz), new Vector3(-x0, height, hz), new Vector3(-x0, -bury, hz),
                W(-hz, -bury), W(-hz, height), W(hz, height), W(hz, -bury));

            var m = new Mesh { name = "Viaducto" };
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}

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

        void BuildLandmarks()
        {
            footprints.Clear();
            if (definition.landmarks == null) return;
            foreach (var l in definition.landmarks)
                if (l.kind == LandmarkKind.Viaducto) BuildViaduct(l);
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

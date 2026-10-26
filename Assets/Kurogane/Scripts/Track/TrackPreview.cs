using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Kurogane.Track
{
    /// <summary>
    /// Vistas de una pista para el menú y para la ventana Kurogane > Pistas: mapa desde arriba, perfil de altitud,
    /// ficha en texto y comprobación del trazado (cruces y tramos demasiado cerca).
    /// </summary>
    public static class TrackPreview
    {
        static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        static readonly Color32 Road = new Color32(230, 234, 255, 255);
        static readonly Color32 Dim = new Color32(230, 234, 255, 90);
        static readonly Color32 Town = new Color32(255, 189, 51, 255);
        static readonly Color32 Start = new Color32(255, 61, 51, 255);
        static readonly Color32 Goal = new Color32(80, 140, 255, 255);
        static readonly Color32 Warn = new Color32(255, 40, 40, 255);

        // ---------- Ficha ----------

        public static string Km(float meters) => (meters / 1000f).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');

        /// <summary>"2,3 km · 9 curvas · 4 horquillas · 1 túnel · 120 m de desnivel", sobre el tramo cronometrado.</summary>
        public static string Summary(TrackData d)
        {
            var parts = new List<string>
            {
                Km((d.FinishIndex - d.RaceStart) * d.Step) + " km",
                d.CurveCount + (d.CurveCount == 1 ? " curva" : " curvas"),
            };
            if (d.HairpinCount > 0) parts.Add(d.HairpinCount + (d.HairpinCount == 1 ? " horquilla" : " horquillas"));
            if (d.TunnelCount > 0) parts.Add(d.TunnelCount + (d.TunnelCount == 1 ? " túnel" : " túneles"));
            parts.Add(Mathf.RoundToInt(Mathf.Abs(d.Drop)) + " m de desnivel");
            return string.Join(" · ", parts);
        }

        // ---------- Mapa y perfil ----------

        /// <summary>
        /// Mapa desde arriba: tramo cronometrado en claro, el resto atenuado, pueblo en ámbar, salida en rojo y meta en
        /// azul (según el sentido de <paramref name="d"/>). Los puntos de <paramref name="marks"/> salen en rojo.
        /// </summary>
        public static Texture2D Map(TrackData d, int size, IEnumerable<int> marks = null)
        {
            var g = d.Geometry;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in g.P)
            {
                minX = Mathf.Min(minX, p.pos.x); maxX = Mathf.Max(maxX, p.pos.x);
                minZ = Mathf.Min(minZ, p.pos.z); maxZ = Mathf.Max(maxZ, p.pos.z);
            }
            float margin = size * 0.08f;
            float scale = (size - 2 * margin) / Mathf.Max(1, Mathf.Max(maxX - minX, maxZ - minZ));
            float ox = (size - (maxX - minX) * scale) / 2, oz = (size - (maxZ - minZ) * scale) / 2;
            Vector2 Px(int geoIndex)
            {
                var p = g.P[geoIndex].pos;
                return new Vector2(ox + (p.x - minX) * scale, oz + (p.z - minZ) * scale);
            }

            var px = new Color32[size * size];
            float line = Mathf.Max(1.2f, size / 128f);
            int start = d.ToGeometry(d.RaceStart), goal = d.ToGeometry(d.FinishIndex);
            int lo = Mathf.Min(start, goal), hi = Mathf.Max(start, goal);
            for (int i = 0; i < g.N - 1; i++)
            {
                var c = g.InTown(i) ? Town : i >= lo && i <= hi ? Road : Dim;
                Segment(px, size, Px(i), Px(i + 1), line, c);
            }
            Disc(px, size, Px(start), line * 2.6f, Start);
            Disc(px, size, Px(goal), line * 2.6f, Goal);
            if (marks != null) foreach (int i in marks) Ring(px, size, Px(Mathf.Clamp(i, 0, g.N - 1)), line * 4, line * 0.8f, Warn);
            return Finish(px, size, size);
        }

        /// <summary>Perfil de altitud en el sentido de <paramref name="d"/>, de la salida a la meta, relleno por debajo.</summary>
        public static Texture2D Profile(TrackData d, int width, int height)
        {
            var px = new Color32[width * height];
            int a = 0, b = d.N - 1;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = a; i <= b; i++) { minY = Mathf.Min(minY, d.P[i].pos.y); maxY = Mathf.Max(maxY, d.P[i].pos.y); }
            float range = Mathf.Max(1, maxY - minY);
            for (int x = 0; x < width; x++)
            {
                int i = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(a, b, x / (float)(width - 1))), 0, d.N - 1);
                int top = Mathf.RoundToInt(2 + (d.P[i].pos.y - minY) / range * (height - 5));
                bool timed = i >= d.RaceStart && i <= d.FinishIndex;
                var fill = d.InTown(i) ? new Color32(255, 189, 51, 70) : timed ? new Color32(230, 234, 255, 60) : new Color32(230, 234, 255, 25);
                var edge = d.InTown(i) ? Town : timed ? Road : Dim;
                for (int y = 0; y <= top && y < height; y++) px[y * width + x] = y >= top - 1 ? edge : fill;
            }
            foreach (var (i, c) in new[] { (d.RaceStart, Start), (d.FinishIndex, Goal) })
            {
                int x = Mathf.RoundToInt(Mathf.InverseLerp(a, b, i) * (width - 1));
                for (int y = 0; y < height; y++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (x + dx >= 0 && x + dx < width) px[y * width + x + dx] = c;
            }
            return Finish(px, width, height);
        }

        static Texture2D Finish(Color32[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "Vista de pista",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        static void Segment(Color32[] px, int size, Vector2 a, Vector2 b, float r, Color32 c)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.5f));
            for (int s = 0; s <= steps; s++) Disc(px, size, Vector2.Lerp(a, b, s / (float)steps), r, c);
        }

        static void Disc(Color32[] px, int size, Vector2 p, float r, Color32 c)
        {
            int x0 = Mathf.FloorToInt(p.x - r), x1 = Mathf.CeilToInt(p.x + r), y0 = Mathf.FloorToInt(p.y - r), y1 = Mathf.CeilToInt(p.y + r);
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(size - 1, y1); y++)
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(size - 1, x1); x++)
                    if ((x - p.x) * (x - p.x) + (y - p.y) * (y - p.y) <= r * r)
                    {
                        // No tapar lo claro con lo atenuado
                        ref var dst = ref px[y * size + x];
                        if (c.a >= dst.a || dst.a == 0) dst = c;
                    }
        }

        static void Ring(Color32[] px, int size, Vector2 p, float r, float t, Color32 c)
        {
            for (int k = 0; k < 64; k++)
            {
                float a = k / 64f * Mathf.PI * 2;
                Disc(px, size, p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, t, c);
            }
        }

        // ---------- Comprobación del trazado ----------

        public struct Issue
        {
            public int a, b;
            public bool crossing;
            public string text;
        }

        /// <summary>
        /// Busca en la pista de bajada cruces consigo misma y tramos que pasan demasiado cerca: la distancia mínima entre
        /// bordes de calzada es <paramref name="clearance"/> metros (arcén, guardarraíl y algo de ladera), salvo que
        /// vayan a distinto nivel (puente o túnel) con al menos <paramref name="levelGap"/> metros de diferencia.
        /// </summary>
        public static List<Issue> Check(TrackData data, float clearance = 4, float levelGap = 7)
        {
            var d = data.Geometry;
            var issues = new List<Issue>();
            const int stride = 2;
            var flagged = new HashSet<(int, int)>();
            for (int i = 0; i < d.N - stride; i += stride)
            {
                for (int j = i + stride * 3; j < d.N - stride; j += stride)
                {
                    var a = d.P[i].pos;
                    var b = d.P[j].pos;
                    float gap = d.HW[i] + d.HW[j] + clearance;
                    float dx = a.x - b.x, dz = a.z - b.z, dist = Mathf.Sqrt(dx * dx + dz * dz);
                    float arc = (j - i) * d.Step;
                    if (dist >= gap || arc < gap * 2.2f) continue;
                    if (Mathf.Abs(a.y - b.y) >= levelGap && (d.Bridge[i] || d.Bridge[j] || d.InTunnel(i) || d.InTunnel(j))) continue;
                    // Un aviso por zona: los puntos vecinos de uno ya marcado no cuentan
                    var key = (i / 20, j / 20);
                    if (!flagged.Add(key)) continue;
                    bool crossing = Crosses(d, i, j, stride);
                    issues.Add(new Issue
                    {
                        a = i, b = j, crossing = crossing,
                        text = crossing
                            ? $"La pista se cruza consigo misma: km {Km(i * d.Step)} y km {Km(j * d.Step)}."
                            : $"Tramos demasiado cerca: km {Km(i * d.Step)} y km {Km(j * d.Step)} a {dist - d.HW[i] - d.HW[j]:0.0} m entre bordes (mínimo {clearance:0} m).",
                    });
                }
            }
            return Merge(issues);
        }

        static bool Crosses(TrackData d, int i, int j, int stride)
        {
            for (int a = Mathf.Max(0, i - 6); a < Mathf.Min(d.N - 1, i + 6); a++)
                for (int b = Mathf.Max(0, j - 6); b < Mathf.Min(d.N - 1, j + 6); b++)
                    if (Intersect(d.P[a].pos, d.P[a + 1].pos, d.P[b].pos, d.P[b + 1].pos)) return true;
            return false;
        }

        static bool Intersect(Vector3 p1, Vector3 p2, Vector3 q1, Vector3 q2)
        {
            float Cross(Vector3 o, Vector3 a, Vector3 b) => (a.x - o.x) * (b.z - o.z) - (a.z - o.z) * (b.x - o.x);
            float d1 = Cross(q1, q2, p1), d2 = Cross(q1, q2, p2), d3 = Cross(p1, p2, q1), d4 = Cross(p1, p2, q2);
            return d1 * d2 < 0 && d3 * d4 < 0;
        }

        /// <summary>Junta los avisos de una misma zona (puntos a menos de 80 m en los dos tramos).</summary>
        static List<Issue> Merge(List<Issue> issues)
        {
            var result = new List<Issue>();
            foreach (var it in issues)
            {
                int k = result.FindIndex(r => Mathf.Abs(r.a - it.a) < 40 && Mathf.Abs(r.b - it.b) < 40);
                if (k < 0) result.Add(it);
                else if (it.crossing && !result[k].crossing) result[k] = it;
            }
            return result;
        }
    }
}

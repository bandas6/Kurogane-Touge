using System.Collections.Generic;
using UnityEngine;

namespace Kurogane.Track
{
    public struct TrackPoint
    {
        public Vector3 pos;
        /// <summary>Rumbo en radianes. 0 = +Z; crece al girar a la derecha.</summary>
        public float heading;
        /// <summary>Curvatura con signo (rad/m). Positiva = curva a la derecha.</summary>
        public float k;
        public Vector3 forward;
        public Vector3 right;
    }

    public struct SegmentInfo
    {
        public SegmentType type;
        public float angle;
        public int start, end;
        public bool tunnel;
    }

    /// <summary>
    /// Pista calculada a partir de una <see cref="TrackDefinition"/>: línea central, perfil de velocidad
    /// del rival, trazada, canaletas y secciones del terreno a cada lado. Es la misma lógica del prototipo web.
    /// Lado <c>s</c>: +1 derecha, -1 izquierda.
    /// </summary>
    public class TrackData
    {
        public readonly TrackPoint[] P;
        public readonly int N;
        public readonly float Step, W, Length;
        public readonly List<SegmentInfo> Segments = new List<SegmentInfo>();
        public readonly List<SegmentInfo> Hairpins = new List<SegmentInfo>();
        public readonly int TunnelStart = -1, TunnelEnd = -2;
        /// <summary>Por punto: lado donde está la canaleta interior de la horquilla (0 si no hay).</summary>
        public readonly sbyte[] GutterSide;
        /// <summary>Velocidad máxima en m/s que puede llevar el rival en cada punto.</summary>
        public readonly float[] VMax;
        /// <summary>Desplazamiento lateral de la trazada del rival (positivo = derecha).</summary>
        public readonly float[] Lane;
        public readonly float[] KSmooth;
        public readonly int[] Sectors;

        /// <summary>Puntos del tramo cronometrado (antes del primer tramo tras la meta).</summary>
        public readonly int TimedCount;
        /// <summary>Punto donde salta la meta. Es el mismo de antes del pueblo, así los récords siguen valiendo.</summary>
        public readonly int FinishIndex;
        /// <summary>Longitud cronometrada en metros (para el rival y la distancia a meta).</summary>
        public readonly float TimedLength;
        /// <summary>Primer punto de la calle mayor del pueblo (primera recta tras la meta).</summary>
        public readonly int TownStart;
        /// <summary>Punto donde se para el coche en la llegada, frente a la tienda de tofu.</summary>
        public readonly int StopIndex;

        public bool InTown(int i) => i >= TownStart;

        // Terreno a cada lado: [0] izquierda, [1] derecha
        readonly float[][] off = new float[2][], dy = new float[2][], plat = new float[2][];

        public TrackData(TrackDefinition def)
        {
            Step = def.step;
            W = def.halfWidth;
            var pts = new List<TrackPoint>();
            float x = 0, z = 0, h = 0, y = 0;
            int timed = -1, town = -1;
            foreach (var s in def.AllSegments())
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(s.Length / Step));
                float d = s.angle * Mathf.Deg2Rad / n;
                float sl = s.afterGoal ? def.afterGoalSlope : s.type == SegmentType.Hairpin ? def.hairpinSlope : def.slope;
                if (s.afterGoal && timed < 0) timed = pts.Count;
                if (s.afterGoal && s.type == SegmentType.Straight && town < 0) town = pts.Count;
                var info = new SegmentInfo { type = s.type, angle = s.angle, start = pts.Count, end = pts.Count + n - 1, tunnel = s.tunnel };
                Segments.Add(info);
                if (s.type == SegmentType.Hairpin) Hairpins.Add(info);
                if (s.tunnel && TunnelStart < 0) { TunnelStart = info.start; TunnelEnd = info.end; }
                for (int i = 0; i < n; i++)
                {
                    h += d;
                    x += Mathf.Sin(h) * Step;
                    z += Mathf.Cos(h) * Step;
                    y -= sl * Step;
                    pts.Add(new TrackPoint
                    {
                        pos = new Vector3(x, y, z),
                        heading = h,
                        k = d / Step,
                        forward = new Vector3(Mathf.Sin(h), 0, Mathf.Cos(h)),
                        right = new Vector3(Mathf.Cos(h), 0, -Mathf.Sin(h))
                    });
                }
            }
            P = pts.ToArray();
            N = P.Length;
            Length = N * Step;
            TimedCount = timed < 0 ? N : timed;
            FinishIndex = TimedCount - 5;
            TimedLength = TimedCount * Step;
            TownStart = town < 0 ? N : town;
            StopIndex = Mathf.Min(N - 8, TownStart + Mathf.RoundToInt((N - TownStart) * 0.5f));

            GutterSide = new sbyte[N];
            foreach (var hp in Hairpins)
                for (int i = Mathf.Max(0, hp.start - 3); i <= Mathf.Min(N - 1, hp.end + 3); i++)
                    GutterSide[i] = (sbyte)Mathf.Sign(hp.angle);

            VMax = new float[N];
            Lane = new float[N];
            KSmooth = new float[N];
            for (int i = 0; i < N; i++)
            {
                float ks = 0;
                for (int j = -6; j <= 6; j++) ks += P[Clamp(i + j)].k;
                ks /= 13;
                KSmooth[i] = ks;
                float r = Mathf.Abs(ks) > 1e-4f ? 1 / Mathf.Abs(ks) : 1e4f;
                VMax[i] = Mathf.Min(52, Mathf.Sqrt(10.5f * r));
                float kl = 0;
                for (int j = -14; j <= 14; j++) kl += P[Clamp(i + j + 6)].k;
                kl /= 29;
                Lane[i] = Mathf.Clamp(Mathf.Sign(kl) * Mathf.Min(1, Mathf.Abs(kl) * 40) * W * 0.55f, -(W - 1.3f), W - 1.3f);
            }
            for (int i = N - 2; i >= 0; i--)
                VMax[i] = Mathf.Min(VMax[i], Mathf.Sqrt(VMax[i + 1] * VMax[i + 1] + 2 * 9 * Step));

            // Sobre el tramo cronometrado, igual que antes del pueblo: los parciales guardados siguen valiendo
            Sectors = new[] { TimedCount / 3, 2 * TimedCount / 3, FinishIndex };
            BuildTerrain();
        }

        int Clamp(int i) => i < 0 ? 0 : i >= N ? N - 1 : i;

        public bool InTunnel(int i) => i >= TunnelStart && i <= TunnelEnd;

        /// <summary>Índice del punto central más cercano, buscando alrededor de <paramref name="hint"/>.</summary>
        public int Nearest(Vector3 p, int hint)
        {
            float best = float.MaxValue;
            int bi = hint;
            int a = Mathf.Max(0, hint - 25), b = Mathf.Min(N - 1, hint + 25);
            for (int i = a; i <= b; i++)
            {
                float dx = p.x - P[i].pos.x, dz = p.z - P[i].pos.z, d = dx * dx + dz * dz;
                if (d < best) { best = d; bi = i; }
            }
            return bi;
        }

        /// <summary>Desplazamiento lateral con signo (positivo = derecha) de <paramref name="p"/> respecto al punto i.</summary>
        public float Lateral(int i, Vector3 p) => (p.x - P[i].pos.x) * P[i].right.x + (p.z - P[i].pos.z) * P[i].right.z;

        public Vector3 At(int i, int side, float lat, float y)
        {
            var p = P[i];
            return new Vector3(p.pos.x + p.right.x * lat * side, y, p.pos.z + p.right.z * lat * side);
        }

        // ---------- Terreno ----------

        void BuildTerrain()
        {
            for (int si = 0; si < 2; si++)
            {
                int s = si == 0 ? -1 : 1;
                var o = off[si] = new float[N];
                var dd = dy[si] = new float[N];
                var pl = plat[si] = new float[N];
                for (int i = 0; i < N; i++)
                {
                    var p = P[i];
                    float best = 1e9f, by = 0;
                    for (int j = 0; j < N; j += 2)
                    {
                        if (Mathf.Abs(i - j) < 25) continue;
                        float dx = P[j].pos.x - p.pos.x, dz = P[j].pos.z - p.pos.z;
                        float lat = (dx * p.right.x + dz * p.right.z) * s;
                        if (lat <= 0) continue;
                        float al = dx * p.forward.x + dz * p.forward.z;
                        if (Mathf.Abs(al) > lat * 0.7f) continue;
                        if (lat < best) { best = lat; by = P[j].pos.y; }
                    }
                    // Si hay otro tramo de carretera a ese lado, el terreno se une a mitad de camino con su altura.
                    if (best < 80) { o[i] = best / 2; dd[i] = (by - p.pos.y) / 2 - 0.4f; }
                    else { o[i] = s < 0 ? 30 : 36; dd[i] = s < 0 ? 16 : -40; }
                    if ((int)Mathf.Sign(KSmooth[i]) == s && Mathf.Abs(KSmooth[i]) > 0.004f) o[i] = Mathf.Min(o[i], 0.9f / Mathf.Abs(KSmooth[i]));
                    o[i] = Mathf.Max(o[i], W + 2.5f);
                    if (InTunnel(i) || Mathf.Abs(i - TunnelStart) < 6 || Mathf.Abs(i - TunnelEnd) < 6)
                    {
                        dd[i] = Mathf.Max(dd[i], 13);
                        o[i] = Mathf.Max(o[i], 18);
                    }
                }
                // Pueblo: terreno casi llano y ancho a los dos lados de la calle mayor
                for (int i = Mathf.Max(0, TownStart - 6); i < N; i++)
                {
                    o[i] = 75;
                    dd[i] = -0.3f;
                }
                // Explanada para el público por fuera de cada horquilla
                foreach (var hp in Hairpins)
                    if ((int)Mathf.Sign(hp.angle) == -s)
                    {
                        int m = (hp.start + hp.end) / 2;
                        for (int i = m - 14; i <= m + 14; i++) if (i >= 0 && i < N) pl[i] = 1;
                    }
                Smooth(o, 4, 2);
                Smooth(dd, 4, 2);
                Smooth(pl, 3, 2);
            }
        }

        static void Smooth(float[] a, int r, int passes)
        {
            var b = new float[a.Length];
            for (int p = 0; p < passes; p++)
            {
                System.Array.Copy(a, b, a.Length);
                for (int i = 0; i < a.Length; i++)
                {
                    float s = 0; int c = 0;
                    for (int j = -r; j <= r; j++)
                    {
                        int k = i + j;
                        if (k >= 0 && k < a.Length) { s += b[k]; c++; }
                    }
                    a[i] = s / c;
                }
            }
        }

        static int Si(int side) => side > 0 ? 1 : 0;

        public float Platform(int i, int side) => plat[Si(side)][i];

        /// <summary>Altura del muro de contención (0 = ladera hacia abajo con guardarraíl).</summary>
        public float WallHeight(int i, int side)
        {
            int si = Si(side);
            return Mathf.Clamp((dy[si][i] - 1.5f) * 0.5f, 0, 3.4f) * (1 - plat[si][i]);
        }

        public bool HasRail(int i, int side) => WallHeight(i, side) < 0.4f && !InTunnel(i) && i < TownStart - 8;

        /// <summary>
        /// Sección transversal del terreno a un lado: 4 filas (distancia lateral sin signo, altura).
        /// 0 borde de calzada, 1 arcén, 2 pie/coronación del muro o explanada, 3 límite exterior.
        /// </summary>
        public void Rows(int i, int side, Vector2[] r)
        {
            int si = Si(side);
            var p = P[i];
            float d = dy[si][i], pl = plat[si][i], wh = WallHeight(i, side), m = Mathf.Min(1, wh);
            float l2 = W + 1.05f + (1 - m) * 0.9f, y2 = p.pos.y - 0.1f - (1 - m) * 0.25f + wh;
            l2 = Mathf.Lerp(l2, W + 7.5f, pl);
            y2 = Mathf.Lerp(y2, p.pos.y - 0.15f, pl);
            r[0] = new Vector2(W + 0.02f, p.pos.y - 0.02f);
            r[1] = new Vector2(W + 0.9f, p.pos.y - 0.1f);
            r[2] = new Vector2(l2, y2);
            r[3] = new Vector2(Mathf.Max(off[si][i], l2 + 2), p.pos.y + d);
        }

        readonly Vector2[] tmpRows = new Vector2[4];

        public float TerrainY(int i, int side, float lat)
        {
            Rows(i, side, tmpRows);
            for (int k = 0; k < 3; k++)
                if (lat <= tmpRows[k + 1].x)
                {
                    float t = Mathf.Clamp01((lat - tmpRows[k].x) / (tmpRows[k + 1].x - tmpRows[k].x));
                    return Mathf.Lerp(tmpRows[k].y, tmpRows[k + 1].y, t);
                }
            return tmpRows[3].y;
        }
    }
}

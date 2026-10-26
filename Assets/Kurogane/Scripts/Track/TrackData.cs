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
    /// La de subida (<see cref="Reversed"/>) es la misma carretera recorrida al revés: los puntos, lados y tramos
    /// van en el sentido de la marcha, así el coche, el rival y la cámara no saben en qué sentido corren. La geometría
    /// (mallas, pueblo, tráfico) siempre se construye con la de bajada (<see cref="Geometry"/>).
    /// </summary>
    public class TrackData
    {
        public readonly TrackPoint[] P;
        public readonly int N;
        public readonly float Step, Length;
        /// <summary>Medio ancho por defecto de la pista (el del pueblo y las piezas que no van por punto).</summary>
        public readonly float W;
        /// <summary>Medio ancho de la calzada en cada punto (los tramos pueden tener el suyo; los cambios se suavizan).</summary>
        public readonly float[] HW;
        /// <summary>Por punto: va sobre un puente (sin terreno debajo).</summary>
        public readonly bool[] Bridge;
        public readonly List<SegmentInfo> Segments = new List<SegmentInfo>();
        public readonly List<SegmentInfo> Hairpins = new List<SegmentInfo>();
        public readonly int TunnelStart = -1, TunnelEnd = -2;
        /// <summary>Por punto: lado donde está la canaleta interior de la horquilla (0 si no hay).</summary>
        public readonly sbyte[] GutterSide;
        /// <summary>Velocidad máxima en m/s que puede llevar el rival en cada punto.</summary>
        public float[] VMax { get; private set; }
        /// <summary>Desplazamiento lateral de la trazada del rival (positivo = derecha).</summary>
        public float[] Lane { get; private set; }
        public float[] KSmooth { get; private set; }
        public readonly int[] Sectors;

        public readonly TrackDirection Direction;
        public bool Uphill => Direction == TrackDirection.Subida;
        /// <summary>Punto de salida del jugador (el rival sale 18 m por delante).</summary>
        public readonly int RaceStart;
        /// <summary>Puntos del tramo cronometrado, contados desde el punto 0.</summary>
        public readonly int TimedCount;
        /// <summary>Punto donde salta la meta. En bajada es el mismo de antes del pueblo, así los récords siguen valiendo.</summary>
        public readonly int FinishIndex;
        /// <summary>Longitud cronometrada en metros desde el punto 0 (para el rival y la distancia a meta).</summary>
        public readonly float TimedLength;
        /// <summary>Puntos del pueblo: de <see cref="TownStart"/> a <see cref="TownEnd"/> (vacío si no hay pueblo).</summary>
        public readonly int TownStart, TownEnd;
        /// <summary>Punto donde se para el coche en la llegada (frente a la tienda de tofu, o en la cima subiendo).</summary>
        public readonly int StopIndex;
        /// <summary>Multiplica la aceleración del rival: subiendo, la pendiente le resta.</summary>
        public readonly float RivalAccel = 1;
        /// <summary>Punto de bajada donde está la meta de subida: deja ~140 m para frenar antes del final de la carretera.</summary>
        public int UphillGoal => Mathf.Clamp(70, 10, Mathf.Max(10, FinishIndex / 4));

        /// <summary>Curvas, horquillas y túneles del tramo cronometrado (para el menú).</summary>
        public readonly int CurveCount, HairpinCount, TunnelCount;
        /// <summary>Desnivel del tramo cronometrado en metros.</summary>
        public readonly float Drop;

        public bool HasTown => TownStart <= TownEnd;
        public bool InTown(int i) => i >= TownStart && i <= TownEnd;
        bool NearTown(int i, int margin) => HasTown ? i >= TownStart - margin && i <= TownEnd + margin : i >= N - margin;

        /// <summary>La de bajada, con la que se construyen las mallas.</summary>
        public TrackData Geometry => source ?? this;
        /// <summary>Índice en la pista de bajada del punto i de esta.</summary>
        public int ToGeometry(int i) => source != null ? N - 1 - i : i;

        readonly TrackData source;
        TrackData reversed;

        // Terreno a cada lado: [0] izquierda, [1] derecha
        readonly float[][] off = new float[2][], dy = new float[2][], plat = new float[2][];

        const float Brake = 9;
        // Perfil del rival de la pista: aceleración lateral en curva y velocidad máxima
        readonly float cornerGrip = 10.5f, topSpeed = 52;

        public TrackData(TrackDefinition def)
        {
            Direction = TrackDirection.Bajada;
            cornerGrip = def.rivalCornerGrip > 0 ? def.rivalCornerGrip : 10.5f;
            topSpeed = def.rivalTopSpeed > 0 ? def.rivalTopSpeed : 52;
            Step = def.step;
            W = def.halfWidth;
            var pts = new List<TrackPoint>();
            var widths = new List<float>();
            var bridges = new List<bool>();
            float x = 0, z = 0, h = 0, y = 0;
            int timed = -1, town = -1;
            foreach (var s in def.AllSegments())
            {
                bool sCurve = s.type == SegmentType.SCurve;
                int n = Mathf.Max(1, Mathf.RoundToInt(s.Length / Step));
                if (sCurve) n = Mathf.Max(2, n / 2 * 2);
                float d = s.angle * Mathf.Deg2Rad / (sCurve ? n / 2 : n);
                float sl = s.slope != 0 ? s.slope
                    : s.afterGoal ? def.afterGoalSlope : s.type == SegmentType.Hairpin ? def.hairpinSlope : def.slope;
                float hw = s.halfWidth > 0 ? s.halfWidth : def.halfWidth;
                if (s.afterGoal && timed < 0) timed = pts.Count;
                if (s.afterGoal && s.type == SegmentType.Straight && town < 0) town = pts.Count;
                var info = new SegmentInfo { type = s.type, angle = s.angle, start = pts.Count, end = pts.Count + n - 1, tunnel = s.tunnel };
                Segments.Add(info);
                if (s.type == SegmentType.Hairpin) Hairpins.Add(info);
                if (s.tunnel && TunnelStart < 0) { TunnelStart = info.start; TunnelEnd = info.end; }
                if (!s.afterGoal)
                {
                    if (s.type == SegmentType.Hairpin) { HairpinCount++; CurveCount++; }
                    else if (sCurve) CurveCount += 2;
                    else if (s.type == SegmentType.Curve && Mathf.Abs(s.angle) >= 20) CurveCount++;
                    if (s.tunnel) TunnelCount++;
                }
                for (int i = 0; i < n; i++)
                {
                    // En la ese, la segunda mitad gira lo mismo al otro lado
                    float di = sCurve && i >= n / 2 ? -d : d;
                    h += di;
                    x += Mathf.Sin(h) * Step;
                    z += Mathf.Cos(h) * Step;
                    y -= sl * Step;
                    pts.Add(new TrackPoint
                    {
                        pos = new Vector3(x, y, z),
                        heading = h,
                        k = di / Step,
                        forward = new Vector3(Mathf.Sin(h), 0, Mathf.Cos(h)),
                        right = new Vector3(Mathf.Cos(h), 0, -Mathf.Sin(h))
                    });
                    widths.Add(hw);
                    bridges.Add(s.bridge);
                }
            }
            P = pts.ToArray();
            N = P.Length;
            Length = N * Step;
            HW = widths.ToArray();
            Bridge = bridges.ToArray();
            // Transición de unos 30 m entre anchos distintos
            bool varied = false;
            for (int i = 1; i < N && !varied; i++) varied = HW[i] != HW[0];
            if (varied) Smooth(HW, 8, 2);

            RaceStart = 5;
            TimedCount = timed < 0 ? N : timed;
            FinishIndex = TimedCount - 5;
            TimedLength = TimedCount * Step;
            TownStart = town < 0 ? N : town;
            TownEnd = N - 1;
            StopIndex = Mathf.Min(N - 8, TownStart + Mathf.RoundToInt((N - TownStart) * 0.5f));
            Drop = P[0].pos.y - P[Mathf.Clamp(FinishIndex, 0, N - 1)].pos.y;

            GutterSide = new sbyte[N];
            foreach (var hp in Hairpins)
                for (int i = Mathf.Max(0, hp.start - 3); i <= Mathf.Min(N - 1, hp.end + 3); i++)
                    GutterSide[i] = (sbyte)Mathf.Sign(hp.angle);

            ComputeRacingLine(Brake);

            // Sobre el tramo cronometrado, igual que antes del pueblo: los parciales guardados siguen valiendo
            Sectors = new[] { TimedCount / 3, 2 * TimedCount / 3, FinishIndex };
            BuildTerrain();
        }

        /// <summary>La misma pista de subida: sale de la meta de bajada (encima del pueblo) y llega cerca de la cima.</summary>
        public TrackData Reversed()
        {
            if (source != null) return source;
            return reversed ?? (reversed = new TrackData(this));
        }

        TrackData(TrackData g)
        {
            source = g;
            Direction = TrackDirection.Subida;
            cornerGrip = g.cornerGrip;
            topSpeed = g.topSpeed;
            Step = g.Step;
            W = g.W;
            N = g.N;
            Length = g.Length;
            P = new TrackPoint[N];
            HW = new float[N];
            Bridge = new bool[N];
            GutterSide = new sbyte[N];
            for (int i = 0; i < N; i++)
            {
                int j = N - 1 - i;
                // El rumbo del punto j de bajada es el de llegada desde j-1; subiendo se llega desde j+1
                var a = g.P[Mathf.Min(N - 1, j + 1)];
                P[i] = new TrackPoint
                {
                    pos = g.P[j].pos,
                    heading = a.heading + Mathf.PI,
                    k = -a.k,
                    forward = -a.forward,
                    right = -a.right,
                };
                HW[i] = g.HW[j];
                Bridge[i] = g.Bridge[j];
                GutterSide[i] = (sbyte)-g.GutterSide[j];
            }
            for (int s = 0; s < 2; s++)
            {
                off[s] = Flip(g.off[1 - s]);
                dy[s] = Flip(g.dy[1 - s]);
                plat[s] = Flip(g.plat[1 - s]);
            }
            for (int k = g.Segments.Count - 1; k >= 0; k--) Segments.Add(Flip(g.Segments[k]));
            for (int k = g.Hairpins.Count - 1; k >= 0; k--) Hairpins.Add(Flip(g.Hairpins[k]));
            if (g.TunnelStart >= 0) { TunnelStart = N - 1 - g.TunnelEnd; TunnelEnd = N - 1 - g.TunnelStart; }
            CurveCount = g.CurveCount;
            HairpinCount = g.HairpinCount;
            TunnelCount = g.TunnelCount;
            Drop = g.Drop;

            // Salida bajo la pancarta de la meta de bajada (la pancarta 6 m por delante, como en la bajada)
            RaceStart = Mathf.Clamp(N - 1 - (g.FinishIndex + 1) - 3, 0, N - 1);
            FinishIndex = N - 1 - g.UphillGoal;
            TimedCount = FinishIndex + 5;
            TimedLength = TimedCount * Step;
            StopIndex = N - 1 - Mathf.Min(12, g.UphillGoal / 2);
            if (g.HasTown) { TownStart = 0; TownEnd = N - 1 - g.TownStart; }
            else { TownStart = 0; TownEnd = -1; }

            // Subiendo, la pendiente frena (se puede frenar más tarde) y resta aceleración al rival
            float grade = Mathf.Max(0, Drop) / Mathf.Max(1, (FinishIndex - RaceStart) * Step);
            RivalAccel = Mathf.Clamp(1 - 9.8f * grade / 4.2f, 0.7f, 1);
            ComputeRacingLine(Brake + 9.8f * grade);

            int span = FinishIndex - RaceStart;
            Sectors = new[] { RaceStart + span / 3, RaceStart + 2 * span / 3, FinishIndex };
        }

        float[] Flip(float[] a)
        {
            var b = new float[a.Length];
            for (int i = 0; i < a.Length; i++) b[i] = a[a.Length - 1 - i];
            return b;
        }

        SegmentInfo Flip(SegmentInfo s) => new SegmentInfo
        {
            // Al revés cada curva gira al otro lado; la ese empieza por la segunda curva, así que gira igual
            type = s.type, angle = s.type == SegmentType.SCurve ? s.angle : -s.angle, start = N - 1 - s.end, end = N - 1 - s.start, tunnel = s.tunnel
        };

        /// <summary>Velocidad máxima del rival por punto y su trazada, con la frenada dada (m/s²).</summary>
        void ComputeRacingLine(float brake)
        {
            var vmax = new float[N];
            var lane = new float[N];
            var ksm = new float[N];
            for (int i = 0; i < N; i++)
            {
                float ks = 0;
                for (int j = -6; j <= 6; j++) ks += P[Clamp(i + j)].k;
                ks /= 13;
                ksm[i] = ks;
                float r = Mathf.Abs(ks) > 1e-4f ? 1 / Mathf.Abs(ks) : 1e4f;
                vmax[i] = Mathf.Min(topSpeed, Mathf.Sqrt(cornerGrip * r));
                float kl = 0;
                for (int j = -14; j <= 14; j++) kl += P[Clamp(i + j + 6)].k;
                kl /= 29;
                lane[i] = Mathf.Clamp(Mathf.Sign(kl) * Mathf.Min(1, Mathf.Abs(kl) * 40) * HW[i] * 0.55f, -(HW[i] - 1.3f), HW[i] - 1.3f);
            }
            for (int i = N - 2; i >= 0; i--)
                vmax[i] = Mathf.Min(vmax[i], Mathf.Sqrt(vmax[i + 1] * vmax[i + 1] + 2 * brake * Step));
            VMax = vmax;
            Lane = lane;
            KSmooth = ksm;
        }

        int Clamp(int i) => i < 0 ? 0 : i >= N ? N - 1 : i;

        public bool InTunnel(int i) => i >= TunnelStart && i <= TunnelEnd;

        /// <summary>
        /// 0 lejos del pueblo, 1 al llegar a él: la cámara alarga el plano lejano para ver la ciudad del valle.
        /// Bajando es el final; subiendo, la salida.
        /// </summary>
        public float TownView(int i) => Uphill
            ? Mathf.InverseLerp(RaceStart + 150, RaceStart + 20, i)
            : Mathf.InverseLerp(FinishIndex - 150, FinishIndex - 20, i);

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
                    o[i] = Mathf.Max(o[i], HW[i] + 2.5f);
                    if (InTunnel(i) || Mathf.Abs(i - TunnelStart) < 6 || Mathf.Abs(i - TunnelEnd) < 6)
                    {
                        dd[i] = Mathf.Max(dd[i], 13);
                        o[i] = Mathf.Max(o[i], 18);
                    }
                    // Puente: el terreno cae a los dos lados (no hay muro ni ladera junto a la calzada)
                    if (Bridge[i]) dd[i] = -40;
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
                        for (int i = m - 14; i <= m + 14; i++) if (i >= 0 && i < N && !Bridge[i]) pl[i] = 1;
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

        public bool HasRail(int i, int side) => WallHeight(i, side) < 0.4f && !InTunnel(i) && !NearTown(i, 8);

        /// <summary>
        /// Sección transversal del terreno a un lado: 4 filas (distancia lateral sin signo, altura).
        /// 0 borde de calzada, 1 arcén, 2 pie/coronación del muro o explanada, 3 límite exterior.
        /// </summary>
        public void Rows(int i, int side, Vector2[] r)
        {
            int si = Si(side);
            var p = P[i];
            float w = HW[i];
            float d = dy[si][i], pl = plat[si][i], wh = WallHeight(i, side), m = Mathf.Min(1, wh);
            float l2 = w + 1.05f + (1 - m) * 0.9f, y2 = p.pos.y - 0.1f - (1 - m) * 0.25f + wh;
            l2 = Mathf.Lerp(l2, w + 7.5f, pl);
            y2 = Mathf.Lerp(y2, p.pos.y - 0.15f, pl);
            r[0] = new Vector2(w + 0.02f, p.pos.y - 0.02f);
            r[1] = new Vector2(w + 0.9f, p.pos.y - 0.1f);
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

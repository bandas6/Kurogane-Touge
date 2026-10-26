using System.Collections.Generic;
using UnityEngine;

namespace Kurogane.City
{
    /// <summary>
    /// Pueblo abierto (bloque H1): red de calles transitables de unos 600 × 600 m alrededor de la calle mayor.
    /// - Calle mayor: la carretera de la pista hasta su final y, desde ahí, calle del pueblo hasta el otro extremo.
    /// - Calles paralelas a 75, 160 y 245 m a cada lado; transversales cada 90-130 m, una de ellas avenida de cuatro
    ///   carriles; la vía del tren de punta a punta con la estación, y un río con puentes en la calle mayor y en las
    ///   paralelas de 160 m (las demás terminan en la orilla, con barandilla).
    /// - Casas a lo largo de las calles, en una o dos filas, con huertos y solares entre ellas; arrozales en la franja
    ///   exterior y cedros en el borde.
    /// - Lugares con función (tienda de tofu, gasolinera, konbini, mirador, aparcamiento al pie de la montaña y
    ///   estación) y la red de calles se guardan en <see cref="TownMap"/>.
    /// Una rejilla de ocupación de 2 m evita que casas, calles y lugares se pisen.
    /// </summary>
    public partial class CityBuilder
    {
        bool Open => style.extent > 0;
        float extent => style.extent;
        bool river => style.river && riverZ > -1000;

        const float NsHalf = 3.5f, Walk = 1.8f, RiverHalf = 12;
        float zEnd, riverZ = -10000;
        /// <summary>Distancia (positiva) a la calle mayor de las calles paralelas, de dentro afuera.</summary>
        readonly List<float> ParallelStreets = new List<float>();
        float zStart = -6;

        // ---------- Rejilla de ocupación ----------

        const float Cell = 2;
        bool[] grid;
        int gw, gh;
        float gx0, gz0;

        void Mark(float x, float z, float sx, float sz)
        {
            if (grid == null) return;
            int i0 = Mathf.FloorToInt((x - sx / 2 - gx0) / Cell), i1 = Mathf.CeilToInt((x + sx / 2 - gx0) / Cell);
            int j0 = Mathf.FloorToInt((z - sz / 2 - gz0) / Cell), j1 = Mathf.CeilToInt((z + sz / 2 - gz0) / Cell);
            for (int j = Mathf.Max(0, j0); j < Mathf.Min(gh, j1); j++)
                for (int i = Mathf.Max(0, i0); i < Mathf.Min(gw, i1); i++)
                    grid[j * gw + i] = true;
        }

        bool Free(float x, float z, float sx, float sz)
        {
            if (grid == null) return true;
            int i0 = Mathf.FloorToInt((x - sx / 2 - gx0) / Cell), i1 = Mathf.CeilToInt((x + sx / 2 - gx0) / Cell);
            int j0 = Mathf.FloorToInt((z - sz / 2 - gz0) / Cell), j1 = Mathf.CeilToInt((z + sz / 2 - gz0) / Cell);
            if (i0 < 0 || j0 < 0 || i1 > gw || j1 > gh) return false;
            for (int j = j0; j < j1; j++)
                for (int i = i0; i < i1; i++)
                    if (grid[j * gw + i]) return false;
            return true;
        }

        // ---------- Trazado ----------

        void OpenLayout()
        {
            zEnd = Mathf.Max(length + 80, style.length);
            foreach (float x in new[] { 75f, 160f, 245f })
                if (x < extent - 25) ParallelStreets.Add(x);

            crossings.Clear();
            crossings.Add((z1, 4.5f));
            crossings.Add((z2, style.avenue ? 7f : 4.5f));
            // Transversales más allá de la pista, cada 90-130 m, y una al final del pueblo
            var extra = new List<float>();
            for (float z = length + 90; z < zEnd - 60; z += 140) extra.Add(z);
            extra.Add(zEnd - 12);
            foreach (float z in extra) crossings.Add((z, 3.5f));
            // Río entre las dos primeras transversales de después de la pista
            if (style.river && extra.Count >= 2 && extra[1] - extra[0] > 2 * RiverHalf + 30) riverZ = (extra[0] + extra[1]) / 2;

            gx0 = -extent - 40;
            gz0 = zStart - 20;
            gw = Mathf.CeilToInt((2 * extent + 80) / Cell);
            gh = Mathf.CeilToInt((zEnd + 60 - gz0) / Cell);
            grid = new bool[gw * gh];

            // Calles, río y vía ocupan su sitio antes que nada
            Mark(0, (zStart + zEnd) / 2, 2 * (W + 2.2f), zEnd - zStart + 4);
            foreach (var c in crossings) Mark(0, c.z, 2 * extent + 60, 2 * (c.hw + Walk + 0.4f));
            foreach (float p in ParallelStreets)
                foreach (int s in new[] { -1, 1 })
                    Mark(s * p, (zStart + zEnd) / 2, 2 * (NsHalf + Walk + 0.4f), zEnd - zStart + 4);
            if (river) Mark(0, riverZ, 2 * extent + 60, 2 * (RiverHalf + 3));
            // Delante de la montaña (z < 0) solo hay terreno de la pista
            Mark(0, gz0 + (-2 - gz0) / 2, 2 * extent + 80, -2 - gz0);
        }

        bool Bridged(float x) => Mathf.Abs(x) < 1 || Mathf.Abs(Mathf.Abs(x) - 160) < 1;

        /// <summary>Posición del mirador a lo ancho (lado izquierdo, entre la calle mayor y la primera paralela).</summary>
        float ViewpointX => -Mathf.Min(40, extent * 0.4f);

        void OpenTown()
        {
            var map = new GameObject("Mapa del pueblo").AddComponent<TownMap>();
            map.transform.SetParent(tb.Root, false);
            Ground();
            OpenStreets();
            if (river) River();
            if (style.station) Station();
            if (river) Viewpoint();
            StreetLamps();
            Frontages();
            Fields();
            EdgeCedars();
            BuildMap(map);
        }

        // ---------- Suelo ----------

        /// <summary>
        /// Césped bajo todo el pueblo en celdas de 20 m, siguiendo la pendiente de la calle mayor. Junto a la calle mayor,
        /// donde la pista ya tiene su terreno, y en el cauce del río no hay.
        /// </summary>
        void Ground()
        {
            const float c = 20;
            for (float z = -10; z < zEnd + 30; z += c)
                for (float x = -extent - 30; x < extent + 30; x += c)
                {
                    float xa = x, xb = x + c, za = z, zb = Mathf.Min(z + c, zEnd + 30);
                    if (Mathf.Min(Mathf.Abs(xa), Mathf.Abs(xb)) < 76 && xa < 76 && xb > -76 && za < length + 2 && zb > -12)
                    {
                        // Recorta la celda a lo que queda fuera del terreno de la pista (|x| >= 76)
                        if (xb <= 76 && xa >= -76) continue;
                        if (xa < -76) xb = Mathf.Min(xb, -76);
                        else xa = Mathf.Max(xa, 76);
                    }
                    if (river && za < riverZ + RiverHalf && zb > riverZ - RiverHalf)
                    {
                        if (za < riverZ - RiverHalf) Top(xa, xb, za, riverZ - RiverHalf, -0.32f, TownCell.Grass);
                        if (zb > riverZ + RiverHalf) Top(xa, xb, riverZ + RiverHalf, zb, -0.32f, TownCell.Grass);
                        continue;
                    }
                    Top(xa, xb, za, zb, -0.32f, TownCell.Grass);
                }
        }

        /// <summary>Cuadrilátero horizontal en ejes locales (visto desde arriba), a <paramref name="y"/> sobre la calle.</summary>
        void Top(float xa, float xb, float za, float zb, float y, TownCell cell, Layer layer = Layer.Ground)
        {
            if (xb - xa < 0.01f || zb - za < 0.01f) return;
            Quad(layer, W3(xa, za, y), W3(xb, za, y), W3(xb, zb, y), W3(xa, zb, y), cell);
        }

        /// <summary>Pared vertical de dos caras entre dos puntos locales.</summary>
        void Fence(float xa, float za, float xb, float zb, float y0, float y1, TownCell cell, Layer layer = Layer.Buildings)
        {
            var a = W3(xa, za, y0);
            var b = W3(xb, zb, y0);
            var up0 = Vector3.up * (y1 - y0);
            Quad(layer, a, b, b + up0, a + up0, cell);
            Quad(layer, b, a, a + up0, b + up0, cell);
        }

        // ---------- Calles ----------

        /// <summary>
        /// Calzada recta en ejes locales (paralela a la calle mayor o transversal), con aceras y bordillo opcionales y
        /// línea central discontinua (dos líneas y separación de carriles en la avenida).
        /// </summary>
        void Road(float xa, float za, float xb, float zb, float hw, bool sidewalks, bool avenue = false)
        {
            bool alongZ = Mathf.Abs(xa - xb) < 0.01f;
            float len = alongZ ? Mathf.Abs(zb - za) : Mathf.Abs(xb - xa);
            if (len < 0.5f) return;
            if (alongZ)
            {
                float z0 = Mathf.Min(za, zb), z1e = Mathf.Max(za, zb), x = xa;
                for (float z = z0; z < z1e; z += 6)
                {
                    float zz = Mathf.Min(z1e, z + 6);
                    Top(x - hw, x + hw, z, zz, 0.02f, TownCell.Asphalt);
                    if (sidewalks)
                        foreach (int s in new[] { -1, 1 })
                        {
                            Top(Mathf.Min(x + s * hw, x + s * (hw + Walk)), Mathf.Max(x + s * hw, x + s * (hw + Walk)), z, zz, 0.15f, TownCell.Sidewalk);
                            Fence(x + s * hw, z, x + s * hw, zz, -0.02f, 0.15f, TownCell.Concrete, Layer.Ground);
                        }
                }
                for (float z = z0 + 2; z < z1e - 2; z += 5) Top(x - 0.08f, x + 0.08f, z, z + 2.4f, 0.04f, TownCell.Paint);
            }
            else
            {
                float x0 = Mathf.Min(xa, xb), x1e = Mathf.Max(xa, xb), z = za;
                Top(x0, x1e, z - hw, z + hw, 0.02f, TownCell.Asphalt);
                if (sidewalks)
                    foreach (int s in new[] { -1, 1 })
                    {
                        Top(x0, x1e, Mathf.Min(z + s * hw, z + s * (hw + Walk)), Mathf.Max(z + s * hw, z + s * (hw + Walk)), 0.15f, TownCell.Sidewalk);
                        Fence(x0, z + s * hw, x1e, z + s * hw, -0.02f, 0.15f, TownCell.Concrete, Layer.Ground);
                    }
                if (avenue)
                {
                    // Doble línea central y separación de carriles
                    Top(x0, x1e, z - 0.25f, z - 0.1f, 0.04f, TownCell.Paint);
                    Top(x0, x1e, z + 0.1f, z + 0.25f, 0.04f, TownCell.Paint);
                    foreach (int s in new[] { -1, 1 })
                        for (float x = x0 + 2; x < x1e - 2; x += 5) Top(x, x + 2.4f, z + s * hw / 2 - 0.08f, z + s * hw / 2 + 0.08f, 0.04f, TownCell.Paint);
                }
                else
                    for (float x = x0 + 2; x < x1e - 2; x += 5) Top(x, x + 2.4f, z - 0.08f, z + 0.08f, 0.04f, TownCell.Paint);
            }
        }

        /// <summary>Tramos de una línea de calle entre cortes (cruces, calle mayor, río) con un hueco en cada corte.</summary>
        static IEnumerable<(float a, float b)> Pieces(float from, float to, List<(float at, float gap)> cuts)
        {
            cuts.Sort((p, q) => p.at.CompareTo(q.at));
            float a = from;
            foreach (var c in cuts)
            {
                if (c.at + c.gap <= from || c.at - c.gap >= to) continue;
                if (c.at - c.gap > a) yield return (a, c.at - c.gap);
                a = Mathf.Max(a, c.at + c.gap);
            }
            if (to > a) yield return (a, to);
        }

        void OpenStreets()
        {
            // Calle mayor más allá de la pista (la pista llega hasta "length"); el río lo cruza un puente
            var mainCuts = new List<(float, float)>();
            if (river) mainCuts.Add((riverZ, RiverHalf + 1));
            foreach (var p in Pieces(length, zEnd, mainCuts)) Road(0, p.a, 0, p.b, W, false);

            // Transversales de punta a punta: la calzada pasa por los cruces; las aceras se cortan en ellos
            foreach (var c in crossings)
            {
                bool avenue = c.hw > 5;
                var cuts = new List<(float, float)> { (0, W + 0.05f) };
                foreach (var p in Pieces(-extent, extent, cuts)) Road(p.a, c.z, p.b, c.z, c.hw, false, avenue);
                var walkCuts = new List<(float, float)> { (0, W + 2.2f) };
                foreach (float x in ParallelStreets) { walkCuts.Add((x, NsHalf + 0.05f)); walkCuts.Add((-x, NsHalf + 0.05f)); }
                foreach (var p in Pieces(-extent, extent, walkCuts))
                    foreach (int s in new[] { -1, 1 })
                    {
                        Top(p.a, p.b, Mathf.Min(c.z + s * c.hw, c.z + s * (c.hw + Walk)), Mathf.Max(c.z + s * c.hw, c.z + s * (c.hw + Walk)), 0.15f, TownCell.Sidewalk);
                        Fence(p.a, c.z + s * c.hw, p.b, c.z + s * c.hw, -0.02f, 0.15f, TownCell.Concrete, Layer.Ground);
                    }
            }

            // Paralelas: entre transversales; sin puente terminan en la orilla del río
            foreach (float px in ParallelStreets)
                foreach (int s in new[] { -1, 1 })
                {
                    float x = s * px;
                    var cuts = new List<(float, float)>();
                    foreach (var c in crossings) cuts.Add((c.z, c.hw + 0.05f));
                    if (river) cuts.Add((riverZ, RiverHalf + (Bridged(x) ? 1 : 3)));
                    foreach (var p in Pieces(zStart, zEnd - 12, cuts)) Road(x, p.a, x, p.b, NsHalf, true);
                }
        }

        // ---------- Río, puentes y mirador ----------

        void River()
        {
            float x0 = -extent - 30, x1 = extent + 30, zs0 = riverZ - RiverHalf, zs1 = riverZ + RiverHalf;
            // Orillas en talud y agua a 2,2 m por debajo de la calle
            for (float x = x0; x < x1; x += 20)
            {
                float xb = Mathf.Min(x1, x + 20);
                Quad(Layer.Ground, W3(x, zs0, -0.32f), W3(xb, zs0, -0.32f), W3(xb, zs0 + 3, -2.6f), W3(x, zs0 + 3, -2.6f), TownCell.Concrete);
                Quad(Layer.Ground, W3(x, zs1 - 3, -2.6f), W3(xb, zs1 - 3, -2.6f), W3(xb, zs1, -0.32f), W3(x, zs1, -0.32f), TownCell.Concrete);
                Top(x, xb, zs0 + 2.5f, zs1 - 2.5f, -2.25f, TownCell.River, Layer.Water);
            }
            // Barandilla en las dos orillas, salvo en los puentes
            var cuts = new List<(float, float)> { (0, W + 2.4f) };
            foreach (float px in ParallelStreets)
                if (Bridged(px)) { cuts.Add((px, NsHalf + Walk + 0.4f)); cuts.Add((-px, NsHalf + Walk + 0.4f)); }
            foreach (float z in new[] { zs0 - 0.3f, zs1 + 0.3f })
            {
                // En la orilla de este lado, el mirador tiene su propia barandilla
                var bankCuts = new List<(float, float)>(cuts);
                if (z < riverZ) bankCuts.Add((ViewpointX, 4.6f));
                foreach (var p in Pieces(x0, x1, bankCuts))
                {
                    Fence(p.a, z, p.b, z, -0.3f, 1.0f, TownCell.Balcony);
                    for (float x = p.a; x < p.b; x += 2.5f) Box(Layer.Buildings, W3(x, z, 0.35f), Street, new Vector3(0.1f, 1.3f, 0.1f), TownCell.MetalDark);
                }
            }
            // Puentes: tablero, pretiles y pilas
            var bridges = new List<(float x, float hw)> { (0, W + 2.2f) };
            foreach (float px in ParallelStreets) if (Bridged(px)) { bridges.Add((px, NsHalf + Walk)); bridges.Add((-px, NsHalf + Walk)); }
            foreach (var b in bridges)
            {
                float hw = b.x == 0 ? W : NsHalf;
                Box(Layer.Ground, W3(b.x, riverZ, -0.3f), Street, new Vector3(2 * b.hw + 0.6f, 0.64f, 2 * RiverHalf + 2), TownCell.Concrete, TownCell.Concrete, TownCell.Concrete, TownCell.Asphalt);
                Top(b.x - hw, b.x + hw, zs0 - 1, zs1 + 1, 0.03f, TownCell.Asphalt);
                for (float z = zs0; z < zs1; z += 5) Top(b.x - 0.08f, b.x + 0.08f, z, z + 2.4f, 0.05f, TownCell.Paint);
                foreach (int s in new[] { -1, 1 })
                {
                    Box(Layer.Buildings, W3(b.x + s * (b.hw + 0.15f), riverZ, 0.5f), Street, new Vector3(0.3f, 1.0f, 2 * RiverHalf + 2), TownCell.Concrete);
                    Box(Layer.Buildings, W3(b.x + s * (b.hw * 0.6f), riverZ, -1.6f), Street, new Vector3(1.2f, 3, 1.6f), TownCell.Concrete);
                }
                Mark(b.x, riverZ, 2 * b.hw + 1, 2 * RiverHalf + 2);
            }
        }

        void Viewpoint()
        {
            // Mirador sobre la orilla de este lado del río, en el lado izquierdo del pueblo
            float x = ViewpointX, z = riverZ - RiverHalf + 1.5f;
            Box(Layer.Ground, W3(x, z, -0.15f), Street, new Vector3(9, 0.3f, 5), TownCell.Concrete, TownCell.Concrete, TownCell.Concrete, TownCell.Wood);
            Fence(x - 4.5f, z + 2.5f, x + 4.5f, z + 2.5f, 0, 1.1f, TownCell.Balcony);
            Fence(x - 4.5f, z - 2.5f, x - 4.5f, z + 2.5f, 0, 1.1f, TownCell.Balcony);
            Fence(x + 4.5f, z - 2.5f, x + 4.5f, z + 2.5f, 0, 1.1f, TownCell.Balcony);
            Box(Layer.Detail, W3(x - 1.5f, z, 0.25f), Street, new Vector3(2.2f, 0.12f, 0.5f), TownCell.Wood);
            Box(Layer.Detail, W3(x + 1.5f, z, 0.25f), Street, new Vector3(2.2f, 0.12f, 0.5f), TownCell.Wood);
            Cylinder(Layer.Detail, W3(x + 4, z - 2, 0), 0.08f, 4, TownCell.MetalDark);
            Box(Layer.Detail, W3(x + 4, z - 2, 4.05f), Street, new Vector3(0.5f, 0.15f, 0.3f), TownCell.MetalDark, TownCell.MetalDark, TownCell.MetalDark, TownCell.MetalDark, TownCell.StreetLamp);
            AddLight(W3(x + 4, z - 2, 3.7f), new Color(1f, 0.9f, 0.72f), 12, 1.3f);
            Box(Layer.Detail, W3(x - 4, z - 3.2f, 1.2f), Street, new Vector3(1.4f, 0.6f, 0.08f), TownCell.SignWood);
            Text("見晴台", W3(x - 4, z - 3.28f, 1.2f), -fwd, 0.35f, new Color(1f, 0.95f, 0.82f));
            Mark(x, z - 2, 12, 9);
            Place(TownMap.PlaceKind.Mirador, "Mirador del río", W3(x, z - 6, 0), fwd);
        }

        // ---------- Lugares ----------

        readonly List<TownMap.Place> places = new List<TownMap.Place>();

        void Place(TownMap.PlaceKind kind, string name, Vector3 pos, Vector3 facing) =>
            places.Add(new TownMap.Place { kind = kind, name = name, position = pos, heading = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg });

        /// <summary>Aparcamiento al pie de la montaña, junto a la calle mayor: punto de encuentro de los rivales.</summary>
        void MountainParking(int side, float za, float zb)
        {
            special.Add((side, za, zb));
            float x0 = W + 2.6f, x1 = W + 30, xc = side * (x0 + x1) / 2, zc = (za + zb) / 2;
            Box(Layer.Ground, W3(xc, zc, -0.17f), Street, new Vector3(x1 - x0, 0.38f, zb - za), TownCell.Concrete, TownCell.Concrete, TownCell.Concrete, TownCell.Asphalt);
            for (float z = za + 2.6f; z < zb - 1; z += 2.8f)
                Top(Mathf.Min(side * (x1 - 6), side * (x1 - 1)), Mathf.Max(side * (x1 - 6), side * (x1 - 1)), z - 0.06f, z + 0.06f, 0.04f, TownCell.Paint);
            // Máquinas, un farol y el cartel
            Box(Layer.Detail, W3(side * (x1 - 1), za + 1.2f, 1.05f), Facing(side), new Vector3(0.95f, 1.85f, 0.8f), TownCell.VendingBlue, TownCell.WallWhite, TownCell.WallWhite, TownCell.MetalDark);
            Cylinder(Layer.Detail, W3(xc, zb - 1, 0), 0.1f, 6, TownCell.MetalDark);
            Box(Layer.Detail, W3(xc, zb - 1, 6.05f), Street, new Vector3(0.55f, 0.16f, 0.32f), TownCell.MetalDark, TownCell.MetalDark, TownCell.MetalDark, TownCell.MetalDark, TownCell.StreetLamp);
            AddLight(W3(xc, zb - 1, 5.7f), new Color(1f, 0.9f, 0.72f), 16, 1.4f);
            Box(Layer.Detail, W3(side * (x0 + 0.8f), za + 1, 1.6f), Facing(side), new Vector3(2.2f, 0.9f, 0.1f), TownCell.SignBlue);
            Text("峠 駐車場", W3(side * (x0 + 0.85f), za + 1, 1.6f), side < 0 ? right : -right, 0.32f, Color.white);
            Mark(xc, zc, x1 - x0 + 2, zb - za + 2);
            Place(TownMap.PlaceKind.Aparcamiento, "Aparcamiento del puerto", W3(xc, zc, 0), side < 0 ? right : -right);
        }

        void Station()
        {
            // Entre la primera y la segunda paralela, al otro lado de la vía, de cara al andén
            float p1 = ParallelStreets.Count > 0 ? ParallelStreets[0] : 75, p2 = ParallelStreets.Count > 1 ? ParallelStreets[1] : p1 + 85;
            float xa = p1 + NsHalf + Walk + 4, xb = p2 - NsHalf - Walk - 4, xc = (xa + xb) / 2;
            float zp = zr + 4.2f;
            var face = Quaternion.LookRotation(-fwd);
            // Andén
            Box(Layer.Ground, W3(xc, zp, 0.2f), Street, new Vector3(xb - xa, 1.0f, 3.2f), TownCell.Concrete, TownCell.Concrete, TownCell.Concrete, TownCell.Sidewalk);
            Top(xa, xb, zp - 1.5f, zp - 1.3f, 0.71f, TownCell.Paint);
            // Marquesina sobre el andén
            for (float x = xa + 3; x < xb - 2; x += 8) Box(Layer.Buildings, W3(x, zp + 0.8f, 2.4f), Street, new Vector3(0.2f, 3.4f, 0.2f), TownCell.MetalDark);
            Box(Layer.Buildings, W3(xc, zp, 4.1f), Street * Quaternion.Euler(-6, 0, 0), new Vector3(xb - xa - 2, 0.12f, 3.6f), TownCell.Roof);
            // Edificio de la estación entre el andén y la avenida, con la entrada y el nombre hacia la avenida
            float north = z2 - CrossHalf(z2) - Walk - 1.5f, south = zp + 2.5f;
            float bw = Mathf.Min(24, xb - xa - 6), depth = Mathf.Clamp(north - south - 1, 6, 10), bz = north - depth / 2;
            var entrance = Quaternion.LookRotation(fwd);
            HouseAt(xc, bz, entrance, bw, depth, true, null, 1);
            Box(Layer.Buildings, W3(xc, bz + depth / 2 + 0.1f, 3.6f), entrance, new Vector3(bw * 0.6f, 0.9f, 0.15f), TownCell.SignWood, TownCell.Wood, TownCell.Wood, TownCell.Wood);
            Text(style.stationName, W3(xc, bz + depth / 2 + 0.2f, 3.6f), fwd, 0.55f, new Color(1f, 0.95f, 0.82f));
            AddLight(W3(xc, zp + 1, 3.6f), new Color(0.9f, 0.95f, 1f), 18, 1.5f);
            AddLight(W3(xc, north + 1, 3.2f), new Color(1f, 0.9f, 0.72f), 12, 1.2f);
            Mark(xc, (zp - 2 + north) / 2, xb - xa, north - zp + 4);
            // Se para en el carril de la avenida que pasa por delante (hacia -x se circula por el lado de la estación)
            Place(TownMap.PlaceKind.Estacion, style.stationName, W3(xc, z2 - CrossHalf(z2) / 2, 0), -right);
        }

        void StreetLamps()
        {
            // Farolas en las calles del pueblo abierto: poste, brazo y cabeza; luz real en una de cada dos
            int n = 0;
            void Lamp(float x, float z, Vector3 toRoad)
            {
                var foot = W3(x, z, 0.15f);
                Cylinder(Layer.Detail, foot, 0.1f, 6.2f, TownCell.MetalDark);
                var head = foot + Vector3.up * 6.1f + toRoad * 1.2f;
                Box(Layer.Detail, foot + Vector3.up * 6.2f + toRoad * 0.6f, Quaternion.LookRotation(toRoad), new Vector3(0.1f, 0.1f, 1.3f), TownCell.MetalDark);
                Box(Layer.Detail, head, Quaternion.LookRotation(toRoad), new Vector3(0.32f, 0.16f, 0.55f), TownCell.MetalDark, TownCell.MetalDark, TownCell.MetalDark, TownCell.MetalDark, TownCell.StreetLamp);
                if (n++ % 2 == 0) AddLight(head - Vector3.up * 0.3f, new Color(1f, 0.9f, 0.72f), 15, 1.3f);
            }
            bool NearCross(float x)
            {
                if (Mathf.Abs(x) < W + 8) return true;
                foreach (float px in ParallelStreets) if (Mathf.Abs(Mathf.Abs(x) - px) < NsHalf + 6) return true;
                return false;
            }
            foreach (var c in crossings)
                for (float x = -extent + 25; x < extent - 10; x += 55)
                    if (!NearCross(x)) Lamp(x, c.z + c.hw + Walk - 0.4f, -fwd);
            foreach (float px in ParallelStreets)
                foreach (int s in new[] { -1, 1 })
                    for (float z = zStart + 20; z < zEnd - 20; z += 55)
                        if (!Reserved(z, 4)) Lamp(s * px + NsHalf + Walk - 0.4f, z, -right);
        }

        // ---------- Casas a lo largo de las calles ----------

        void Frontages()
        {
            // Transversales: dos filas a cada lado (la de detrás, menos densa)
            foreach (var c in crossings)
                foreach (int s in new[] { -1, 1 })
                {
                    var face = Quaternion.LookRotation(-s * fwd);
                    for (int row = 0; row < 2; row++)
                        for (float x = -extent + 8; x < extent - 8;)
                        {
                            float w = R(7.5f, 11), depth = R(7, 9.5f);
                            float setback = c.hw + Walk + R(1, 2.5f) + row * 13;
                            float zc = c.z + s * (setback + depth / 2);
                            if (Mathf.Abs(x) > W + 10 && Chance(row == 0 ? style.density : style.density * 0.55f))
                                HouseAt(x + w / 2, zc, face, w, depth, row == 0 && Chance(style.shopShare * 0.5f), null, Chance(0.25f) ? 1 : 2);
                            else if (row == 0 && Chance(0.25f) && Free(x + w / 2, zc, w, depth))
                                Garden(x + w / 2, zc, w, depth);
                            x += w + R(0.8f, 2.5f);
                        }
                }
            // Paralelas: dos filas a cada lado de cada una
            foreach (float px in ParallelStreets)
                foreach (int s in new[] { -1, 1 })
                    foreach (int side in new[] { -1, 1 })
                    {
                        float x0 = s * px;
                        var face = Quaternion.LookRotation(-side * right);
                        for (int row = 0; row < 2; row++)
                            for (float z = zStart; z < zEnd - 10;)
                            {
                                float w = R(7.5f, 11), depth = R(7, 9.5f);
                                float setback = NsHalf + Walk + R(1, 2.5f) + row * 13;
                                float xc = x0 + side * (setback + depth / 2);
                                if (Chance(row == 0 ? style.density : style.density * 0.55f))
                                    HouseAt(xc, z + w / 2, face, w, depth, row == 0 && Chance(style.shopShare * 0.4f), null, Chance(0.25f) ? 1 : 2);
                                else if (row == 0 && Chance(0.25f) && Free(xc, z + w / 2, depth, w))
                                    Garden(xc, z + w / 2, depth, w);
                                z += w + R(0.8f, 2.5f);
                            }
                    }
        }

        /// <summary>Huerto o solar con valla baja.</summary>
        void Garden(float x, float z, float sx, float sz)
        {
            Top(x - sx / 2, x + sx / 2, z - sz / 2, z + sz / 2, -0.29f, Chance(0.6f) ? TownCell.Earth : TownCell.Lot);
            Fence(x - sx / 2, z - sz / 2, x + sx / 2, z - sz / 2, -0.3f, 0.5f, TownCell.Wood);
            Fence(x - sx / 2, z + sz / 2, x + sx / 2, z + sz / 2, -0.3f, 0.5f, TownCell.Wood);
            Mark(x, z, sx, sz);
        }

        // ---------- Alrededores ----------

        /// <summary>Arrozales en los huecos libres de la franja exterior, más allá de la última paralela.</summary>
        void Fields()
        {
            float inner = ParallelStreets.Count > 0 ? ParallelStreets[ParallelStreets.Count - 1] + 28 : W + 40;
            foreach (int s in new[] { -1, 1 })
                for (float z = zStart + 4; z < zEnd - 4; z += 24)
                    for (float x = inner; x + 24 <= extent + 20; x += 28)
                    {
                        float xc = s * (x + 12), zc = z + 12;
                        if (!Free(xc, zc, 24, 24)) continue;
                        Mark(xc, zc, 24, 24);
                        var a = W3(xc - 11.6f, zc - 11.6f, -0.27f);
                        var b = W3(xc + 11.6f, zc - 11.6f, -0.27f);
                        var cc = W3(xc + 11.6f, zc + 11.6f, -0.27f);
                        var e = W3(xc - 11.6f, zc + 11.6f, -0.27f);
                        Quad(Layer.Water, a, b, cc, e, TownCell.Paddy);
                        Box(Layer.Ground, W3(xc, zc - 12, -0.2f), Street, new Vector3(24, 0.3f, 0.7f), TownCell.Dyke);
                        Box(Layer.Ground, W3(xc - 12, zc, -0.2f), Street, new Vector3(0.7f, 0.3f, 24), TownCell.Dyke);
                    }
        }

        /// <summary>Cedros en el borde del pueblo: a los lados y al final.</summary>
        void EdgeCedars()
        {
            void Tree(float x, float z)
            {
                var foot = W3(x, z, -0.3f);
                float sc = R(0.85f, 1.3f);
                Cylinder(Layer.Buildings, foot, 0.2f * sc, 1.4f * sc, TownCell.Trunk, 5);
                Cone(foot + Vector3.up * 0.8f * sc, 1.7f * sc, 4.2f * sc, TownCell.Cedar);
                Cone(foot + Vector3.up * 3.2f * sc, 1.25f * sc, 3.4f * sc, TownCell.Cedar);
                Cone(foot + Vector3.up * 5.4f * sc, 0.7f * sc, 2.2f * sc, TownCell.Cedar);
            }
            foreach (int s in new[] { -1, 1 })
                for (float z = -8; z < zEnd + 25; z += R(5, 8))
                    Tree(s * (extent + R(22, 28)), z);
            for (float x = -extent - 20; x < extent + 20; x += R(5, 8))
                if (Mathf.Abs(x) > W + 3) Tree(x, zEnd + R(22, 28));
        }

        // ---------- Mapa ----------

        void BuildMap(TownMap map)
        {
            Vector3 P(float x, float z) => W3(x, z, 0.02f);
            int Node(float x, float z) => map.AddNode(P(x, z));
            void Edge(float xa, float za, float xb, float zb, float hw, int lanes, bool bridge, bool main)
            {
                map.edges.Add(new TownMap.Edge { a = Node(xa, za), b = Node(xb, zb), halfWidth = hw, lanes = lanes, bridge = bridge, main = main });
            }

            var lines = new List<float> { 0 };
            foreach (float px in ParallelStreets) { lines.Add(px); lines.Add(-px); }
            lines.Sort();

            // Paralelas y calle mayor: de cruce en cruce
            foreach (float x in lines)
            {
                bool main = Mathf.Abs(x) < 0.5f;
                var stops = new List<float> { main ? 0 : zStart };
                foreach (var c in crossings) stops.Add(c.z);
                stops.Add(main ? zEnd : zEnd - 12);
                if (river && !Bridged(x)) { stops.Add(riverZ - RiverHalf - 3); stops.Add(riverZ + RiverHalf + 3); }
                stops.Sort();
                for (int k = 0; k + 1 < stops.Count; k++)
                {
                    float a = stops[k], b = stops[k + 1];
                    if (b - a < 0.5f) continue;
                    bool crossesRiver = river && a < riverZ && b > riverZ;
                    if (crossesRiver && !Bridged(x)) continue;
                    Edge(x, a, x, b, main ? W : NsHalf, 1, crossesRiver, main);
                }
            }
            // Transversales: de calle en calle, de punta a punta
            foreach (var c in crossings)
            {
                var stops = new List<float> { -extent };
                stops.AddRange(lines);
                stops.Add(extent);
                stops.Sort();
                for (int k = 0; k + 1 < stops.Count; k++)
                    Edge(stops[k], c.z, stops[k + 1], c.z, c.hw, c.hw > 5 ? 2 : 1, false, false);
            }

            // Lugares de la calle mayor (los del pueblo abierto ya están en la lista)
            float kz = z2 + CrossHalf(z2) + 1.5f;
            Place(TownMap.PlaceKind.TiendaTofu, style.tofuShopName, W3(-2.4f, zs, 0), fwd);
            Place(TownMap.PlaceKind.Gasolinera, "Gasolinera", W3(W + 9, 20, 0), fwd);
            Place(TownMap.PlaceKind.Konbini, style.konbiniName, W3(W + 5.6f, kz + 6, 0), right);
            Place(TownMap.PlaceKind.SalidaMontana, "Salida a la montaña", W3(0, 2, 0), -fwd);
            map.places.AddRange(places);
        }
    }
}

using System.Collections.Generic;
using Kurogane.Cars;
using UnityEngine;

namespace Kurogane.Track
{
    /// <summary>
    /// Colisionadores para la superficie del mundo (bloque G, <see cref="WorldSurface"/>). Se construyen con la pista y
    /// se hornean con ella; en carrera no se usan (la superficie de pista no los mira).
    /// - Suelo (capa Suelo): las mismas mallas de calzada, canaleta, arcén, ladera y suelo del pueblo.
    /// - Bordes (capa Obstaculo): cajas invisibles por fuera de donde la pista pone el límite del coche, desplazadas el
    ///   radio de la cápsula del coche; así se choca en el mismo sitio con las dos superficies. En el pueblo no hay bordes: las
    ///   paredes son las casas.
    /// - Canaleta (capa Canaleta): disparadores sobre el desagüe de cada horquilla, donde la pista detecta las ruedas.
    /// </summary>
    public partial class TrackBuilder
    {
        public const string CollidersName = "Colisiones";

        static readonly HashSet<string> GroundMeshes = new HashSet<string>
        {
            "Road", "Gutter", "Shoulder", "Slope", "BridgeDeck", Kurogane.City.CityBuilder.GroundName,
        };

        void BuildColliders()
        {
            var d = Geometry;
            var parent = new GameObject(CollidersName).transform;
            parent.SetParent(root, false);

            // Suelo y casas: colisionadores sobre las mallas que ya existen (no ocupan memoria aparte)
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                string n = mf.gameObject.name;
                if (GroundMeshes.Contains(n)) MeshCollider(parent, "Suelo", mf.sharedMesh, mf.transform, WorldSurface.GroundLayer);
                else if (n == Kurogane.City.CityBuilder.BuildingsName) MeshCollider(parent, "Casas", mf.sharedMesh, mf.transform, WorldSurface.ObstacleLayer);
            }

            // Bordes: una caja maciza de 2 m por punto, centrada en él y orientada con su tangente, por fuera de donde la
            // superficie de pista frena el centro del coche (más el radio de la cápsula). Es el mismo límite que la pista,
            // que mide el lateral contra la tangente del punto más cercano. Cajas convexas: la separación siempre empuja
            // hacia la calzada (con mallas finas, o con una cara recta cada 4 m, en las curvas los roces frenaban más)
            const float thick = 2;
            var borders = new GameObject("Bordes").transform;
            borders.SetParent(parent, false);
            foreach (int s in new[] { -1, 1 })
                for (int i = 0; i < d.N; i++)
                {
                    if (d.HasTown && i >= d.TownStart - 8) break;
                    if (InGap(i, s)) continue;
                    var p = d.P[i];
                    var go = new GameObject("Borde") { layer = WorldSurface.ObstacleLayer };
                    go.transform.SetParent(borders, false);
                    go.transform.SetPositionAndRotation(Edge(d, i, s) + p.right * s * thick / 2 + Vector3.up * 0.5f,
                        Quaternion.LookRotation(p.forward));
                    go.AddComponent<BoxCollider>().size = new Vector3(thick, 4, d.Step);
                }

            // Canaleta: del borde interior del desagüe hasta donde llega el centro del coche por ese lado
            foreach (var hp in d.Hairpins)
            {
                int s = (int)Mathf.Sign(hp.angle), first = Mathf.Max(0, hp.start - 3), last = Mathf.Min(d.N - 1, hp.end + 3);
                for (int i = first; i <= last; i = i + 2 > last && i < last ? last : i + 2)
                {
                    var p = d.P[i];
                    float inner = d.HW[i] - 1.75f, outer = d.HW[i] - 0.6f + 0.3f;
                    var go = new GameObject("Canaleta") { layer = WorldSurface.GutterLayer };
                    go.transform.SetParent(parent, false);
                    go.transform.SetPositionAndRotation(p.pos + p.right * s * (inner + outer) / 2 + Vector3.up * 0.3f,
                        Quaternion.Euler(0, p.heading * Mathf.Rad2Deg, 0));
                    var box = go.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.size = new Vector3(outer - inner, 1, d.Step * 2 + 0.2f);
                }
            }
        }

        Dictionary<Collider, int> borderIndex;

        /// <summary>
        /// Punto de la pista de bajada al que pertenece una caja de borde (-1 si no es un borde). La superficie del mundo
        /// lo usa para mirar solo la caja del punto del coche, como el límite de la pista.
        /// </summary>
        public int BorderIndex(Collider c)
        {
            if (borderIndex == null)
            {
                borderIndex = new Dictionary<Collider, int>();
                var borders = root != null ? root.Find(CollidersName + "/Bordes") : null;
                if (borders != null)
                {
                    var d = Geometry;
                    // Cada lado tiene una caja por punto hasta el pueblo, en orden; primero el izquierdo
                    int perSide = d.HasTown ? Mathf.Min(d.N, Mathf.Max(0, d.TownStart - 8)) : d.N;
                    for (int k = 0; k < borders.childCount; k++)
                    {
                        var col = borders.GetChild(k).GetComponent<Collider>();
                        if (col != null && perSide > 0) borderIndex[col] = k % perSide;
                    }
                }
            }
            return borderIndex.TryGetValue(c, out int i) ? i : -1;
        }

        /// <summary>Punto del borde de la superficie del mundo en el punto i, lado s (límite de la pista más el radio del coche).</summary>
        static Vector3 Edge(TrackData d, int i, int s)
        {
            var p = d.P[i];
            float lim = (d.GutterSide[i] == s ? d.HW[i] - 0.6f : d.HW[i] - 0.95f) + WorldSurface.Radius;
            return p.pos + p.right * lim * s;
        }

        static void MeshCollider(Transform parent, string name, Mesh mesh, Transform like, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            if (like != null) go.transform.SetPositionAndRotation(like.position, like.rotation);
            if (like != null) go.transform.localScale = like.lossyScale;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
    }
}

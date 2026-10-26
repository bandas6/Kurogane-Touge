using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kurogane.Core
{
    /// <summary>Utilidades para construir mallas por código: cintas a lo largo de la pista y extrusión de perfiles.</summary>
    public static class MeshKit
    {
        /// <summary>
        /// Cinta de dos vértices por punto. <paramref name="left"/> debe quedar a la izquierda de
        /// <paramref name="right"/> respecto al sentido de avance para que la cara mire hacia arriba.
        /// <paramref name="keep"/> decide si el cuadrilátero entre i e i+1 existe.
        /// </summary>
        public static Mesh Ribbon(int count, System.Func<int, (Vector3 left, Vector3 right, float uLeft, float uRight, float v)> row,
            System.Func<int, bool> keep = null, bool doubleSided = false)
        {
            var verts = new List<Vector3>(count * 2);
            var uvs = new List<Vector2>(count * 2);
            var tris = new List<int>(count * 6);
            for (int i = 0; i < count; i++)
            {
                var r = row(i);
                verts.Add(r.left);
                verts.Add(r.right);
                uvs.Add(new Vector2(r.uLeft, r.v));
                uvs.Add(new Vector2(r.uRight, r.v));
                if (i < count - 1 && (keep == null || (keep(i) && keep(i + 1))))
                {
                    int o = i * 2;
                    tris.Add(o); tris.Add(o + 2); tris.Add(o + 1);
                    tris.Add(o + 1); tris.Add(o + 2); tris.Add(o + 3);
                }
            }
            return Finish(verts, uvs, tris, doubleSided);
        }

        /// <summary>Malla de rejilla: <paramref name="cols"/> vértices por fila (perfil) a lo largo de la pista.</summary>
        public static Mesh Grid(int rows, int cols, System.Func<int, int, Vector3> pos, System.Func<int, int, Vector2> uv, bool doubleSided)
        {
            var verts = new List<Vector3>(rows * cols);
            var uvs = new List<Vector2>(rows * cols);
            var tris = new List<int>();
            for (int i = 0; i < rows; i++)
                for (int k = 0; k < cols; k++)
                {
                    verts.Add(pos(i, k));
                    uvs.Add(uv(i, k));
                    if (i < rows - 1 && k < cols - 1)
                    {
                        int o = i * cols + k;
                        tris.Add(o); tris.Add(o + cols); tris.Add(o + 1);
                        tris.Add(o + 1); tris.Add(o + cols); tris.Add(o + cols + 1);
                    }
                }
            return Finish(verts, uvs, tris, doubleSided);
        }

        static Mesh Finish(List<Vector3> verts, List<Vector2> uvs, List<int> tris, bool doubleSided)
        {
            if (doubleSided)
            {
                int n = tris.Count;
                for (int i = 0; i < n; i += 3) { tris.Add(tris[i]); tris.Add(tris[i + 2]); tris.Add(tris[i + 1]); }
            }
            var m = new Mesh { indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // ---------- Extrusión de perfiles laterales (carrocerías) ----------

        /// <summary>
        /// Extruye un contorno 2D (x = eje Z del coche, y = altura) a lo ancho del eje X,
        /// con sombreado plano. Admite contornos cóncavos (pasos de rueda).
        /// </summary>
        public static Mesh Extrude(IList<Vector2> outline, float width)
        {
            var pts = new List<Vector2>(outline);
            if (SignedArea(pts) < 0) pts.Reverse();
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float hw = width / 2;
            var tri = Triangulate(pts);
            for (int i = 0; i < tri.Count; i += 3)
            {
                Vector2 a = pts[tri[i]], b = pts[tri[i + 1]], c = pts[tri[i + 2]];
                AddTri(verts, tris, P(hw, a), P(hw, b), P(hw, c), Vector3.right);
                AddTri(verts, tris, P(-hw, a), P(-hw, b), P(-hw, c), Vector3.left);
            }
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % pts.Count];
                var e = b - a;
                var nrm = new Vector3(0, -e.x, e.y).normalized; // normal exterior para contorno antihorario
                AddQuad(verts, tris, P(hw, a), P(hw, b), P(-hw, b), P(-hw, a), nrm);
            }
            var m = new Mesh();
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Vector3 P(float x, Vector2 zy) => new Vector3(x, zy.y, zy.x);

        static void AddTri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 wanted)
        {
            // En Unity la cara frontal es la de orden horario; Cross(b-a, c-a) apunta hacia ella.
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), wanted) < 0) { var tmp = b; b = c; c = tmp; }
            int o = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            t.Add(o); t.Add(o + 1); t.Add(o + 2);
        }

        static void AddQuad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 wanted)
        {
            AddTri(v, t, a, b, c, wanted);
            AddTri(v, t, a, c, d, wanted);
        }

        static float SignedArea(List<Vector2> p)
        {
            float a = 0;
            for (int i = 0; i < p.Count; i++)
            {
                var q = p[i];
                var r = p[(i + 1) % p.Count];
                a += q.x * r.y - r.x * q.y;
            }
            return a / 2;
        }

        /// <summary>Triangulación por recorte de orejas para un polígono antihorario simple.</summary>
        public static List<int> Triangulate(List<Vector2> p)
        {
            var res = new List<int>();
            var idx = new List<int>();
            for (int i = 0; i < p.Count; i++) idx.Add(i);
            int guard = 0;
            while (idx.Count > 3 && guard++ < 5000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int i0 = idx[(i + idx.Count - 1) % idx.Count], i1 = idx[i], i2 = idx[(i + 1) % idx.Count];
                    Vector2 a = p[i0], b = p[i1], c = p[i2];
                    if (Cross(b - a, c - b) <= 1e-7f) continue; // vértice reflejo
                    bool inside = false;
                    foreach (int j in idx)
                    {
                        if (j == i0 || j == i1 || j == i2) continue;
                        if (InTri(p[j], a, b, c)) { inside = true; break; }
                    }
                    if (inside) continue;
                    res.Add(i0); res.Add(i1); res.Add(i2);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3) { res.Add(idx[0]); res.Add(idx[1]); res.Add(idx[2]); }
            return res;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            return d1 >= 0 && d2 >= 0 && d3 >= 0;
        }

        /// <summary>Añade un arco (pasa por arriba) desde el ángulo <paramref name="a0"/> a <paramref name="a1"/>.</summary>
        public static void Arc(List<Vector2> pts, Vector2 center, float radius, float a0, float a1, int segments)
        {
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)segments);
                pts.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
        }

        /// <summary>
        /// Fusiona todas las mallas que cuelgan de <paramref name="parent"/> en una por material (menos llamadas
        /// de dibujo) y elimina las piezas originales. <paramref name="owned"/> es la lista de mallas creadas por
        /// código: las originales que estén en ella se destruyen y las fusionadas se añaden, para poder liberarlas
        /// después. Las mallas de las primitivas de Unity nunca están en esa lista, así que no se tocan.
        /// </summary>
        public static void MergeByMaterial(Transform parent, List<Mesh> owned)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var toRemove = new List<GameObject>();
            var sources = new HashSet<Mesh>();
            var toLocal = parent.worldToLocalMatrix;
            foreach (var mr in parent.GetComponentsInChildren<MeshRenderer>())
            {
                if (mr.transform == parent) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || mr.sharedMaterial == null) continue;
                if (!groups.TryGetValue(mr.sharedMaterial, out var list)) groups[mr.sharedMaterial] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = toLocal * mr.transform.localToWorldMatrix });
                toRemove.Add(mr.gameObject);
                sources.Add(mf.sharedMesh);
            }
            foreach (var g in groups)
            {
                var m = new Mesh { name = "Fusión " + g.Key.name };
                m.CombineMeshes(g.Value.ToArray(), true, true);
                owned.Add(m);
                var go = new GameObject("Fusión");
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                go.AddComponent<MeshRenderer>().sharedMaterial = g.Key;
            }
            foreach (var go in toRemove)
            {
                go.SetActive(false);
                if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
            }
            foreach (var m in sources)
            {
                if (!owned.Remove(m)) continue;
                if (Application.isPlaying) Object.Destroy(m); else Object.DestroyImmediate(m);
            }
        }

        /// <summary>Combina muchas mallas iguales (árboles, postes) en una o varias mallas grandes.</summary>
        public static List<Mesh> Combine(Mesh mesh, IList<Matrix4x4> matrices)
        {
            var result = new List<Mesh>();
            int perMesh = Mathf.Max(1, 60000 / Mathf.Max(1, mesh.vertexCount));
            for (int start = 0; start < matrices.Count; start += perMesh)
            {
                int n = Mathf.Min(perMesh, matrices.Count - start);
                var ci = new CombineInstance[n];
                for (int i = 0; i < n; i++) ci[i] = new CombineInstance { mesh = mesh, transform = matrices[start + i] };
                var m = new Mesh();
                m.CombineMeshes(ci, true, true);
                result.Add(m);
            }
            return result;
        }
    }
}

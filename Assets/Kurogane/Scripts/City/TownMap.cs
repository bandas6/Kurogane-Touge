using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kurogane.City
{
    /// <summary>
    /// Red de calles del pueblo (bloque H1) y sus lugares con función. La genera CityBuilder y se hornea con la pista;
    /// la leen el tráfico del pueblo (H2) y el modo libre (H3).
    /// Nodos: cruces y finales de calle, en coordenadas del mundo (a la altura de la calzada). Aristas: tramos de calle
    /// entre dos nodos, con su medio ancho y su número de carriles por sentido.
    /// </summary>
    public class TownMap : MonoBehaviour
    {
        [Serializable]
        public struct Edge
        {
            public int a, b;
            public float halfWidth;
            public int lanes;
            [Tooltip("Tramo sobre un puente.")]
            public bool bridge;
            [Tooltip("Calle mayor (la carretera de la pista llega por aquí desde la montaña).")]
            public bool main;
        }

        public enum PlaceKind { TiendaTofu, Gasolinera, Konbini, Mirador, Aparcamiento, Estacion, SalidaMontana }

        [Serializable]
        public struct Place
        {
            public PlaceKind kind;
            public string name;
            [Tooltip("Donde se para el coche, en el mundo.")]
            public Vector3 position;
            [Tooltip("Rumbo al aparcar, en grados.")]
            public float heading;
        }

        public List<Vector3> nodes = new List<Vector3>();
        public List<Edge> edges = new List<Edge>();
        public List<Place> places = new List<Place>();

        public int AddNode(Vector3 p)
        {
            for (int i = 0; i < nodes.Count; i++)
                if ((new Vector2(nodes[i].x - p.x, nodes[i].z - p.z)).sqrMagnitude < 0.25f) return i;
            nodes.Add(p);
            return nodes.Count - 1;
        }

        public bool TryGetPlace(PlaceKind kind, out Place place)
        {
            foreach (var p in places)
                if (p.kind == kind) { place = p; return true; }
            place = default;
            return false;
        }

        /// <summary>Aristas que salen de un nodo.</summary>
        public IEnumerable<int> EdgesAt(int node)
        {
            for (int i = 0; i < edges.Count; i++)
                if (edges[i].a == node || edges[i].b == node) yield return i;
        }

        /// <summary>Longitud total de calles, en metros.</summary>
        public float TotalLength
        {
            get
            {
                float t = 0;
                foreach (var e in edges) t += Vector3.Distance(nodes[e.a], nodes[e.b]);
                return t;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.75f, 0.2f);
            foreach (var e in edges) Gizmos.DrawLine(nodes[e.a] + Vector3.up, nodes[e.b] + Vector3.up);
            Gizmos.color = Color.cyan;
            foreach (var p in places) Gizmos.DrawWireSphere(p.position + Vector3.up, 3);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Kurogane.Track
{
    /// <summary>
    /// Lista de pistas del juego, en orden de menú. Vive en Resources (Assets/Kurogane/Resources/Pistas.asset) para que
    /// la lean la escena Inicio y las de cada pista sin enlazarla a mano. La ventana Kurogane > Pistas añade las nuevas.
    /// </summary>
    [CreateAssetMenu(menuName = "Kurogane/Track Catalog", fileName = "Pistas")]
    public class TrackCatalog : ScriptableObject
    {
        public const string ResourceName = "Pistas";

        public List<TrackDefinition> tracks = new List<TrackDefinition>();

        static TrackCatalog cached;

        public static TrackCatalog Load()
        {
            if (cached == null) cached = Resources.Load<TrackCatalog>(ResourceName);
            if (cached == null) cached = CreateInstance<TrackCatalog>();
            return cached;
        }

        public int IndexOf(string id)
        {
            for (int i = 0; i < tracks.Count; i++)
                if (tracks[i] != null && tracks[i].Id == id) return i;
            return -1;
        }

        public TrackDefinition Find(string id)
        {
            int i = IndexOf(id);
            return i >= 0 ? tracks[i] : null;
        }
    }
}

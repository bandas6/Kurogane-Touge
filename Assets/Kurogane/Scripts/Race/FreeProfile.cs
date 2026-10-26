using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Kurogane.Race
{
    /// <summary>
    /// Progreso del modo libre en una pista (bloque H3): puntos, repartos, desafíos ganados, lugares descubiertos y
    /// mejores tiempos de la contrarreloj libre. JSON en persistentDataPath, como los récords.
    /// </summary>
    [Serializable]
    public class FreeProfile
    {
        public int points;
        public int deliveries, deliveriesFailed;
        public int challengesWon;
        public List<string> discovered = new List<string>();
        public List<string> challengesBeaten = new List<string>();
        public List<Trial> trials = new List<Trial>();

        [Serializable]
        public class Trial
        {
            public string key;
            public float time = float.PositiveInfinity;
        }

        [NonSerialized] string path;

        public static FreeProfile Load(string trackId)
        {
            var file = Path.Combine(Application.persistentDataPath, $"libre_{trackId}.json");
            FreeProfile p = null;
            try { if (File.Exists(file)) p = JsonUtility.FromJson<FreeProfile>(File.ReadAllText(file)); }
            catch (Exception e) { Debug.LogWarning("No se pudo leer el modo libre: " + e.Message); }
            p = p ?? new FreeProfile();
            p.path = file;
            return p;
        }

        public void Save()
        {
            if (string.IsNullOrEmpty(path)) return;
            try { File.WriteAllText(path, JsonUtility.ToJson(this)); }
            catch (Exception e) { Debug.LogWarning("No se pudo guardar el modo libre: " + e.Message); }
        }

        public float BestTrial(string key)
        {
            foreach (var t in trials) if (t.key == key) return t.time;
            return float.PositiveInfinity;
        }

        /// <summary>Guarda el tiempo si es mejor; devuelve si lo era.</summary>
        public bool SetTrial(string key, float time)
        {
            foreach (var t in trials)
                if (t.key == key)
                {
                    if (time >= t.time) return false;
                    t.time = time;
                    return true;
                }
            trials.Add(new Trial { key = key, time = time });
            return true;
        }
    }
}

using System.Collections.Generic;
using Kurogane.Track;
using UnityEngine;

namespace Kurogane.Race
{
    /// <summary>
    /// Elección de pista y sentido (bloque E). Las pistas salen de <see cref="TrackCatalog"/>; cada una tiene su escena
    /// y se carga con <see cref="SceneLoader"/>. El sentido se cambia sin recargar: es la misma geometría.
    /// </summary>
    public partial class RaceManager
    {
        TrackDefinition previewFor;
        TrackDirection previewDirection;
        TrackData previewData;

        /// <summary>Pistas que ofrece el menú: las del catálogo, más la de esta escena si no está en él.</summary>
        public List<TrackDefinition> Tracks
        {
            get
            {
                var list = new List<TrackDefinition>();
                foreach (var t in TrackCatalog.Load().tracks) if (t != null && !list.Contains(t)) list.Add(t);
                if (track != null && track.definition != null && list.TrueForAll(t => t.Id != track.definition.Id)) list.Add(track.definition);
                return list;
            }
        }

        /// <summary>La pista elegida en el menú (en la escena de una pista, al arrancar es la suya).</summary>
        public TrackDefinition SelectedTrack
        {
            get
            {
                var list = Tracks;
                foreach (var t in list) if (t.Id == trackId) return t;
                return list.Count > 0 ? list[0] : null;
            }
        }

        /// <summary>La pista elegida tiene pueblo abierto: se puede jugar en modo libre.</summary>
        public bool FreeAvailable => SelectedTrack != null && SelectedTrack.HasOpenTown;

        /// <summary>Sentido con el que se corre la pista elegida (bajada si no admite subida).</summary>
        public TrackDirection EffectiveDirection =>
            SelectedTrack != null && !SelectedTrack.allowUphill ? TrackDirection.Bajada : direction;

        /// <summary>"bajada" o "subida", para los textos del menú y de los resultados.</summary>
        public string RunName => EffectiveDirection == TrackDirection.Subida ? "subida" : "bajada";

        /// <summary>La pista elegida calculada en su sentido (mapa, perfil y ficha del menú). Se recalcula al cambiarla.</summary>
        public TrackData SelectedData
        {
            get
            {
                var def = SelectedTrack;
                if (def == null) return null;
                if (def != previewFor || EffectiveDirection != previewDirection || previewData == null)
                {
                    previewFor = def;
                    previewDirection = EffectiveDirection;
                    // En la escena de esa pista ya está calculada
                    var g = track != null && track.definition == def && track.Geometry != null ? track.Geometry : new TrackData(def);
                    previewData = previewDirection == TrackDirection.Subida ? g.Reversed() : g;
                }
                return previewData;
            }
        }

        /// <summary>Si la pista elegida es la de esta escena (si no, Empezar carga su escena).</summary>
        bool SelectedIsHere => !frontEnd && track != null && SelectedTrack == track.definition;

        float ParTime
        {
            get
            {
                var def = SelectedTrack;
                if (def == null) return 120;
                return EffectiveDirection == TrackDirection.Subida && def.parTimeUphill > 0 ? def.parTimeUphill : def.parTime;
            }
        }

        void ChangeTrack(int dir)
        {
            var list = Tracks;
            if (list.Count == 0) return;
            int i = Mathf.Max(0, list.IndexOf(SelectedTrack));
            trackId = list[(i + dir + list.Count) % list.Count].Id;
            if (mode == RaceMode.Libre && !FreeAvailable) mode = RaceMode.Batalla;
            // De vuelta a la pista de esta escena: el coche a la salida del sentido elegido
            if (SelectedIsHere && track.Direction != EffectiveDirection)
            {
                ApplyDirection();
                PreviewCar();
            }
            best = LoadBest();
        }

        void ChangeDirection()
        {
            if (SelectedTrack == null || !SelectedTrack.allowUphill) return;
            direction = direction == TrackDirection.Bajada ? TrackDirection.Subida : TrackDirection.Bajada;
            // Misma escena: se cambia el sentido en el sitio y el coche pasa a la salida del otro lado
            if (SelectedIsHere)
            {
                ApplyDirection();
                PreviewCar();
            }
            best = LoadBest();
        }

        /// <summary>Guarda las opciones y carga la escena de la pista elegida, que empieza la carrera al cargar.</summary>
        void LoadSelectedTrack()
        {
            var def = SelectedTrack;
            if (def == null) { Debug.LogWarning("Kurogane: no hay pistas en el catálogo (Resources/Pistas)."); return; }
            RaceSettings.Save(this);
            GameSession.FromStartScene |= frontEnd;
            GameSession.StartRaceOnLoad = true;
            var data = SelectedData;
            string title = string.IsNullOrEmpty(def.japaneseName) ? def.DisplayName.ToUpper() : $"{def.japaneseName}  {def.DisplayName.ToUpper()}";
            SceneLoader.Load(def.SceneName, title, $"{(data.Uphill ? "Subida" : "Bajada")} · {TrackPreview.Summary(data)}");
        }

        /// <summary>Vuelve a la escena Inicio (si la pista se cargó desde allí).</summary>
        void LoadStartScene()
        {
            RaceSettings.Save(this);
            GameSession.StartRaceOnLoad = false;
            SceneLoader.Load(GameSession.StartScene, "黒鉄峠  KUROGANE TOUGE", "Garaje");
        }
    }
}

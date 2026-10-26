using System.Collections.Generic;
using System.IO;
using Kurogane.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kurogane.EditorTools
{
    /// <summary>
    /// Ventana Kurogane > Pistas: vista desde arriba y perfil de altitud de la TrackDefinition elegida, avisos si la
    /// pista se cruza consigo misma o si dos tramos pasan demasiado cerca, y botones para crear su escena y hornearla.
    /// Crear una pista: Assets > Create > Kurogane > Track Definition (o "Nueva pista" aquí), revisarla y pulsar
    /// "Crear escena y hornear".
    /// </summary>
    public class KuroganeTrackTools : EditorWindow
    {
        TrackDefinition def;
        TrackDirection direction;
        Texture2D map, profile;
        TrackData data;
        List<TrackPreview.Issue> issues = new List<TrackPreview.Issue>();
        string shownFor;
        Vector2 scroll;

        [MenuItem("Kurogane/Pistas", priority = 3)]
        public static void Open() => GetWindow<KuroganeTrackTools>("Pistas");

        void OnEnable()
        {
            if (def == null) def = Selection.activeObject as TrackDefinition;
            if (def == null)
            {
                var catalog = KuroganeSceneSetup.Catalog();
                if (catalog.tracks.Count > 0) def = catalog.tracks[0];
            }
        }

        void OnSelectionChange()
        {
            if (Selection.activeObject is TrackDefinition t && t != def) { def = t; Repaint(); }
        }

        void OnDisable() => Release();

        void Release()
        {
            if (map != null) DestroyImmediate(map);
            if (profile != null) DestroyImmediate(profile);
            map = profile = null;
        }

        /// <summary>Recalcula la pista si el asset cambió (se compara su contenido en JSON).</summary>
        void Refresh()
        {
            if (def == null) { data = null; Release(); return; }
            string key = EditorJsonUtility.ToJson(def) + direction;
            if (key == shownFor && map != null) return;
            shownFor = key;
            Release();
            var g = new TrackData(def);
            data = direction == TrackDirection.Subida && def.allowUphill ? g.Reversed() : g;
            issues = TrackPreview.Check(g);
            var marks = new List<int>();
            foreach (var it in issues) { marks.Add(it.a); marks.Add(it.b); }
            map = TrackPreview.Map(data, 512, marks);
            profile = TrackPreview.Profile(data, 512, 96);
        }

        void OnGUI()
        {
            EditorGUILayout.Space(4);
            def = (TrackDefinition)EditorGUILayout.ObjectField("Pista", def, typeof(TrackDefinition), false);
            if (def == null)
            {
                EditorGUILayout.HelpBox("Elige una Track Definition o crea una nueva.", MessageType.Info);
                if (GUILayout.Button("Nueva pista")) NewTrack();
                return;
            }
            using (new EditorGUI.DisabledScope(!def.allowUphill))
                direction = (TrackDirection)EditorGUILayout.EnumPopup("Sentido", def.allowUphill ? direction : TrackDirection.Bajada);
            Refresh();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField($"{def.japaneseName}  {def.DisplayName}".Trim(), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(TrackPreview.Summary(data));
            EditorGUILayout.LabelField($"Escena: {KuroganeSceneSetup.ScenePath(def)}" + (File.Exists(KuroganeSceneSetup.ScenePath(def)) ? "" : "  (sin crear)"),
                EditorStyles.miniLabel);

            // Mapa (cuadrado) y perfil de altitud a todo el ancho
            float w = Mathf.Max(200, position.width - 24);
            var r = GUILayoutUtility.GetRect(w, Mathf.Min(w, 512));
            float side = Mathf.Min(r.width, r.height);
            var mapRect = new Rect(r.x + (r.width - side) / 2, r.y, side, side);
            EditorGUI.DrawRect(mapRect, new Color(0.05f, 0.06f, 0.1f));
            GUI.DrawTexture(mapRect, map, ScaleMode.ScaleToFit, true);
            EditorGUILayout.LabelField("Rojo: salida · Azul: meta · Ámbar: pueblo · Círculos: avisos", EditorStyles.centeredGreyMiniLabel);
            var pr = GUILayoutUtility.GetRect(w, 96);
            EditorGUI.DrawRect(pr, new Color(0.05f, 0.06f, 0.1f));
            GUI.DrawTexture(pr, profile, ScaleMode.StretchToFill, true);
            EditorGUILayout.LabelField($"Perfil de altitud ({(data.Uphill ? "subida" : "bajada")}, de la salida al final de la carretera)",
                EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.Space(6);
            if (issues.Count == 0)
                EditorGUILayout.HelpBox("Sin avisos: la pista no se cruza consigo misma y los tramos guardan la distancia mínima.", MessageType.Info);
            foreach (var it in issues)
                EditorGUILayout.HelpBox(it.text, it.crossing ? MessageType.Error : MessageType.Warning);

            EditorGUILayout.Space(6);
            bool blocking = issues.Exists(i => i.crossing);
            using (new EditorGUI.DisabledScope(blocking))
                if (GUILayout.Button(blocking ? "Corrige los cruces antes de crear la escena" : "Crear escena y hornear", GUILayout.Height(30)))
                    CreateAndBake();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!File.Exists(KuroganeSceneSetup.ScenePath(def))))
                {
                    if (GUILayout.Button("Abrir escena")) OpenScene();
                    if (GUILayout.Button("Hornear pista")) { if (OpenScene()) KuroganeBake.BakeTrack(); }
                    if (GUILayout.Button("Hornear oclusión")) { if (OpenScene()) KuroganeBake.BakeOcclusion(); }
                    if (GUILayout.Button("Hornear luces")) { if (OpenScene()) KuroganeBake.BakeLights(); }
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Nueva pista")) NewTrack();
                if (GUILayout.Button("Seleccionar asset")) Selection.activeObject = def;
            }
            EditorGUILayout.EndScrollView();
        }

        bool OpenScene()
        {
            string path = KuroganeSceneSetup.ScenePath(def);
            if (EditorSceneManager.GetActiveScene().path == path) return true;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(path);
            return true;
        }

        void CreateAndBake()
        {
            string path = KuroganeSceneSetup.ScenePath(def);
            if (File.Exists(path) && !EditorUtility.DisplayDialog("Kurogane: pistas",
                    $"Ya existe {path}. ¿Rehacerla? Se pierde lo que se haya cambiado a mano en esa escena.", "Rehacer", "Cancelar"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            KuroganeSceneSetup.CreateTrackScene(def);
            KuroganeBake.BakeTrack();
            KuroganeBake.BakeOcclusion();
            KuroganeBake.BakeLights();
        }

        /// <summary>Asset nuevo en Data con un trazado corto de ejemplo, listo para editar.</summary>
        void NewTrack()
        {
            string path = EditorUtility.SaveFilePanelInProject("Nueva pista", "PistaNueva", "asset", "Nombre del asset de la pista", "Assets/Kurogane/Data");
            if (string.IsNullOrEmpty(path)) return;
            var t = CreateInstance<TrackDefinition>();
            t.id = Path.GetFileNameWithoutExtension(path).Replace(" ", "");
            t.displayName = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(t, path);
            AssetDatabase.SaveAssets();
            def = t;
            Selection.activeObject = t;
        }
    }
}

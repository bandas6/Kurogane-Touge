using System.IO;
using System.Linq;
using Kurogane.CameraRig;
using Kurogane.Cars;
using Kurogane.Race;
using Kurogane.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kurogane.EditorTools
{
    /// <summary>Menú "Kurogane" del editor: crea los datos por defecto y la escena jugable de la Fase 1.</summary>
    public static class KuroganeSceneSetup
    {
        const string DataDir = "Assets/Kurogane/Data";
        const string SceneDir = "Assets/Kurogane/Scenes";
        const string ScenePath = SceneDir + "/Prototipo.unity";

        [MenuItem("Kurogane/Crear escena de prueba", priority = 0)]
        public static void CreatePrototypeScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(DataDir);
            Directory.CreateDirectory(SceneDir);

            var trackDef = LoadOrCreate(DataDir + "/MonteKurogane.asset", () => ScriptableObject.CreateInstance<TrackDefinition>());
            var panda = LoadOrCreate(DataDir + "/Car_Panda.asset", CarSpec.CreatePanda);
            var coupe = LoadOrCreate(DataDir + "/Car_Coupe.asset", CarSpec.CreateCoupe);
            var sedan = LoadOrCreate(DataDir + "/Car_Sedan.asset", CarSpec.CreateSedan);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Ambiente de medianoche
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.07f, 0.1f, 0.23f);
            RenderSettings.fogStartDistance = 30;
            RenderSettings.fogEndDistance = 240;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.32f, 0.38f, 0.6f);
            RenderSettings.ambientEquatorColor = new Color(0.14f, 0.16f, 0.26f);
            RenderSettings.ambientGroundColor = new Color(0.06f, 0.06f, 0.09f);
            RenderSettings.skybox = null;

            var moon = new GameObject("Luna").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.66f, 0.72f, 0.94f);
            moon.intensity = 0.35f;
            moon.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(50, -30, 0);

            var trackGo = new GameObject("Pista");
            var builder = trackGo.AddComponent<TrackBuilder>();
            builder.definition = trackDef;

            var playerGo = new GameObject("Jugador");
            var player = playerGo.AddComponent<CarController>();
            player.spec = panda;
            playerGo.AddComponent<CarVisual>();

            var rivalGo = new GameObject("Rival");
            var rival = rivalGo.AddComponent<RivalAI>();
            rival.spec = coupe;
            rivalGo.AddComponent<CarVisual>();

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 2000;
            cam.fieldOfView = 60;
            camGo.AddComponent<AudioListener>();
            var chase = camGo.AddComponent<ChaseCamera>();
            chase.target = player;

            var raceGo = new GameObject("Carrera");
            var race = raceGo.AddComponent<RaceManager>();
            race.track = builder;
            race.player = player;
            race.rival = rival;
            race.chaseCamera = chase;
            race.cars = new[] { panda, coupe, sedan };

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            Selection.activeGameObject = raceGo;
            Debug.Log("Kurogane: escena creada en " + ScenePath + ". Pulsa Play para bajar el Monte Kurogane.");
        }

        [MenuItem("Kurogane/Abrir escena de prueba", priority = 1)]
        public static void OpenPrototypeScene()
        {
            if (!File.Exists(ScenePath)) { CreatePrototypeScene(); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        static T LoadOrCreate<T>(string path, System.Func<T> create) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = create();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }
    }
}

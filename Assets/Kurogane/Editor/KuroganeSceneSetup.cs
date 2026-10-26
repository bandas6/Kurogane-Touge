using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kurogane.Atmosphere;
using Kurogane.CameraRig;
using Kurogane.Cars;
using Kurogane.Race;
using Kurogane.Retro;
using Kurogane.Sound;
using Kurogane.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kurogane.EditorTools
{
    /// <summary>
    /// Menú "Kurogane" del editor: crea los datos por defecto, la escena Inicio (menú y garaje) y la escena de cada
    /// pista (Pista_&lt;id&gt;), y mantiene la lista de escenas de compilación: Inicio primero y luego las pistas
    /// del catálogo (Resources/Pistas).
    /// </summary>
    public static class KuroganeSceneSetup
    {
        const string DataDir = "Assets/Kurogane/Data";
        public const string SceneDir = "Assets/Kurogane/Scenes";
        const string ResourcesDir = "Assets/Kurogane/Resources";
        const string CatalogPath = ResourcesDir + "/" + TrackCatalog.ResourceName + ".asset";
        const string ShaderPath = "Assets/Kurogane/Shaders/PS2Filter.shader";
        public const string SkyShaderPath = "Assets/Kurogane/Shaders/Sky.shader";
        const string MusicDir = "Assets/Kurogane/Audio/Musica";
        public static string StartScenePath => $"{SceneDir}/{GameSession.StartScene}.unity";

        public static string ScenePath(TrackDefinition def) => $"{SceneDir}/{def.SceneName}.unity";

        [MenuItem("Kurogane/Crear escena Inicio", priority = 0)]
        public static void CreateStartSceneMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateStartScene();
        }

        [MenuItem("Kurogane/Abrir escena Inicio", priority = 1)]
        public static void OpenStartScene()
        {
            if (!File.Exists(StartScenePath)) { CreateStartSceneMenu(); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(StartScenePath);
        }

        [MenuItem("Kurogane/Abrir escena del Monte Kurogane", priority = 2)]
        public static void OpenMonteKurogane()
        {
            var def = DefaultTrack();
            if (!File.Exists(ScenePath(def)))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) CreateTrackScene(def);
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath(def));
        }

        // ---------- Datos ----------

        static CarSpec[] Cars() => new[]
        {
            LoadOrCreate(DataDir + "/Car_Panda.asset", CarSpec.CreatePanda),
            LoadOrCreate(DataDir + "/Car_Coupe.asset", CarSpec.CreateCoupe),
            LoadOrCreate(DataDir + "/Car_Sedan.asset", CarSpec.CreateSedan),
        };

        static TrackDefinition DefaultTrack()
        {
            Directory.CreateDirectory(DataDir);
            return LoadOrCreate(DataDir + "/MonteKurogane.asset", () =>
            {
                var t = ScriptableObject.CreateInstance<TrackDefinition>();
                t.id = "MonteKurogane";
                t.displayName = "Monte Kurogane";
                t.japaneseName = "黒鉄峠";
                return t;
            });
        }

        /// <summary>Catálogo de pistas en Resources; si no existe se crea con el Monte Kurogane.</summary>
        public static TrackCatalog Catalog()
        {
            var c = AssetDatabase.LoadAssetAtPath<TrackCatalog>(CatalogPath);
            if (c != null) return c;
            Directory.CreateDirectory(ResourcesDir);
            c = ScriptableObject.CreateInstance<TrackCatalog>();
            c.tracks.Add(DefaultTrack());
            AssetDatabase.CreateAsset(c, CatalogPath);
            AssetDatabase.SaveAssets();
            return c;
        }

        public static void AddToCatalog(TrackDefinition def)
        {
            var c = Catalog();
            if (c.tracks.Contains(def)) return;
            c.tracks.Add(def);
            EditorUtility.SetDirty(c);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Deja la lista de escenas de compilación en Inicio y las pistas del catálogo que ya tienen escena, por ese
        /// orden. Devuelve las rutas.
        /// </summary>
        public static string[] SyncBuildScenes()
        {
            var paths = new List<string>();
            if (File.Exists(StartScenePath)) paths.Add(StartScenePath);
            foreach (var t in Catalog().tracks)
                if (t != null && File.Exists(ScenePath(t)) && !paths.Contains(ScenePath(t))) paths.Add(ScenePath(t));
            EditorBuildSettings.scenes = paths.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            return paths.ToArray();
        }

        // ---------- Escena de una pista ----------

        /// <summary>
        /// Crea (o rehace) la escena Pista_&lt;id&gt; de <paramref name="def"/>: ambiente de medianoche, pista, cielo,
        /// coches, cámara con filtro PS2, música, carrera y clima. La deja abierta y en la lista de compilación.
        /// </summary>
        public static string CreateTrackScene(TrackDefinition def)
        {
            Directory.CreateDirectory(DataDir);
            Directory.CreateDirectory(SceneDir);
            var cars = Cars();
            var cityStyle = LoadOrCreate(DataDir + "/PuebloMeta.asset", () => ScriptableObject.CreateInstance<Kurogane.City.CityStyle>());
            AssetDatabase.SaveAssets();

            // Abrir una escena nueva descarga los assets que nadie usa: la pista se vuelve a cargar desde su ruta
            string defPath = AssetDatabase.GetAssetPath(def);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!string.IsNullOrEmpty(defPath)) def = AssetDatabase.LoadAssetAtPath<TrackDefinition>(defPath);

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
            RenderSettings.sun = moon;

            var trackGo = new GameObject("Pista");
            var builder = trackGo.AddComponent<TrackBuilder>();
            builder.definition = def;
            builder.cityStyle = cityStyle;

            // Cielo y horizonte: sigue a la cámara y se genera al arrancar (o se hornea con la pista)
            var sky = new GameObject("Cielo").AddComponent<SkyDome>();
            sky.shader = AssetDatabase.LoadAssetAtPath<Shader>(SkyShaderPath);

            var (player, rival) = Cars(cars);
            var (chase, retro) = MainCamera(player);
            var music = Music();

            var raceGo = new GameObject("Carrera");
            var race = raceGo.AddComponent<RaceManager>();
            race.track = builder;
            race.player = player;
            race.rival = rival;
            race.chaseCamera = chase;
            race.cars = cars;
            race.retro = retro;
            race.music = music;

            // Perfiles de clima editables en Data/Clima (se crean con los valores de serie)
            Directory.CreateDirectory(DataDir + "/Clima");
            var weather = raceGo.AddComponent<WeatherSystem>();
            weather.profiles = System.Enum.GetValues(typeof(WeatherKind)).Cast<WeatherKind>()
                .Select(k => LoadOrCreate($"{DataDir}/Clima/{k}.asset", () => WeatherProfile.Preset(k))).ToArray();
            AssetDatabase.SaveAssets();

            string path = ScenePath(def);
            EditorSceneManager.SaveScene(scene, path);
            AddToCatalog(def);
            SyncBuildScenes();
            Selection.activeGameObject = raceGo;
            Debug.Log($"Kurogane: escena creada en {path}. Para la build, hornea la pista y las luces (Kurogane > Hornear pista).");
            return path;
        }

        // ---------- Escena Inicio ----------

        /// <summary>Escena Inicio: garaje, coche elegido, menú y cámara con filtro PS2. Carga las pistas al empezar.</summary>
        public static void CreateStartScene()
        {
            Directory.CreateDirectory(SceneDir);
            var cars = Cars();
            Catalog();
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.3f, 0.32f, 0.38f);
            RenderSettings.ambientEquatorColor = new Color(0.16f, 0.16f, 0.18f);
            RenderSettings.ambientGroundColor = new Color(0.07f, 0.07f, 0.08f);
            RenderSettings.skybox = null;

            var garage = new GameObject("Garaje").AddComponent<Garage>();
            var (player, rival) = Cars(cars);
            var (chase, retro) = MainCamera(player);
            chase.GetComponent<Camera>().backgroundColor = new Color(0.02f, 0.03f, 0.07f);
            chase.GetComponent<Camera>().farClipPlane = 200;
            chase.showroom = true;
            var music = Music();

            var raceGo = new GameObject("Menú");
            var race = raceGo.AddComponent<RaceManager>();
            race.frontEnd = true;
            race.garage = garage;
            race.player = player;
            race.rival = rival;
            race.chaseCamera = chase;
            race.cars = cars;
            race.retro = retro;
            race.music = music;

            EditorSceneManager.SaveScene(scene, StartScenePath);
            SyncBuildScenes();
            Selection.activeGameObject = raceGo;
            Debug.Log($"Kurogane: escena creada en {StartScenePath}. Pulsa Play para elegir pista y coche en el garaje.");
        }

        // ---------- Piezas comunes ----------

        static (CarController, RivalAI) Cars(CarSpec[] cars)
        {
            var playerGo = new GameObject("Jugador");
            var player = playerGo.AddComponent<CarController>();
            player.spec = cars[0];
            playerGo.AddComponent<CarVisual>();
            playerGo.AddComponent<CarAudio>();

            var rivalGo = new GameObject("Rival");
            var rival = rivalGo.AddComponent<RivalAI>();
            rival.spec = cars[1];
            rivalGo.AddComponent<CarVisual>();
            rivalGo.AddComponent<CarAudio>();
            return (player, rival);
        }

        static (ChaseCamera, PS2Filter) MainCamera(CarController player)
        {
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
            var retro = camGo.AddComponent<PS2Filter>();
            retro.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            return (chase, retro);
        }

        static MusicPlayer Music()
        {
            var music = new GameObject("Música").AddComponent<MusicPlayer>();
            music.tracks = LoadMusic();
            return music;
        }

        /// <summary>Pistas de música que haya en Assets/Kurogane/Audio/Musica, por orden de nombre.</summary>
        static AudioClip[] LoadMusic()
        {
            if (!AssetDatabase.IsValidFolder(MusicDir)) return new AudioClip[0];
            return AssetDatabase.FindAssets("t:AudioClip", new[] { MusicDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
                .Where(c => c != null)
                .ToArray();
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

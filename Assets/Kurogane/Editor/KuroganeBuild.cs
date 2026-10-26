using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Kurogane.EditorTools
{
    /// <summary>
    /// Menú "Kurogane > Compilar": Windows x64, Android AAB firmado para Google Play y APK de desarrollo.
    /// También se puede llamar sin abrir el editor:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Kurogane.EditorTools.KuroganeBuild.BuildAndroidRelease
    /// La firma de Android se lee de variables de entorno (ver README); las contraseñas nunca se guardan en el proyecto.
    /// </summary>
    public static class KuroganeBuild
    {
        // Antes de la primera subida a Google Play revisa el identificador: no se puede cambiar después
        const string PackageName = "com.bandas6.kuroganetouge";
        const string CompanyName = "bandas6";
        const string ProductName = "Kurogane Touge";
        const string Version = "0.7.0";

        const string ScenePath = "Assets/Kurogane/Scenes/Prototipo.unity";
        const string OutputDir = "Builds";

        // Shaders que se buscan con Shader.Find en ejecución: si nada los referencia, no entran en la build
        static readonly string[] RuntimeShaders =
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Unlit",
            "Hidden/Kurogane/PS2Filter",
            "Kurogane/Sky",
        };

        [MenuItem("Kurogane/Compilar/Windows x64", priority = 20)]
        public static void BuildWindows()
        {
            Prepare();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            string path = Path.Combine(OutputDir, "Windows", ProductName + ".exe");
            Run(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, path, BuildOptions.None);
        }

        [MenuItem("Kurogane/Compilar/Android AAB (Google Play)", priority = 21)]
        public static void BuildAndroidRelease()
        {
            var key = Keystore.FromEnvironment();
            if (key == null)
            {
                Fail("Falta la firma de Android. Define KUROGANE_KEYSTORE, KUROGANE_KEYSTORE_PASS, KUROGANE_KEY_ALIAS y " +
                     "KUROGANE_KEY_PASS (ver README) y vuelve a abrir Unity desde esa consola.");
                return;
            }
            Prepare();
            PrepareAndroid();
            EditorUserBuildSettings.buildAppBundle = true;
            PlayerSettings.Android.bundleVersionCode++;
            string path = Path.Combine(OutputDir, "Android", $"KuroganeTouge-{Version}-{PlayerSettings.Android.bundleVersionCode}.aab");
            try
            {
                key.Apply();
                Run(BuildTarget.Android, BuildTargetGroup.Android, path, BuildOptions.None);
            }
            finally
            {
                // No dejar la ruta local del keystore en ProjectSettings (se subiría al repositorio)
                Keystore.Clear();
            }
        }

        [MenuItem("Kurogane/Compilar/Android APK de desarrollo (Profiler)", priority = 22)]
        public static void BuildAndroidDevelopment()
        {
            Prepare();
            PrepareAndroid();
            EditorUserBuildSettings.buildAppBundle = false;
            Keystore.Clear(); // firma de depuración de Unity: sirve para instalar en tu móvil, no para Google Play
            string path = Path.Combine(OutputDir, "Android", "KuroganeTouge-desarrollo.apk");
            // Tiempos de CPU y GPU por cuadro para el benchmark (Benchmark.cs); solo en esta build
            bool timing = PlayerSettings.enableFrameTimingStats;
            PlayerSettings.enableFrameTimingStats = true;
            try
            {
                Run(BuildTarget.Android, BuildTargetGroup.Android, path,
                    BuildOptions.Development | BuildOptions.ConnectWithProfiler | BuildOptions.AutoRunPlayer);
            }
            finally
            {
                PlayerSettings.enableFrameTimingStats = timing;
            }
        }

        // ---------- Configuración común ----------

        static void Prepare()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, PackageName);

            // Juego horizontal
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            IncludeRuntimeShaders();
        }

        static void PrepareAndroid()
        {
            // Google Play exige 64 bits, y ARM64 exige IL2CPP
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            // Frame pacing de Android (Swappy): 60 fps estables. Se fija aquí porque el editor abierto pisa
            // los cambios hechos a mano en ProjectSettings.asset
            PlayerSettings.Android.optimizedFramePacing = true;
        }

        /// <summary>Añade a Graphics > Always Included Shaders los que se buscan por nombre en ejecución.</summary>
        static void IncludeRuntimeShaders()
        {
            var settings = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset");
            var so = new SerializedObject(settings);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in RuntimeShaders)
            {
                var shader = Shader.Find(name);
                if (shader == null) { Debug.LogWarning("Kurogane: no se encontró el shader " + name); continue; }
                bool present = false;
                for (int i = 0; i < list.arraySize && !present; i++)
                    present = list.GetArrayElementAtIndex(i).objectReferenceValue == shader;
                if (present) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        static void Run(BuildTarget target, BuildTargetGroup group, string path, BuildOptions options)
        {
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
            {
                Fail($"Este Unity no tiene instalado el módulo para {target}. Añádelo en Unity Hub > Installs > Add modules.");
                return;
            }
            if (!File.Exists(ScenePath))
            {
                Fail("No existe " + ScenePath + ". Ejecuta antes Kurogane > Crear escena de prueba.");
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.SaveAssets();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = target,
                targetGroup = group,
                options = options,
            });
            var s = report.summary;
            if (s.result == BuildResult.Succeeded)
            {
                Debug.Log($"Kurogane: compilación lista en {path} ({s.totalSize / (1024f * 1024f):0.0} MB, {s.totalTime:mm\\:ss}).");
                if (!Application.isBatchMode) EditorUtility.RevealInFinder(path);
            }
            else Fail($"La compilación para {target} terminó en {s.result} con {s.totalErrors} errores. Revisa la consola.");
        }

        static void Fail(string message)
        {
            Debug.LogError("Kurogane: " + message);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else EditorUtility.DisplayDialog("Kurogane: compilar", message, "Aceptar");
        }

        /// <summary>Datos de firma de Android leídos de variables de entorno.</summary>
        class Keystore
        {
            string path, pass, alias, aliasPass;

            public static Keystore FromEnvironment()
            {
                var k = new Keystore
                {
                    path = Environment.GetEnvironmentVariable("KUROGANE_KEYSTORE"),
                    pass = Environment.GetEnvironmentVariable("KUROGANE_KEYSTORE_PASS"),
                    alias = Environment.GetEnvironmentVariable("KUROGANE_KEY_ALIAS"),
                    aliasPass = Environment.GetEnvironmentVariable("KUROGANE_KEY_PASS"),
                };
                if (string.IsNullOrEmpty(k.path) || string.IsNullOrEmpty(k.pass) || string.IsNullOrEmpty(k.alias)) return null;
                if (!File.Exists(k.path)) { Debug.LogError("Kurogane: no existe el keystore " + k.path); return null; }
                if (string.IsNullOrEmpty(k.aliasPass)) k.aliasPass = k.pass;
                return k;
            }

            public void Apply()
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = path;
                PlayerSettings.Android.keystorePass = pass;
                PlayerSettings.Android.keyaliasName = alias;
                PlayerSettings.Android.keyaliasPass = aliasPass;
            }

            public static void Clear()
            {
                PlayerSettings.Android.useCustomKeystore = false;
                PlayerSettings.Android.keystoreName = "";
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasName = "";
                PlayerSettings.Android.keyaliasPass = "";
            }
        }
    }
}

using System.Collections.Generic;
using System.IO;
using Kurogane.Atmosphere;
using Kurogane.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kurogane.EditorTools
{
    /// <summary>
    /// Horneado de la pista en el editor:
    /// - "Hornear pista" genera la geometría y la guarda en la escena, con mallas, texturas y materiales como
    ///   assets en Assets/Kurogane/Baked. Al arrancar ya no se genera nada y los materiales entran en la build.
    /// - "Hornear luces" pasa faroles y luces del túnel a lightmaps y sondas de luz: en ejecución dejan de costar
    ///   como luces en tiempo real. Los faros de los coches y la luna siguen en tiempo real.
    /// </summary>
    public static class KuroganeBake
    {
        const string BakedDir = "Assets/Kurogane/Baked";

        // Piezas grandes que reciben lightmap; el resto (árboles, postes, público, señales) usa sondas de luz
        static readonly HashSet<string> Lightmapped = new HashSet<string>
        {
            "Road", "Gutter", "Shoulder", "Wall", "Slope", "GuardRail", "TunnelShell", "Block", "LampPoles", "LampArms",
            Kurogane.City.CityBuilder.GroundName, Kurogane.City.CityBuilder.WaterName,
        };

        [MenuItem("Kurogane/Hornear pista", priority = 10)]
        public static void BakeTrack()
        {
            var builder = FindBuilder();
            if (builder == null) return;
            var scene = builder.gameObject.scene;

            builder.Rebuild();
            Directory.CreateDirectory(BakedDir);
            string path = $"{BakedDir}/{scene.name}_Pista.asset";
            AssetDatabase.DeleteAsset(path);
            var container = ScriptableObject.CreateInstance<BakedTrackAssets>();
            AssetDatabase.CreateAsset(container, path);

            var count = new int[3];
            Save(builder.Root, container, true, count);

            // El cielo también se hornea (en el mismo asset), pero no participa en la iluminación global
            var sky = Object.FindAnyObjectByType<SkyDome>();
            if (sky == null)
            {
                sky = new GameObject("Cielo").AddComponent<SkyDome>();
                sky.shader = AssetDatabase.LoadAssetAtPath<Shader>(KuroganeSceneSetup.SkyShaderPath);
            }
            sky.Build();
            Save(sky.Root, container, false, count);
            sky.baked = true;
            EditorUtility.SetDirty(sky);

            builder.baked = true;
            EditorUtility.SetDirty(builder);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Kurogane: pista y cielo horneados en {path} ({count[0]} mallas, {count[1]} materiales, {count[2]} texturas). " +
                      "Para las luces, usa Kurogane > Hornear luces.");
        }

        /// <summary>
        /// Guarda como sub-assets de <paramref name="container"/> las mallas, materiales y texturas creados por código
        /// bajo <paramref name="root"/>. Con <paramref name="staticGI"/> marca las piezas para la iluminación global.
        /// </summary>
        static void Save(Transform root, Object container, bool staticGI, int[] count)
        {
            var saved = new HashSet<Object>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                // Las primitivas de Unity ya son assets: no se copian
                if (mesh == null || EditorUtility.IsPersistent(mesh) || !saved.Add(mesh)) continue;
                if (staticGI && Lightmapped.Contains(mf.gameObject.name)) Unwrapping.GenerateSecondaryUVSet(mesh);
                mesh.name = mf.gameObject.name;
                AssetDatabase.AddObjectToAsset(mesh, container);
                count[0]++;
            }
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach (var mat in mr.sharedMaterials)
                {
                    if (mat == null || EditorUtility.IsPersistent(mat) || !saved.Add(mat)) continue;
                    foreach (var prop in new[] { "_BaseMap", "_MainTex", "_EmissionMap" })
                    {
                        if (!mat.HasProperty(prop)) continue;
                        var tex = mat.GetTexture(prop);
                        if (tex == null || EditorUtility.IsPersistent(tex) || !saved.Add(tex)) continue;
                        AssetDatabase.AddObjectToAsset(tex, container);
                        count[2]++;
                    }
                    if (string.IsNullOrEmpty(mat.name)) mat.name = mr.gameObject.name;
                    AssetDatabase.AddObjectToAsset(mat, container);
                    count[1]++;
                }
                if (!staticGI) continue;
                // Estático para la iluminación global; sin batching estático (las mallas ya van combinadas)
                GameObjectUtility.SetStaticEditorFlags(mr.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccludeeStatic);
                mr.receiveGI = Lightmapped.Contains(mr.gameObject.name) ? ReceiveGI.Lightmaps : ReceiveGI.LightProbes;
            }
        }

        [MenuItem("Kurogane/Hornear luces (lightmaps)", priority = 11)]
        public static void BakeLights()
        {
            var builder = FindBuilder();
            if (builder == null) return;
            if (!builder.baked || builder.transform.Find("Generated") == null)
            {
                Dialog("Primero ejecuta Kurogane > Hornear pista.");
                return;
            }
            var scene = builder.gameObject.scene;
            var root = builder.transform.Find("Generated");

            foreach (var l in root.GetComponentsInChildren<Light>(true)) l.lightmapBakeType = LightmapBakeType.Baked;
            AddLightProbes(builder, root);
            AddReflectionProbes(builder, root);

            // Lightmaps pequeños y sin direccionalidad: con el filtro PS2 no hace falta más
            var settings = new LightingSettings
            {
                name = scene.name + "_Iluminacion",
                bakedGI = true,
                realtimeGI = false,
                lightmapper = LightingSettings.Lightmapper.ProgressiveGPU,
                mixedBakeMode = MixedLightingMode.IndirectOnly,
                lightmapResolution = 2,
                lightmapMaxSize = 1024,
                directionalityMode = LightmapsMode.NonDirectional,
                lightmapCompression = LightmapCompression.NormalQuality,
                directSampleCount = 16,
                indirectSampleCount = 64,
                environmentSampleCount = 64,
                maxBounces = 1,
            };
            string path = $"{BakedDir}/{scene.name}_Iluminacion.lighting";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(settings, path);
            Lightmapping.lightingSettings = settings;

            Lightmapping.bakeCompleted -= OnLightsBaked;
            Lightmapping.bakeCompleted += OnLightsBaked;
            if (!Lightmapping.BakeAsync()) Dialog("No se pudo empezar el horneado de luces. Revisa la consola.");
            else Debug.Log("Kurogane: horneando luces... el progreso aparece abajo a la derecha del editor.");
        }

        [MenuItem("Kurogane/Quitar horneado", priority = 12)]
        public static void ClearBake()
        {
            var builder = FindBuilder();
            if (builder == null) return;
            var scene = builder.gameObject.scene;
            var root = builder.transform.Find("Generated");
            if (root != null) Object.DestroyImmediate(root.gameObject);
            builder.baked = false;
            EditorUtility.SetDirty(builder);
            var sky = Object.FindAnyObjectByType<SkyDome>();
            if (sky != null)
            {
                var skyRoot = sky.transform.Find("Generated");
                if (skyRoot != null) Object.DestroyImmediate(skyRoot.gameObject);
                sky.baked = false;
                EditorUtility.SetDirty(sky);
            }
            Lightmapping.Clear();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Kurogane: horneado quitado; la pista se vuelve a generar al pulsar Play.");
        }

        static void OnLightsBaked()
        {
            Lightmapping.bakeCompleted -= OnLightsBaked;
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Kurogane: luces horneadas y escena guardada.");
        }

        /// <summary>Sondas de luz a lo largo de la carretera para que los coches reciban la luz de los faroles.</summary>
        static void AddLightProbes(TrackBuilder builder, Transform root)
        {
            var existing = root.Find("Sondas de luz");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var go = new GameObject("Sondas de luz");
            go.transform.SetParent(root, false);
            var group = go.AddComponent<LightProbeGroup>();

            var d = new TrackData(builder.definition != null ? builder.definition : ScriptableObject.CreateInstance<TrackDefinition>());
            var points = new List<Vector3>();
            for (int i = 0; i < d.N; i += 6)
            {
                var p = d.P[i];
                foreach (float lat in new[] { -d.W, 0, d.W })
                    foreach (float h in new[] { 0.6f, 2.6f })
                        points.Add(go.transform.InverseTransformPoint(p.pos + p.right * lat + Vector3.up * h));
            }
            group.probePositions = points.ToArray();
        }

        /// <summary>
        /// Sondas de reflexión horneadas a lo largo de la carretera y en el pueblo: con el asfalto mojado (lluvia) los
        /// faroles se reflejan sin reflejos en espacio de pantalla. Resolución baja, como pide el filtro PS2.
        /// </summary>
        static void AddReflectionProbes(TrackBuilder builder, Transform root)
        {
            var existing = root.Find("Reflejos");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var parent = new GameObject("Reflejos").transform;
            parent.SetParent(root, false);
            var d = new TrackData(builder.definition != null ? builder.definition : ScriptableObject.CreateInstance<TrackDefinition>());
            for (int i = 10; i < d.N; i += d.InTown(i) ? 30 : 70)
            {
                var probe = new GameObject("Reflejo").AddComponent<ReflectionProbe>();
                probe.transform.SetParent(parent, false);
                probe.transform.position = d.P[i].pos + Vector3.up * 2;
                probe.mode = ReflectionProbeMode.Baked;
                probe.size = new Vector3(70, 30, 70);
                probe.resolution = 64;
                probe.hdr = false;
            }
        }

        static TrackBuilder FindBuilder()
        {
            var builder = Object.FindAnyObjectByType<TrackBuilder>();
            if (builder == null) { Dialog("No hay ninguna pista (TrackBuilder) en la escena abierta. Abre Kurogane > Abrir escena de prueba."); return null; }
            if (string.IsNullOrEmpty(builder.gameObject.scene.path)) { Dialog("Guarda la escena antes de hornear."); return null; }
            return builder;
        }

        static void Dialog(string message) => EditorUtility.DisplayDialog("Kurogane: horneado", message, "Aceptar");
    }
}

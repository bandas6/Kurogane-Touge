using Kurogane.CameraRig;
using Kurogane.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kurogane.Atmosphere
{
    /// <summary>Materiales de efectos con el shader del cielo (sin niebla), para no añadir otro shader a la build.</summary>
    public static class FxMaterials
    {
        static Texture2D soft;
        public static Texture2D Soft => soft != null ? soft : soft = ProceduralTextures.SoftDot();

        /// <summary>Aditivo (luz: lluvia a contraluz, conos de los faros) o transparente (salpicaduras).</summary>
        public static Material Make(bool additive, int queue = (int)RenderQueue.Transparent)
        {
            var shader = Shader.Find("Kurogane/Sky");
            if (shader == null) return null;
            var m = new Material(shader) { renderQueue = queue };
            m.SetTexture("_MainTex", Soft);
            m.SetFloat("_SrcBlend", (float)(additive ? BlendMode.One : BlendMode.SrcAlpha));
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", 0);
            return m;
        }
    }

    /// <summary>
    /// Lluvia alrededor de la cámara (sistema de partículas normal, gotas alargadas con la velocidad) y
    /// limpiaparabrisas en la vista de capó. La intensidad la fija WeatherSystem; dentro del túnel no llueve.
    /// </summary>
    [DefaultExecutionOrder(1001)]
    public class RainFx : MonoBehaviour
    {
        [Range(0, 1)] public float intensity;
        [Tooltip("Tope de gotas: 1.500 en móvil (plan) y el doble en PC.")]
        public int maxDrops = 3000;
        /// <summary>Lo fija WeatherSystem: la cámara está bajo techo (túnel).</summary>
        public System.Func<bool> sheltered;

        Camera cam;
        ChaseCamera chase;
        ParticleSystem ps;
        Transform wipers;
        Transform[] blades;

        void Awake()
        {
            cam = GetComponent<Camera>();
            chase = GetComponent<ChaseCamera>();
            if (Application.isMobilePlatform) maxDrops = 1500;
        }

        void Build()
        {
            var go = new GameObject("Lluvia");
            ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 1.1f;
            main.startSpeed = 0;
            // Gotas gruesas y claras: con la imagen a 448 líneas, más finas desaparecen
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.08f);
            main.startColor = new Color(0.62f, 0.68f, 0.8f, 1) * 0.75f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxDrops;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            vel.y = new ParticleSystem.MinMaxCurve(-15, -12);
            vel.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(26, 1, 26);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.05f;
            r.cameraVelocityScale = 0.04f;
            r.lengthScale = 1;
            r.sharedMaterial = FxMaterials.Make(true);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();

            // Limpiaparabrisas: dos escobillas delante de la cámara de capó
            wipers = new GameObject("Limpiaparabrisas").transform;
            wipers.SetParent(transform, false);
            var black = MaterialFactory.Unlit(new Color(0.02f, 0.02f, 0.025f));
            blades = new Transform[2];
            for (int k = 0; k < 2; k++)
            {
                var pivot = new GameObject("Brazo").transform;
                pivot.SetParent(wipers, false);
                pivot.localPosition = new Vector3(k == 0 ? -0.32f : 0.08f, -0.3f, 0.5f);
                var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var col = blade.GetComponent<Collider>();
                if (col != null) Destroy(col);
                blade.transform.SetParent(pivot, false);
                blade.transform.localPosition = new Vector3(0, 0.16f, 0);
                blade.transform.localScale = new Vector3(0.007f, 0.32f, 0.006f);
                var mr = blade.GetComponent<MeshRenderer>();
                mr.sharedMaterial = black;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                blades[k] = pivot;
            }
        }

        void LateUpdate()
        {
            if (intensity <= 0.001f && ps == null) return;
            if (ps == null) Build();

            // El emisor va por encima y por delante de la cámara, sin girar con ella
            var t = cam.transform;
            var ahead = t.forward;
            ahead.y = 0;
            ps.transform.SetPositionAndRotation(t.position + Vector3.up * 9 + ahead.normalized * 8, Quaternion.identity);
            var emission = ps.emission;
            bool dry = sheltered != null && sheltered();
            emission.rateOverTime = dry ? 0 : intensity * maxDrops / 1.1f;

            bool hood = intensity > 0.001f && chase != null && chase.mode == ChaseMode.Capo && !chase.showroom && !chase.cinematic;
            if (wipers.gameObject.activeSelf != hood) wipers.gameObject.SetActive(hood);
            if (hood)
            {
                float a = Mathf.Sin(Time.time * Mathf.PI / 0.75f) * 0.5f + 0.5f;
                for (int k = 0; k < 2; k++) blades[k].localRotation = Quaternion.Euler(0, 0, 70 - a * 120);
            }
        }
    }
}

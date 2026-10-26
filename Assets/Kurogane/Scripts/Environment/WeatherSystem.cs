using System.Collections.Generic;
using Kurogane.CameraRig;
using Kurogane.Cars;
using Kurogane.Retro;
using Kurogane.Track;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kurogane.Atmosphere
{
    /// <summary>
    /// Aplica un <see cref="WeatherProfile"/> (bloque C): niebla, luz ambiente, luna o sol, cielo, faroles, faros
    /// (con conos de luz en niebla y lluvia), asfalto mojado, agarre de los coches, gotas en el filtro PS2, lluvia,
    /// sonido, salpicaduras y relámpagos. Los colores y el mojado van con MaterialPropertyBlock, así no se modifican
    /// los materiales horneados. RaceManager lo crea y lo llama al empezar cada bajada y al cambiar de clima.
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        [Tooltip("Perfiles en el orden de WeatherKind. Vacíos = perfiles de serie (WeatherProfile.Preset).")]
        public WeatherProfile[] profiles = new WeatherProfile[5];

        public WeatherProfile Current { get; private set; }

        TrackBuilder track;
        ChaseCamera chase;
        PS2Filter retro;
        SkyDome sky;
        RainFx rain;
        RainAudio rainAudio;
        readonly List<(CarVisual visual, WheelSpray spray)> cars = new List<(CarVisual, WheelSpray)>();
        CarController player;
        RivalAI rival;

        readonly Dictionary<Light, float> lampBase = new Dictionary<Light, float>();
        readonly List<(Renderer r, Color color, float smooth)> wetSurfaces = new List<(Renderer, Color, float)>();
        readonly List<Renderer> midCity = new List<Renderer>();
        MaterialPropertyBlock block;
        Material coneMat;
        float lightningTimer = 10, flash;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), SmoothId = Shader.PropertyToID("_Smoothness");
        static readonly int HorizonId = Shader.PropertyToID("_Horizon"), FogMixId = Shader.PropertyToID("_FogMix");
        static readonly HashSet<string> WetNames = new HashSet<string> { "Road", "Gutter", City.CityBuilder.GroundName };

        public void Init(TrackBuilder trackBuilder, ChaseCamera chaseCamera, PS2Filter filter, CarController playerCar, RivalAI rivalCar)
        {
            track = trackBuilder;
            chase = chaseCamera;
            retro = filter;
            player = playerCar;
            rival = rivalCar;
            sky = FindAnyObjectByType<SkyDome>();
            block = new MaterialPropertyBlock();

            var cam = chase.GetComponent<Camera>();
            rain = cam.GetComponent<RainFx>();
            if (rain == null) rain = cam.gameObject.AddComponent<RainFx>();
            rain.sheltered = () => player != null && player.Track != null && player.Track.InTunnel(player.Index);
            var audioGo = new GameObject("Sonido de lluvia");
            audioGo.transform.SetParent(cam.transform, false);
            rainAudio = audioGo.AddComponent<RainAudio>();

            cars.Clear();
            foreach (Component c in new Component[] { player, rival })
            {
                if (c == null) continue;
                var spray = c.GetComponent<WheelSpray>();
                if (spray == null) spray = c.gameObject.AddComponent<WheelSpray>();
                cars.Add((c.GetComponent<CarVisual>(), spray));
            }

            // Superficies que se mojan, luces de la pista y ciudad media (se funde con la niebla)
            var root = track.Root;
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (WetNames.Contains(r.gameObject.name) && r.sharedMaterial != null && r.sharedMaterial.HasProperty(SmoothId))
                    wetSurfaces.Add((r, r.sharedMaterial.GetColor(BaseColorId), r.sharedMaterial.GetFloat(SmoothId)));
                else if (r.gameObject.name == City.CityBuilder.MidCityName) midCity.Add(r);
            }
            foreach (var l in root.GetComponentsInChildren<Light>(true)) lampBase[l] = l.intensity;
        }

        public WeatherProfile Profile(WeatherKind kind)
        {
            int i = (int)kind;
            if (profiles == null || profiles.Length <= i) System.Array.Resize(ref profiles, 5);
            if (profiles[i] == null) profiles[i] = WeatherProfile.Preset(kind);
            return profiles[i];
        }

        public void Apply(WeatherKind kind) => Apply(Profile(kind));

        public void Apply(WeatherProfile p)
        {
            Current = p;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = p.fogColor;
            RenderSettings.fogStartDistance = p.fogStart;
            RenderSettings.fogEndDistance = p.fogEnd;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = p.ambientSky;
            RenderSettings.ambientEquatorColor = p.ambientEquator;
            RenderSettings.ambientGroundColor = p.ambientGround;

            var sun = RenderSettings.sun;
            if (sun == null)
                foreach (var l in FindObjectsByType<Light>())
                    if (l.type == LightType.Directional) { sun = l; break; }
            if (sun != null)
            {
                sun.color = p.sunColor;
                sun.intensity = p.sunIntensity;
                sun.transform.rotation = Quaternion.Euler(p.sunAngles.x, p.sunAngles.y, 0);
            }

            ApplySky(1);
            foreach (var kv in lampBase) if (kv.Key != null) kv.Key.intensity = kv.Value * p.lampIntensity;

            // Asfalto mojado: más oscuro y con mucho más brillo (los charcos de la máscara del asfalto brillan más).
            // El reflejo del entorno se baja: sin cielo en la iluminación, a plena intensidad los charcos salen blancos
            foreach (var w in wetSurfaces)
            {
                if (w.r == null) continue;
                block.Clear();
                block.SetColor(BaseColorId, w.color * Mathf.Lerp(1, 0.5f, p.wetness));
                block.SetFloat(SmoothId, Mathf.Lerp(w.smooth, 0.8f, p.wetness));
                w.r.SetPropertyBlock(block);
            }
            RenderSettings.reflectionIntensity = Mathf.Lerp(1, 0.25f, p.wetness);
            foreach (var r in midCity)
            {
                if (r == null) continue;
                block.Clear();
                block.SetColor(HorizonId, p.fogColor.gamma);
                block.SetFloat(FogMixId, p.ridgeHaze);
                r.SetPropertyBlock(block);
            }

            if (player != null) { player.GripScale = p.gripScale; player.DriftGripScale = p.driftGripScale; }
            if (rival != null) rival.GripScale = p.gripScale;
            if (retro != null) retro.rain = p.rain;
            if (rain != null) rain.intensity = p.rain;
            foreach (var c in cars) if (c.spray != null) c.spray.wetness = p.wetness;
            RefreshCars();
        }

        /// <summary>Faros y conos de luz; hay que llamarlo cuando se reconstruye un coche (cambio en el garaje).</summary>
        public void RefreshCars()
        {
            if (Current == null) return;
            foreach (var c in cars)
            {
                var rig = c.visual != null ? c.visual.Rig : null;
                if (rig == null || rig.headlight == null) continue;
                rig.headlight.intensity = 4 * Current.headlightIntensity;
                var cone = rig.headlight.transform.Find("Cono de luz");
                if (cone == null && Current.headlightCones) cone = Cone(rig.headlight.transform);
                if (cone != null) cone.gameObject.SetActive(Current.headlightCones);
            }
        }

        /// <summary>Cono abierto aditivo delante del faro: se ve en la niebla y la lluvia.</summary>
        Transform Cone(Transform parent)
        {
            if (coneMat == null) coneMat = FxMaterials.Make(true);
            // Tres anillos: transparente junto al faro, máximo a 7 m (lejos del coche, para que no parezca humo) y nada
            // al final. Muy tenue: en la niebla se suma a la luz del faro
            const int sides = 12;
            float[] dist = { 1.5f, 7f, 18f }, radius = { 0.6f, 2.2f, 4.6f };
            var bright = new Color(1f, 0.95f, 0.8f, 1) * 0.018f;
            Color[] ring = { Color.clear, bright, Color.clear };
            var v = new List<Vector3>();
            var c = new List<Color>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int s = 0; s <= sides; s++)
            {
                float a = s * Mathf.PI * 2 / sides;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.55f, 0);
                for (int k = 0; k < 3; k++)
                {
                    v.Add(dir * radius[k] + Vector3.forward * dist[k]);
                    c.Add(ring[k]);
                    uv.Add(new Vector2(0.5f, 0.5f));
                }
                if (s < sides)
                    for (int k = 0; k < 2; k++)
                    {
                        int o = s * 3 + k;
                        t.Add(o); t.Add(o + 1); t.Add(o + 3);
                        t.Add(o + 3); t.Add(o + 1); t.Add(o + 4);
                    }
            }
            var m = new Mesh { name = "Cono de luz" };
            m.SetVertices(v);
            m.SetColors(c);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            var go = new GameObject("Cono de luz");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = coneMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go.transform;
        }

        void ApplySky(float brightness)
        {
            if (sky == null || Current == null) return;
            var p = Current;
            sky.SetWeather(p.fogColor * brightness, p.zenith * brightness, p.fogColor * 0.4f, p.horizonBand, p.stars, p.valleyLights,
                p.moon, p.ridgeHaze * (brightness > 1 ? 0.3f : 1));
        }

        void Update()
        {
            if (Current == null) return;

            bool hood = chase != null && chase.mode == ChaseMode.Capo && !chase.showroom && !chase.cinematic;
            // En la vista de capó la cámara queda dentro del cono del faro del jugador: se oculta
            if (Current.headlightCones && cars.Count > 0 && cars[0].visual != null && cars[0].visual.Rig != null && cars[0].visual.Rig.headlight != null)
            {
                var cone = cars[0].visual.Rig.headlight.transform.Find("Cono de luz");
                if (cone != null && cone.gameObject.activeSelf == hood) cone.gameObject.SetActive(!hood);
            }

            // Sonido: más fuerte dentro del coche (capó) y casi nada en el túnel
            if (rainAudio != null)
            {
                bool tunnel = rain != null && rain.sheltered != null && rain.sheltered();
                rainAudio.volume = Time.timeScale == 0 ? 0 : Current.rain * (hood ? 1 : 0.6f) * (tunnel ? 0.15f : 1);
            }

            // Relámpagos lejanos: iluminan cielo y cordilleras un instante, y el trueno llega después
            if (!Current.lightning) return;
            lightningTimer -= Time.deltaTime;
            if (lightningTimer <= 0)
            {
                lightningTimer = Random.Range(12f, 35f);
                flash = 0.35f;
                if (rainAudio != null) rainAudio.Thunder();
            }
            if (flash > 0)
            {
                flash -= Time.deltaTime;
                bool on = flash > 0.25f || (flash > 0.08f && flash < 0.16f);
                ApplySky(on ? 4 : 1);
                RenderSettings.ambientSkyColor = Current.ambientSky * (on ? 2.5f : 1);
            }
        }
    }
}

using UnityEngine;

namespace Kurogane.Atmosphere
{
    public enum WeatherKind { Despejado, Amanecer, Atardecer, Niebla, Lluvia }

    /// <summary>
    /// Perfil de clima y hora del día (bloque C): niebla, cielo, luz ambiente, luna o sol, faroles y faros, lluvia,
    /// asfalto mojado y agarre. Los cinco perfiles de serie salen de <see cref="Preset"/>; los assets de
    /// Data/Clima se crean a partir de ellos y se pueden retocar en el inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "Kurogane/Weather Profile", fileName = "Clima")]
    public class WeatherProfile : ScriptableObject
    {
        public WeatherKind kind;
        public string displayName = "Despejado";

        [Header("Niebla")]
        public Color fogColor = new Color(0.07f, 0.1f, 0.23f);
        public float fogStart = 30, fogEnd = 240;

        [Header("Cielo")]
        public Color zenith = new Color(0.008f, 0.01f, 0.03f);
        [Tooltip("Franja de color sobre el horizonte (alfa = fuerza): naranja al amanecer y al atardecer.")]
        public Color horizonBand = Color.clear;
        [Range(0, 1)] public float stars = 1;
        [Range(0, 1)] public float valleyLights = 1;
        [Range(0, 1)] public float moon = 1;
        [Tooltip("Cuánto se funden las cordilleras con la niebla (0 = nítidas).")]
        [Range(0, 1)] public float ridgeHaze;

        [Header("Luz")]
        public Color ambientSky = new Color(0.32f, 0.38f, 0.6f);
        public Color ambientEquator = new Color(0.14f, 0.16f, 0.26f);
        public Color ambientGround = new Color(0.06f, 0.06f, 0.09f);
        [Tooltip("Luz direccional de la escena: la luna de noche, el sol al amanecer o al atardecer.")]
        public Color sunColor = new Color(0.66f, 0.72f, 0.94f);
        public float sunIntensity = 0.35f;
        [Tooltip("Inclinación y rumbo de la luz direccional, en grados.")]
        public Vector2 sunAngles = new Vector2(50, -30);
        [Tooltip("Multiplica la intensidad de faroles y luces del pueblo en tiempo real.")]
        public float lampIntensity = 1;
        public float headlightIntensity = 1;
        [Tooltip("Conos de luz visibles delante de los faros (niebla y lluvia).")]
        public bool headlightCones;

        [Header("Lluvia")]
        [Range(0, 1)] public float rain;
        [Tooltip("Asfalto mojado: más oscuro y brillante, con charcos.")]
        [Range(0, 1)] public float wetness;
        public bool lightning;

        [Header("Física")]
        [Tooltip("Multiplica el agarre (CarController.GripScale).")]
        public float gripScale = 1;
        [Tooltip("Multiplica el agarre en derrape (CarController.DriftGripScale).")]
        public float driftGripScale = 1;

        public static WeatherProfile Preset(WeatherKind kind)
        {
            var p = CreateInstance<WeatherProfile>();
            p.kind = kind;
            p.name = kind.ToString();
            switch (kind)
            {
                case WeatherKind.Despejado:
                    p.displayName = "Despejado";
                    break;

                case WeatherKind.Amanecer:
                    // Paleta del prototipo web: niebla lila, horizonte naranja y faroles al 35 %
                    p.displayName = "Amanecer";
                    p.fogColor = new Color(0.56f, 0.46f, 0.6f);
                    p.fogStart = 40; p.fogEnd = 260;
                    p.zenith = new Color(0.24f, 0.32f, 0.58f);
                    p.horizonBand = new Color(1f, 0.55f, 0.25f, 0.55f);
                    p.stars = 0.12f; p.valleyLights = 0.35f; p.moon = 0.35f; p.ridgeHaze = 0.25f;
                    p.ambientSky = new Color(0.62f, 0.58f, 0.72f);
                    p.ambientEquator = new Color(0.48f, 0.4f, 0.48f);
                    p.ambientGround = new Color(0.16f, 0.14f, 0.16f);
                    p.sunColor = new Color(1f, 0.72f, 0.52f); p.sunIntensity = 0.9f; p.sunAngles = new Vector2(10, 75);
                    p.lampIntensity = 0.35f; p.headlightIntensity = 0.5f;
                    break;

                case WeatherKind.Atardecer:
                    p.displayName = "Atardecer";
                    p.fogColor = new Color(0.52f, 0.34f, 0.3f);
                    p.fogStart = 40; p.fogEnd = 250;
                    p.zenith = new Color(0.16f, 0.18f, 0.38f);
                    p.horizonBand = new Color(1f, 0.42f, 0.16f, 0.75f);
                    p.stars = 0.05f; p.valleyLights = 0.6f; p.moon = 0; p.ridgeHaze = 0.2f;
                    p.ambientSky = new Color(0.56f, 0.46f, 0.46f);
                    p.ambientEquator = new Color(0.44f, 0.32f, 0.28f);
                    p.ambientGround = new Color(0.14f, 0.1f, 0.1f);
                    p.sunColor = new Color(1f, 0.56f, 0.3f); p.sunIntensity = 1.0f; p.sunAngles = new Vector2(7, -105);
                    p.lampIntensity = 0.6f; p.headlightIntensity = 0.7f;
                    break;

                case WeatherKind.Niebla:
                    p.displayName = "Niebla";
                    p.fogColor = new Color(0.16f, 0.17f, 0.22f);
                    p.fogStart = 8; p.fogEnd = 90;
                    p.zenith = new Color(0.12f, 0.13f, 0.17f);
                    p.stars = 0; p.valleyLights = 0; p.moon = 0.15f; p.ridgeHaze = 1;
                    p.ambientSky = new Color(0.3f, 0.32f, 0.4f);
                    p.ambientEquator = new Color(0.2f, 0.21f, 0.26f);
                    p.ambientGround = new Color(0.08f, 0.08f, 0.1f);
                    p.sunIntensity = 0.15f;
                    p.headlightCones = true;
                    break;

                case WeatherKind.Lluvia:
                    p.displayName = "Lluvia";
                    p.fogColor = new Color(0.08f, 0.09f, 0.13f);
                    p.fogStart = 15; p.fogEnd = 160;
                    p.zenith = new Color(0.03f, 0.035f, 0.05f);
                    p.stars = 0; p.valleyLights = 0.45f; p.moon = 0; p.ridgeHaze = 0.6f;
                    p.ambientSky = new Color(0.24f, 0.27f, 0.36f);
                    p.ambientEquator = new Color(0.12f, 0.13f, 0.17f);
                    p.ambientGround = new Color(0.05f, 0.05f, 0.06f);
                    p.sunIntensity = 0.18f;
                    p.rain = 1; p.wetness = 1; p.lightning = true;
                    p.gripScale = 0.8f; p.driftGripScale = 0.85f;
                    break;
            }
            return p;
        }
    }
}

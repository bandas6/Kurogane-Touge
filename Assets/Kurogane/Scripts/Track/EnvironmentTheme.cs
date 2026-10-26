using Kurogane.Atmosphere;
using Kurogane.City;
using UnityEngine;

namespace Kurogane.Track
{
    public enum Vegetation { Cedros, HojaAncha, ArcesOtono }
    public enum WallStyle { Hormigon, Roca, Piedra }

    /// <summary>
    /// Ambiente de una pista: lo que cambia de un puerto a otro sin tocar el trazado. Lo leen TrackBuilder (árboles,
    /// muros, faroles y guardarraíl), SkyDome (cordilleras y ciudad lejana), el pueblo del pie y el clima.
    /// Los valores por defecto son los del Monte Kurogane.
    /// </summary>
    [CreateAssetMenu(menuName = "Kurogane/Environment Theme", fileName = "EnvironmentTheme")]
    public class EnvironmentTheme : ScriptableObject
    {
        [Header("Bosque")]
        public Vegetation vegetation = Vegetation.Cedros;
        [Tooltip("Proporción de árboles de otro tipo: hoja ancha entre los cedros, o cedros entre la hoja ancha y los arces.")]
        [Range(0, 1)] public float mix;
        [Tooltip("Multiplica el número de árboles de la pista.")]
        [Range(0, 2)] public float treeDensity = 1;

        [Header("Carretera")]
        public WallStyle walls = WallStyle.Hormigon;
        [Tooltip("Faroles: 0 = ninguno (solo los faros), 1 = como el Monte Kurogane, 2 = el doble.")]
        [Range(0, 2)] public float lampDensity = 1;
        [Tooltip("Tinte del guardarraíl (blanco = galvanizado).")]
        public Color railColor = Color.white;

        [Header("Horizonte (SkyDome)")]
        [Tooltip("Altura en grados de la cordillera lejana.")]
        [Range(0, 25)] public float farRidge = 9;
        [Tooltip("Altura en grados de la cordillera cercana.")]
        [Range(0, 25)] public float nearRidge = 6;
        [Tooltip("Lo quebradas que son las cordilleras (agujas de roca con valores altos).")]
        [Range(0, 1.5f)] public float ridgeRoughness = 0.6f;
        [Tooltip("Siluetas de la ciudad lejana en el horizonte del valle.")]
        public bool distantCity = true;
        [Range(0, 4000)] public int valleyLights = 1800;

        [Header("Pueblo y clima")]
        [Tooltip("Pueblo del pie de la montaña. Vacío = el estilo del TrackBuilder.")]
        public CityStyle cityStyle;
        [Tooltip("Clima con la opción \"Según la pista\" del menú.")]
        public WeatherKind defaultWeather = WeatherKind.Despejado;
    }
}

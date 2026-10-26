using UnityEngine;

namespace Kurogane.City
{
    /// <summary>
    /// Estilo del pueblo de la meta: semilla, densidad, proporción de tiendas y ventanas encendidas, nombres
    /// (inventados) de las tiendas y tamaño de la ciudad media del valle. La paleta está en el atlas del pueblo.
    /// </summary>
    [CreateAssetMenu(menuName = "Kurogane/City Style", fileName = "CityStyle")]
    public class CityStyle : ScriptableObject
    {
        public int seed = 1987;
        [Tooltip("Proporción de ventanas con luz.")]
        [Range(0, 1)] public float litWindows = 0.45f;
        [Tooltip("Proporción de tiendas en la primera fila de la calle mayor.")]
        [Range(0, 1)] public float shopShare = 0.3f;
        [Tooltip("Densidad de la segunda fila de casas, detrás.")]
        [Range(0, 1)] public float backRowDensity = 0.75f;
        [Tooltip("Metros entre postes eléctricos.")]
        public float poleSpacing = 30;

        [Header("Nombres (inventados)")]
        public string tofuShopName = "豆腐店 黒鉄";
        public string konbiniName = "HOSHI MART";
        public string[] shopNames = { "まるや", "金田商店", "さくら", "酒 よしだ", "理容 ナカ", "おもちゃ 星" };

        [Header("Ciudad media del valle (300 m - 1,5 km)")]
        [Range(0, 400)] public int midBuildings = 170;
    }
}

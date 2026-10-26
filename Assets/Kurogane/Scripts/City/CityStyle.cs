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

        [Header("Pueblo abierto (bloque H1)")]
        [Tooltip("Medio ancho del pueblo a cada lado de la calle mayor, en metros. 0 = solo la calle mayor (aldea).")]
        [Range(0, 400)] public float extent = 300;
        [Tooltip("Largo del pueblo desde la entrada, en metros: la calle mayor sigue más allá del final de la pista.")]
        [Range(200, 800)] public float length = 560;
        [Tooltip("Proporción de solares con casa a lo largo de las calles (el resto, huertos y descampados).")]
        [Range(0, 1)] public float density = 0.6f;
        public bool river = true;
        public bool station = true;
        [Tooltip("Una de las calles transversales es una avenida de cuatro carriles.")]
        public bool avenue = true;
        public string stationName = "黒鉄駅";
    }
}

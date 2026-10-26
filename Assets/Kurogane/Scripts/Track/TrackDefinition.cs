using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kurogane.Track
{
    /// <summary>Tipos de tramo. Los valores se guardan como número en los assets: los nuevos van al final.</summary>
    public enum SegmentType { Straight, Curve, Hairpin, SCurve }

    public enum TrackDirection { Bajada, Subida }

    public enum LandmarkKind { Viaducto }

    /// <summary>Decorado propio de una pista junto a la carretera (no se puede pisar).</summary>
    [Serializable]
    public struct Landmark
    {
        public LandmarkKind kind;
        [Tooltip("Metros desde la salida de bajada donde queda el centro del monumento.")]
        public float atMeters;
        [Tooltip("Lado: +1 derecha, -1 izquierda (en bajada), 0 = el lado donde el terreno cae.")]
        public int side;
        [Tooltip("Distancia del centro de la carretera al monumento, en metros.")]
        public float lateral;
        [Tooltip("Largo del monumento, en metros.")]
        public float length;
    }

    [Serializable]
    public struct TrackSegment
    {
        public SegmentType type;
        [Tooltip("Grados que gira el tramo. Positivo = derecha, negativo = izquierda. En una ese, lo que gira la primera curva (la segunda gira lo mismo al revés).")]
        public float angle;
        [Tooltip("Recta, curva y ese: longitud en metros. Horquilla: radio en metros.")]
        public float lengthOrRadius;
        [Tooltip("El tramo va dentro de un túnel (solo rectas).")]
        public bool tunnel;
        [Tooltip("Tramo después de la meta (bajada al pueblo): no se cronometra.")]
        public bool afterGoal;
        [Tooltip("Medio ancho propio de la calzada en metros. 0 = el de la pista.")]
        public float halfWidth;
        [Tooltip("Desnivel por metro propio (positivo = baja). 0 = el de la pista para este tipo de tramo.")]
        public float slope;
        [Tooltip("Puente: sin terreno debajo y con barandilla a los dos lados.")]
        public bool bridge;

        public TrackSegment(SegmentType type, float angle, float lengthOrRadius, bool tunnel = false, bool afterGoal = false)
        {
            this.type = type;
            this.angle = angle;
            this.lengthOrRadius = lengthOrRadius;
            this.tunnel = tunnel;
            this.afterGoal = afterGoal;
            halfWidth = 0;
            slope = 0;
            bridge = false;
        }

        public float Length => type == SegmentType.Hairpin
            ? Mathf.Abs(angle) * Mathf.Deg2Rad * lengthOrRadius
            : lengthOrRadius;
    }

    /// <summary>
    /// Trazado de un paso de montaña: datos para el menú, ambiente y lista de tramos. Los valores por defecto son
    /// la bajada del Monte Kurogane del prototipo web (2,3 km, 4 horquillas, 1 túnel). Cada pista tiene su escena
    /// <see cref="SceneName"/> y se lista en <see cref="TrackCatalog"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Kurogane/Track Definition", fileName = "TrackDefinition")]
    public class TrackDefinition : ScriptableObject
    {
        [Header("Menú")]
        [Tooltip("Identificador sin espacios: da nombre a la escena (Pista_<id>) y a los récords. Vacío = nombre del asset.")]
        public string id = "";
        public string displayName = "";
        [Tooltip("Nombre en japonés (kanji del puerto).")]
        public string japaneseName = "";
        [TextArea(2, 4)] public string description = "";
        [Tooltip("Ambiente: vegetación, muros, faroles, guardarraíl, cordilleras, pueblo del pie y clima por defecto. Vacío = el del Monte Kurogane.")]
        public EnvironmentTheme theme;
        [Tooltip("Se puede correr de subida (misma carretera, del pie a la cima).")]
        public bool allowUphill = true;
        [Tooltip("Tiempo de referencia de la nota (s): hacerlo en este tiempo da 30 de los 40 puntos de tiempo; cada segundo menos suma 1.")]
        public float parTime = 120;
        [Tooltip("Tiempo de referencia de subida (s). 0 = el de bajada.")]
        public float parTimeUphill;

        [Header("Rival")]
        [Tooltip("Aceleración lateral (m/s²) con la que el rival calcula la velocidad en curva. En calzadas estrechas su trazada es más cerrada y conviene bajarla.")]
        public float rivalCornerGrip = 10.5f;
        [Tooltip("Velocidad máxima del rival en m/s (52 ≈ 187 km/h).")]
        public float rivalTopSpeed = 52;

        [Header("Decorado propio")]
        public List<Landmark> landmarks = new List<Landmark>();

        [Header("Trazado")]
        [Tooltip("Separación entre puntos de la línea central, en metros.")]
        public float step = 2f;
        [Tooltip("Medio ancho de la calzada, en metros (cada tramo puede tener el suyo).")]
        public float halfWidth = 5.2f;
        [Tooltip("Desnivel por metro en rectas y curvas.")]
        public float slope = 0.055f;
        [Tooltip("Desnivel por metro en horquillas.")]
        public float hairpinSlope = 0.035f;
        [Tooltip("Desnivel por metro en los tramos tras la meta (bajada suave al pueblo).")]
        public float afterGoalSlope = 0.03f;

        public List<TrackSegment> segments = new List<TrackSegment>(DefaultSegments());

        public string Id => string.IsNullOrEmpty(id) ? name.Replace(" ", "") : id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        /// <summary>Escena de la pista, con su geometría y luces horneadas.</summary>
        public string SceneName => "Pista_" + Id;

        public static IEnumerable<TrackSegment> DefaultSegments()
        {
            var timed = new[]
            {
                S(110), C(50, 70), S(70), C(-70, 80), S(90), C(35, 40), S(120),
                H(-175, 18), S(110), H(175, 18), S(90), C(-60, 70), C(45, 60), S(150, true),
                C(90, 90), S(60), C(-30, 40), S(80), H(175, 17), S(100), H(-175, 17), S(130),
                C(-50, 70), S(60), C(70, 80), S(120), C(-40, 50), S(160)
            };
            return new List<TrackSegment>(timed).Concat(AfterGoalSegments());
        }

        /// <summary>Unos 300 m tras la meta hasta el pueblo: curva suave y la calle mayor en dos rectas.</summary>
        public static IEnumerable<TrackSegment> AfterGoalSegments() => new[]
        {
            new TrackSegment(SegmentType.Curve, 25, 60, false, true),
            new TrackSegment(SegmentType.Straight, 0, 120, false, true),
            new TrackSegment(SegmentType.Straight, 0, 120, false, true),
        };

        /// <summary>
        /// Tramos que se usan para calcular la pista. Los trazados guardados antes del pueblo no tienen tramos
        /// tras la meta: se añaden los de por defecto, sin tocar el asset ni el tramo cronometrado.
        /// </summary>
        public IEnumerable<TrackSegment> AllSegments()
        {
            foreach (var s in segments) if (s.afterGoal) return segments;
            return segments.Concat(AfterGoalSegments());
        }

        static TrackSegment S(float len, bool tun = false) => new TrackSegment(SegmentType.Straight, 0, len, tun);
        static TrackSegment C(float ang, float len) => new TrackSegment(SegmentType.Curve, ang, len);
        static TrackSegment H(float ang, float r) => new TrackSegment(SegmentType.Hairpin, ang, r);

        [ContextMenu("Restaurar trazado por defecto")]
        void ResetToDefault() => segments = new List<TrackSegment>(DefaultSegments());
    }
}

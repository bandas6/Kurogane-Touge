using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kurogane.Track
{
    public enum SegmentType { Straight, Curve, Hairpin }

    [Serializable]
    public struct TrackSegment
    {
        public SegmentType type;
        [Tooltip("Grados que gira el tramo. Positivo = derecha, negativo = izquierda.")]
        public float angle;
        [Tooltip("Recta y curva: longitud en metros. Horquilla: radio en metros.")]
        public float lengthOrRadius;
        [Tooltip("El tramo va dentro de un túnel (solo rectas).")]
        public bool tunnel;

        public TrackSegment(SegmentType type, float angle, float lengthOrRadius, bool tunnel = false)
        {
            this.type = type;
            this.angle = angle;
            this.lengthOrRadius = lengthOrRadius;
            this.tunnel = tunnel;
        }

        public float Length => type == SegmentType.Hairpin
            ? Mathf.Abs(angle) * Mathf.Deg2Rad * lengthOrRadius
            : lengthOrRadius;
    }

    /// <summary>
    /// Trazado del paso de montaña. Los valores por defecto son la bajada del Monte Kurogane
    /// del prototipo web (2,3 km, 4 horquillas, 1 túnel).
    /// </summary>
    [CreateAssetMenu(menuName = "Kurogane/Track Definition", fileName = "TrackDefinition")]
    public class TrackDefinition : ScriptableObject
    {
        [Tooltip("Separación entre puntos de la línea central, en metros.")]
        public float step = 2f;
        [Tooltip("Medio ancho de la calzada, en metros.")]
        public float halfWidth = 5.2f;
        [Tooltip("Desnivel por metro en rectas y curvas.")]
        public float slope = 0.055f;
        [Tooltip("Desnivel por metro en horquillas.")]
        public float hairpinSlope = 0.035f;

        public List<TrackSegment> segments = new List<TrackSegment>(DefaultSegments());

        public static IEnumerable<TrackSegment> DefaultSegments()
        {
            return new[]
            {
                S(110), C(50, 70), S(70), C(-70, 80), S(90), C(35, 40), S(120),
                H(-175, 18), S(110), H(175, 18), S(90), C(-60, 70), C(45, 60), S(150, true),
                C(90, 90), S(60), C(-30, 40), S(80), H(175, 17), S(100), H(-175, 17), S(130),
                C(-50, 70), S(60), C(70, 80), S(120), C(-40, 50), S(160)
            };
        }

        static TrackSegment S(float len, bool tun = false) => new TrackSegment(SegmentType.Straight, 0, len, tun);
        static TrackSegment C(float ang, float len) => new TrackSegment(SegmentType.Curve, ang, len);
        static TrackSegment H(float ang, float r) => new TrackSegment(SegmentType.Hairpin, ang, r);

        [ContextMenu("Restaurar trazado por defecto")]
        void ResetToDefault() => segments = new List<TrackSegment>(DefaultSegments());
    }
}

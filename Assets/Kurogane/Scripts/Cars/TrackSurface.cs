using UnityEngine;

namespace Kurogane.Cars
{
    /// <summary>
    /// Superficie de carrera: la de siempre. La altura sale de la línea central y el lateral se limita contra el borde
    /// de la calzada (por el lado de la canaleta las ruedas pueden bajar al desagüe). Son las mismas operaciones, en el
    /// mismo orden, que había en CarController antes del bloque G.
    /// </summary>
    public sealed class TrackSurface : ICarSurface
    {
        public static readonly TrackSurface Instance = new TrackSurface();

        public Vector3 Normal => Vector3.up;

        public bool OverGutter(CarController car, Vector3 pos)
        {
            var t = car.Track;
            int i = car.Index;
            int gs = t.GutterSide[i];
            return gs != 0 && t.Lateral(i, pos) * gs > t.HW[i] - 1.75f;
        }

        public void Constrain(CarController car, ref Vector3 pos, ref float vx, ref float vz, bool inGutter, out float impact, out Vector3 point)
        {
            var t = car.Track;
            int i = car.Index;
            impact = 0;
            point = pos;
            var p = t.P[i];
            float d = t.Lateral(i, pos);
            int sd = d >= 0 ? 1 : -1;
            float lim = t.GutterSide[i] == sd ? t.HW[i] - 0.6f : t.HW[i] - 0.95f;
            float along = (pos.x - p.pos.x) * p.forward.x + (pos.z - p.pos.z) * p.forward.z;
            var next = t.P[Mathf.Min(t.N - 1, i + 1)];
            pos.y = Mathf.Lerp(p.pos.y, next.pos.y, Mathf.Clamp01(along / t.Step)) - (inGutter ? 0.06f : 0);
            if (Mathf.Abs(d) > lim)
            {
                float over = Mathf.Abs(d) - lim;
                pos.x -= p.right.x * over * sd;
                pos.z -= p.right.z * over * sd;
                float vn = (vx * p.right.x + vz * p.right.z) * sd;
                if (vn > 0)
                {
                    vx -= p.right.x * sd * vn * 1.3f;
                    vz -= p.right.z * sd * vn * 1.3f;
                    vx *= 0.985f;
                    vz *= 0.985f;
                    impact = vn;
                    point = pos + p.right * sd * 0.9f;
                }
            }
        }
    }
}

using UnityEngine;

namespace Kurogane.Cars
{
    /// <summary>
    /// Superficie del mundo (bloque G): la altura sale de un rayo hacia abajo contra la capa Suelo y la inclinación de
    /// la normal; las paredes, casas y bordes son colisionadores de la capa Obstaculo y el coche rebota contra ellos igual
    /// que contra el borde de la calzada. Los bordes de la pista (cajas, una por punto) se resuelven como el límite de la
    /// pista, contra la cara de la caja más cercana; el resto, con Physics.ComputePenetration. La canaleta son volúmenes
    /// disparador (capa Canaleta) en las horquillas.
    /// El coche es una cápsula vertical de 0,85 m de radio: contra un muro su centro se queda a la misma distancia que
    /// el límite de la pista, gire como gire, y así se conduce igual con las dos superficies.
    /// Coste: un rayo y una consulta de solapes por paso de física (120 Hz); con <see cref="overlapEveryOtherStep"/>
    /// los solapes van a 60 Hz.
    /// </summary>
    public sealed class WorldSurface : ICarSurface
    {
        public const int GutterLayer = 27, GroundLayer = 28, ObstacleLayer = 29;
        public const float Radius = 0.85f;
        // Alto entre las esferas de la cápsula y altura de su punto más bajo: 25 cm sobre el suelo, para no
        // engancharse en raíles, bordillos y piezas bajas del pueblo
        const float BodyHeight = 0.4f, Bottom = 0.25f, Skin = 0.02f, Reach = 0.6f;

        public Vector3 Normal { get; private set; } = Vector3.up;
        /// <summary>Consulta las paredes solo uno de cada dos pasos (si en el móvil hiciera falta).</summary>
        public bool overlapEveryOtherStep;

        readonly CapsuleCollider body;
        readonly Collider[] hits = new Collider[32];
        int step;

        readonly System.Func<Collider, int> borderIndex;

        /// <param name="borderIndex">Punto de la pista de bajada de cada caja de borde (TrackBuilder.BorderIndex), o null.</param>
        public WorldSurface(CarController car, System.Func<Collider, int> borderIndex = null)
        {
            this.borderIndex = borderIndex;
            var go = new GameObject("Cuerpo (colisión)");
            go.layer = 2; // Ignore Raycast: ni el rayo del suelo ni las paredes lo ven
            go.transform.SetParent(car.transform, false);
            // La cápsula de consulta lleva un margen: PhysX no informa de penetraciones de milímetros, y en un roce
            // largo la pista frena en todos los pasos. Con el margen se detectan todas y solo cuenta lo que pasa de él
            body = go.AddComponent<CapsuleCollider>();
            body.radius = Radius + Skin;
            body.height = BodyHeight + 2 * (Radius + Skin);
            body.direction = 1;
            body.center = new Vector3(0, Bottom + Radius + BodyHeight / 2, 0);
        }

        /// <summary>Quita el colisionador del coche (al volver a la superficie de pista).</summary>
        public void Dispose()
        {
            if (body != null) Object.Destroy(body.gameObject);
        }

        public bool OverGutter(CarController car, Vector3 pos) =>
            Physics.CheckSphere(pos + Vector3.up * 0.3f, 0.05f, 1 << GutterLayer, QueryTriggerInteraction.Collide);

        public void Constrain(CarController car, ref Vector3 pos, ref float vx, ref float vz, bool inGutter, out float impact, out Vector3 point)
        {
            impact = 0;
            point = pos;

            // Suelo: rayo desde 2,5 m por encima. Sin suelo debajo (fuera del mapa) se mantiene la altura
            if (Physics.Raycast(pos + Vector3.up * 2.5f, Vector3.down, out var hit, 12, 1 << GroundLayer, QueryTriggerInteraction.Ignore))
            {
                pos.y = hit.point.y - (inGutter ? 0.06f : 0);
                Normal = hit.normal;
            }

            if (overlapEveryOtherStep && (step++ & 1) == 1) return;
            var rot = Quaternion.identity;
            // Las mismas esferas extremas que la cápsula del coche
            var a = pos + Vector3.up * (Bottom + Radius);
            var b = pos + Vector3.up * (Bottom + Radius + BodyHeight);
            // Consulta algo más ancha que el coche: así entra también la caja de borde de su punto aunque no la toque
            int n = Physics.OverlapCapsuleNonAlloc(a, b, Radius + Reach, hits, 1 << ObstacleLayer, QueryTriggerInteraction.Ignore);

            // Bordes de la pista (cajas, una por punto): como el límite de la pista, solo cuenta la caja del punto más
            // cercano a lo largo de la carretera, aunque no toque, y solo su cara lateral. Con las cajas vecinas, en las
            // curvas cerradas el coche chocaba con sus extremos, que se meten hacia la calzada
            // Con el índice de cada caja se usa la del punto del coche (el mismo que usa la pista); sin él, la más
            // cercana a lo largo de la carretera
            int own = borderIndex != null && car.Track != null ? car.Track.ToGeometry(car.Index) : -1;
            BoxCollider border = null;
            Vector3 borderLocal = default;
            float nearest = float.MaxValue;
            for (int k = 0; k < n; k++)
                if (hits[k] is BoxCollider bx)
                {
                    var local = Quaternion.Inverse(bx.transform.rotation) * (pos - bx.transform.TransformPoint(bx.center));
                    if (own >= 0)
                    {
                        if (borderIndex(bx) == own) Border(bx, local, ref pos, ref vx, ref vz, ref impact, ref point);
                    }
                    else if (Mathf.Abs(local.z) < nearest) { nearest = Mathf.Abs(local.z); border = bx; borderLocal = local; }
                }
            if (border != null) Border(border, borderLocal, ref pos, ref vx, ref vz, ref impact, ref point);

            // Casas y demás mallas: separación de PhysX
            for (int k = 0; k < n; k++)
            {
                var other = hits[k];
                if (other is BoxCollider) continue;
                if (!Physics.ComputePenetration(body, pos, rot, other, other.transform.position, other.transform.rotation, out var dir, out float dist))
                    continue;
                dir.y = 0;
                float len = dir.magnitude;
                if (len < 1e-4f) continue;
                // Solo la parte horizontal de la separación, y solo lo que pasa del margen
                float over = dist * len - Skin;
                if (over > 0) Push(dir / len, over, ref pos, ref vx, ref vz, ref impact, ref point);
            }
        }

        /// <summary>Contra la cara lateral de una caja de borde (el lado que mira a la calzada), como el límite de la pista.</summary>
        static void Border(BoxCollider box, Vector3 local, ref Vector3 pos, ref float vx, ref float vz, ref float impact, ref Vector3 point)
        {
            float side = local.x < 0 ? -1 : 1, over = Radius - (Mathf.Abs(local.x) - box.size.x * 0.5f);
            if (over <= 0) return;
            var dir = box.transform.rotation * new Vector3(side, 0, 0);
            dir.y = 0;
            Push(dir.normalized, over, ref pos, ref vx, ref vz, ref impact, ref point);
        }

        /// <summary>Separa el coche y rebota igual que contra el borde de la calzada en la pista.</summary>
        static void Push(Vector3 dir, float over, ref Vector3 pos, ref float vx, ref float vz, ref float impact, ref Vector3 point)
        {
            pos.x += dir.x * over;
            pos.z += dir.z * over;
            float vn = -(vx * dir.x + vz * dir.z);
            if (vn <= 0) return;
            vx += dir.x * vn * 1.3f;
            vz += dir.z * vn * 1.3f;
            vx *= 0.985f;
            vz *= 0.985f;
            if (vn > impact)
            {
                impact = vn;
                point = pos - dir * 0.9f;
            }
        }
    }
}

using UnityEngine;

namespace Kurogane.Cars
{
    /// <summary>
    /// Anima el modelo: giro y dirección de ruedas, balanceo de carrocería, luces de freno y petardeo.
    /// Sirve tanto para el jugador (<see cref="CarController"/>) como para el rival (<see cref="RivalAI"/>).
    /// </summary>
    public class CarVisual : MonoBehaviour
    {
        public bool headlights = true;

        public CarRig Rig { get; private set; }
        CarController player;
        RivalAI rival;
        float wheelAngle, flameTimer, popTimer;
        CarSpec builtFor;

        public void Build(CarSpec spec)
        {
            if (Rig != null && builtFor == spec) return;
            if (Rig != null) Release(Rig);
            Rig = CarModelBuilder.Build(transform, spec, headlights);
            builtFor = spec;
            player = GetComponent<CarController>();
            rival = GetComponent<RivalAI>();
            if (player != null)
            {
                player.Upshift -= OnUpshift;
                player.LiftOff -= OnLiftOff;
                player.Upshift += OnUpshift;
                player.LiftOff += OnLiftOff;
            }
        }

        /// <summary>
        /// Destruye el modelo junto con las mallas y materiales que se crearon para él; si no, cambiar de coche en
        /// el garaje dejaría memoria perdida. Las mallas de las primitivas de Unity no están en la lista.
        /// </summary>
        static void Release(CarRig rig)
        {
            foreach (var m in rig.ownedMeshes) if (m != null) Destroy(m);
            foreach (var m in rig.ownedMaterials) if (m != null) Destroy(m);
            rig.ownedMeshes.Clear();
            rig.ownedMaterials.Clear();
            if (rig.root != null) Destroy(rig.root.gameObject);
        }

        void OnDestroy()
        {
            if (Rig != null) Release(Rig);
        }

        void OnUpshift() => flameTimer = 0.09f;
        void OnLiftOff() => popTimer = 0.35f;

        void LateUpdate()
        {
            if (Rig == null) return;
            float dt = Time.deltaTime, speed, steerDeg, roll = 0, pitch = 0;
            bool braking;
            if (player != null)
            {
                speed = player.Speed;
                steerDeg = player.Steer * 26f + (player.Drifting ? player.Slip * Mathf.Rad2Deg * 0.6f : 0);
                roll = Mathf.Clamp(player.LateralAccel * 0.7f, -4.5f, 4.5f) + (player.InGutter ? -player.Track.GutterSide[player.Index] * 3.5f : 0);
                pitch = Mathf.Clamp(-player.LongitudinalAccel * 0.35f, -3, 3);
                braking = player.Braking;
            }
            else if (rival != null)
            {
                speed = rival.Speed;
                steerDeg = -rival.YawOffset * Mathf.Rad2Deg * 0.9f;
                braking = rival.Braking;
            }
            else return;

            wheelAngle += speed * dt / 0.31f * Mathf.Rad2Deg;
            foreach (var w in Rig.wheelSpin) w.localRotation = Quaternion.Euler(wheelAngle, 0, 0);
            foreach (var p in Rig.steerPivot) p.localRotation = Quaternion.Euler(0, steerDeg, 0);
            var target = Quaternion.Euler(pitch, 0, roll);
            Rig.body.localRotation = Quaternion.Slerp(Rig.body.localRotation, target, Mathf.Min(1, dt * 12));

            Rig.tailLights.SetColor("_EmissionColor", new Color(1f, 0.1f, 0.16f) * (braking ? 2.2f : 0.5f));

            flameTimer -= dt;
            popTimer -= dt;
            bool flame = flameTimer > 0 || (popTimer > 0 && Random.value < 0.25f);
            Rig.flame.SetActive(flame);
            if (flame) Rig.flame.transform.localScale = new Vector3(0.16f, 0.16f, Random.Range(0.2f, 0.45f));
        }
    }
}

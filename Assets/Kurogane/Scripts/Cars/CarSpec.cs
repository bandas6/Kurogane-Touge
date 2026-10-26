using UnityEngine;

namespace Kurogane.Cars
{
    public enum CarModel { Panda, Coupe, Sedan }

    /// <summary>Datos de manejo y aspecto de un coche. Mismos números que el prototipo web.</summary>
    [CreateAssetMenu(menuName = "Kurogane/Car Spec", fileName = "CarSpec")]
    public class CarSpec : ScriptableObject
    {
        public string displayName = "Hatchback Panda";
        [TextArea] public string tagline = "FR · 1.6 L 4 cilindros · 130 cv · 940 kg";
        public CarModel model = CarModel.Panda;

        [Header("Caja de cambios")]
        [Tooltip("Velocidad en m/s a 8.200 rpm en cada marcha.")]
        public float[] gearTopSpeed = { 12.5f, 21, 29.5f, 38.5f, 48 };
        [Tooltip("Aceleración base (m/s²) en cada marcha con el gas a fondo.")]
        public float[] gearAccel = { 9.8f, 7.3f, 5.5f, 4.3f, 3.3f };

        [Header("Manejo")]
        [Tooltip("Agarre lateral normal (cuánto se corrige el deslizamiento por segundo).")]
        public float grip = 7.6f;
        [Tooltip("Agarre lateral mientras derrapa.")]
        public float driftGrip = 1.5f;
        [Tooltip("Velocidad de giro del volante.")]
        public float yawRate = 2.65f;
        [Tooltip("Tracción total: recupera agarre al acelerar en derrape.")]
        public bool allWheelDrive;
        public bool turbo;

        [Header("Ficha (1-5)")]
        [Range(1, 5)] public int statPower = 2;
        [Range(1, 5)] public int statGrip = 4;
        [Range(1, 5)] public int statDrift = 5;
        [Range(1, 5)] public int statLightness = 5;

        [Header("Aspecto")]
        public Color paint = new Color(0.945f, 0.945f, 0.925f);
        public Color band = new Color(0.067f, 0.07f, 0.086f);
        public string doorText = "豆腐店 黒鉄";

        public int Gears => gearTopSpeed.Length;

        public static CarSpec CreatePanda()
        {
            var s = CreateInstance<CarSpec>();
            s.name = "Panda";
            return s;
        }

        public static CarSpec CreateCoupe()
        {
            var s = CreateInstance<CarSpec>();
            s.name = "Coupe";
            s.displayName = "Coupé Rotativo";
            s.tagline = "FR · rotativo biturbo · 255 cv · 1280 kg";
            s.model = CarModel.Coupe;
            s.gearTopSpeed = new[] { 14f, 24, 33.5f, 42.5f, 53 };
            s.gearAccel = new[] { 10.8f, 8.3f, 6.4f, 5.0f, 3.9f };
            s.grip = 7.2f; s.driftGrip = 1.35f; s.yawRate = 2.45f; s.turbo = true;
            s.statPower = 4; s.statGrip = 4; s.statDrift = 4; s.statLightness = 3;
            s.paint = new Color(0.114f, 0.247f, 0.612f);
            s.band = new Color(0.055f, 0.12f, 0.32f);
            s.doorText = "";
            return s;
        }

        public static CarSpec CreateSedan()
        {
            var s = CreateInstance<CarSpec>();
            s.name = "Sedan";
            s.displayName = "Sedán GT-4WD";
            s.tagline = "4WD · 2.6 L 6 cilindros turbo · 280 cv · 1430 kg";
            s.model = CarModel.Sedan;
            s.gearTopSpeed = new[] { 13.5f, 23, 32.5f, 42, 52 };
            s.gearAccel = new[] { 11.2f, 8.6f, 6.6f, 5.1f, 4.0f };
            s.grip = 9.6f; s.driftGrip = 2.4f; s.yawRate = 2.25f; s.turbo = true; s.allWheelDrive = true;
            s.statPower = 5; s.statGrip = 5; s.statDrift = 2; s.statLightness = 2;
            s.paint = new Color(0.235f, 0.25f, 0.28f);
            s.band = new Color(0.086f, 0.094f, 0.11f);
            s.doorText = "";
            return s;
        }
    }
}

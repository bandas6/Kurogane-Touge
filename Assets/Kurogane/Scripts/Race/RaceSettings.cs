using Kurogane.Cars;
using UnityEngine;

namespace Kurogane.Race
{
    /// <summary>Opciones elegidas en el menú, guardadas en PlayerPrefs entre sesiones.</summary>
    public static class RaceSettings
    {
        const string Car = "kurogane.coche", Mode = "kurogane.modo", Skill = "kurogane.rival",
            Manual = "kurogane.cajaManual", Mph = "kurogane.mph";

        public static void Load(RaceManager r)
        {
            if (!PlayerPrefs.HasKey(Car)) return;
            r.selectedCar = Mathf.Clamp(PlayerPrefs.GetInt(Car), 0, r.cars.Length - 1);
            r.mode = (RaceMode)Mathf.Clamp(PlayerPrefs.GetInt(Mode), 0, 1);
            r.rivalSkill = (RivalSkill)Mathf.Clamp(PlayerPrefs.GetInt(Skill), 0, 2);
            r.manualGearbox = PlayerPrefs.GetInt(Manual) == 1;
            r.useMph = PlayerPrefs.GetInt(Mph) == 1;
        }

        public static void Save(RaceManager r)
        {
            PlayerPrefs.SetInt(Car, r.selectedCar);
            PlayerPrefs.SetInt(Mode, (int)r.mode);
            PlayerPrefs.SetInt(Skill, (int)r.rivalSkill);
            PlayerPrefs.SetInt(Manual, r.manualGearbox ? 1 : 0);
            PlayerPrefs.SetInt(Mph, r.useMph ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}

namespace Kurogane.Race
{
    /// <summary>
    /// Lo que pasa de una escena a otra al cargar con <see cref="SceneLoader"/>. Las opciones (pista, sentido, coche…)
    /// van en PlayerPrefs con <see cref="RaceSettings"/>; aquí solo queda cómo se llegó a la escena.
    /// </summary>
    public static class GameSession
    {
        public const string StartScene = "Inicio";

        /// <summary>La pista se cargó desde la escena Inicio: el menú de la pista vuelve a Inicio.</summary>
        public static bool FromStartScene;
        /// <summary>Al cargar la pista se empieza la carrera directamente (sin menú).</summary>
        public static bool StartRaceOnLoad;
        /// <summary>Se pidió el benchmark en la escena Inicio: lo corre la primera pista.</summary>
        public static bool Benchmark;
    }
}

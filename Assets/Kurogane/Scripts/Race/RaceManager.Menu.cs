using System.Collections.Generic;
using System.Linq;
using Kurogane.Cars;
using Kurogane.Controls;
using Kurogane.Sound;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Kurogane.Race
{
    /// <summary>Nota final de la bajada: suma de tiempo, agua, estilo, limpieza y resultado de la batalla.</summary>
    public struct RaceGrade
    {
        public int time, water, style, clean, battle, total;
        public string letter;
    }

    public enum MenuRow
    {
        Coche, Modo, Rival, Caja, Clima, Opciones, Empezar,
        Velocidad, Filtro, Sonido, Musica, Tactil, Resolucion, Pantalla, Vsync, Calidad, Fps, Volver
    }

    public enum TouchMode { Auto, Si, No }

    /// <summary>
    /// Lógica del menú (página de carrera y página de opciones), la pausa, las opciones de pantalla y la nota final.
    /// Teclado y mando navegan aquí; <see cref="Kurogane.Hud.MenuView"/> lo dibuja y llama a estos métodos al
    /// pulsar con el ratón o el dedo.
    /// </summary>
    public partial class RaceManager
    {
        [Header("Controles y pantalla")]
        public TouchMode touchControls = TouchMode.Auto;
        public bool vSync = true;
        [Tooltip("Nivel de calidad elegido en el menú; -1 deja el de Project Settings.")]
        public int qualityLevel = -1;
        public FullScreenMode screenMode = FullScreenMode.FullScreenWindow;
        [Tooltip("Resolución elegida en el menú; (0, 0) deja la actual.")]
        public Vector2Int resolution;
        [Tooltip("Muestra los fps y el tiempo por cuadro en el HUD, para medir el rendimiento.")]
        public bool showFps;

        static readonly MenuRow[] RacePage = { MenuRow.Coche, MenuRow.Modo, MenuRow.Rival, MenuRow.Caja, MenuRow.Clima, MenuRow.Opciones, MenuRow.Empezar };
        static readonly string[] WeatherNames = { "Despejado", "Amanecer", "Atardecer", "Niebla", "Lluvia", "Al azar" };
        static readonly MenuRow[] OptionsPageDesktop =
        {
            MenuRow.Velocidad, MenuRow.Filtro, MenuRow.Sonido, MenuRow.Musica, MenuRow.Tactil,
            MenuRow.Resolucion, MenuRow.Pantalla, MenuRow.Vsync, MenuRow.Calidad, MenuRow.Fps, MenuRow.Volver
        };
        static readonly MenuRow[] OptionsPageMobile =
        {
            MenuRow.Velocidad, MenuRow.Filtro, MenuRow.Sonido, MenuRow.Musica, MenuRow.Tactil, MenuRow.Calidad, MenuRow.Fps, MenuRow.Volver
        };
        public static readonly string[] PauseLabels = { "Continuar", "Reiniciar bajada", "Menú principal" };
        static readonly string[] SkillNames = { "Fácil", "Normal", "Leyenda" };

        public RaceGrade Result { get; private set; }
        public bool InOptions { get; private set; }
        public int MenuIndex => menuRow;
        public int PauseIndex => pauseRow;
        public IReadOnlyList<MenuRow> MenuRows => InOptions ? (Application.isMobilePlatform ? OptionsPageMobile : OptionsPageDesktop) : RacePage;

        int menuRow, pauseRow;
        Light showroomLight;
        Vector2Int[] resolutions;

        // ---------- Menú principal y garaje ----------

        public void OpenMenu()
        {
            Time.timeScale = 1;
            ResetArrival();
            State = RaceState.Menu;
            InOptions = false;
            menuRow = System.Array.IndexOf(RacePage, MenuRow.Empezar);
            rival.gameObject.SetActive(false);
            player.rival = null;
            PreviewCar();
        }

        /// <summary>Pone el coche elegido en la salida, parado, con la cámara de garaje girando alrededor.</summary>
        void PreviewCar()
        {
            var ps = PlayerSpec;
            player.Init(track.Data, ps, 5, 0);
            player.ControlsEnabled = false;
            player.AutoBrake = false;
            player.GetComponent<CarVisual>().Build(ps);
            chaseCamera.target = player;
            chaseCamera.showroom = true;
            chaseCamera.Snap();
            if (retro != null) retro.ResetHistory();
            SetShowroomLight(true);
            if (weatherSystem != null) weatherSystem.RefreshCars(); // faros y conos del coche recién construido
            best = LoadBest();
        }

        /// <summary>Luz cálida encima del coche mientras está en el garaje.</summary>
        void SetShowroomLight(bool on)
        {
            if (showroomLight == null)
            {
                if (!on) return;
                showroomLight = new GameObject("Luz del garaje").AddComponent<Light>();
                showroomLight.type = LightType.Point;
                showroomLight.color = new Color(1f, 0.86f, 0.7f);
                showroomLight.range = 14;
                showroomLight.intensity = 2.2f;
            }
            showroomLight.enabled = on;
            if (on) showroomLight.transform.position = player.transform.position + Vector3.up * 4.5f + player.transform.forward * 1.5f;
        }

        void MenuInput()
        {
            if (DriverInput.MenuUp) MoveRow(-1);
            if (DriverInput.MenuDown) MoveRow(1);
            if (DriverInput.MenuLeft) Change(MenuRows[menuRow], -1);
            if (DriverInput.MenuRight) Change(MenuRows[menuRow], 1);
            if (!InOptions && DriverInput.NextCar) Change(MenuRow.Coche, 1);
            if (DriverInput.Confirm) Activate(MenuRows[menuRow]);
            else if (InOptions && DriverInput.Pause) Activate(MenuRow.Volver);
        }

        public bool RowEnabled(MenuRow row) => row != MenuRow.Rival || mode == RaceMode.Batalla;
        public static bool IsAction(MenuRow row) => row == MenuRow.Opciones || row == MenuRow.Empezar || row == MenuRow.Volver;

        void MoveRow(int dir)
        {
            var rows = MenuRows;
            do menuRow = (menuRow + dir + rows.Count) % rows.Count;
            while (!RowEnabled(rows[menuRow]));
        }

        /// <summary>Clic o toque en una fila: la selecciona y, si es un botón, lo pulsa.</summary>
        public void TapRow(int index)
        {
            var rows = MenuRows;
            if (index < 0 || index >= rows.Count || !RowEnabled(rows[index])) return;
            menuRow = index;
            if (IsAction(rows[index])) Activate(rows[index]);
        }

        /// <summary>Clic o toque en las flechas ◀ ▶ de una fila.</summary>
        public void ChangeRow(int index, int dir)
        {
            var rows = MenuRows;
            if (index < 0 || index >= rows.Count || !RowEnabled(rows[index])) return;
            menuRow = index;
            Change(rows[index], dir);
        }

        void Activate(MenuRow row)
        {
            switch (row)
            {
                case MenuRow.Opciones:
                    InOptions = true;
                    menuRow = 0;
                    break;
                case MenuRow.Volver:
                    InOptions = false;
                    menuRow = System.Array.IndexOf(RacePage, MenuRow.Opciones);
                    RaceSettings.Save(this);
                    break;
                case MenuRow.Empezar:
                    BeginRace();
                    break;
                default:
                    // En la página de carrera, Enter arranca desde cualquier fila; en opciones cambia el valor
                    if (InOptions) Change(row, 1);
                    else BeginRace();
                    break;
            }
        }

        void Change(MenuRow row, int dir)
        {
            switch (row)
            {
                case MenuRow.Coche:
                    selectedCar = (selectedCar + dir + cars.Length) % cars.Length;
                    PreviewCar();
                    break;
                case MenuRow.Modo:
                    mode = mode == RaceMode.Batalla ? RaceMode.Contrarreloj : RaceMode.Batalla;
                    best = LoadBest();
                    break;
                case MenuRow.Rival: rivalSkill = (RivalSkill)(((int)rivalSkill + dir + 3) % 3); break;
                case MenuRow.Caja: manualGearbox = !manualGearbox; break;
                case MenuRow.Clima:
                    weather = (weather + dir + WeatherNames.Length) % WeatherNames.Length;
                    ApplyWeather(true);
                    break;
                case MenuRow.Velocidad: useMph = !useMph; break;
                case MenuRow.Filtro: retroFilter = !retroFilter; break;
                case MenuRow.Sonido: soundVolume = Mathf.Clamp(soundVolume + dir, 0, 10); break;
                case MenuRow.Musica: musicVolume = Mathf.Clamp(musicVolume + dir, 0, 10); break;
                case MenuRow.Tactil: touchControls = (TouchMode)(((int)touchControls + dir + 3) % 3); break;
                case MenuRow.Resolucion: StepResolution(dir); ApplyDisplay(true); break;
                case MenuRow.Pantalla:
                    screenMode = screenMode == FullScreenMode.Windowed ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                    ApplyDisplay(true);
                    break;
                case MenuRow.Vsync: vSync = !vSync; ApplyDisplay(false); break;
                case MenuRow.Calidad:
                    int n = QualitySettings.names.Length;
                    qualityLevel = ((qualityLevel < 0 ? QualitySettings.GetQualityLevel() : qualityLevel) + dir + n) % n;
                    ApplyDisplay(false);
                    break;
                case MenuRow.Fps: showFps = !showFps; break;
                default: return;
            }
            ApplyOptions();
        }

        void BeginRace()
        {
            RaceSettings.Save(this);
            StartRace();
        }

        public void NextCarAndRestart()
        {
            selectedCar = (selectedCar + 1) % cars.Length;
            RaceSettings.Save(this);
            StartRace();
        }

        public static string RowLabel(MenuRow row)
        {
            switch (row)
            {
                case MenuRow.Opciones: return "OPCIONES";
                case MenuRow.Empezar: return "EMPEZAR BAJADA";
                case MenuRow.Volver: return "VOLVER";
                case MenuRow.Filtro: return "Filtro PS2";
                case MenuRow.Musica: return "Música";
                case MenuRow.Tactil: return "Táctil";
                case MenuRow.Resolucion: return "Resolución";
                case MenuRow.Vsync: return "VSync";
                case MenuRow.Fps: return "Ver FPS";
                default: return row.ToString();
            }
        }

        public string RowValue(MenuRow row)
        {
            switch (row)
            {
                case MenuRow.Coche: return PlayerSpec.displayName;
                case MenuRow.Modo: return mode == RaceMode.Batalla ? "Batalla" : "Contrarreloj";
                case MenuRow.Rival: return mode == RaceMode.Batalla ? SkillNames[(int)rivalSkill] : "—";
                case MenuRow.Caja: return manualGearbox ? "Manual (Q / E)" : "Automática";
                case MenuRow.Clima: return WeatherNames[Mathf.Clamp(weather, 0, WeatherNames.Length - 1)];
                case MenuRow.Velocidad: return useMph ? "mph" : "km/h";
                case MenuRow.Filtro: return retro == null ? "No disponible" : retroFilter ? "Sí" : "No";
                case MenuRow.Sonido: return Bar(soundVolume);
                case MenuRow.Musica: return music == null || music.tracks == null || music.tracks.Length == 0 ? Bar(musicVolume) + "  (sin pistas)" : Bar(musicVolume);
                case MenuRow.Tactil: return touchControls == TouchMode.Auto ? $"Auto ({(TouchDevice ? "sí" : "no")})" : touchControls == TouchMode.Si ? "Sí" : "No";
                case MenuRow.Resolucion:
                    var r = resolution == Vector2Int.zero ? new Vector2Int(Screen.width, Screen.height) : resolution;
                    return $"{r.x} × {r.y}";
                case MenuRow.Pantalla: return screenMode == FullScreenMode.Windowed ? "Ventana" : "Pantalla completa";
                case MenuRow.Vsync: return vSync ? "Sí" : "No (60 fps)";
                case MenuRow.Calidad:
                    var names = QualitySettings.names;
                    int q = qualityLevel < 0 ? QualitySettings.GetQualityLevel() : qualityLevel;
                    return q >= 0 && q < names.Length ? names[q] : "—";
                case MenuRow.Fps: return showFps ? "Sí" : "No";
                default: return "";
            }
        }

        static string Bar(int value) => new string('■', value) + new string('□', 10 - value);

        // ---------- Opciones ----------

        /// <summary>Aplica filtro y volúmenes; se llama al cargar y cada vez que cambian en el menú.</summary>
        void ApplyOptions()
        {
            if (retro != null) retro.enabled = retroFilter;
            CarAudio.MasterVolume = soundVolume / 10f;
            if (music != null) music.volume = musicVolume / 10f;
        }

        /// <summary>Aplica VSync, calidad y, si se pide, resolución y modo de pantalla (solo en PC).</summary>
        void ApplyDisplay(bool resolutionChanged)
        {
            // En móvil el VSync no se controla: se fija 60 fps (Unity limita a 30 por defecto en Android)
            QualitySettings.vSyncCount = vSync ? 1 : 0;
            Application.targetFrameRate = Application.isMobilePlatform || !vSync ? 60 : -1;
            if (qualityLevel >= 0 && qualityLevel < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != qualityLevel)
                QualitySettings.SetQualityLevel(qualityLevel, true);
            if (!resolutionChanged || Application.isMobilePlatform) return;
            var r = resolution == Vector2Int.zero ? new Vector2Int(Screen.width, Screen.height) : resolution;
            if (r.x != Screen.width || r.y != Screen.height || Screen.fullScreenMode != screenMode)
                Screen.SetResolution(r.x, r.y, screenMode);
        }

        void StepResolution(int dir)
        {
            if (resolutions == null)
                resolutions = Screen.resolutions.Select(x => new Vector2Int(x.width, x.height)).Distinct()
                    .OrderBy(v => v.x * v.y).ToArray();
            if (resolutions.Length == 0) return;
            var current = resolution == Vector2Int.zero ? new Vector2Int(Screen.width, Screen.height) : resolution;
            int i = System.Array.IndexOf(resolutions, current);
            if (i < 0) i = resolutions.Count(v => v.x * v.y < current.x * current.y) - (dir > 0 ? 1 : 0);
            resolution = resolutions[Mathf.Clamp(i + dir, 0, resolutions.Length - 1)];
        }

        static bool TouchDevice
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Application.isMobilePlatform || Touchscreen.current != null;
#else
                return Application.isMobilePlatform || UnityEngine.Input.touchSupported;
#endif
            }
        }

        /// <summary>Si se muestran los controles táctiles (solo durante la bajada).</summary>
        public bool TouchVisible => touchControls == TouchMode.Si || (touchControls == TouchMode.Auto && TouchDevice);

        // ---------- Pausa ----------

        void PauseInput()
        {
            if (DriverInput.Pause) { Resume(); return; }
            if (DriverInput.MenuUp) pauseRow = (pauseRow + PauseLabels.Length - 1) % PauseLabels.Length;
            if (DriverInput.MenuDown) pauseRow = (pauseRow + 1) % PauseLabels.Length;
            if (DriverInput.Confirm) PauseAction(pauseRow);
        }

        public void PauseAction(int i)
        {
            pauseRow = i;
            if (i == 0) Resume();
            else if (i == 1) StartRace();
            else OpenMenu();
        }

        void Resume()
        {
            State = stateBeforePause;
            Time.timeScale = 1;
        }

        // ---------- Nota final ----------

        RaceGrade Grade()
        {
            var g = new RaceGrade
            {
                time = Mathf.Clamp(Mathf.RoundToInt(30 + (parTime - RaceTime)), 0, 40),
                water = Mathf.RoundToInt(Water * 0.25f),
                style = Mathf.RoundToInt(Mathf.Clamp01(Score / (float)driftPar) * 15) + Mathf.Min(5, gutterRuns),
                clean = Mathf.Max(0, 15 - hits * 3),
                battle = mode == RaceMode.Batalla ? (won ? 10 : -10) : 0,
            };
            g.total = Mathf.Clamp(g.time + g.water + g.style + g.clean + g.battle, 0, 100);
            g.letter = g.total >= 90 ? "S" : g.total >= 75 ? "A" : g.total >= 60 ? "B" : "C";
            return g;
        }
    }
}

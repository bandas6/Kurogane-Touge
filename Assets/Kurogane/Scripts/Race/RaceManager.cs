using System.Collections.Generic;
using System.IO;
using Kurogane.Atmosphere;
using Kurogane.CameraRig;
using Kurogane.Cars;
using Kurogane.Controls;
using Kurogane.Hud;
using Kurogane.Retro;
using Kurogane.Sound;
using Kurogane.Track;
using UnityEngine;

namespace Kurogane.Race
{
    public enum RaceMode { Batalla, Contrarreloj }
    public enum RaceState { Countdown, Racing, Finished, Paused, Menu }

    /// <summary>
    /// Flujo de la bajada: cuenta atrás, cronómetro, vaso de agua, puntos de drift, canaleta, sectores,
    /// llegada y récords. La lógica del menú, la pausa, las opciones y la nota final está en RaceManager.Menu.cs;
    /// la interfaz que lo dibuja está en Kurogane.Hud (HudView, MenuView y TouchControls), que se añaden solas.
    /// </summary>
    public partial class RaceManager : MonoBehaviour
    {
        [Header("Escena")]
        [Tooltip("Escena Inicio: solo menú y garaje, sin pista. Empezar carga la escena de la pista elegida.")]
        public bool frontEnd;
        [Tooltip("Garaje de la escena Inicio (donde se aparca el coche en el menú).")]
        public Garage garage;
        public TrackBuilder track;
        public CarController player;
        public RivalAI rival;
        public ChaseCamera chaseCamera;

        [Header("Coches")]
        public CarSpec[] cars;
        public int selectedCar;

        [Header("Pista")]
        [Tooltip("Pista elegida en el menú (id de su TrackDefinition). En la escena de una pista, al arrancar es la suya.")]
        public string trackId = "";
        public TrackDirection direction = TrackDirection.Bajada;

        [Header("Opciones")]
        public RaceMode mode = RaceMode.Batalla;
        public RivalSkill rivalSkill = RivalSkill.Normal;
        public bool manualGearbox;
        public bool useMph;
        [Tooltip("Empieza en el menú de coche y opciones. Si está apagado, arranca la bajada directamente con estos valores.")]
        public bool showMenuOnStart = true;

        [Header("Pantalla y sonido")]
        public PS2Filter retro;
        public MusicPlayer music;
        public bool retroFilter = true;
        [Range(0, 10)] public int soundVolume = 8;
        [Range(0, 10)] public int musicVolume = 6;

        [Header("Clima")]
        [Tooltip("0 Despejado, 1 Amanecer, 2 Atardecer, 3 Niebla, 4 Lluvia, 5 Al azar (cambia en cada bajada), 6 Según la pista.")]
        [Range(0, 6)] public int weather;
        WeatherSystem weatherSystem;
        WeatherKind randomWeather;

        [Header("Tráfico")]
        [Tooltip("0 Auto (apagado en Batalla, encendido en Contrarreloj), 1 Sí, 2 No.")]
        [Range(0, 2)] public int traffic;
        TrafficManager trafficManager;
        bool MountainTraffic => traffic == 1 || (traffic == 0 && mode == RaceMode.Contrarreloj);

        [Header("Nota final")]
        [Tooltip("Puntos de drift que dan los 15 puntos de estilo. El tiempo de referencia es de cada pista (TrackDefinition.parTime).")]
        public int driftPar = 8000;

        public RaceState State { get; private set; }
        public float RaceTime { get; private set; }
        public float Water { get; private set; } = 100;
        public int Score { get; private set; }

        float countdown, chain, chainTimer, linkTimer, gutterTime, finishTimer, toastTimer, msgTimer, splitTimer;
        int multiplier = 1, sector, hits, gutterRuns;
        bool won, passed;
        string toast = "", bigMessage = "", splitText = "";
        readonly List<float> splits = new List<float>();
        BestRecord best;
        RaceState stateBeforePause;

        [System.Serializable]
        class BestRecord
        {
            public float time = float.PositiveInfinity;
            public List<float> splits = new List<float>();
        }

        public CarSpec PlayerSpec => cars[Mathf.Clamp(selectedCar, 0, cars.Length - 1)];
        CarSpec RivalSpec
        {
            get
            {
                // el rival es el Coupé, salvo que el jugador lleve el Coupé: entonces es el Sedán
                foreach (var c in cars) if (c.model == (PlayerSpec.model == CarModel.Coupe ? CarModel.Sedan : CarModel.Coupe)) return c;
                return cars[0];
            }
        }

        void Awake()
        {
            if (cars == null || cars.Length == 0) cars = new[] { CarSpec.CreatePanda(), CarSpec.CreateCoupe(), CarSpec.CreateSedan() };
            if (showMenuOnStart || frontEnd) RaceSettings.Load(this);
            ApplyOptions();
            ApplyDisplay(true);
            Ensure<HudView>();
            Ensure<MenuView>();
            Ensure<TouchControls>();
            // Benchmark para el móvil (ver Benchmark.cs): desde Inicio lo corre la primera pista, de bajada
            bool bench = GameSession.Benchmark || Benchmark.Requested();
            GameSession.Benchmark = false;
            if (frontEnd)
            {
                if (SelectedTrack != null) trackId = SelectedTrack.Id;
                if (bench)
                {
                    GameSession.Benchmark = true;
                    direction = TrackDirection.Bajada;
                    LoadSelectedTrack();
                }
                return;
            }

            // La escena es de una pista: la elegida pasa a ser la suya, en el sentido elegido si lo permite
            track.Build();
            trackId = track.definition.Id;
            if (bench) direction = TrackDirection.Bajada;
            ApplyDirection();
            // Escenas creadas antes del cielo (bloque A) no lo traen: se añade uno al arrancar
            if (FindAnyObjectByType<SkyDome>() == null) new GameObject("Cielo").AddComponent<SkyDome>();
            Ensure<WeatherSystem>();
            weatherSystem = GetComponent<WeatherSystem>();
            weatherSystem.Init(track, chaseCamera, retro, player, rival);
            ApplyWeather(true);

            Ensure<TrafficManager>();
            trafficManager = GetComponent<TrafficManager>();
            trafficManager.Init(player, track, () => State == RaceState.Racing);
            // El paso a nivel no se cierra mientras el jugador cruza el pueblo en la llegada (subiendo no lo cruza)
            var crossing = track.Root != null ? track.Root.GetComponentInChildren<Kurogane.City.CrossingController>(true) : null;
            if (crossing != null)
                crossing.allowed = () => track.Data.Uphill || player.Index < track.Data.FinishIndex - 40 || player.Speed < 1 || State == RaceState.Menu;

            if (bench) gameObject.AddComponent<Benchmark>();
            player.WallHit += OnWallHit;
            player.RivalContact += v => { Water = Mathf.Max(0, Water - v * 0.8f); chaseCamera.Shake(0.4f); };
        }

        /// <summary>Aplica el clima elegido; con "Al azar", <paramref name="reroll"/> sortea uno nuevo.</summary>
        void ApplyWeather(bool reroll)
        {
            if (weatherSystem == null) return;
            if (weather == 6) weatherSystem.Apply(track.Theme != null ? track.Theme.defaultWeather : WeatherKind.Despejado);
            else if (weather >= 5)
            {
                if (reroll) randomWeather = (WeatherKind)Random.Range(0, 5);
                weatherSystem.Apply(randomWeather);
            }
            else weatherSystem.Apply((WeatherKind)Mathf.Clamp(weather, 0, 4));
        }

        void Ensure<T>() where T : Component
        {
            if (GetComponent<T>() == null) gameObject.AddComponent<T>();
        }

        void Start()
        {
            if (frontEnd) { OpenMenu(); return; }
            // Cargada desde el menú (Inicio u otra pista): se empieza ya
            if (GameSession.StartRaceOnLoad) { GameSession.StartRaceOnLoad = false; StartRace(); }
            else if (showMenuOnStart) OpenMenu();
            else StartRace();
        }

        /// <summary>Pone la pista en el sentido elegido (subida solo si la pista lo permite).</summary>
        void ApplyDirection()
        {
            if (track == null || track.definition == null) return;
            if (!track.definition.allowUphill) direction = TrackDirection.Bajada;
            track.Direction = direction;
            track.ApplyDirection();
        }

        public void StartRace()
        {
            if (frontEnd) { LoadSelectedTrack(); return; }
            Time.timeScale = 1;
            var data = track.Data;
            var ps = PlayerSpec;
            player.Init(data, ps, data.RaceStart, -2.2f);
            player.manualGearbox = manualGearbox;
            player.ControlsEnabled = false;
            player.AutoBrake = false;
            player.GetComponent<CarVisual>().Build(ps);

            bool battle = mode == RaceMode.Batalla;
            rival.gameObject.SetActive(battle);
            player.rival = battle ? rival : null;
            if (battle)
            {
                rival.skill = rivalSkill;
                rival.Player = player;
                rival.Clock = () => RaceTime;
                rival.Init(data, RivalSpec, data.RaceStart * data.Step + 18);
                rival.Running = false;
                rival.GetComponent<CarVisual>().Build(RivalSpec);
            }

            ResetArrival();
            ApplyWeather(true);
            if (trafficManager != null)
            {
                trafficManager.mountainTraffic = MountainTraffic;
                trafficManager.ResetMountain();
            }
            chaseCamera.target = player;
            chaseCamera.showroom = false;
            chaseCamera.Snap();
            if (retro != null) retro.ResetHistory();
            SetShowroomLight(false);

            State = RaceState.Countdown;
            countdown = 3.6f;
            RaceTime = 0;
            Water = 100;
            Score = 0;
            chain = chainTimer = linkTimer = gutterTime = finishTimer = 0;
            multiplier = 1;
            sector = hits = gutterRuns = 0;
            won = passed = false;
            splits.Clear();
            best = LoadBest();
            Show("", 0);
        }

        void Update()
        {
            if (SceneLoader.Busy) return;
            if (State == RaceState.Menu) { MenuInput(); return; }
            if (State == RaceState.Paused) { PauseInput(); return; }
            // El estado va primero: leer Pause consume la pulsación táctil
            if ((State == RaceState.Racing || State == RaceState.Countdown) && DriverInput.Pause)
            {
                stateBeforePause = State;
                State = RaceState.Paused;
                pauseRow = 0;
                Time.timeScale = 0;
                return;
            }
            if (DriverInput.CycleCamera) chaseCamera.Cycle();

            float dt = Time.deltaTime;
            toastTimer -= dt;
            msgTimer -= dt;
            splitTimer -= dt;

            switch (State)
            {
                case RaceState.Countdown:
                    int before = Mathf.CeilToInt(countdown);
                    countdown -= dt;
                    int after = Mathf.CeilToInt(countdown);
                    if (after != before && after > 0 && after <= 3) Big(after.ToString());
                    if (countdown <= 0)
                    {
                        State = RaceState.Racing;
                        player.ControlsEnabled = true;
                        if (rival.isActiveAndEnabled) rival.Running = true;
                        Big("GO!");
                        if (DriverInput.Gas > 0) Show("SALIDA PERFECTA");
                    }
                    break;

                case RaceState.Racing:
                    RaceTime += dt;
                    if (DriverInput.Respawn) { player.Respawn(); chain = 0; multiplier = 1; Show("DE VUELTA EN LA PISTA"); }
                    Scoring(dt);
                    if (player.Index >= track.Data.FinishIndex) Finish();
                    break;

                case RaceState.Finished:
                    finishTimer += dt;
                    if (!ArrivalDone)
                    {
                        UpdateArrival(dt);
                        if (DriverInput.Confirm) SkipArrival();
                    }
                    else if (DriverInput.Confirm) StartRace();
                    else if (DriverInput.NextCar) NextCarAndRestart();
                    else if (DriverInput.Pause) OpenMenu();
                    break;
            }
            // Cerca del pueblo se alarga el plano lejano para ver la ciudad del valle
            chaseCamera.farBlend = track.Data.TownView(player.Index);
        }

        void Scoring(float dt)
        {
            float sp = player.Speed;
            // Vaso de agua: se derrama con fuerzas G por encima de ~1 g
            float gf = Mathf.Sqrt(player.LateralAccel * player.LateralAccel + Mathf.Pow(player.LongitudinalAccel * 0.8f, 2)) / 9.8f;
            if (gf > 1.05f) Water = Mathf.Max(0, Water - (gf - 1.05f) * 22 * dt);

            // Drift encadenado con multiplicador
            if (Mathf.Abs(player.Slip) > 0.22f && sp > 11)
            {
                if (chain == 0 && linkTimer > 0) multiplier = Mathf.Min(5, multiplier + 1);
                chain += sp * Mathf.Abs(player.Slip) * dt * 14;
                chainTimer = 0.9f;
            }
            else if (chainTimer > 0)
            {
                chainTimer -= dt;
                if (chainTimer <= 0 && chain > 0)
                {
                    int pts = Mathf.RoundToInt(chain * multiplier);
                    Score += pts;
                    if (pts > 300) Show($"{Tier(pts)} +{pts}");
                    chain = 0;
                    linkTimer = 1.6f;
                }
            }
            else if (linkTimer > 0) { linkTimer -= dt; if (linkTimer <= 0) multiplier = 1; }

            // Canaleta (溝落とし)
            if (player.InGutter) gutterTime += dt;
            else if (gutterTime > 0)
            {
                if (gutterTime > 0.5f) { Score += 600; gutterRuns++; Big("溝落とし"); Show("MIZO OTOSHI · +600"); }
                gutterTime = 0;
            }

            // Sectores contra tu mejor bajada
            var sectors = track.Data.Sectors;
            if (sector < 3 && player.Index >= sectors[sector])
            {
                splits.Add(RaceTime);
                splitText = $"S{sector + 1}  {Fmt(RaceTime)}";
                if (best.splits.Count > sector)
                {
                    float diff = RaceTime - best.splits[sector];
                    splitText += (diff <= 0 ? "  −" : "  +") + Mathf.Abs(diff).ToString("0.00");
                }
                splitTimer = 3.5f;
                sector++;
            }

            // Adelantamientos
            if (rival.isActiveAndEnabled)
            {
                bool first = player.Index * track.Data.Step > rival.Distance;
                if (first && !passed && RaceTime > 3) { passed = true; Big("抜いた!"); Show("ADELANTASTE AL " + RivalSpec.displayName.ToUpper()); }
                if (!first && passed) { passed = false; Show("TE ADELANTARON"); }
            }
        }

        void OnWallHit(float impact, Vector3 point)
        {
            chaseCamera.Shake(impact * 0.08f);
            Water = Mathf.Max(0, Water - impact * 0.9f);
            hits++;
            if (chain > 0) { chain = 0; chainTimer = 0; multiplier = 1; Show("CHOQUE · DRIFT PERDIDO"); }
        }

        void Finish()
        {
            State = RaceState.Finished;
            Score += Mathf.RoundToInt(chain * multiplier);
            chain = 0;
            won = mode == RaceMode.Contrarreloj || !rival.Finished;
            BeginArrival();
            Result = Grade();
            Big("GOAL");
            if (RaceTime < best.time)
            {
                best.time = RaceTime;
                best.splits = new List<float>(splits);
                SaveBest(best);
                Show("¡NUEVO RÉCORD!");
            }
        }

        // ---------- Récords (JSON en persistentDataPath) ----------

        /// <summary>
        /// Un récord por pista, sentido, modo y coche. La bajada del Monte Kurogane conserva el nombre de antes de
        /// las pistas, así sus récords siguen valiendo.
        /// </summary>
        string BestPath
        {
            get
            {
                var def = SelectedTrack;
                string id = def != null ? def.Id : "MonteKurogane";
                var dir = EffectiveDirection;
                string file = id == "MonteKurogane" && dir == TrackDirection.Bajada
                    ? $"best_{mode}_{PlayerSpec.model}.json"
                    : $"best_{id}_{dir}_{mode}_{PlayerSpec.model}.json";
                return Path.Combine(Application.persistentDataPath, file);
            }
        }

        BestRecord LoadBest()
        {
            try
            {
                if (File.Exists(BestPath)) return JsonUtility.FromJson<BestRecord>(File.ReadAllText(BestPath)) ?? new BestRecord();
            }
            catch (System.Exception e) { Debug.LogWarning("No se pudo leer el récord: " + e.Message); }
            return new BestRecord();
        }

        void SaveBest(BestRecord r)
        {
            try { File.WriteAllText(BestPath, JsonUtility.ToJson(r)); }
            catch (System.Exception e) { Debug.LogWarning("No se pudo guardar el récord: " + e.Message); }
        }

        // ---------- Datos para la interfaz (Kurogane.Hud) ----------

        public static string Tier(float c) => c > 4000 ? "LEGEND" : c > 2000 ? "AWESOME" : c > 900 ? "GREAT" : c > 300 ? "NICE" : "DRIFT";

        public static string Fmt(float t)
        {
            if (float.IsInfinity(t)) return "—";
            int m = (int)(t / 60);
            float s = t % 60;
            return $"{m}'{(int)s:00}\"{(int)((s % 1) * 100):00}";
        }

        void Show(string text, float time = 1.6f) { toast = text; toastTimer = time; }
        void Big(string text) { bigMessage = text; msgTimer = 0.9f; }

        public float BestTime => best.time;
        public string SplitText => splitTimer > 0 ? splitText : null;
        public string Toast => toastTimer > 0 ? toast : null;
        public string BigMessage => msgTimer > 0 ? bigMessage : null;
        public float Chain => chain;
        public int Multiplier => multiplier;
        public bool Won => won;
        public int Hits => hits;
        public int GutterRuns => gutterRuns;
        public bool HasRival => rival.isActiveAndEnabled;
        public bool PlayerAhead => State == RaceState.Finished ? won : player.Index * track.Data.Step > rival.Distance;
        public float MetersToGoal => Mathf.Max(0, track.Data.TimedLength - player.Index * track.Data.Step);
        public bool ShowResults => State == RaceState.Finished && ArrivalDone && finishTimer > 1.5f;
        public CarSpec NextCarSpec => cars[(selectedCar + 1) % cars.Length];
    }
}
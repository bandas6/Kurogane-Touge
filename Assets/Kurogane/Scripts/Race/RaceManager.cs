using System.Collections.Generic;
using System.IO;
using Kurogane.CameraRig;
using Kurogane.Cars;
using Kurogane.Controls;
using Kurogane.Track;
using UnityEngine;

namespace Kurogane.Race
{
    public enum RaceMode { Batalla, Contrarreloj }
    public enum RaceState { Countdown, Racing, Finished, Paused, Menu }

    /// <summary>
    /// Flujo de la bajada: cuenta atrás, cronómetro, vaso de agua, puntos de drift, canaleta, sectores,
    /// llegada y récords. El HUD de la Fase 1 es IMGUI provisional; la interfaz definitiva llega en la Fase 5.
    /// El menú, la pausa, la caja en pantalla y la nota final están en RaceManager.Menu.cs.
    /// </summary>
    public partial class RaceManager : MonoBehaviour
    {
        [Header("Escena")]
        public TrackBuilder track;
        public CarController player;
        public RivalAI rival;
        public ChaseCamera chaseCamera;

        [Header("Coches")]
        public CarSpec[] cars;
        public int selectedCar;

        [Header("Opciones")]
        public RaceMode mode = RaceMode.Batalla;
        public RivalSkill rivalSkill = RivalSkill.Normal;
        public bool manualGearbox;
        public bool useMph;
        [Tooltip("Empieza en el menú de coche y opciones. Si está apagado, arranca la bajada directamente con estos valores.")]
        public bool showMenuOnStart = true;

        [Header("Nota final")]
        [Tooltip("Tiempo de referencia (s): bajar en este tiempo da 30 de los 40 puntos de tiempo; cada segundo menos suma 1.")]
        public float parTime = 120;
        [Tooltip("Puntos de drift que dan los 15 puntos de estilo.")]
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

        CarSpec PlayerSpec => cars[Mathf.Clamp(selectedCar, 0, cars.Length - 1)];
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
            if (showMenuOnStart) RaceSettings.Load(this);
            track.Build();
            player.WallHit += OnWallHit;
            player.RivalContact += v => { Water = Mathf.Max(0, Water - v * 0.8f); chaseCamera.Shake(0.4f); };
        }

        void Start()
        {
            if (showMenuOnStart) OpenMenu();
            else StartRace();
        }

        public void StartRace()
        {
            Time.timeScale = 1;
            var data = track.Data;
            var ps = PlayerSpec;
            player.Init(data, ps, 5, -2.2f);
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
                rival.Init(data, RivalSpec, 28);
                rival.Running = false;
                rival.GetComponent<CarVisual>().Build(RivalSpec);
            }

            chaseCamera.target = player;
            chaseCamera.Snap();

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
            if (State == RaceState.Menu) { MenuInput(); return; }
            if (State == RaceState.Paused) { PauseInput(); return; }
            if (DriverInput.Pause && (State == RaceState.Racing || State == RaceState.Countdown))
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
                    if (player.Index >= track.Data.N - 5) Finish();
                    break;

                case RaceState.Finished:
                    finishTimer += dt;
                    if (DriverInput.Confirm) StartRace();
                    if (DriverInput.NextCar) { selectedCar = (selectedCar + 1) % cars.Length; RaceSettings.Save(this); StartRace(); }
                    if (DriverInput.Pause) OpenMenu();
                    break;
            }
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
            player.ControlsEnabled = false;
            player.AutoBrake = true;
            won = mode == RaceMode.Contrarreloj || !rival.Finished;
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

        string BestPath => Path.Combine(Application.persistentDataPath, $"best_{mode}_{PlayerSpec.model}.json");

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

        // ---------- HUD provisional ----------

        static string Tier(float c) => c > 4000 ? "LEGEND" : c > 2000 ? "AWESOME" : c > 900 ? "GREAT" : c > 300 ? "NICE" : "DRIFT";

        static string Fmt(float t)
        {
            if (float.IsInfinity(t)) return "—";
            int m = (int)(t / 60);
            float s = t % 60;
            return $"{m}'{(int)s:00}\"{(int)((s % 1) * 100):00}";
        }

        void Show(string text, float time = 1.6f) { toast = text; toastTimer = time; }
        void Big(string text) { bigMessage = text; msgTimer = 0.9f; }

        GUIStyle big, mid, small, center, smallCenter, gearStyle;

        void OnGUI()
        {
            if (player == null || player.Track == null) return;
            float u = Screen.height / 720f;
            if (big == null)
            {
                big = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.BoldAndItalic, alignment = TextAnchor.MiddleCenter };
                mid = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                small = new GUIStyle(GUI.skin.label);
                center = new GUIStyle(mid) { alignment = TextAnchor.MiddleCenter };
                smallCenter = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
                gearStyle = new GUIStyle(big) { fontStyle = FontStyle.Bold };
            }
            big.fontSize = (int)(72 * u);
            mid.fontSize = (int)(30 * u);
            small.fontSize = (int)(16 * u);
            center.fontSize = (int)(26 * u);
            smallCenter.fontSize = (int)(16 * u);
            gearStyle.fontSize = (int)(56 * u);

            if (State == RaceState.Menu) { DrawMenu(u); return; }

            float x = 20 * u, y = 16 * u, w = Screen.width;
            GUI.Label(new Rect(x, y, 400 * u, 40 * u), Fmt(RaceTime), mid);
            GUI.Label(new Rect(x, y + 36 * u, 400 * u, 24 * u), "Récord " + Fmt(best.time), small);
            if (splitTimer > 0) GUI.Label(new Rect(x, y + 60 * u, 400 * u, 30 * u), splitText, small);
            if (rival.isActiveAndEnabled)
            {
                bool first = State == RaceState.Finished ? won : player.Index * track.Data.Step > rival.Distance;
                GUI.Label(new Rect(x, y + 90 * u, 400 * u, 40 * u), first ? "1º" : "2º", mid);
            }
            GUI.Label(new Rect(x, Screen.height - 60 * u, 400 * u, 40 * u), $"Vaso {Mathf.RoundToInt(Water)}%", mid);

            DrawGearbox(u);
            GUI.Label(new Rect(w - 320 * u, y, 300 * u, 30 * u), $"{(track.Data.Length - player.Index * track.Data.Step) / 1000f:0.00} km a meta", small);

            var flags = new List<string>();
            if (player.Slipstream) flags.Add("REBUFO");
            if (player.InGutter) flags.Add("溝 MIZO");
            if (player.RevLimiter) flags.Add("LÍMITE RPM");
            if (flags.Count > 0) GUI.Label(new Rect(0, Screen.height - 170 * u, w, 30 * u), string.Join("   ", flags), center);

            if (chain > 5) GUI.Label(new Rect(0, Screen.height * 0.2f, w, 80 * u),
                $"{Tier(chain * multiplier)}  {Mathf.RoundToInt(chain)}  x{multiplier}  ({Mathf.RoundToInt(Mathf.Abs(player.Slip) * Mathf.Rad2Deg)}°)", center);
            if (msgTimer > 0) GUI.Label(new Rect(0, Screen.height * 0.32f, w, 100 * u), bigMessage, big);
            if (toastTimer > 0) GUI.Label(new Rect(0, Screen.height * 0.62f, w, 30 * u), toast, center);

            if (State == RaceState.Paused) DrawPause(u);
            if (State == RaceState.Finished && finishTimer > 1.5f) DrawResults(u);
        }
    }
}

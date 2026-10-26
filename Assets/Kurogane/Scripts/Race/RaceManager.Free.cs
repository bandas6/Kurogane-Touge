using System.Collections.Generic;
using Kurogane.Atmosphere;
using Kurogane.Cars;
using Kurogane.City;
using Kurogane.Controls;
using Kurogane.Core;
using Kurogane.Track;
using UnityEngine;

namespace Kurogane.Race
{
    /// <summary>
    /// Modo libre (bloque H3): se empieza en la tienda de tofu y se recorre el pueblo y la montaña sin volver al menú,
    /// con la superficie del mundo.
    /// - Repartos: en la tienda se recoge un pedido para el hotel de la montaña o una casa del pueblo, con tiempo límite
    ///   y el vaso de agua; paga más cuanta más agua quede.
    /// - Desafíos: junto a los coches con las luces de emergencia de las horquillas empieza una batalla de bajada desde
    ///   ahí, con el rival y la llegada de siempre; después se sigue en el modo libre.
    /// - Contrarreloj libre: al pasar por la línea de salida (bajando) o por la de meta (subiendo) se cronometra solo.
    /// - Puntos de drift en las zonas marcadas del pueblo (explanada y aparcamiento del puerto).
    /// - Lugares descubiertos, viaje rápido desde el mapa de la pausa y ciclo opcional de hora y clima.
    /// El progreso se guarda en <see cref="FreeProfile"/>.
    /// </summary>
    public partial class RaceManager
    {
        /// <summary>En modo libre (fuera de los desafíos sigue siendo modo libre).</summary>
        public bool IsFree => mode == RaceMode.Libre && !frontEnd;
        /// <summary>Hay un desafío (batalla de bajada) en curso dentro del modo libre.</summary>
        public bool InFreeBattle { get; private set; }
        public FreeProfile Profile => profile;
        public TownMap Map => townMap;

        /// <summary>Acción disponible (se hace con Confirmar o el botón OK táctil); null si no hay ninguna.</summary>
        public string FreePrompt { get; private set; }
        /// <summary>Texto de la misión en curso (reparto o contrarreloj), o null.</summary>
        public string FreeMission { get; private set; }
        /// <summary>Destino de la misión, para el minimapa y la flecha.</summary>
        public Vector3? FreeTarget { get; private set; }
        public bool DeliveryActive => deliveryActive;
        /// <summary>Lugares a los que se puede viajar desde el mapa (los descubiertos).</summary>
        public List<TownMap.Place> Discovered { get; } = new List<TownMap.Place>();

        FreeProfile profile;
        TownMap townMap;
        System.Action promptAction;
        bool okTapped;

        bool deliveryActive, deliveryToHotel;
        Vector3 deliveryTarget;
        string deliveryName;
        float deliveryTime;
        GameObject beacon;

        bool trialActive, trialUp;
        float trialTime, trialIdle;
        int trialBest;

        float zoneChain, zoneTimer;
        string challengeKey;

        static readonly string[] Surnames = { "Tanaka", "Sato", "Kobayashi", "Mori", "Nakamura", "Ishii", "Kimura", "Hayashi", "Ono", "Shimizu" };

        /// <summary>Botón OK de los controles táctiles: hace la acción disponible.</summary>
        public void TapOk() => okTapped = true;

        // ---------- Empezar y volver ----------

        void StartFree()
        {
            Time.timeScale = 1;
            InFreeBattle = false;
            freeRoam = true;
            saveRecords = true;
            if (direction != TrackDirection.Bajada) { direction = TrackDirection.Bajada; ApplyDirection(); }
            ResetArrival();
            var data = track.Data;
            var ps = PlayerSpec;
            player.Init(data, ps, data.RaceStart, 0);
            townMap = track.Root != null ? track.Root.GetComponentInChildren<TownMap>(true) : null;
            if (townMap != null && townMap.TryGetPlace(TownMap.PlaceKind.TiendaTofu, out var shop))
                player.PlaceAt(shop.position, shop.heading * Mathf.Deg2Rad);
            player.manualGearbox = manualGearbox;
            player.ControlsEnabled = true;
            player.AutoBrake = false;
            player.GetComponent<CarVisual>().Build(ps);
            rival.gameObject.SetActive(false);
            player.rival = null;
            ApplySurface();
            if (trafficManager != null)
            {
                trafficManager.mountainTraffic = traffic != 2;
                trafficManager.ResetMountain();
            }
            chaseCamera.target = player;
            chaseCamera.showroom = false;
            chaseCamera.Snap();
            if (retro != null) retro.ResetHistory();
            SetShowroomLight(false);
            if (weatherSystem != null) weatherSystem.RefreshCars();

            profile = FreeProfile.Load(track.definition.Id);
            Discovered.Clear();
            if (townMap != null)
                foreach (var p in townMap.places)
                    if (profile.discovered.Contains(PlaceKey(p))) Discovered.Add(p);

            State = RaceState.Racing;
            RaceTime = 0;
            Water = 100;
            Score = 0;
            chain = chainTimer = linkTimer = 0;
            multiplier = 1;
            EndDelivery();
            trialActive = false;
            FreePrompt = FreeMission = null;
            cycleStarted = false;
            ApplyWeather(true);
            Big("自由");
            Show("MODO LIBRE · " + profile.points + " puntos");
        }

        /// <summary>Tras los resultados de un desafío: se sigue en el modo libre donde está el coche.</summary>
        void ResumeFree()
        {
            if (won && challengeKey != null)
            {
                profile.challengesWon++;
                profile.points += 500;
                if (!profile.challengesBeaten.Contains(challengeKey)) profile.challengesBeaten.Add(challengeKey);
                profile.Save();
            }
            challengeKey = null;
            InFreeBattle = false;
            freeRoam = true;
            saveRecords = true;
            ResetArrival();
            rival.Running = false;
            rival.gameObject.SetActive(false);
            player.rival = null;
            if (player.spec != PlayerSpec)
            {
                player.spec = PlayerSpec;
                player.GetComponent<CarVisual>().Build(PlayerSpec);
                if (weatherSystem != null) weatherSystem.RefreshCars();
            }
            player.manualGearbox = manualGearbox;
            player.ControlsEnabled = true;
            ApplySurface();
            chaseCamera.cinematic = false;
            State = RaceState.Racing;
            Water = 100;
            chain = 0;
            Show(won ? "DESAFÍO GANADO · +500" : "MODO LIBRE");
        }

        // ---------- Cada cuadro ----------

        void FreeUpdate(float dt)
        {
            RaceTime += dt;
            bool ok = DriverInput.Confirm || okTapped;
            okTapped = false;
            if (DriverInput.Respawn) RespawnFree();

            FreePrompt = null;
            promptAction = null;
            FreeMission = null;
            FreeTarget = null;

            Discover();
            UpdateDelivery(dt);
            UpdateTrial(dt);
            UpdateChallenges();
            UpdateZones(dt);
            UpdateCycle(dt);

            if (ok && promptAction != null) promptAction();
        }

        void Prompt(string text, System.Action action)
        {
            if (FreePrompt != null) return;
            FreePrompt = text;
            promptAction = action;
        }

        /// <summary>Tecla R: a la carretera más cercana o, en el pueblo, al cruce más cercano.</summary>
        void RespawnFree()
        {
            var pos = player.transform.position;
            var d = player.Track;
            if ((d.P[player.Index].pos - pos).magnitude < d.HW[player.Index] + 15) { player.Respawn(); return; }
            if (townMap == null || townMap.nodes.Count == 0) { player.Respawn(); return; }
            int best = 0;
            for (int i = 1; i < townMap.nodes.Count; i++)
                if ((townMap.nodes[i] - pos).sqrMagnitude < (townMap.nodes[best] - pos).sqrMagnitude) best = i;
            player.PlaceAt(townMap.nodes[best], player.Heading);
            Show("DE VUELTA EN LA CALLE");
        }

        static string PlaceKey(TownMap.Place p) => p.kind + ":" + p.name;

        void Discover()
        {
            if (townMap == null) return;
            var pos = player.transform.position;
            foreach (var p in townMap.places)
            {
                if ((p.position - pos).sqrMagnitude > 18 * 18) continue;
                string key = PlaceKey(p);
                if (profile.discovered.Contains(key)) continue;
                profile.discovered.Add(key);
                Discovered.Add(p);
                profile.Save();
                Show("DESCUBIERTO · " + p.name.ToUpper());
            }
        }

        // ---------- Repartos ----------

        void UpdateDelivery(float dt)
        {
            var pos = player.transform.position;
            if (!deliveryActive)
            {
                if (townMap != null && townMap.TryGetPlace(TownMap.PlaceKind.TiendaTofu, out var shop)
                    && (shop.position - pos).sqrMagnitude < 12 * 12 && player.Speed < 3)
                    Prompt("Recoger un reparto de tofu", StartDelivery);
                return;
            }
            deliveryTime -= dt;
            // El vaso se derrama igual que en la bajada: con fuerzas G por encima de ~1 g
            float gf = Mathf.Sqrt(player.LateralAccel * player.LateralAccel + Mathf.Pow(player.LongitudinalAccel * 0.8f, 2)) / 9.8f;
            if (gf > 1.05f) Water = Mathf.Max(0, Water - (gf - 1.05f) * 22 * dt);
            float dist = Vector3.Distance(pos, deliveryTarget);
            FreeTarget = deliveryTarget;
            FreeMission = $"Reparto a {deliveryName} · {Fmt(Mathf.Max(0, deliveryTime))} · {dist:0} m";
            if (dist < 10 && player.Speed < 3)
            {
                int pts = Mathf.RoundToInt((100 + Water * 4 + Mathf.Max(0, deliveryTime) * 2) * (deliveryToHotel ? 2 : 1));
                profile.points += pts;
                profile.deliveries++;
                profile.Save();
                Big("配達完了");
                Show($"REPARTO ENTREGADO · {Mathf.RoundToInt(Water)}% de agua · +{pts}", 3);
                EndDelivery();
            }
            else if (deliveryTime <= 0 || Water <= 0)
            {
                profile.deliveriesFailed++;
                profile.Save();
                Show(Water <= 0 ? "REPARTO FALLIDO · SE DERRAMÓ EL AGUA" : "REPARTO FALLIDO · FUERA DE TIEMPO", 3);
                EndDelivery();
            }
        }

        void StartDelivery()
        {
            var shopPos = player.transform.position;
            // Uno de cada dos al hotel de la montaña (si lo hay); el resto, a una casa del pueblo lejos de la tienda
            deliveryToHotel = profile.deliveries % 2 == 0 && townMap.TryGetPlace(TownMap.PlaceKind.Hotel, out _);
            if (deliveryToHotel)
            {
                townMap.TryGetPlace(TownMap.PlaceKind.Hotel, out var hotel);
                deliveryTarget = hotel.position;
                deliveryName = hotel.name;
                // Por carretera: hasta la entrada del pueblo y luego la subida
                var g = track.Geometry;
                int hi = NearestGlobal(g, hotel.position);
                float road = (g.TownStart - hi) * g.Step + Vector3.Distance(shopPos, g.P[g.TownStart].pos);
                deliveryTime = road / 13f + 30;
            }
            else
            {
                var candidates = new List<Vector3>();
                foreach (var e in townMap.edges)
                {
                    if (e.main || e.bridge) continue;
                    var mid = (townMap.nodes[e.a] + townMap.nodes[e.b]) / 2;
                    if ((mid - shopPos).sqrMagnitude > 160 * 160) candidates.Add(mid);
                }
                if (candidates.Count == 0) { Show("NO HAY REPARTOS"); return; }
                deliveryTarget = candidates[Random.Range(0, candidates.Count)];
                deliveryName = "casa de los " + Surnames[Random.Range(0, Surnames.Length)];
                deliveryTime = Vector3.Distance(shopPos, deliveryTarget) * 1.5f / 10f + 25;
            }
            deliveryActive = true;
            Water = 100;
            trialActive = false;
            beacon = Beacon(deliveryTarget);
            Big("配達");
            Show($"REPARTO · {deliveryName.ToUpper()} · {Fmt(deliveryTime)}", 3);
        }

        void EndDelivery()
        {
            deliveryActive = false;
            if (beacon != null) Destroy(beacon);
            beacon = null;
        }

        static int NearestGlobal(TrackData d, Vector3 p)
        {
            int best = 0;
            float bd = float.MaxValue;
            for (int i = 0; i < d.N; i += 2)
            {
                float dd = (d.P[i].pos - p).sqrMagnitude;
                if (dd < bd) { bd = dd; best = i; }
            }
            return d.Nearest(p, best);
        }

        /// <summary>Columna de luz ámbar sobre el destino, visible desde lejos.</summary>
        GameObject Beacon(Vector3 at)
        {
            var go = new GameObject("Destino");
            var mesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialFactory.Unlit(new Color(1f, 0.7f, 0.2f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.position = at + Vector3.up * 30;
            go.transform.localScale = new Vector3(0.6f, 30, 0.6f);
            return go;
        }

        // ---------- Contrarreloj libre ----------

        void UpdateTrial(float dt)
        {
            if (deliveryActive) { trialActive = false; return; }
            var d = player.Track;
            int i = player.Index;
            var v = player.Velocity;
            float along = Vector3.Dot(v, d.P[i].forward);
            int startDown = 8, goalDown = d.FinishIndex, startUp = d.FinishIndex + 1, goalUp = d.UphillGoal - 1;
            bool onRoad = Mathf.Abs(d.Lateral(i, player.transform.position)) < d.HW[i] + 3;
            if (!trialActive)
            {
                if (onRoad && along > 3 && i >= startDown && i <= startDown + 3) BeginTrial(false, i);
                else if (onRoad && along < -3 && i <= startUp && i >= startUp - 3) BeginTrial(true, i);
                return;
            }
            trialTime += dt;
            trialIdle = player.Speed < 1 ? trialIdle + dt : 0;
            if (trialUp) trialBest = Mathf.Min(trialBest, i); else trialBest = Mathf.Max(trialBest, i);
            bool back = trialUp ? i > trialBest + 15 : i < trialBest - 15;
            if (!onRoad || back || trialIdle > 6) { trialActive = false; Show("CONTRARRELOJ ANULADA"); return; }
            FreeMission = $"Contrarreloj libre ({(trialUp ? "subida" : "bajada")}) · {Fmt(trialTime)}";
            if (trialUp ? i <= goalUp : i >= goalDown)
            {
                trialActive = false;
                string key = (trialUp ? "subida_" : "bajada_") + PlayerSpec.model;
                float before = profile.BestTrial(key);
                bool record = profile.SetTrial(key, trialTime);
                if (record) { profile.points += 300; profile.Save(); }
                Big("GOAL");
                Show(record ? $"¡NUEVO RÉCORD LIBRE! {Fmt(trialTime)} · +300" : $"{Fmt(trialTime)} · récord {Fmt(before)}", 3);
            }
        }

        void BeginTrial(bool up, int i)
        {
            trialActive = true;
            trialUp = up;
            trialTime = 0;
            trialIdle = 0;
            trialBest = i;
            Big("GO!");
            Show("CONTRARRELOJ LIBRE · " + (up ? "SUBIDA" : "BAJADA"));
        }

        // ---------- Desafíos ----------

        void UpdateChallenges()
        {
            if (deliveryActive || trialActive) return;
            var d = track.Geometry;
            var pos = player.transform.position;
            for (int h = 0; h < d.Hairpins.Count; h++)
            {
                var hp = d.Hairpins[h];
                int i = (hp.start + hp.end) / 2 + 10, outer = -(int)Mathf.Sign(hp.angle);
                if (i >= d.N || d.Platform(i, outer) < 0.6f) continue;
                var car = d.At(i, outer, d.HW[i] + 3.4f, d.P[i].pos.y);
                if ((car - pos).sqrMagnitude > 16 * 16 || player.Speed > 4) continue;
                int start = Mathf.Min(d.FinishIndex - 60, i + 4), hairpin = h;
                string key = "horquilla" + (h + 1);
                Prompt($"Desafío de la horquilla {h + 1}: bajada contra el {RivalSpec.displayName}" +
                       (profile.challengesBeaten.Contains(key) ? " (ganado)" : ""), () => StartChallenge(start, key));
            }
        }

        /// <summary>Batalla de bajada desde un punto de la montaña, con la superficie de pista y la llegada de siempre.</summary>
        void StartChallenge(int start, string key)
        {
            challengeKey = key;
            InFreeBattle = true;
            freeRoam = false;
            saveRecords = false; // no es una bajada entera
            ApplySurface();
            var data = track.Data;
            player.ResetTo(start, -2.2f);
            player.ControlsEnabled = false;
            rival.gameObject.SetActive(true);
            player.rival = rival;
            rival.skill = rivalSkill;
            rival.Player = player;
            rival.Clock = () => RaceTime;
            rival.Init(data, RivalSpec, start * data.Step + 18);
            rival.Running = false;
            rival.GetComponent<CarVisual>().Build(RivalSpec);
            if (weatherSystem != null) weatherSystem.RefreshCars();
            chaseCamera.Snap();
            if (retro != null) retro.ResetHistory();
            State = RaceState.Countdown;
            countdown = 3.6f;
            RaceTime = 0;
            Water = 100;
            Score = 0;
            chain = chainTimer = linkTimer = gutterTime = finishTimer = 0;
            multiplier = 1;
            hits = gutterRuns = 0;
            sector = 3; // sin parciales: no es una bajada entera
            won = passed = false;
            splits.Clear();
            best = new BestRecord();
            Show("DESAFÍO · BAJADA HASTA LA META");
        }

        // ---------- Zonas de drift ----------

        void UpdateZones(float dt)
        {
            if (townMap == null) return;
            var pos = player.transform.position;
            bool inZone = false;
            foreach (var p in townMap.places)
            {
                float r = p.kind == TownMap.PlaceKind.Explanada ? 20 : p.kind == TownMap.PlaceKind.Aparcamiento ? 16 : 0;
                if (r > 0 && (p.position - pos).sqrMagnitude < r * r) { inZone = true; break; }
            }
            if (inZone && !deliveryActive && FreeMission == null) FreeMission = "Zona de drift · " + profile.points + " puntos";
            float sp = player.Speed;
            if (inZone && Mathf.Abs(player.Slip) > 0.22f && sp > 7)
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
                    int pts = Mathf.RoundToInt(chain * multiplier / 10);
                    profile.points += pts;
                    profile.Save();
                    Show($"{Tier(chain * multiplier)} · +{pts} puntos");
                    chain = 0;
                    linkTimer = 1.6f;
                }
            }
            else if (linkTimer > 0) { linkTimer -= dt; if (linkTimer <= 0) multiplier = 1; }
        }

        // ---------- Viaje rápido ----------

        /// <summary>Lleva el coche a un lugar descubierto (mapa de la pausa).</summary>
        public void FastTravel(TownMap.Place place)
        {
            if (!IsFree || InFreeBattle) return;
            EndDelivery();
            trialActive = false;
            player.PlaceAt(place.position, place.heading * Mathf.Deg2Rad);
            chaseCamera.Snap();
            if (retro != null) retro.ResetHistory();
            mapOpen = false;
            Resume();
            Show("VIAJE A " + place.name.ToUpper());
        }

        // ---------- Ciclo de hora y clima ----------

        static readonly WeatherKind[] Cycle =
        {
            WeatherKind.Atardecer, WeatherKind.Despejado, WeatherKind.Niebla, WeatherKind.Despejado, WeatherKind.Lluvia,
            WeatherKind.Despejado, WeatherKind.Amanecer,
        };
        const float CycleHold = 150, CycleBlend = 40;
        bool cycleStarted;
        int cycleIndex;
        float cycleClock, cycleApply;
        WeatherProfile cycleMix;

        /// <summary>Clima "Ciclo" del modo libre: cada perfil dura unos minutos y pasa al siguiente poco a poco.</summary>
        void UpdateCycle(float dt)
        {
            if (weather != 7 || weatherSystem == null) return;
            if (!cycleStarted)
            {
                cycleStarted = true;
                cycleIndex = 0;
                cycleClock = 0;
                weatherSystem.Apply(Cycle[0]);
                return;
            }
            cycleClock += dt;
            if (cycleClock < CycleHold) return;
            float t = (cycleClock - CycleHold) / CycleBlend;
            var a = weatherSystem.Profile(Cycle[cycleIndex]);
            var b = weatherSystem.Profile(Cycle[(cycleIndex + 1) % Cycle.Length]);
            if (t >= 1)
            {
                cycleIndex = (cycleIndex + 1) % Cycle.Length;
                cycleClock = 0;
                weatherSystem.Apply(b);
                return;
            }
            // Mezcla dos veces por segundo: Apply recorre materiales y luces
            cycleApply -= dt;
            if (cycleApply > 0) return;
            cycleApply = 0.5f;
            if (cycleMix == null) cycleMix = ScriptableObject.CreateInstance<WeatherProfile>();
            WeatherProfile.Blend(a, b, t, cycleMix);
            weatherSystem.Apply(cycleMix);
        }
    }
}

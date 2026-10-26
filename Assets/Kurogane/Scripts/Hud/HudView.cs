using Kurogane.Controls;
using Kurogane.Race;
using UnityEngine;
using UnityEngine.UI;

namespace Kurogane.Hud
{
    /// <summary>
    /// HUD de la bajada: cronómetro, récord, sectores, posición, vaso, distancia a meta, cadena de drift, avisos
    /// y la caja de cambios (velocímetro, marcha, cuentavueltas por segmentos y botones de marcha).
    /// Con los controles táctiles visibles, la caja sube a la esquina superior derecha y el vaso a la izquierda.
    /// </summary>
    [RequireComponent(typeof(RaceManager))]
    public class HudView : MonoBehaviour
    {
        /// <summary>A partir de estas vueltas se enciende la luz de cambio (la caja automática sube a 7.700).</summary>
        const float ShiftRpm = 7700;
        const int Segments = 18;

        RaceManager race;
        RectTransform root, waterGroup, gearbox, shiftRow;
        Text time, record, split, position, waterText, goal, flags, chain, big, toast, speed, gear, gearMode, rpm, fps;
        float fpsTimer, fpsTime;
        int fpsFrames;
        Image waterFill, gearBg, shiftUpBg;
        readonly Image[] segments = new Image[Segments];
        int shownGear = -1;
        float gearChangedAt = -10;

        // Último valor mostrado de cada texto: solo se formatea una cadena nueva cuando cambia (menos basura en móvil)
        int keyTime = -1, keyGoal = -1, keyWater = -1, keyFlags = -1, keySpeed = -1, keyRpm = -1, keyChain = -1;
        float keyBest = -1;
        bool keyMph, keyShift;
        static readonly string[] GearNames = { "1", "2", "3", "4", "5", "6", "7", "8" };
        static readonly string[] FlagTexts = BuildFlagTexts();

        static string[] BuildFlagTexts()
        {
            var r = new string[8];
            for (int m = 0; m < 8; m++)
            {
                string f = "";
                if ((m & 1) != 0) f += "REBUFO   ";
                if ((m & 2) != 0) f += "溝 MIZO   ";
                if ((m & 4) != 0) f += "LÍMITE RPM";
                r[m] = f.Trim();
            }
            return r;
        }

        void Start()
        {
            race = GetComponent<RaceManager>();
            Build();
        }

        void Build()
        {
            var canvas = UiKit.CreateCanvas("HUD", 0);
            root = UiKit.Node("Raíz", canvas.transform).Stretch();
            var tl = new Vector2(0, 1);

            time = UiKit.Label("Tiempo", root, 36, TextAnchor.UpperLeft, FontStyle.Bold);
            time.rectTransform.Place(tl, new Vector2(24, -14), new Vector2(400, 44));
            record = UiKit.Label("Récord", root, 18, TextAnchor.UpperLeft);
            record.rectTransform.Place(tl, new Vector2(24, -58), new Vector2(400, 24));
            split = UiKit.Label("Sector", root, 18, TextAnchor.UpperLeft);
            split.rectTransform.Place(tl, new Vector2(24, -82), new Vector2(400, 24));
            position = UiKit.Label("Posición", root, 40, TextAnchor.UpperLeft, FontStyle.BoldAndItalic);
            position.rectTransform.Place(tl, new Vector2(24, -108), new Vector2(200, 48));

            goal = UiKit.Label("Meta", root, 18, TextAnchor.UpperRight);
            goal.rectTransform.Place(new Vector2(1, 1), new Vector2(-24, -16), new Vector2(300, 24));
            // Arriba al centro, debajo de los botones táctiles, para no tapar la caja de cambios
            fps = UiKit.Label("FPS", root, 16, TextAnchor.UpperCenter);
            fps.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(300, 22));

            // Vaso de agua
            waterGroup = UiKit.Node("Vaso", root);
            waterText = UiKit.Label("Texto", waterGroup, 26, TextAnchor.LowerLeft, FontStyle.Bold);
            waterText.rectTransform.Place(Vector2.zero, new Vector2(0, 22), new Vector2(240, 34));
            var waterBack = UiKit.Panel("Fondo", waterGroup, UiKit.Faint);
            waterBack.rectTransform.Place(Vector2.zero, Vector2.zero, new Vector2(220, 14));
            waterFill = UiKit.Panel("Agua", waterBack.transform, new Color(0.45f, 0.75f, 1f, 0.9f));
            waterFill.rectTransform.Place(Vector2.zero, Vector2.zero, new Vector2(220, 14));

            // Mensajes centrales
            var bottom = new Vector2(0.5f, 0);
            flags = UiKit.Label("Avisos", root, 24, TextAnchor.MiddleCenter, FontStyle.Bold);
            flags.rectTransform.Place(bottom, new Vector2(0, 180), new Vector2(900, 34));
            toast = UiKit.Label("Aviso", root, 28, TextAnchor.MiddleCenter, FontStyle.Bold);
            toast.rectTransform.Place(bottom, new Vector2(0, 260), new Vector2(1000, 40));
            chain = UiKit.Label("Cadena", root, 32, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            chain.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(1000, 44));
            big = UiKit.Label("Grande", root, 84, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            big.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(1000, 110));

            BuildGearbox();
        }

        void BuildGearbox()
        {
            gearbox = UiKit.Panel("Caja", root, UiKit.PanelColor).rectTransform;
            var tl = new Vector2(0, 1);
            speed = UiKit.Label("Velocidad", gearbox, 32, TextAnchor.UpperLeft, FontStyle.Bold);
            speed.rectTransform.Place(tl, new Vector2(14, -6), new Vector2(240, 40));

            gearBg = UiKit.Panel("Marcha", gearbox, UiKit.Faint);
            gearBg.rectTransform.Place(new Vector2(1, 1), new Vector2(-14, -10), new Vector2(80, 70));
            gear = UiKit.Label("Número", gearBg.transform, 58, TextAnchor.MiddleCenter, FontStyle.Bold);
            gear.rectTransform.Stretch();
            gearMode = UiKit.Label("Modo", gearbox, 16, TextAnchor.UpperCenter);
            gearMode.rectTransform.Place(new Vector2(1, 1), new Vector2(-14, -82), new Vector2(80, 22));

            float barW = 360 - 128, segW = barW / Segments;
            for (int i = 0; i < Segments; i++)
            {
                segments[i] = UiKit.Panel("Segmento", gearbox, UiKit.Pale);
                segments[i].rectTransform.Place(tl, new Vector2(14 + i * segW, -54), new Vector2(segW - 2, 18));
            }
            rpm = UiKit.Label("Vueltas", gearbox, 16, TextAnchor.UpperLeft);
            rpm.rectTransform.Place(tl, new Vector2(14, -76), new Vector2(barW, 24));

            // Botones de marcha: ratón, dedo o Q / E
            shiftRow = UiKit.Node("Marchas", gearbox);
            shiftRow.Place(Vector2.zero, new Vector2(14, 10), new Vector2(310, 36));
            var down = UiKit.Panel("Bajar", shiftRow, UiKit.Faint, true);
            down.rectTransform.Place(Vector2.zero, Vector2.zero, new Vector2(150, 36));
            UiKit.Label("Texto", down.transform, 24, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Stretch();
            down.GetComponentInChildren<Text>().text = "▼  Q";
            down.gameObject.AddComponent<HoldButton>().pressed = () => DriverInput.Touch.shiftDown = true;
            shiftUpBg = UiKit.Panel("Subir", shiftRow, UiKit.Faint, true);
            shiftUpBg.rectTransform.Place(Vector2.zero, new Vector2(160, 0), new Vector2(150, 36));
            UiKit.Label("Texto", shiftUpBg.transform, 24, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Stretch();
            shiftUpBg.GetComponentInChildren<Text>().text = "E  ▲";
            shiftUpBg.gameObject.AddComponent<HoldButton>().pressed = () => DriverInput.Touch.shiftUp = true;
        }

        void LateUpdate()
        {
            var player = race.player;
            bool visible = race.State != RaceState.Menu && !race.HideHud && player != null && player.Track != null;
            root.Show(visible);
            if (!visible) return;

            bool touch = race.TouchVisible && (race.State == RaceState.Countdown || race.State == RaceState.Racing);
            gearbox.Place(touch ? new Vector2(1, 1) : new Vector2(1, 0), touch ? new Vector2(-20, -48) : new Vector2(-20, 20),
                new Vector2(360, player.manualGearbox ? 150 : 110));
            waterGroup.Place(touch ? new Vector2(0, 1) : Vector2.zero, touch ? new Vector2(24, -210) : new Vector2(24, 24), new Vector2(240, 60));

            // Modo libre (fuera de los desafíos): sin cronómetro ni meta; el vaso, solo durante un reparto
            bool roaming = race.IsFree && !race.InFreeBattle;
            time.Show(!roaming);
            record.Show(!roaming);
            split.Show(!roaming);
            position.Show(!roaming);
            goal.Show(!roaming);
            waterGroup.Show(!roaming || race.DeliveryActive);

            int k = Mathf.FloorToInt(race.RaceTime * 100);
            if (k != keyTime) { keyTime = k; time.Set(RaceManager.Fmt(race.RaceTime)); }
            if (race.BestTime != keyBest) { keyBest = race.BestTime; record.Set("Récord " + RaceManager.Fmt(race.BestTime)); }
            split.Set(race.SplitText ?? "");
            position.Set(race.HasRival ? (race.PlayerAhead ? "1º" : "2º") : "");
            k = Mathf.RoundToInt(race.MetersToGoal / 10);
            if (k != keyGoal) { keyGoal = k; goal.Set($"{k / 100f:0.00} km a meta"); }

            k = Mathf.RoundToInt(race.Water);
            if (k != keyWater)
            {
                keyWater = k;
                waterText.Set($"Vaso {k}%");
                waterFill.rectTransform.sizeDelta = new Vector2(220 * Mathf.Clamp01(k / 100f), 14);
            }

            k = (player.Slipstream ? 1 : 0) | (player.InGutter ? 2 : 0) | (player.RevLimiter ? 4 : 0);
            if (k != keyFlags) { keyFlags = k; flags.Set(FlagTexts[k]); }

            if (race.Chain > 5)
            {
                int deg = Mathf.RoundToInt(Mathf.Abs(player.Slip) * Mathf.Rad2Deg), pts = Mathf.RoundToInt(race.Chain);
                k = pts * 1000 + race.Multiplier * 100 + deg;
                if (k != keyChain)
                {
                    keyChain = k;
                    chain.Set($"{RaceManager.Tier(race.Chain * race.Multiplier)}  {pts}  x{race.Multiplier}  ({deg}°)");
                }
            }
            else if (keyChain != -1) { keyChain = -1; chain.Set(""); }
            big.Set(race.BigMessage ?? "");
            toast.Set(race.Toast ?? "");

            UpdateGearbox(player);
            UpdateFps();
        }

        /// <summary>Media de fps y milisegundos por cuadro cada medio segundo (opción "Ver FPS").</summary>
        void UpdateFps()
        {
            fps.Show(race.showFps);
            if (!race.showFps) return;
            fpsFrames++;
            fpsTime += Time.unscaledDeltaTime;
            fpsTimer -= Time.unscaledDeltaTime;
            if (fpsTimer > 0) return;
            float ms = fpsTime / Mathf.Max(1, fpsFrames) * 1000;
            fps.Set($"{Mathf.RoundToInt(1000 / Mathf.Max(0.01f, ms))} fps · {ms:0.0} ms");
            fpsTimer = 0.5f;
            fpsTime = 0;
            fpsFrames = 0;
        }

        void UpdateGearbox(Kurogane.Cars.CarController player)
        {
            int v = Mathf.RoundToInt(player.Speed * (race.useMph ? 2.237f : 3.6f));
            if (v != keySpeed || race.useMph != keyMph)
            {
                keySpeed = v;
                keyMph = race.useMph;
                speed.Set($"{v} {(race.useMph ? "mph" : "km/h")}");
            }

            if (player.Gear != shownGear) { shownGear = player.Gear; gearChangedAt = Time.time; }
            bool shiftLight = player.Rpm >= ShiftRpm && player.Gear < player.spec.Gears - 1;
            bool blink = Time.unscaledTime % 0.2f < 0.1f;
            gearBg.color = shiftLight && blink ? UiKit.Red : Time.time - gearChangedAt < 0.25f ? UiKit.Amber : UiKit.Faint;
            gear.Set(player.Gear < GearNames.Length ? GearNames[player.Gear] : (player.Gear + 1).ToString());
            gearMode.Set(player.manualGearbox ? "MT" : "AT");

            // Cuentavueltas: blanco, ámbar desde 6.500 y rojo en la zona de cambio
            for (int i = 0; i < Segments; i++)
            {
                float at = (i + 1) * 500;
                Color c = at > ShiftRpm ? UiKit.Red : at > 6500 ? UiKit.Amber : UiKit.Pale;
                if (player.Rpm < at - 250) c.a = 0.18f;
                segments[i].color = c;
            }
            int r = Mathf.RoundToInt(player.Rpm / 10) * 10;
            bool shiftHint = shiftLight && player.manualGearbox;
            if (r != keyRpm || shiftHint != keyShift)
            {
                keyRpm = r;
                keyShift = shiftHint;
                rpm.Set($"{r} rpm{(shiftHint ? "   ▲ CAMBIA" : "")}");
            }

            shiftRow.Show(player.manualGearbox);
            shiftUpBg.color = shiftLight ? UiKit.Red : UiKit.Faint;
        }
    }
}

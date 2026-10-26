using Kurogane.Cars;
using Kurogane.Controls;
using UnityEngine;

namespace Kurogane.Race
{
    /// <summary>Nota final de la bajada: suma de tiempo, agua, estilo, limpieza y resultado de la batalla.</summary>
    public struct RaceGrade
    {
        public int time, water, style, clean, battle, total;
        public string letter;
    }

    /// <summary>
    /// Fase 2: menú de coche y opciones, menú de pausa, caja de cambios en pantalla y nota final S–C.
    /// Sigue siendo IMGUI provisional (se navega con teclado, mando o ratón); la interfaz definitiva llega en la Fase 5.
    /// </summary>
    public partial class RaceManager
    {
        enum MenuRow { Coche, Modo, Rival, Caja, Velocidad, Empezar }

        static readonly MenuRow[] Rows = (MenuRow[])System.Enum.GetValues(typeof(MenuRow));
        static readonly string[] PauseRows = { "Continuar", "Reiniciar bajada", "Menú principal" };
        static readonly string[] SkillNames = { "Fácil", "Normal", "Leyenda" };

        /// <summary>A partir de estas vueltas se enciende la luz de cambio (la caja automática sube a 7.700).</summary>
        const float ShiftRpm = 7700;

        static readonly Color PanelColor = new Color(0.02f, 0.03f, 0.08f, 0.7f);
        static readonly Color Highlight = new Color(0.8f, 0.16f, 0.22f, 0.6f);
        static readonly Color Pale = new Color(0.9f, 0.92f, 1f);
        static readonly Color Amber = new Color(1f, 0.74f, 0.2f);
        static readonly Color Red = new Color(1f, 0.24f, 0.2f);

        public RaceGrade Result { get; private set; }

        int menuRow, pauseRow, shownGear;
        float gearChangedAt = -1;

        // ---------- Menú principal ----------

        void OpenMenu()
        {
            Time.timeScale = 1;
            State = RaceState.Menu;
            menuRow = (int)MenuRow.Empezar;
            rival.gameObject.SetActive(false);
            player.rival = null;
            PreviewCar();
        }

        /// <summary>Pone el coche elegido en la salida, parado, para verlo detrás del menú.</summary>
        void PreviewCar()
        {
            var ps = PlayerSpec;
            player.Init(track.Data, ps, 5, 0);
            player.ControlsEnabled = false;
            player.AutoBrake = false;
            player.GetComponent<CarVisual>().Build(ps);
            chaseCamera.target = player;
            chaseCamera.Snap();
            best = LoadBest();
        }

        void MenuInput()
        {
            if (DriverInput.MenuUp) MoveRow(-1);
            if (DriverInput.MenuDown) MoveRow(1);
            if (DriverInput.MenuLeft) Change((MenuRow)menuRow, -1);
            if (DriverInput.MenuRight) Change((MenuRow)menuRow, 1);
            if (DriverInput.NextCar) Change(MenuRow.Coche, 1);
            if (DriverInput.Confirm) BeginRace();
        }

        bool RowEnabled(MenuRow row) => row != MenuRow.Rival || mode == RaceMode.Batalla;

        void MoveRow(int dir)
        {
            do menuRow = (menuRow + dir + Rows.Length) % Rows.Length;
            while (!RowEnabled((MenuRow)menuRow));
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
                case MenuRow.Velocidad: useMph = !useMph; break;
            }
        }

        void BeginRace()
        {
            RaceSettings.Save(this);
            StartRace();
        }

        string RowValue(MenuRow row)
        {
            switch (row)
            {
                case MenuRow.Coche: return PlayerSpec.displayName;
                case MenuRow.Modo: return mode == RaceMode.Batalla ? "Batalla" : "Contrarreloj";
                case MenuRow.Rival: return mode == RaceMode.Batalla ? SkillNames[(int)rivalSkill] : "—";
                case MenuRow.Caja: return manualGearbox ? "Manual (Q / E)" : "Automática";
                case MenuRow.Velocidad: return useMph ? "mph" : "km/h";
                default: return "";
            }
        }

        void DrawMenu(float u)
        {
            float x = 40 * u, y = 50 * u, w = 600 * u, rowH = 46 * u;
            float h = 150 * u + Rows.Length * rowH + 150 * u;
            Fill(new Rect(x, y, w, h), PanelColor);

            GUI.Label(new Rect(x + 20 * u, y + 12 * u, w, 40 * u), "黒鉄峠  KUROGANE TOUGE", mid);
            GUI.Label(new Rect(x + 20 * u, y + 50 * u, w, 24 * u), "Monte Kurogane · 2,3 km · 4 horquillas · 1 túnel", small);
            GUI.Label(new Rect(x + 20 * u, y + 74 * u, w, 24 * u), PlayerSpec.tagline, small);
            GUI.Label(new Rect(x + 20 * u, y + 98 * u, w, 24 * u), $"Récord ({RowValue(MenuRow.Modo)}) {Fmt(best.time)}", small);

            float ry = y + 136 * u;
            for (int i = 0; i < Rows.Length; i++)
            {
                var row = Rows[i];
                var r = new Rect(x + 10 * u, ry + i * rowH, w - 20 * u, rowH - 4 * u);
                bool enabled = RowEnabled(row);
                if (i == menuRow) Fill(r, Highlight);
                var old = GUI.color;
                if (!enabled) GUI.color = new Color(1, 1, 1, 0.35f);

                if (row == MenuRow.Empezar)
                {
                    if (GUI.Button(r, "EMPEZAR BAJADA", center)) BeginRace();
                }
                else
                {
                    if (GUI.Button(new Rect(r.x, r.y, 170 * u, r.height), "  " + row, mid) && enabled) menuRow = i;
                    var left = new Rect(r.x + 180 * u, r.y, 44 * u, r.height);
                    var right = new Rect(r.xMax - 50 * u, r.y, 44 * u, r.height);
                    var value = new Rect(left.xMax, r.y, right.x - left.xMax, r.height);
                    if (GUI.Button(left, "◀", center) && enabled) { menuRow = i; Change(row, -1); }
                    GUI.Label(value, RowValue(row), center);
                    if (GUI.Button(right, "▶", center) && enabled) { menuRow = i; Change(row, 1); }
                }
                GUI.color = old;
            }

            // Ficha del coche
            float sy = ry + Rows.Length * rowH + 14 * u;
            var spec = PlayerSpec;
            DrawStat(x + 20 * u, sy, u, "Potencia", spec.statPower);
            DrawStat(x + 20 * u, sy + 26 * u, u, "Agarre", spec.statGrip);
            DrawStat(x + 310 * u, sy, u, "Drift", spec.statDrift);
            DrawStat(x + 310 * u, sy + 26 * u, u, "Ligereza", spec.statLightness);

            GUI.Label(new Rect(x + 20 * u, sy + 70 * u, w - 40 * u, 50 * u),
                "↑ ↓ elegir   ·   ← → cambiar   ·   Tab otro coche   ·   Enter empezar", small);
        }

        void DrawStat(float x, float y, float u, string label, int value)
        {
            GUI.Label(new Rect(x, y, 110 * u, 24 * u), label, small);
            for (int i = 0; i < 5; i++)
                Fill(new Rect(x + 110 * u + i * 30 * u, y + 7 * u, 26 * u, 10 * u), i < value ? Pale : new Color(1, 1, 1, 0.15f));
        }

        // ---------- Pausa ----------

        void PauseInput()
        {
            if (DriverInput.Pause) { Resume(); return; }
            if (DriverInput.MenuUp) pauseRow = (pauseRow + PauseRows.Length - 1) % PauseRows.Length;
            if (DriverInput.MenuDown) pauseRow = (pauseRow + 1) % PauseRows.Length;
            if (DriverInput.Confirm) PauseAction(pauseRow);
        }

        void PauseAction(int i)
        {
            if (i == 0) Resume();
            else if (i == 1) StartRace();
            else OpenMenu();
        }

        void Resume()
        {
            State = stateBeforePause;
            Time.timeScale = 1;
        }

        void DrawPause(float u)
        {
            float w = 420 * u, rowH = 50 * u, h = 70 * u + PauseRows.Length * rowH;
            float x = (Screen.width - w) / 2, y = (Screen.height - h) / 2;
            Fill(new Rect(x, y, w, h), PanelColor);
            GUI.Label(new Rect(x, y + 10 * u, w, 40 * u), "PAUSA", center);
            for (int i = 0; i < PauseRows.Length; i++)
            {
                var r = new Rect(x + 20 * u, y + 60 * u + i * rowH, w - 40 * u, rowH - 6 * u);
                if (i == pauseRow) Fill(r, Highlight);
                if (GUI.Button(r, PauseRows[i], center)) PauseAction(i);
            }
        }

        // ---------- Caja de cambios en pantalla ----------

        void DrawGearbox(float u)
        {
            float w = 360 * u, h = player.manualGearbox ? 150 * u : 110 * u;
            float x = Screen.width - w - 20 * u, y = Screen.height - h - 20 * u;
            Fill(new Rect(x, y, w, h), PanelColor);

            float speed = player.Speed * (useMph ? 2.237f : 3.6f);
            GUI.Label(new Rect(x + 14 * u, y + 6 * u, 240 * u, 40 * u), $"{Mathf.RoundToInt(speed)} {(useMph ? "mph" : "km/h")}", mid);

            if (player.Gear != shownGear) { shownGear = player.Gear; gearChangedAt = Time.time; }
            bool shiftLight = player.Rpm >= ShiftRpm && player.Gear < player.spec.Gears - 1;
            bool blink = Time.unscaledTime % 0.2f < 0.1f;
            var gearRect = new Rect(x + w - 94 * u, y + 10 * u, 80 * u, 70 * u);
            Color gearBg = shiftLight && blink ? Red : Time.time - gearChangedAt < 0.25f ? Amber : new Color(1, 1, 1, 0.08f);
            Fill(gearRect, gearBg);
            GUI.Label(gearRect, (player.Gear + 1).ToString(), gearStyle);
            GUI.Label(new Rect(gearRect.x, gearRect.yMax, gearRect.width, 22 * u), player.manualGearbox ? "MT" : "AT", smallCenter);

            // Cuentavueltas por segmentos: blanco, ámbar desde 6.500 y rojo en la zona de cambio
            const int segments = 18;
            float barW = w - 128 * u, segW = barW / segments;
            for (int i = 0; i < segments; i++)
            {
                float at = (i + 1) * 500;
                Color c = at > ShiftRpm ? Red : at > 6500 ? Amber : Pale;
                bool lit = player.Rpm >= at - 250;
                Fill(new Rect(x + 14 * u + i * segW, y + 54 * u, segW - 2 * u, 18 * u), lit ? c : new Color(c.r, c.g, c.b, 0.18f));
            }
            GUI.Label(new Rect(x + 14 * u, y + 76 * u, barW, 24 * u),
                $"{Mathf.RoundToInt(player.Rpm)} rpm{(shiftLight && player.manualGearbox ? "   ▲ CAMBIA" : "")}", small);

            if (player.manualGearbox)
            {
                // Botones clicables: sirven con ratón ahora y con el dedo cuando lleguen los controles táctiles
                var down = new Rect(x + 14 * u, y + h - 46 * u, 150 * u, 36 * u);
                var up = new Rect(x + 174 * u, y + h - 46 * u, 150 * u, 36 * u);
                Fill(down, new Color(1, 1, 1, 0.1f));
                Fill(up, shiftLight ? Red : new Color(1, 1, 1, 0.1f));
                if (GUI.Button(down, "▼  Q", center)) player.RequestShift(-1);
                if (GUI.Button(up, "E  ▲", center)) player.RequestShift(1);
            }
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

        void DrawResults(float u)
        {
            var g = Result;
            float w = 720 * u, h = 380 * u, x = (Screen.width - w) / 2, y = Screen.height * 0.18f;
            Fill(new Rect(x, y, w, h), PanelColor);

            string verdict = mode == RaceMode.Batalla ? (won ? "勝利 · Victoria" : "敗北 · Derrota") : "ゴール · Meta";
            GUI.Label(new Rect(x, y + 10 * u, w, 40 * u), verdict, center);

            var letterRect = new Rect(x + 30 * u, y + 70 * u, 150 * u, 150 * u);
            Fill(letterRect, g.letter == "S" ? Red : g.letter == "A" ? Amber : new Color(1, 1, 1, 0.12f));
            GUI.Label(letterRect, g.letter, big);
            GUI.Label(new Rect(letterRect.x, letterRect.yMax + 4 * u, letterRect.width, 24 * u), $"{g.total} / 100", smallCenter);

            float lx = letterRect.xMax + 30 * u, ly = y + 70 * u, lw = w - (lx - x) - 30 * u, lh = 30 * u;
            GUI.Label(new Rect(lx, ly, lw, lh), $"Tiempo  {Fmt(RaceTime)}   (récord {Fmt(best.time)})", small);
            GUI.Label(new Rect(lx, ly, lw, lh), $"{g.time} / 40", RightAligned(small));
            GUI.Label(new Rect(lx, ly + lh, lw, lh), $"Agua  {Mathf.RoundToInt(Water)}%", small);
            GUI.Label(new Rect(lx, ly + lh, lw, lh), $"{g.water} / 25", RightAligned(small));
            GUI.Label(new Rect(lx, ly + lh * 2, lw, lh), $"Estilo  {Score} drift · {gutterRuns} canaletas", small);
            GUI.Label(new Rect(lx, ly + lh * 2, lw, lh), $"{g.style} / 20", RightAligned(small));
            GUI.Label(new Rect(lx, ly + lh * 3, lw, lh), $"Limpieza  {hits} golpes", small);
            GUI.Label(new Rect(lx, ly + lh * 3, lw, lh), $"{g.clean} / 15", RightAligned(small));
            if (mode == RaceMode.Batalla)
            {
                GUI.Label(new Rect(lx, ly + lh * 4, lw, lh), won ? "Batalla ganada" : "Batalla perdida", small);
                GUI.Label(new Rect(lx, ly + lh * 4, lw, lh), (g.battle > 0 ? "+" : "") + g.battle, RightAligned(small));
            }

            GUI.Label(new Rect(x, y + h - 50 * u, w, 30 * u),
                $"Enter otra bajada   ·   Tab cambiar coche ({cars[(selectedCar + 1) % cars.Length].displayName})   ·   Esc menú", smallCenter);
        }

        // ---------- Utilidades de dibujo ----------

        GUIStyle rightAligned;

        GUIStyle RightAligned(GUIStyle from)
        {
            if (rightAligned == null) rightAligned = new GUIStyle(from) { alignment = TextAnchor.UpperRight };
            rightAligned.fontSize = from.fontSize;
            return rightAligned;
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}

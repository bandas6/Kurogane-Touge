using Kurogane.Race;
using UnityEngine;
using UnityEngine.UI;

namespace Kurogane.Hud
{
    /// <summary>
    /// Dibuja el menú principal (página de carrera y de opciones), la pausa y los resultados con la nota.
    /// La lógica y la navegación con teclado o mando están en el RaceManager; aquí solo se pinta su estado
    /// y se le pasan los clics y toques.
    /// </summary>
    [RequireComponent(typeof(RaceManager))]
    public class MenuView : MonoBehaviour
    {
        const int MaxRows = 12;
        const float RowH = 40, PanelW = 600;

        RaceManager race;
        RectTransform drag, menu, pause, results;
        Text subtitle, tagline, record, hint;
        RectTransform stats;
        readonly Row[] rows = new Row[MaxRows];
        readonly Image[] statBars = new Image[20];
        readonly Image[] pauseRows = new Image[3];

        // Ficha de la pista elegida: mapa, perfil de altitud, descripción y récord
        RectTransform card;
        RawImage map, profile;
        Text cardTitle, cardSummary, cardText;
        Kurogane.Track.TrackData cardFor;

        readonly Text[] pauseTexts = new Text[RaceManager.PauseCount];
        Text againLabel;

        Text verdict, letter, total, resultHint;
        Image letterBg;
        readonly Text[] lineLeft = new Text[5], lineRight = new Text[5];

        class Row
        {
            public RectTransform root;
            public CanvasGroup group;
            public Image back;
            public Text label, value, center;
            public Image left, right;
        }

        void Start()
        {
            race = GetComponent<RaceManager>();
            var canvas = UiKit.CreateCanvas("Menús", 10);

            // Arrastrar en cualquier parte fuera del panel gira la cámara del garaje
            var dragImg = UiKit.Panel("Arrastre", canvas.transform, Color.clear, true);
            drag = dragImg.rectTransform.Stretch();
            dragImg.gameObject.AddComponent<DragArea>().dragged = dx => race.chaseCamera.Orbit(dx);

            BuildMenu(canvas.transform);
            BuildCard(canvas.transform);
            BuildPause(canvas.transform);
            BuildResults(canvas.transform);
        }

        // ---------- Menú principal ----------

        void BuildMenu(Transform parent)
        {
            menu = UiKit.Panel("Menú", parent, UiKit.PanelColor, true).rectTransform;
            var tl = new Vector2(0, 1);
            UiKit.Label("Título", menu, 32, TextAnchor.UpperLeft, FontStyle.Bold).rectTransform.Place(tl, new Vector2(20, -12), new Vector2(560, 40))
                .GetComponent<Text>().text = "黒鉄峠  KUROGANE TOUGE";
            subtitle = UiKit.Label("Subtítulo", menu, 17, TextAnchor.UpperLeft);
            subtitle.rectTransform.Place(tl, new Vector2(20, -52), new Vector2(560, 24));
            tagline = UiKit.Label("Ficha", menu, 17, TextAnchor.UpperLeft);
            tagline.rectTransform.Place(tl, new Vector2(20, -76), new Vector2(560, 24));
            record = UiKit.Label("Récord", menu, 17, TextAnchor.UpperLeft);
            record.rectTransform.Place(tl, new Vector2(20, -100), new Vector2(560, 24));

            for (int i = 0; i < MaxRows; i++)
            {
                int index = i;
                var r = new Row { root = UiKit.Node("Fila " + i, menu) };
                r.root.Place(tl, new Vector2(10, -136 - i * RowH), new Vector2(PanelW - 20, RowH - 4));
                r.group = r.root.gameObject.AddComponent<CanvasGroup>();
                r.back = UiKit.Panel("Fondo", r.root, Color.clear, true);
                r.back.rectTransform.Stretch();
                r.back.OnTap(() => race.TapRow(index));
                r.label = UiKit.Label("Nombre", r.root, 26, TextAnchor.MiddleLeft, FontStyle.Bold);
                r.label.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(170, RowH));
                r.left = Arrow(r.root, "◀", new Vector2(180, 0), () => race.ChangeRow(index, -1));
                r.right = Arrow(r.root, "▶", new Vector2(PanelW - 20 - 50, 0), () => race.ChangeRow(index, 1));
                r.value = UiKit.Label("Valor", r.root, 24, TextAnchor.MiddleCenter, FontStyle.Bold);
                r.value.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(226, 0), new Vector2(PanelW - 20 - 50 - 226, RowH));
                r.center = UiKit.Label("Botón", r.root, 26, TextAnchor.MiddleCenter, FontStyle.Bold);
                r.center.rectTransform.Stretch();
                rows[i] = r;
            }

            // Ficha del coche: cuatro barras de 1 a 5
            stats = UiKit.Node("Ficha", menu);
            string[] names = { "Potencia", "Agarre", "Drift", "Ligereza" };
            for (int s = 0; s < 4; s++)
            {
                var origin = new Vector2(20 + (s / 2) * 290, -(s % 2) * 26);
                UiKit.Label(names[s], stats, 17, TextAnchor.UpperLeft).rectTransform.Place(tl, origin, new Vector2(110, 24))
                    .GetComponent<Text>().text = names[s];
                for (int b = 0; b < 5; b++)
                {
                    statBars[s * 5 + b] = UiKit.Panel("Barra", stats, UiKit.Pale);
                    statBars[s * 5 + b].rectTransform.Place(tl, origin + new Vector2(110 + b * 30, -7), new Vector2(26, 10));
                }
            }
            hint = UiKit.Label("Ayuda", menu, 16, TextAnchor.LowerLeft);
            hint.rectTransform.Place(Vector2.zero, new Vector2(20, 14), new Vector2(560, 24));
        }

        Image Arrow(Transform parent, string glyph, Vector2 pos, System.Action action)
        {
            var img = UiKit.Panel("Flecha", parent, UiKit.Faint, true);
            img.rectTransform.Place(new Vector2(0, 0.5f), pos, new Vector2(44, RowH - 8));
            UiKit.Label("Texto", img.transform, 22, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Stretch();
            img.GetComponentInChildren<Text>().text = glyph;
            img.OnTap(action);
            return img;
        }

        void UpdateMenu()
        {
            var list = race.MenuRows;
            float h = 136 + list.Count * RowH + (race.InOptions ? 50 : 110);
            menu.Place(new Vector2(0, 1), new Vector2(40, -36), new Vector2(PanelW, h));

            var def = race.SelectedTrack;
            subtitle.Set(race.InOptions ? "Opciones" : def != null ? $"{def.DisplayName} · {race.RowValue(MenuRow.Sentido)}" : "");
            tagline.Set(race.InOptions ? "Se guardan al volver." : race.PlayerSpec.tagline);
            record.Set(race.InOptions ? "" : $"Récord de {race.RunName} ({race.RowValue(MenuRow.Modo)}, {race.PlayerSpec.displayName}) {RaceManager.Fmt(race.BestTime)}");

            for (int i = 0; i < MaxRows; i++)
            {
                var r = rows[i];
                r.root.Show(i < list.Count);
                if (i >= list.Count) continue;
                var row = list[i];
                bool action = RaceManager.IsAction(row), enabled = race.RowEnabled(row);
                r.group.alpha = enabled ? 1 : 0.35f;
                r.back.color = i == race.MenuIndex ? UiKit.Highlight : action ? UiKit.Faint : Color.clear;
                r.label.Show(!action);
                r.left.Show(!action);
                r.right.Show(!action);
                r.value.Show(!action);
                r.center.Show(action);
                if (action) r.center.Set(race.RowLabel(row));
                else
                {
                    r.label.Set(race.RowLabel(row));
                    r.value.Set(race.RowValue(row));
                }
            }

            stats.Show(!race.InOptions);
            if (!race.InOptions)
            {
                stats.Place(new Vector2(0, 1), new Vector2(0, -136 - list.Count * RowH - 14), new Vector2(PanelW, 60));
                var spec = race.PlayerSpec;
                int[] values = { spec.statPower, spec.statGrip, spec.statDrift, spec.statLightness };
                for (int s = 0; s < 4; s++)
                    for (int b = 0; b < 5; b++)
                        statBars[s * 5 + b].color = b < values[s] ? UiKit.Pale : new Color(1, 1, 1, 0.15f);
            }
            hint.Set(race.InOptions
                ? "↑ ↓ elegir   ·   ← → cambiar   ·   Esc volver"
                : "↑ ↓ elegir   ·   ← → cambiar   ·   Tab otro coche   ·   Enter empezar   ·   arrastra para girar");
        }

        // ---------- Ficha de la pista ----------

        const float CardW = 380, MapSize = 250;

        void BuildCard(Transform parent)
        {
            card = UiKit.Panel("Ficha de pista", parent, UiKit.PanelColor, true).rectTransform;
            card.Place(new Vector2(1, 1), new Vector2(-40, -36), new Vector2(CardW, 490));
            var tl = new Vector2(0, 1);
            cardTitle = UiKit.Label("Nombre", card, 24, TextAnchor.UpperLeft, FontStyle.Bold);
            cardTitle.rectTransform.Place(tl, new Vector2(16, -10), new Vector2(CardW - 32, 32));
            map = new GameObject("Mapa", typeof(RectTransform)).AddComponent<RawImage>();
            map.transform.SetParent(card, false);
            map.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -46), new Vector2(MapSize, MapSize));
            map.raycastTarget = false;
            profile = new GameObject("Perfil", typeof(RectTransform)).AddComponent<RawImage>();
            profile.transform.SetParent(card, false);
            profile.rectTransform.Place(tl, new Vector2(16, -46 - MapSize - 8), new Vector2(CardW - 32, 56));
            profile.raycastTarget = false;
            cardSummary = UiKit.Label("Ficha", card, 16, TextAnchor.UpperLeft);
            cardSummary.rectTransform.Place(tl, new Vector2(16, -46 - MapSize - 72), new Vector2(CardW - 32, 42));
            cardSummary.horizontalOverflow = HorizontalWrapMode.Wrap;
            cardText = UiKit.Label("Descripción", card, 16, TextAnchor.UpperLeft);
            cardText.rectTransform.Place(tl, new Vector2(16, -46 - MapSize - 118), new Vector2(CardW - 32, 66));
            cardText.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        void UpdateCard()
        {
            var data = race.SelectedData;
            var def = race.SelectedTrack;
            card.Show(data != null);
            if (data == null) return;
            if (data != cardFor)
            {
                cardFor = data;
                if (map.texture != null) Destroy(map.texture);
                if (profile.texture != null) Destroy(profile.texture);
                map.texture = Kurogane.Track.TrackPreview.Map(data, 256);
                profile.texture = Kurogane.Track.TrackPreview.Profile(data, 348, 56);
                cardTitle.Set(string.IsNullOrEmpty(def.japaneseName) ? def.DisplayName : $"{def.japaneseName}  {def.DisplayName}");
                cardSummary.Set(Kurogane.Track.TrackPreview.Summary(data));
                cardText.Set(def.description);
            }
        }

        // ---------- Pausa ----------

        void BuildPause(Transform parent)
        {
            float h = 70 + 3 * 50;
            pause = UiKit.Panel("Pausa", parent, UiKit.PanelColor, true).rectTransform;
            pause.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420, h));
            UiKit.Label("Título", pause, 30, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform
                .Place(new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(420, 40)).GetComponent<Text>().text = "PAUSA";
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                pauseRows[i] = UiKit.Panel("Opción", pause, Color.clear, true);
                pauseRows[i].rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -60 - i * 50), new Vector2(380, 44));
                UiKit.Label("Texto", pauseRows[i].transform, 26, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Stretch();
                pauseTexts[i] = pauseRows[i].GetComponentInChildren<Text>();
                pauseTexts[i].text = race.PauseLabel(i);
                pauseRows[i].OnTap(() => race.PauseAction(index));
            }
        }

        // ---------- Resultados ----------

        void BuildResults(Transform parent)
        {
            results = UiKit.Panel("Resultados", parent, UiKit.PanelColor, true).rectTransform;
            results.Place(new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(760, 400));
            var tl = new Vector2(0, 1);
            verdict = UiKit.Label("Veredicto", results, 30, TextAnchor.MiddleCenter, FontStyle.Bold);
            verdict.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(760, 40));

            letterBg = UiKit.Panel("Nota", results, UiKit.Faint);
            letterBg.rectTransform.Place(tl, new Vector2(30, -70), new Vector2(150, 150));
            letter = UiKit.Label("Letra", letterBg.transform, 96, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            letter.rectTransform.Stretch();
            total = UiKit.Label("Total", results, 18, TextAnchor.UpperCenter);
            total.rectTransform.Place(tl, new Vector2(30, -226), new Vector2(150, 24));

            for (int i = 0; i < 5; i++)
            {
                lineLeft[i] = UiKit.Label("Línea", results, 19, TextAnchor.UpperLeft);
                lineLeft[i].rectTransform.Place(tl, new Vector2(210, -74 - i * 32), new Vector2(400, 28));
                lineRight[i] = UiKit.Label("Puntos", results, 19, TextAnchor.UpperRight, FontStyle.Bold);
                lineRight[i].rectTransform.Place(new Vector2(1, 1), new Vector2(-30, -74 - i * 32), new Vector2(120, 28));
            }

            // Botones de la pantalla final, también con Enter / Tab / Esc
            string[] labels = { "Otra bajada  (Enter)", "Cambiar coche  (Tab)", "Menú  (Esc)" };
            System.Action[] actions = { () => race.StartRace(), () => race.NextCarAndRestart(), () => race.OpenMenu() };
            for (int i = 0; i < 3; i++)
            {
                var b = UiKit.Panel("Botón", results, UiKit.Faint, true);
                b.rectTransform.Place(Vector2.zero, new Vector2(20 + i * 245, 20), new Vector2(230, 48));
                var t = UiKit.Label("Texto", b.transform, 19, TextAnchor.MiddleCenter, FontStyle.Bold);
                t.rectTransform.Stretch();
                t.text = labels[i];
                if (i == 0) againLabel = t;
                b.OnTap(actions[i]);
            }
            resultHint = UiKit.Label("Siguiente", results, 15, TextAnchor.LowerCenter);
            resultHint.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 74), new Vector2(760, 22));
        }

        void UpdateResults()
        {
            var g = race.Result;
            bool battle = race.mode == RaceMode.Batalla;
            verdict.Set(battle ? (race.Won ? "勝利 · Victoria" : "敗北 · Derrota") : "ゴール · Meta");
            letter.Set(g.letter);
            letterBg.color = g.letter == "S" ? UiKit.Red : g.letter == "A" ? UiKit.Amber : UiKit.Faint;
            total.Set($"{g.total} / 100");

            Line(0, $"Tiempo  {RaceManager.Fmt(race.RaceTime)}   (récord {RaceManager.Fmt(race.BestTime)})", $"{g.time} / 40");
            Line(1, $"Agua  {Mathf.RoundToInt(race.Water)}%", $"{g.water} / 25");
            Line(2, $"Estilo  {race.Score} drift · {race.GutterRuns} canaletas", $"{g.style} / 20");
            Line(3, $"Limpieza  {race.Hits} golpes", $"{g.clean} / 15");
            Line(4, battle ? (race.Won ? "Batalla ganada" : "Batalla perdida") : "", battle ? (g.battle > 0 ? "+" : "") + g.battle : "");
            resultHint.Set("Siguiente coche: " + race.NextCarSpec.displayName);
            againLabel.Set(race.IsFree ? "Seguir en modo libre  (Enter)" : $"Otra {race.RunName}  (Enter)");
        }

        void Line(int i, string left, string right)
        {
            lineLeft[i].Set(left);
            lineRight[i].Set(right);
        }

        // ---------- Cada cuadro ----------

        void LateUpdate()
        {
            bool inMenu = race.State == RaceState.Menu;
            drag.Show(inMenu);
            menu.Show(inMenu);
            if (inMenu) UpdateMenu();
            card.Show(inMenu && !race.InOptions);
            if (inMenu && !race.InOptions) UpdateCard();

            bool paused = race.State == RaceState.Paused;
            pause.Show(paused && !race.MapOpen);
            if (paused)
                for (int i = 0; i < RaceManager.PauseCount; i++)
                {
                    pauseRows[i].color = i == race.PauseIndex ? UiKit.Highlight : UiKit.Faint;
                    pauseTexts[i].Set(race.PauseLabel(i));
                }

            bool showResults = race.ShowResults;
            results.Show(showResults);
            if (showResults) UpdateResults();
        }
    }
}

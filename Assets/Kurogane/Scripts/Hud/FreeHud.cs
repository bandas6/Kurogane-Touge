using Kurogane.City;
using Kurogane.Race;
using Kurogane.Track;
using UnityEngine;
using UnityEngine.UI;

namespace Kurogane.Hud
{
    /// <summary>
    /// Interfaz del modo libre (bloque H3): minimapa con el norte arriba (carretera de montaña y calles del pueblo),
    /// flecha del coche y del destino, puntos, misión en curso y acción disponible (con botón OK táctil), y el mapa de
    /// la pausa con los lugares descubiertos para el viaje rápido.
    /// </summary>
    [RequireComponent(typeof(RaceManager))]
    public class FreeHud : MonoBehaviour
    {
        const float MiniSize = 230, MiniRange = 180, MapSize = 520;

        RaceManager race;
        RectTransform root, mini, miniArrow, miniTarget, okButton, mapPanel, mapPlayer;
        RawImage miniImage, mapImage;
        Text points, mission, prompt;
        readonly Image[] mapMarks = new Image[16];
        readonly Image[] mapRows = new Image[12];
        readonly Text[] mapTexts = new Text[12];
        Texture2D world;
        Rect bounds;
        int keyPoints = -1;

        void Start()
        {
            race = GetComponent<RaceManager>();
            var canvas = UiKit.CreateCanvas("Modo libre", 1);
            root = UiKit.Node("Raíz", canvas.transform).Stretch();

            // Minimapa
            var frame = UiKit.Panel("Minimapa", root, new Color(0, 0, 0, 0.55f));
            mini = frame.rectTransform;
            frame.gameObject.AddComponent<RectMask2D>();
            miniImage = new GameObject("Mapa", typeof(RectTransform)).AddComponent<RawImage>();
            miniImage.transform.SetParent(mini, false);
            miniImage.rectTransform.Stretch();
            miniImage.raycastTarget = false;
            miniTarget = UiKit.Panel("Destino", mini, UiKit.Amber).rectTransform;
            miniTarget.sizeDelta = new Vector2(12, 12);
            miniArrow = UiKit.Label("Coche", mini, 26, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform;
            miniArrow.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            miniArrow.GetComponent<Text>().text = "▲";
            miniArrow.GetComponent<Text>().color = UiKit.Red;

            points = UiKit.Label("Puntos", root, 26, TextAnchor.UpperLeft, FontStyle.Bold);
            points.rectTransform.Place(new Vector2(0, 1), new Vector2(24, -14), new Vector2(400, 34));
            mission = UiKit.Label("Misión", root, 22, TextAnchor.UpperCenter, FontStyle.Bold);
            mission.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(900, 30));
            prompt = UiKit.Label("Acción", root, 24, TextAnchor.MiddleCenter, FontStyle.Bold);
            prompt.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 320), new Vector2(1000, 34));
            prompt.color = UiKit.Amber;
            var ok = UiKit.Panel("OK", root, UiKit.Faint, true);
            okButton = ok.rectTransform;
            okButton.Place(new Vector2(0.5f, 0), new Vector2(0, 250), new Vector2(160, 56));
            UiKit.Label("Texto", ok.transform, 28, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Stretch();
            ok.GetComponentInChildren<Text>().text = "OK";
            ok.OnTap(() => race.TapOk());

            BuildMap(canvas.transform);
        }

        void BuildMap(Transform parent)
        {
            mapPanel = UiKit.Panel("Mapa", parent, UiKit.PanelColor, true).rectTransform;
            mapPanel.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(MapSize + 420, MapSize + 80));
            var title = UiKit.Label("Título", mapPanel, 26, TextAnchor.UpperLeft, FontStyle.Bold);
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(20, -12), new Vector2(600, 34));
            title.text = "MAPA · VIAJE RÁPIDO";
            mapImage = new GameObject("Imagen", typeof(RectTransform)).AddComponent<RawImage>();
            mapImage.transform.SetParent(mapPanel, false);
            mapImage.rectTransform.Place(new Vector2(0, 1), new Vector2(20, -56), new Vector2(MapSize, MapSize - 20));
            for (int i = 0; i < mapMarks.Length; i++)
            {
                mapMarks[i] = UiKit.Panel("Lugar", mapImage.transform, UiKit.Amber);
                mapMarks[i].rectTransform.sizeDelta = new Vector2(10, 10);
            }
            mapPlayer = UiKit.Panel("Coche", mapImage.transform, UiKit.Red).rectTransform;
            mapPlayer.sizeDelta = new Vector2(12, 12);
            for (int i = 0; i < mapRows.Length; i++)
            {
                int index = i;
                mapRows[i] = UiKit.Panel("Lugar", mapPanel, Color.clear, true);
                mapRows[i].rectTransform.Place(new Vector2(0, 1), new Vector2(MapSize + 40, -56 - i * 40), new Vector2(360, 36));
                mapTexts[i] = UiKit.Label("Nombre", mapRows[i].transform, 20, TextAnchor.MiddleLeft, FontStyle.Bold);
                mapTexts[i].rectTransform.Stretch();
                mapRows[i].OnTap(() => race.MapSelect(index));
            }
            var hint = UiKit.Label("Ayuda", mapPanel, 16, TextAnchor.LowerLeft);
            hint.rectTransform.Place(Vector2.zero, new Vector2(MapSize + 40, 14), new Vector2(360, 40));
            hint.text = "↑ ↓ elegir · Enter viajar · Esc volver\nSolo los lugares que ya has visitado";
            var close = UiKit.Panel("Cerrar", mapPanel, UiKit.Faint, true);
            close.rectTransform.Place(new Vector2(1, 1), new Vector2(-14, -12), new Vector2(120, 34));
            UiKit.Label("Texto", close.transform, 18, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Stretch();
            close.GetComponentInChildren<Text>().text = "Volver";
            close.OnTap(() => race.CloseMap());
        }

        Vector2 Uv(Vector3 p) => new Vector2((p.x - bounds.x) / bounds.width, (p.z - bounds.y) / bounds.height);

        void LateUpdate()
        {
            var player = race.player;
            bool free = race.IsFree && player != null && player.Track != null && race.State != RaceState.Menu;
            if (free && world == null && race.track != null && race.track.Geometry != null)
                world = WorldMap(race.track.Geometry, race.Map, 1024, out bounds);
            bool paused = race.State == RaceState.Paused;
            root.Show(free && !race.InFreeBattle && !paused && !race.HideHud && world != null);
            mapPanel.Show(free && paused && race.MapOpen && world != null);
            if (world == null) return;
            if (mapPanel.gameObject.activeSelf) UpdateMap(player);
            if (!root.gameObject.activeSelf) return;

            bool touch = race.TouchVisible;
            // Con controles táctiles, arriba a la izquierda junto al vaso; si no, abajo a la izquierda sobre él
            mini.Place(touch ? new Vector2(0, 1) : Vector2.zero, touch ? new Vector2(280, -60) : new Vector2(24, 100), new Vector2(MiniSize, MiniSize));
            miniImage.texture = world;
            var pos = player.transform.position;
            var uv = Uv(pos);
            float w = MiniRange / bounds.width, h = MiniRange / bounds.height;
            miniImage.uvRect = new Rect(uv.x - w, uv.y - h, 2 * w, 2 * h);
            miniArrow.localEulerAngles = new Vector3(0, 0, -player.Heading * Mathf.Rad2Deg);

            var target = race.FreeTarget;
            miniTarget.gameObject.SetActive(target.HasValue);
            if (target.HasValue)
            {
                var d = new Vector2(target.Value.x - pos.x, target.Value.z - pos.z) / MiniRange * (MiniSize / 2);
                if (d.magnitude > MiniSize / 2 - 8) d = d.normalized * (MiniSize / 2 - 8);
                miniTarget.anchoredPosition = d;
            }

            int pts = race.Profile != null ? race.Profile.points : 0;
            if (pts != keyPoints) { keyPoints = pts; points.Set($"{pts} puntos"); }
            mission.Set(race.FreeMission ?? "");
            bool hasPrompt = race.FreePrompt != null;
            prompt.Set(hasPrompt ? race.FreePrompt + (touch ? "" : "  ·  Enter") : "");
            okButton.Show(hasPrompt && touch);
        }

        void UpdateMap(Kurogane.Cars.CarController player)
        {
            mapImage.texture = world;
            var size = mapImage.rectTransform.rect.size;
            var list = race.Discovered;
            for (int i = 0; i < mapMarks.Length; i++)
            {
                bool on = i < list.Count;
                mapMarks[i].gameObject.SetActive(on);
                if (on) mapMarks[i].rectTransform.anchoredPosition = Scale(Uv(list[i].position), size);
            }
            mapPlayer.anchoredPosition = Scale(Uv(player.transform.position), size);
            for (int i = 0; i < mapRows.Length; i++)
            {
                bool on = i < list.Count;
                mapRows[i].gameObject.SetActive(on || (i == 0 && list.Count == 0));
                if (!on)
                {
                    if (i == 0 && list.Count == 0) { mapTexts[0].Set("Aún no has descubierto ningún lugar"); mapRows[0].color = Color.clear; }
                    continue;
                }
                mapTexts[i].Set(list[i].name);
                mapRows[i].color = i == race.MapRow ? UiKit.Highlight : UiKit.Faint;
            }
        }

        static Vector2 Scale(Vector2 uv, Vector2 size)
        {
            // Anclas en la esquina inferior izquierda del mapa
            return new Vector2((uv.x - 0.5f) * size.x, (uv.y - 0.5f) * size.y);
        }

        /// <summary>
        /// Mapa del mundo con el norte arriba: la carretera de montaña con su ancho y las calles del pueblo, sobre fondo
        /// oscuro. <paramref name="area"/> es el rectángulo del mundo (x, z) que cubre.
        /// </summary>
        static Texture2D WorldMap(TrackData g, TownMap map, int size, out Rect area)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            void Grow(Vector3 p) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z); }
            foreach (var p in g.P) Grow(p.pos);
            if (map != null) foreach (var n in map.nodes) Grow(n);
            float side = Mathf.Max(maxX - minX, maxZ - minZ) + 120;
            float cx = (minX + maxX) / 2, cz = (minZ + maxZ) / 2;
            area = new Rect(cx - side / 2, cz - side / 2, side, side);
            var px = new Color32[size * size];
            var back = new Color32(16, 20, 32, 255);
            for (int i = 0; i < px.Length; i++) px[i] = back;
            float scale = size / side;
            var rect = area;
            Vector2 P(Vector3 w) => new Vector2((w.x - rect.x) * scale, (w.z - rect.y) * scale);
            var road = new Color32(205, 210, 225, 255);
            var street = new Color32(150, 156, 172, 255);
            if (map != null)
                foreach (var e in map.edges)
                    Segment(px, size, P(map.nodes[e.a]), P(map.nodes[e.b]), Mathf.Max(1, e.halfWidth * scale), street);
            for (int i = 0; i + 1 < g.N; i++)
                Segment(px, size, P(g.P[i].pos), P(g.P[i + 1].pos), Mathf.Max(1.2f, g.HW[i] * scale), road);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Mapa del mundo", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        static void Segment(Color32[] px, int size, Vector2 a, Vector2 b, float r, Color32 c)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / Mathf.Max(0.5f, r * 0.5f)));
            for (int s = 0; s <= steps; s++)
            {
                var p = Vector2.Lerp(a, b, s / (float)steps);
                int x0 = Mathf.FloorToInt(p.x - r), x1 = Mathf.CeilToInt(p.x + r), y0 = Mathf.FloorToInt(p.y - r), y1 = Mathf.CeilToInt(p.y + r);
                for (int y = Mathf.Max(0, y0); y <= Mathf.Min(size - 1, y1); y++)
                    for (int x = Mathf.Max(0, x0); x <= Mathf.Min(size - 1, x1); x++)
                        if ((x - p.x) * (x - p.x) + (y - p.y) * (y - p.y) <= r * r) px[y * size + x] = c;
            }
        }
    }
}

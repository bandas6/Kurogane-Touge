using Kurogane.Controls;
using Kurogane.Race;
using UnityEngine;
using UnityEngine.UI;

namespace Kurogane.Hud
{
    /// <summary>
    /// Controles táctiles de la bajada: volante a la izquierda, gas, freno y freno de mano a la derecha, y arriba
    /// pausa, cámara y volver a la pista. Escriben en <see cref="DriverInput.Touch"/>. Las marchas manuales se
    /// cambian con los botones ▼ / ▲ de la caja del HUD. Se muestran según la opción "Táctil" del menú.
    /// </summary>
    [RequireComponent(typeof(RaceManager))]
    public class TouchControls : MonoBehaviour
    {
        static readonly Color Idle = new Color(1, 1, 1, 0.16f);
        static readonly Color Pressed = new Color(1, 1, 1, 0.42f);

        RaceManager race;
        RectTransform root;
        HoldButton left, right, gas, brake, hand;
        readonly System.Collections.Generic.List<(HoldButton button, Image image)> holds = new System.Collections.Generic.List<(HoldButton, Image)>();

        void Start()
        {
            race = GetComponent<RaceManager>();
            var canvas = UiKit.CreateCanvas("Táctil", 5);
            root = UiKit.Node("Raíz", canvas.transform).Stretch();

            left = Hold("◀", Vector2.zero, new Vector2(30, 30), new Vector2(160, 160));
            right = Hold("▶", Vector2.zero, new Vector2(210, 30), new Vector2(160, 160));
            gas = Hold("GAS", new Vector2(1, 0), new Vector2(-30, 30), new Vector2(160, 220));
            brake = Hold("FRENO", new Vector2(1, 0), new Vector2(-210, 30), new Vector2(150, 150));
            hand = Hold("MANO", new Vector2(1, 0), new Vector2(-210, 200), new Vector2(150, 100));

            Tap("II", new Vector2(0.5f, 1), new Vector2(0, -12), () => DriverInput.Touch.pause = true);
            Tap("CÁM", new Vector2(0.5f, 1), new Vector2(110, -12), () => DriverInput.Touch.camera = true);
            Tap("PISTA", new Vector2(0.5f, 1), new Vector2(-110, -12), () => DriverInput.Touch.respawn = true);
        }

        HoldButton Hold(string text, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var img = Button(text, anchor, pos, size, 30);
            var b = img.gameObject.AddComponent<HoldButton>();
            holds.Add((b, img));
            return b;
        }

        void Tap(string text, Vector2 anchor, Vector2 pos, System.Action action)
        {
            // Se dispara al tocar (no al soltar) para que responda en seguida
            var img = Button(text, anchor, pos, new Vector2(96, 60), 22);
            img.gameObject.AddComponent<HoldButton>().pressed = action;
        }

        Image Button(string text, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize)
        {
            var img = UiKit.Panel(text, root, Idle, true);
            img.rectTransform.Place(anchor, pos, size);
            var t = UiKit.Label("Texto", img.transform, fontSize, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Stretch();
            t.text = text;
            return img;
        }

        void Update()
        {
            bool visible = race.TouchVisible && (race.State == RaceState.Countdown || race.State == RaceState.Racing);
            root.Show(visible);
            if (!visible)
            {
                DriverInput.Touch.gas = DriverInput.Touch.brake = DriverInput.Touch.steer = 0;
                DriverInput.Touch.hand = false;
                return;
            }
            DriverInput.Touch.gas = gas.Held ? 1 : 0;
            DriverInput.Touch.brake = brake.Held ? 1 : 0;
            DriverInput.Touch.steer = (right.Held ? 1 : 0) - (left.Held ? 1 : 0);
            DriverInput.Touch.hand = hand.Held;
            foreach (var b in holds) b.image.color = b.button.Held ? Pressed : Idle;
        }
    }
}

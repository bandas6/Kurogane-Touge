using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Kurogane.Hud
{
    /// <summary>
    /// Piezas de uGUI creadas por código: lienzo, paneles, textos y zonas pulsables.
    /// Todo se mide en una pantalla de referencia de 1280×720 que escala con el alto.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color PanelColor = new Color(0.02f, 0.03f, 0.08f, 0.72f);
        public static readonly Color Highlight = new Color(0.8f, 0.16f, 0.22f, 0.65f);
        public static readonly Color Pale = new Color(0.9f, 0.92f, 1f);
        public static readonly Color Amber = new Color(1f, 0.74f, 0.2f);
        public static readonly Color Red = new Color(1f, 0.24f, 0.2f);
        public static readonly Color Faint = new Color(1, 1, 1, 0.1f);

        static Font font;
        public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static Canvas CreateCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 1;
            EnsureEventSystem();
            return canvas;
        }

        /// <summary>
        /// Sistema de eventos para ratón y pantalla táctil. La navegación con teclado y mando la lleva el
        /// RaceManager, por eso los botones no son Selectable: así Enter no los activa dos veces.
        /// </summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Coloca un rectángulo anclado a un punto del padre (0-1), con su pivote en ese mismo punto.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Panel(string name, Transform parent, Color color, bool raycast = false)
        {
            var img = Node(name, parent).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Text Label(string name, Transform parent, int size, TextAnchor align, FontStyle style = FontStyle.Normal)
        {
            var t = Node(name, parent).gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = Pale;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var shadow = t.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(2, -2);
            return t;
        }

        /// <summary>Hace pulsable un gráfico (clic o toque) sin convertirlo en Selectable.</summary>
        public static Tap OnTap(this Graphic g, Action action)
        {
            g.raycastTarget = true;
            var tap = g.gameObject.GetComponent<Tap>();
            if (tap == null) tap = g.gameObject.AddComponent<Tap>();
            tap.action = action;
            return tap;
        }

        /// <summary>Asigna el texto solo si cambia, para no reconstruir la malla cada cuadro.</summary>
        public static void Set(this Text t, string value)
        {
            if (t.text != value) t.text = value;
        }

        public static void Show(this Component c, bool visible)
        {
            if (c.gameObject.activeSelf != visible) c.gameObject.SetActive(visible);
        }
    }

    public class Tap : MonoBehaviour, IPointerClickHandler
    {
        public Action action;
        public void OnPointerClick(PointerEventData e) => action?.Invoke();
    }

    /// <summary>Botón que se mantiene pulsado (gas, freno, volante). Admite varios dedos a la vez.</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        readonly System.Collections.Generic.HashSet<int> pointers = new System.Collections.Generic.HashSet<int>();
        public bool Held => pointers.Count > 0;
        public Action pressed;

        public void OnPointerDown(PointerEventData e)
        {
            if (pointers.Add(e.pointerId) && pointers.Count == 1) pressed?.Invoke();
        }

        // Al deslizar el dedo fuera del botón se suelta, como en un mando táctil
        public void OnPointerUp(PointerEventData e) => pointers.Remove(e.pointerId);
        public void OnPointerExit(PointerEventData e) => pointers.Remove(e.pointerId);
        void OnDisable() => pointers.Clear();
    }

    /// <summary>Zona que gira la cámara del garaje al arrastrar con el ratón o el dedo.</summary>
    public class DragArea : MonoBehaviour, IDragHandler
    {
        public Action<float> dragged;
        public void OnDrag(PointerEventData e) => dragged?.Invoke(e.delta.x);
    }
}

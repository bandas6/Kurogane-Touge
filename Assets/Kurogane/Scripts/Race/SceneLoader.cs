using System.Collections;
using Kurogane.Hud;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kurogane.Race
{
    /// <summary>
    /// Carga una escena (Inicio o una pista) en segundo plano con una pantalla de carga. La pantalla es un lienzo
    /// en modo cámara sobre la cámara principal: la dibuja la propia cámara, así que pasa por el filtro PS2 como el
    /// resto de la imagen. Sobrevive al cambio de escena y se quita sola cuando la nueva ya está en marcha.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static bool Busy { get; private set; }

        Image bar;
        Text dots;

        /// <summary>Carga <paramref name="scene"/> mostrando <paramref name="title"/> y <paramref name="subtitle"/>.</summary>
        public static void Load(string scene, string title, string subtitle)
        {
            if (Busy) return;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError($"Kurogane: la escena {scene} no está en la lista de compilación. Usa Kurogane > Pistas para añadirla.");
                return;
            }
            Busy = true;
            Time.timeScale = 1;
            var go = new GameObject("Carga");
            DontDestroyOnLoad(go);
            go.AddComponent<SceneLoader>().StartCoroutine(go.GetComponent<SceneLoader>().Run(scene, title, subtitle));
        }

        IEnumerator Run(string scene, string title, string subtitle)
        {
            var canvas = Build(title, subtitle);
            // Un cuadro con la pantalla de carga dibujada antes de empezar (la carga puede bloquear un momento)
            yield return null;
            yield return null;
            var op = SceneManager.LoadSceneAsync(scene);
            while (!op.isDone)
            {
                SetProgress(op.progress / 0.9f);
                yield return null;
            }
            // La escena nueva tiene su cámara: se vuelve a enganchar el lienzo para los primeros cuadros
            canvas.worldCamera = Camera.main;
            SetProgress(1);
            yield return null;
            yield return null;
            Busy = false;
            Destroy(gameObject);
        }

        Canvas Build(string title, string subtitle)
        {
            var canvas = UiKit.CreateCanvas("Pantalla de carga", 100);
            canvas.transform.SetParent(transform, false);
            var cam = Camera.main;
            if (cam != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = Mathf.Max(cam.nearClipPlane + 0.05f, 0.5f);
            }
            var back = UiKit.Panel("Fondo", canvas.transform, new Color(0.01f, 0.012f, 0.03f, 1));
            back.rectTransform.Stretch();
            var t = UiKit.Label("Título", back.transform, 46, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1100, 64));
            t.text = title;
            var s = UiKit.Label("Subtítulo", back.transform, 22, TextAnchor.MiddleCenter);
            s.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(1100, 32));
            s.text = subtitle;
            var frame = UiKit.Panel("Barra", back.transform, UiKit.Faint);
            frame.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(420, 10));
            bar = UiKit.Panel("Progreso", frame.transform, UiKit.Red);
            bar.rectTransform.anchorMin = Vector2.zero;
            bar.rectTransform.anchorMax = new Vector2(0, 1);
            bar.rectTransform.offsetMin = bar.rectTransform.offsetMax = Vector2.zero;
            dots = UiKit.Label("Cargando", back.transform, 18, TextAnchor.MiddleCenter);
            dots.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(420, 26));
            SetProgress(0);
            return canvas;
        }

        void SetProgress(float p)
        {
            bar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(p), 1);
            dots.Set("CARGANDO" + new string('.', 1 + (int)(Time.unscaledTime * 3) % 3));
        }
    }
}

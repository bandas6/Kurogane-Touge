using UnityEngine;
using UnityEngine.UI;

namespace Kurogane.Retro
{
    /// <summary>
    /// Filtro PS2: la cámara dibuja a baja resolución (448 líneas, como la PS2 en NTSC) y la imagen se compone con
    /// estela entre cuadros, brillo, tramado y color de 16 bits antes de mostrarse a pantalla completa.
    /// Compone con Graphics.Blit en LateUpdate (sobre el cuadro anterior, un cuadro de retraso que no se nota) y lo
    /// muestra en un RawImage al fondo de su propio lienzo, debajo del HUD, así que no depende del pipeline.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PS2Filter : MonoBehaviour
    {
        public Shader shader;
        [Tooltip("Líneas de la imagen interna. La PS2 en NTSC dibujaba 448.")]
        [Range(224, 1080)] public int height = 448;
        [Tooltip("Cuánto queda del cuadro anterior a 60 fps (estela).")]
        [Range(0, 0.9f)] public float trail = 0.3f;
        [Range(0, 1)] public float bloomThreshold = 0.72f;
        [Range(0, 2)] public float bloomIntensity = 0.55f;
        [Tooltip("0 = sin tramado (bandas de color), 1 = tramado Bayer completo.")]
        [Range(0, 1)] public float dither = 1;
        [Tooltip("Píxeles duros al escalar. Apagado se ve suave, como en una tele de tubo.")]
        public bool sharpPixels;

        static readonly int HistoryId = Shader.PropertyToID("_History");
        static readonly int BloomId = Shader.PropertyToID("_Bloom");
        static readonly int TrailId = Shader.PropertyToID("_Trail");
        static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        static readonly int BloomIntensityId = Shader.PropertyToID("_BloomIntensity");
        static readonly int DitherId = Shader.PropertyToID("_Dither");
        static readonly int BlurDirId = Shader.PropertyToID("_BlurDir");

        Camera cam, display;
        Canvas canvas;
        RawImage screen;
        Material mat;
        RenderTexture frame, history, historyNext, bloomA, bloomB, output;
        bool historyValid;

        /// <summary>Olvida la estela, para que un corte de cámara (salida, menú) no deje fantasmas.</summary>
        public void ResetHistory() => historyValid = false;

        void OnEnable()
        {
            cam = GetComponent<Camera>();
            if (shader == null) shader = Shader.Find("Hidden/Kurogane/PS2Filter");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning("Kurogane: el shader del filtro PS2 no está disponible; se desactiva el filtro.");
                enabled = false;
                return;
            }
            mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            // Con la cámara principal dibujando en una textura, esta cámara vacía es la que presenta la pantalla
            display = new GameObject("Pantalla PS2") { hideFlags = HideFlags.DontSave }.AddComponent<Camera>();
            display.cullingMask = 0;
            display.clearFlags = CameraClearFlags.SolidColor;
            display.backgroundColor = Color.black;
            display.depth = cam.depth + 10;
            display.allowHDR = false;
            display.allowMSAA = false;

            // Lienzo al fondo de todo: los lienzos del HUD y los menús tienen un orden mayor
            canvas = new GameObject("Imagen PS2", typeof(Canvas)) { hideFlags = HideFlags.DontSave }.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100;
            screen = new GameObject("Pantalla", typeof(RectTransform)).AddComponent<RawImage>();
            screen.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)screen.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            screen.raycastTarget = false;
            screen.color = Color.black;
            EnsureTargets();
        }

        void OnDisable()
        {
            if (cam != null) cam.targetTexture = null;
            if (display != null) Destroy(display.gameObject);
            if (canvas != null) Destroy(canvas.gameObject);
            Release();
            if (mat != null) Destroy(mat);
        }

        void LateUpdate()
        {
            if (EnsureTargets()) return; // texturas nuevas: la cámara aún no ha dibujado en ellas
            Composite();
            screen.texture = output;
            screen.color = Color.white;
        }

        /// <summary>Crea las texturas si no existen o si cambió el tamaño de la pantalla. Devuelve si las creó.</summary>
        bool EnsureTargets()
        {
            int h = height, w = Mathf.Max(1, Mathf.RoundToInt(h * Screen.width / (float)Mathf.Max(1, Screen.height)));
            if (frame != null && frame.width == w && frame.height == h)
            {
                output.filterMode = sharpPixels ? FilterMode.Point : FilterMode.Bilinear;
                return false;
            }
            Release();
            var hdr = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR) ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
            frame = Create(w, h, 24, hdr);
            history = Create(w, h, 0, hdr);
            historyNext = Create(w, h, 0, hdr);
            bloomA = Create(w / 4, h / 4, 0, hdr);
            bloomB = Create(w / 4, h / 4, 0, hdr);
            output = Create(w, h, 0, RenderTextureFormat.Default);
            output.filterMode = sharpPixels ? FilterMode.Point : FilterMode.Bilinear;
            cam.targetTexture = frame;
            historyValid = false;
            if (screen != null) { screen.texture = null; screen.color = Color.black; }
            return true;
        }

        static RenderTexture Create(int w, int h, int depth, RenderTextureFormat format)
        {
            var rt = new RenderTexture(Mathf.Max(1, w), Mathf.Max(1, h), depth, format)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            rt.Create();
            return rt;
        }

        void Release()
        {
            foreach (var rt in new[] { frame, history, historyNext, bloomA, bloomB, output })
                if (rt != null) { rt.Release(); Destroy(rt); }
            frame = history = historyNext = bloomA = bloomB = output = null;
        }

        void Composite()
        {
            var previous = RenderTexture.active;

            // Estela, independiente de los fps
            if (!historyValid) { Graphics.Blit(frame, history); historyValid = true; }
            mat.SetTexture(HistoryId, history);
            mat.SetFloat(TrailId, Mathf.Pow(trail, Mathf.Clamp(Time.unscaledDeltaTime * 60, 0.25f, 4)));
            Graphics.Blit(frame, historyNext, mat, 2);
            (history, historyNext) = (historyNext, history);

            // Brillo
            mat.SetFloat(ThresholdId, bloomThreshold);
            Graphics.Blit(history, bloomA, mat, 0);
            mat.SetVector(BlurDirId, new Vector4(1, 0));
            Graphics.Blit(bloomA, bloomB, mat, 1);
            mat.SetVector(BlurDirId, new Vector4(0, 1));
            Graphics.Blit(bloomB, bloomA, mat, 1);

            // Tramado y 16 bits
            mat.SetTexture(BloomId, bloomA);
            mat.SetFloat(BloomIntensityId, bloomIntensity);
            mat.SetFloat(DitherId, dither);
            Graphics.Blit(history, output, mat, 3);

            RenderTexture.active = previous;
        }
    }
}

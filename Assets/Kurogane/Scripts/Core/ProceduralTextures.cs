using UnityEngine;

namespace Kurogane.Core
{
    /// <summary>
    /// Texturas de baja resolución con filtrado de píxel, generadas por código (como en el prototipo web).
    /// En la Fase 3 se sustituyen por texturas pintadas a mano.
    /// </summary>
    public static class ProceduralTextures
    {
        static System.Random rng = new System.Random(86);

        class Canvas
        {
            public readonly int w, h;
            readonly Color32[] px;
            public Canvas(int w, int h) { this.w = w; this.h = h; px = new Color32[w * h]; }

            // (0,0) arriba a la izquierda, igual que en canvas 2D
            public void Rect(int x, int y, int rw, int rh, Color32 c)
            {
                for (int j = y; j < y + rh; j++)
                {
                    if (j < 0 || j >= h) continue;
                    for (int i = x; i < x + rw; i++)
                    {
                        if (i < 0 || i >= w) continue;
                        px[(h - 1 - j) * w + i] = c;
                    }
                }
            }

            public void Fill(Color32 c) => Rect(0, 0, w, h, c);

            public void Blend(int x, int y, int rw, int rh, Color32 c, float a)
            {
                for (int j = y; j < y + rh; j++)
                {
                    if (j < 0 || j >= h) continue;
                    for (int i = x; i < x + rw; i++)
                    {
                        if (i < 0 || i >= w) continue;
                        int k = (h - 1 - j) * w + i;
                        px[k] = Color32.Lerp(px[k], c, a);
                    }
                }
            }

            public void Speck(int n, Color32[] cols, int sw = 1, int sh = 1)
            {
                for (int i = 0; i < n; i++) Rect(rng.Next(w), rng.Next(h), sw, sh, cols[i % cols.Length]);
            }

            public Texture2D ToTexture(string name, bool repeat = true)
            {
                var t = new Texture2D(w, h, TextureFormat.RGBA32, true)
                {
                    name = name,
                    filterMode = FilterMode.Point,
                    wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                    anisoLevel = 0
                };
                t.SetPixels32(px);
                t.Apply(true);
                return t;
            }
        }

        static Color32 H(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        static int R(int a, int b) => rng.Next(a, b);

        public static Texture2D Asphalt()
        {
            var g = new Canvas(64, 128);
            g.Fill(H("#3a3d46"));
            g.Speck(3000, new[] { H("#2c2e35"), H("#474a54"), H("#34363e"), H("#50535c") });
            for (int i = 0; i < 6; i++) g.Blend(R(6, 50), R(0, 128), R(6, 14), R(10, 30), H("#141418"), 0.35f);
            // líneas de borde blancas y doble línea central amarilla
            g.Rect(2, 0, 2, 128, H("#d9d8cc"));
            g.Rect(60, 0, 2, 128, H("#d9d8cc"));
            g.Rect(30, 0, 1, 128, H("#e6b13a"));
            g.Rect(33, 0, 1, 128, H("#e6b13a"));
            for (int i = 0; i < 50; i++) g.Blend(R(0, 64), R(0, 128), R(3, 9), 1, H("#000000"), 0.35f);
            // rodadas
            g.Blend(14, 0, 6, 128, H("#000000"), 0.18f);
            g.Blend(44, 0, 6, 128, H("#000000"), 0.18f);
            return g.ToTexture("Asphalt");
        }

        public static Texture2D Gravel()
        {
            var g = new Canvas(32, 32);
            g.Fill(H("#4a4840"));
            g.Speck(500, new[] { H("#5c5a50"), H("#35332d"), H("#6a675c"), H("#2a2925") }, 2, 1);
            return g.ToTexture("Gravel");
        }

        public static Texture2D Grass()
        {
            var g = new Canvas(64, 64);
            g.Fill(H("#1d2a1a"));
            g.Speck(1100, new[] { H("#283a22"), H("#141e12"), H("#33402a"), H("#3a3326"), H("#22301c") }, 2, 3);
            return g.ToTexture("Grass");
        }

        /// <summary>Muro de contención con retícula de hormigón (法枠) y hierba en los huecos.</summary>
        public static Texture2D RetainingWall()
        {
            var g = new Canvas(64, 64);
            g.Fill(H("#8d8f90"));
            g.Speck(600, new[] { H("#7b7d7e"), H("#a0a2a3"), H("#868889") });
            for (int y = 0; y < 2; y++)
                for (int x = 0; x < 2; x++)
                    g.Rect(x * 32 + 6, y * 32 + 6, 20, 20, H("#2a3a22"));
            for (int i = 0; i < 120; i++) g.Rect(rng.Next(2) * 32 + 6 + R(0, 20), rng.Next(2) * 32 + 6 + R(0, 20), 1, 2, H("#3a4e2c"));
            g.Blend(0, 30, 64, 2, H("#000000"), 0.25f);
            g.Blend(30, 0, 2, 64, H("#000000"), 0.25f);
            return g.ToTexture("RetainingWall");
        }

        public static Texture2D GuardRail()
        {
            var g = new Canvas(8, 32);
            var c = new[] { H("#c9ced8"), H("#8e94a0"), H("#e0e4ec"), H("#9aa0ac"), H("#c2c7d0"), H("#7c828e") };
            for (int i = 0; i < 32; i++) g.Rect(0, i, 8, 1, c[(int)(i / 5.4f) % 6]);
            return g.ToTexture("GuardRail");
        }

        public static Texture2D TunnelPanel()
        {
            var g = new Canvas(32, 32);
            g.Fill(H("#b8b4a8"));
            g.Speck(250, new[] { H("#a8a498"), H("#c8c4b8") });
            g.Rect(0, 15, 32, 1, H("#8a867c"));
            g.Rect(15, 0, 1, 32, H("#8a867c"));
            return g.ToTexture("TunnelPanel");
        }

        /// <summary>Rejilla de la canaleta (溝) del interior de las horquillas.</summary>
        public static Texture2D Grate()
        {
            var g = new Canvas(16, 16);
            g.Fill(H("#1a1b1f"));
            for (int i = 0; i < 16; i += 3) g.Rect(i, 0, 1, 16, H("#6a6c72"));
            g.Rect(0, 0, 16, 1, H("#6a6c72"));
            return g.ToTexture("Grate");
        }
    }
}

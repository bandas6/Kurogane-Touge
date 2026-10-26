using UnityEngine;

namespace Kurogane.Core
{
    /// <summary>
    /// Texturas de baja resolución con filtrado de píxel, generadas por código (como en el prototipo web).
    /// Se pueden sustituir por texturas pintadas a mano cuando las haya.
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

            public void Set(int x, int y, Color32 c) { if (x >= 0 && x < w && y >= 0 && y < h) px[(h - 1 - y) * w + x] = c; }

            /// <summary>Cambia solo el canal alfa (máscaras, como los charcos del asfalto).</summary>
            public void Alpha(int x, int y, byte a)
            {
                if (x < 0 || x >= w || y < 0 || y >= h) return;
                int k = (h - 1 - y) * w + x;
                px[k].a = a;
            }
            public Color32 Get(int x, int y) => px[(h - 1 - y) * w + x];

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

            public Texture2D ToTexture(string name, bool repeat = true, bool mips = true)
            {
                var t = new Texture2D(w, h, TextureFormat.RGBA32, mips)
                {
                    name = name,
                    filterMode = FilterMode.Point,
                    wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                    anisoLevel = 0
                };
                t.SetPixels32(px);
                t.Apply(mips);
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
            // Alfa = máscara de brillo: el material toma el smoothness de aquí. Charcos en las rodadas y los bordes,
            // que solo se notan con el asfalto mojado (WeatherSystem sube el smoothness)
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 64; x++) g.Alpha(x, y, 115);
            for (int i = 0; i < 9; i++)
            {
                int cx = rng.Next(2) == 0 ? R(12, 22) : R(42, 52), cy = R(0, 128), rx = R(3, 7), ry = R(6, 14);
                for (int y = -ry; y <= ry; y++)
                    for (int x = -rx; x <= rx; x++)
                        if (x * x * ry * ry + y * y * rx * rx <= rx * rx * ry * ry) g.Alpha(cx + x, (cy + y + 128) % 128, 255);
            }
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

        // ---------- Cielo (SkyDome) ----------

        /// <summary>Disco de luna llena con mares oscuros; fuera del disco, transparente (negro en mezcla aditiva).</summary>
        public static Texture2D Moon()
        {
            var g = new Canvas(32, 32);
            var r = new System.Random(7);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float dx = x - 15.5f, dy = y - 15.5f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 14.5f) continue;
                    float shade = 0.92f - 0.18f * (d / 14.5f) * (d / 14.5f);
                    g.Set(x, y, new Color(shade, shade * 0.97f, shade * 0.9f, 1));
                }
            // mares
            for (int i = 0; i < 7; i++)
            {
                int cx = r.Next(8, 24), cy = r.Next(8, 24), rad = r.Next(2, 5);
                for (int y = cy - rad; y <= cy + rad; y++)
                    for (int x = cx - rad; x <= cx + rad; x++)
                    {
                        if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > rad * rad) continue;
                        if (x < 0 || x > 31 || y < 0 || y > 31) continue;
                        var c = (Color)g.Get(x, y);
                        if (c.a > 0) g.Set(x, y, c * 0.78f);
                    }
            }
            return g.ToTexture("Luna", false);
        }

        /// <summary>Punto difuso con caída suave (halo de la luna, bruma del valle). Filtrado bilineal.</summary>
        public static Texture2D SoftDot()
        {
            var g = new Canvas(32, 32);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float dx = (x - 15.5f) / 15.5f, dy = (y - 15.5f) / 15.5f;
                    float a = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy));
                    a = a * a * (3 - 2 * a);
                    g.Set(x, y, new Color(a, a, a, a));
                }
            var t = g.ToTexture("PuntoSuave", false);
            t.filterMode = FilterMode.Bilinear;
            return t;
        }

        // ---------- Pueblo de la meta (bloque B) ----------

        public const int TownCellPx = 16, TownCells = 8;

        /// <summary>Rectángulo UV de una celda del atlas del pueblo, con medio píxel de margen para que no sangre.</summary>
        public static Rect TownCell(int cell)
        {
            int cx = cell % TownCells, cy = cell / TownCells;
            float size = TownCellPx * TownCells, inset = 0.5f / size;
            float u0 = cx * TownCellPx / size, v1 = 1 - cy * TownCellPx / size;
            float s = TownCellPx / size;
            return new Rect(u0 + inset, v1 - s + inset, s - 2 * inset, s - 2 * inset);
        }

        /// <summary>
        /// Atlas del pueblo (8×8 celdas de 16 px): todo el pueblo usa un único material con esta textura y su mapa
        /// de emisión, donde solo brillan ventanas encendidas, carteles, konbini, máquinas y faroles.
        /// El orden de las celdas lo da <c>Kurogane.City.TownCell</c>.
        /// </summary>
        public static Texture2D TownAtlas(out Texture2D emission)
        {
            int n = TownCellPx * TownCells;
            var g = new Canvas(n, n);
            var e = new Canvas(n, n);
            e.Fill(H("#000000"));
            var r = new System.Random(19);

            void Cell(int c, System.Action<int, int> draw) => draw(c % TownCells * TownCellPx, c / TownCells * TownCellPx);
            void Plain(int c, string hex, string speck)
            {
                Cell(c, (x, y) =>
                {
                    g.Rect(x, y, 16, 16, H(hex));
                    for (int i = 0; i < 26; i++) g.Rect(x + r.Next(16), y + r.Next(16), 1, 1, H(speck));
                });
            }
            void Glow(int c, float k)
            {
                Cell(c, (x, y) =>
                {
                    for (int j = 0; j < 16; j++)
                        for (int i = 0; i < 16; i++)
                        {
                            var col = (Color)g.Get(x + i, y + j);
                            e.Set(x + i, y + j, col * k);
                        }
                });
            }

            Plain(0, "#cfc6ae", "#bdb49c");   // pared crema
            Plain(1, "#dcdcd6", "#c8c8c2");   // pared blanca
            Plain(2, "#a8927a", "#968068");   // pared beige
            Plain(3, "#8e9aa6", "#7e8a96");   // pared gris azulado
            Cell(4, (x, y) =>                  // tablas de madera
            {
                g.Rect(x, y, 16, 16, H("#4a3426"));
                for (int k = 0; k < 16; k += 4) g.Rect(x, y + k, 16, 1, H("#2e2018"));
                for (int i = 0; i < 20; i++) g.Rect(x + r.Next(16), y + r.Next(16), 2, 1, H("#5a4232"));
            });
            Cell(5, (x, y) =>                  // tejas kawara
            {
                g.Rect(x, y, 16, 16, H("#3c4250"));
                for (int k = 0; k < 16; k += 4) g.Rect(x, y + k, 16, 1, H("#262a34"));
                for (int k = 0; k < 16; k += 3) g.Rect(x + k, y, 1, 16, H("#323844"));
            });
            void Window(int c, string glass, bool lit)
            {
                Cell(c, (x, y) =>
                {
                    g.Rect(x, y, 16, 16, H("#5a5a58"));
                    g.Rect(x + 1, y + 1, 14, 14, H(glass));
                    g.Rect(x + 7, y + 1, 2, 14, H("#5a5a58"));
                    if (lit) g.Rect(x + 1, y + 1, 14, 4, H("#ffe2a8")); // cortina iluminada
                });
                if (lit) Glow(c, 1.1f);
            }
            Window(6, "#f4c070", true);         // ventana encendida
            Window(7, "#1c2230", false);        // ventana apagada
            Cell(8, (x, y) =>                  // persiana metálica
            {
                g.Rect(x, y, 16, 16, H("#8a8e94"));
                for (int k = 0; k < 16; k += 2) g.Rect(x, y + k, 16, 1, H("#6c7076"));
            });
            Plain(9, "#eeeeea", "#e2e2de");   // konbini fachada
            Cell(10, (x, y) =>                 // konbini franjas
            {
                g.Rect(x, y, 16, 6, H("#1e8a4a"));
                g.Rect(x, y + 6, 16, 4, H("#f08a1e"));
                g.Rect(x, y + 10, 16, 6, H("#2a5ab0"));
            });
            Cell(11, (x, y) =>                 // konbini cristal con estanterías dentro
            {
                g.Rect(x, y, 16, 16, H("#e8f2ff"));
                for (int k = 3; k < 16; k += 4) g.Rect(x + 1, y + k, 14, 1, H("#9aa6b8"));
                for (int i = 0; i < 18; i++) g.Rect(x + r.Next(15), y + r.Next(15), 1, 2, i % 2 == 0 ? H("#d05050") : H("#5080d0"));
            });
            Glow(11, 1.2f);
            Plain(12, "#8c8c88", "#7a7a76");  // hormigón
            Plain(13, "#2e3038", "#3a3c44");  // asfalto
            Cell(14, (x, y) =>                 // acera de baldosas
            {
                g.Rect(x, y, 16, 16, H("#76726c"));
                g.Rect(x, y + 7, 16, 1, H("#5e5a54"));
                g.Rect(x + 7, y, 1, 16, H("#5e5a54"));
            });
            Plain(15, "#3a3c40", "#2c2e32");  // metal oscuro
            Cell(16, (x, y) =>                 // noren azul con raya blanca
            {
                g.Rect(x, y, 16, 16, H("#1c2a5a"));
                g.Rect(x + 3, y + 5, 10, 2, H("#e8e8e8"));
                g.Rect(x + 7, y + 3, 2, 8, H("#e8e8e8"));
            });
            void Vending(int c, string body, string accent)
            {
                Cell(c, (x, y) =>
                {
                    g.Rect(x, y, 16, 16, H(body));
                    g.Rect(x + 2, y + 2, 12, 7, H("#f4f4f0"));
                    for (int i = 0; i < 6; i++) g.Rect(x + 3 + i * 2, y + 4, 1, 3, H(i % 2 == 0 ? accent : "#3080d0"));
                    g.Rect(x + 4, y + 11, 8, 3, H("#202020"));
                });
                Glow(c, 1.0f);
            }
            Vending(17, "#c02828", "#f0d020");  // máquina roja
            Vending(18, "#2858b0", "#f04040");  // máquina azul
            Vending(19, "#f0f0ec", "#30a050");  // máquina blanca
            Plain(20, "#c03030", "#d84040");  // cartel rojo
            Glow(20, 0.9f);
            Plain(21, "#2a52a0", "#3a62b0");  // cartel azul
            Glow(21, 0.9f);
            Plain(22, "#f6f6f0", "#ffffff");  // luz de marquesina
            Glow(22, 1.4f);
            Plain(23, "#1a2236", "#22304a");  // agua del arrozal
            Glow(23, 0.18f);
            Plain(24, "#3a4028", "#4a5032");  // caballón de tierra con hierba
            Plain(25, "#4a3a2a", "#3a2c20");  // traviesa
            Plain(26, "#a0a4aa", "#c0c4ca");  // carril
            Cell(27, (x, y) =>                 // franjas amarillas y negras
            {
                for (int k = 0; k < 16; k += 4) { g.Rect(x, y + k, 16, 2, H("#e8c020")); g.Rect(x, y + k + 2, 16, 2, H("#181818")); }
            });
            Plain(28, "#202224", "#2a2c2e");  // carcasa de semáforo
            Plain(29, "#5a4030", "#4a3424");  // puerta de madera
            Cell(30, (x, y) =>                 // barandilla de balcón
            {
                g.Rect(x, y, 16, 16, H("#2a2c30"));
                for (int k = 1; k < 16; k += 3) g.Rect(x + k, y, 1, 16, H("#9aa0a6"));
                g.Rect(x, y, 16, 1, H("#9aa0a6"));
            });
            Cell(31, (x, y) =>                 // aire acondicionado exterior
            {
                g.Rect(x, y, 16, 16, H("#d8d8d2"));
                for (int k = 3; k < 13; k += 2) g.Rect(x + 3, y + k, 8, 1, H("#8a8a86"));
            });
            Cell(32, (x, y) =>                 // cabina de teléfono: cristal verde iluminado
            {
                g.Rect(x, y, 16, 16, H("#2a8a4a"));
                g.Rect(x + 2, y + 2, 12, 12, H("#c8f0d8"));
            });
            Glow(32, 0.9f);
            Plain(33, "#1c2c1c", "#243624");  // cedro
            Plain(34, "#2c2018", "#3a2a20");  // tronco
            Plain(35, "#5a5850", "#4a4840");  // grava de la vía
            Plain(36, "#e8e8e0", "#ffffff");  // pintura blanca
            Cell(37, (x, y) =>                 // tablero de madera clara del cartel
            {
                g.Rect(x, y, 16, 16, H("#8a6a40"));
                g.Rect(x, y, 16, 1, H("#5a4028")); g.Rect(x, y + 15, 16, 1, H("#5a4028"));
            });
            Plain(38, "#e04a2a", "#f06a3a");  // farolillo rojo (chōchin)
            Glow(38, 1.2f);
            Plain(39, "#ffd080", "#fff0c0");  // farola de calle
            Glow(39, 1.4f);

            emission = e.ToTexture("PuebloEmision", false, false);
            return g.ToTexture("PuebloAtlas", false, false);
        }

        /// <summary>Fachada con ventanas para la ciudad media del valle: muro oscuro y ventanas encendidas al azar. Se repite.</summary>
        public static Texture2D CityWindows()
        {
            var g = new Canvas(32, 32);
            var r = new System.Random(23);
            g.Fill(H("#0c0e16"));
            for (int y = 2; y < 32; y += 4)
                for (int x = 1; x < 32; x += 3)
                {
                    int roll = r.Next(10);
                    var c = roll < 4 ? H("#ffcf7a") : roll < 5 ? H("#cfe4ff") : H("#151a26");
                    g.Rect(x, y, 2, 2, c);
                }
            return g.ToTexture("VentanasCiudad");
        }

        // ---------- Lluvia (bloque C) ----------

        /// <summary>
        /// Mapa de gotas para el filtro PS2: RG = hacia dónde desvía cada gota la imagen (0,5 = nada), A = dónde hay
        /// gota. Gotas redondas con un reguero hacia arriba (se deslizan hacia abajo). Se repite y es bilineal.
        /// </summary>
        public static Texture2D RainDrops()
        {
            const int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Gotas", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int k = 0; k < px.Length; k++) px[k] = new Color32(128, 128, 128, 0);
            var r = new System.Random(5);
            for (int i = 0; i < 46; i++)
            {
                int cx = r.Next(n), cy = r.Next(n), rad = r.Next(2, 7);
                for (int y = -rad; y <= rad; y++)
                    for (int x = -rad; x <= rad; x++)
                    {
                        float d = Mathf.Sqrt(x * x + y * y) / rad;
                        if (d > 1) continue;
                        int k = ((cy + y + n) % n) * n + (cx + x + n) % n;
                        px[k] = new Color32((byte)(128 + x * 127 / rad), (byte)(128 + y * 127 / rad), 128, (byte)(255 * (1 - d * d)));
                    }
                // reguero por encima de la gota
                int trail = r.Next(0, rad * 4);
                for (int y = 1; y <= trail; y++)
                {
                    int k = ((cy + rad + y) % n) * n + cx % n;
                    px[k] = new Color32(128, 150, 128, (byte)(110 * (1 - y / (float)(trail + 1))));
                }
            }
            t.SetPixels32(px);
            t.Apply(false);
            return t;
        }

        // ---------- Señales (Fase 3) ----------

        // Fuente de píxel 3×5 para números y rótulos cortos
        static readonly System.Collections.Generic.Dictionary<char, string> Glyphs = new System.Collections.Generic.Dictionary<char, string>
        {
            ['0'] = "111101101101111", ['1'] = "010110010010111", ['2'] = "111001111100111", ['3'] = "111001111001111",
            ['4'] = "101101111001001", ['5'] = "111100111001111", ['6'] = "111100111101111", ['7'] = "111001010010010",
            ['8'] = "111101111101111", ['9'] = "111101111001111", ['.'] = "000000000000010", [' '] = "000000000000000",
            ['A'] = "010101111101101", ['E'] = "111100110100111", ['G'] = "111100101101111", ['K'] = "101101110101101",
            ['M'] = "101111111101101", ['N'] = "110101101101101", ['O'] = "111101101101111", ['R'] = "110101110101101",
            ['U'] = "101101101101111", ['T'] = "111010010010010", ['L'] = "100100100100111", ['I'] = "111010010010111",
        };

        /// <summary>Escribe <paramref name="text"/> centrado en (cx, y) con píxeles de tamaño <paramref name="scale"/>.</summary>
        static void Text(Canvas g, string text, int cx, int y, int scale, Color32 c)
        {
            int width = text.Length * 4 * scale - scale;
            int x = cx - width / 2;
            foreach (char ch in text)
            {
                if (Glyphs.TryGetValue(char.ToUpperInvariant(ch), out var bits))
                    for (int k = 0; k < 15; k++)
                        if (bits[k] == '1') g.Rect(x + (k % 3) * scale, y + (k / 3) * scale, scale, scale, c);
                x += 4 * scale;
            }
        }

        static Canvas Mirror(Canvas g, bool mirror)
        {
            if (!mirror) return g;
            var m = new Canvas(g.w, g.h);
            for (int y = 0; y < g.h; y++)
                for (int x = 0; x < g.w; x++) m.Set(x, y, g.Get(g.w - 1 - x, y));
            return m;
        }

        /// <summary>Panel de chevrones para el exterior de las horquillas: flechas negras sobre amarillo hacia el giro.</summary>
        public static Texture2D Chevron(bool left)
        {
            var g = new Canvas(16, 20);
            g.Fill(H("#e8b81e"));
            g.Rect(0, 0, 16, 1, H("#1a1a1a")); g.Rect(0, 19, 16, 1, H("#1a1a1a"));
            g.Rect(0, 0, 1, 20, H("#1a1a1a")); g.Rect(15, 0, 1, 20, H("#1a1a1a"));
            // flecha ">" de dos trazos
            for (int y = 0; y < 8; y++)
            {
                g.Rect(4 + y, 2 + y, 3, 1, H("#141414"));
                g.Rect(4 + y, 17 - y, 3, 1, H("#141414"));
            }
            return Mirror(g, left).ToTexture(left ? "ChevronIzq" : "ChevronDer", false);
        }

        /// <summary>Señal de peligro en rombo amarillo con una curva cerrada (horquilla) hacia el lado del giro.</summary>
        public static Texture2D HairpinWarning(bool left)
        {
            var g = new Canvas(32, 32);
            var yellow = H("#e8b81e");
            var black = H("#151515");
            for (int y = 0; y < 32; y++)
            {
                int half = y < 16 ? y : 31 - y;
                g.Rect(15 - half, y, half * 2 + 2, 1, black);
                if (half > 1) g.Rect(17 - half, y, half * 2 - 2, 1, yellow);
            }
            // horquilla: sube, gira 180° hacia la derecha y baja con punta de flecha
            g.Rect(11, 11, 2, 12, black);
            g.Rect(11, 9, 9, 2, black);
            g.Rect(19, 9, 2, 9, black);
            g.Rect(17, 17, 6, 2, black);
            g.Rect(18, 19, 4, 1, black);
            g.Rect(19, 20, 2, 1, black);
            return Mirror(g, left).ToTexture(left ? "AvisoHorquillaIzq" : "AvisoHorquillaDer", false);
        }

        /// <summary>Límite de velocidad: número negro en círculo blanco con anillo rojo.</summary>
        public static Texture2D SpeedLimit(int kmh)
        {
            var g = new Canvas(32, 32);
            g.Fill(new Color32(0, 0, 0, 0));
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float d = Mathf.Sqrt((x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f));
                    if (d < 15.5f) g.Set(x, y, d > 11.5f ? H("#c41e24") : H("#f2f2ee"));
                }
            Text(g, kmh.ToString(), 16, 11, 2, H("#1c2a6e"));
            return g.ToTexture("Limite" + kmh, false);
        }

        /// <summary>Poste kilométrico: distancia desde la salida en km, negro sobre blanco.</summary>
        public static Texture2D KmPost(string km)
        {
            var g = new Canvas(16, 24);
            g.Fill(H("#efefea"));
            g.Rect(0, 0, 16, 4, H("#2b5a2e"));
            Text(g, km, 8, 9, 1, H("#1a1a1a"));
            return g.ToTexture("Km" + km, false);
        }

        /// <summary>Rótulo de la boca del túnel: letras blancas sobre verde.</summary>
        public static Texture2D TunnelPlate(string name)
        {
            var g = new Canvas(name.Length * 4 + 5, 11);
            g.Fill(H("#1f5a3a"));
            g.Rect(0, 0, g.w, 1, H("#e6e6e0")); g.Rect(0, 10, g.w, 1, H("#e6e6e0"));
            g.Rect(0, 0, 1, 11, H("#e6e6e0")); g.Rect(g.w - 1, 0, 1, 11, H("#e6e6e0"));
            Text(g, name, g.w / 2 + 1, 3, 1, H("#f0f0ea"));
            return g.ToTexture("Tunel" + name, false);
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

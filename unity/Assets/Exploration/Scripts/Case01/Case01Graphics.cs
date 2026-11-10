using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// Phase 1.9：世界內圖文的正式載體。圖像（相片、日曆、街區舊地圖、旅行路線、粉筆記號、黃銅刻紋符號、紙張底紋）全部在程式內繪製
    /// （自製、無外部素材、無授權問題）；文字用世界內畫布寫在紙／搪瓷牌上，依載體實際尺寸縮放（不超出載體）。
    /// </summary>
    public static class Case01Graphics
    {
        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        private static Font hand;

        /// <summary>手寫感的字型（標楷體等；沒有就退回正黑體）。</summary>
        public static Font Hand
        {
            get
            {
                if (hand != null) return hand;
                try { hand = Font.CreateDynamicFontFromOSFont(new[] { "DFKai-SB", "BiauKai", "KaiTi", "STKaiti", "Microsoft JhengHei UI", "Microsoft JhengHei" }, 32); }
                catch (System.Exception) { hand = null; }
                if (hand == null) hand = Case01Hud.UiFont;
                return hand;
            }
        }

        // ---------------- 繪圖工具 ----------------

        private sealed class Canvas2D
        {
            public readonly int W, H;
            public readonly Color[] Px;

            public Canvas2D(int w, int h, Color fill)
            {
                W = w;
                H = h;
                Px = new Color[w * h];
                for (int i = 0; i < Px.Length; i++) Px[i] = fill;
            }

            public void Blend(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= W || y >= H) return;
                int i = y * W + x;
                Px[i] = Color.Lerp(Px[i], new Color(c.r, c.g, c.b, Mathf.Max(Px[i].a, c.a)), c.a);
            }

            public void Rect(float x0, float y0, float x1, float y1, Color c)
            {
                for (int y = Mathf.Max(0, (int)(y0 * H)); y < Mathf.Min(H, (int)(y1 * H)); y++)
                for (int x = Mathf.Max(0, (int)(x0 * W)); x < Mathf.Min(W, (int)(x1 * W)); x++)
                    Blend(x, y, c);
            }

            public void Line(float ax, float ay, float bx, float by, float width, Color c)
            {
                float len = Mathf.Max(1f, Vector2.Distance(new Vector2(ax * W, ay * H), new Vector2(bx * W, by * H)));
                for (float t = 0f; t <= 1f; t += 0.5f / len)
                {
                    float x = Mathf.Lerp(ax, bx, t) * W, y = Mathf.Lerp(ay, by, t) * H;
                    for (int dy = (int)-width; dy <= (int)width; dy++)
                    for (int dx = (int)-width; dx <= (int)width; dx++)
                        if (dx * dx + dy * dy <= width * width) Blend((int)x + dx, (int)y + dy, c);
                }
            }

            public void Ring(float cx, float cy, float r, float width, Color c)
            {
                for (float a = 0f; a < Mathf.PI * 2f; a += 0.02f)
                    Line(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r * W / H, cx + Mathf.Cos(a + 0.02f) * r, cy + Mathf.Sin(a + 0.02f) * r * W / H, width, c);
            }

            public void Disc(float cx, float cy, float r, Color c)
            {
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = x / (float)W - cx, dy = (y / (float)H - cy) * H / W;
                    if (dx * dx + dy * dy <= r * r) Blend(x, y, c);
                }
            }

            public void Noise(float amount, int seed)
            {
                var rng = new System.Random(seed);
                for (int i = 0; i < Px.Length; i++)
                {
                    float n = ((float)rng.NextDouble() - 0.5f) * amount;
                    Px[i] = new Color(Px[i].r + n, Px[i].g + n, Px[i].b + n, Px[i].a);
                }
            }

            public void Vignette(float amount)
            {
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = x / (float)W - 0.5f, dy = y / (float)H - 0.5f;
                    float k = 1f - amount * Mathf.Clamp01((dx * dx + dy * dy) * 2.6f);
                    int i = y * W + x;
                    Px[i] = new Color(Px[i].r * k, Px[i].g * k, Px[i].b * k, Px[i].a);
                }
            }

            public Texture2D Done(string name)
            {
                var t = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "Case01Gfx_" + name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
                t.SetPixels(Px);
                t.Apply(true);
                return t;
            }
        }

        /// <summary>筆畫數字（地圖上的門牌號、黃銅牌的「17」）：七段式粗筆畫。</summary>
        private static void Digit(Canvas2D c, char d, float x, float y, float w, float h, float width, Color col)
        {
            // 段：a 上 b 右上 c 右下 d 下 e 左下 f 左上 g 中
            string segs;
            switch (d)
            {
                case '0': segs = "abcdef"; break;
                case '1': segs = "bc"; break;
                case '2': segs = "abged"; break;
                case '3': segs = "abgcd"; break;
                case '4': segs = "fgbc"; break;
                case '5': segs = "afgcd"; break;
                case '6': segs = "afgedc"; break;
                case '7': segs = "abc"; break;
                case '8': segs = "abcdefg"; break;
                default: segs = "abcfgd"; break;
            }
            float x1 = x + w, ym = y + h * 0.5f, y1 = y + h;
            foreach (var s in segs)
                switch (s)
                {
                    case 'a': c.Line(x, y1, x1, y1, width, col); break;
                    case 'b': c.Line(x1, y1, x1, ym, width, col); break;
                    case 'c': c.Line(x1, ym, x1, y, width, col); break;
                    case 'd': c.Line(x, y, x1, y, width, col); break;
                    case 'e': c.Line(x, ym, x, y, width, col); break;
                    case 'f': c.Line(x, y1, x, ym, width, col); break;
                    case 'g': c.Line(x, ym, x1, ym, width, col); break;
                }
        }

        private static void Number(Canvas2D c, string n, float x, float y, float h, float width, Color col)
        {
            float w = h * 0.5f;
            for (int i = 0; i < n.Length; i++) Digit(c, n[i], x + i * w * 1.5f, y, w, h, width, col);
        }

        private static readonly Color PaperC = new Color(0.93f, 0.9f, 0.82f);
        private static readonly Color Ink = new Color(0.12f, 0.12f, 0.16f);
        private static readonly Color Graphite = new Color(0.32f, 0.32f, 0.34f);

        // ---------------- 圖像 ----------------

        public static Texture2D Tex(string name)
        {
            Texture2D t;
            if (cache.TryGetValue(name, out t) && t != null) return t;
            t = Make(name);
            cache[name] = t;
            return t;
        }

        private static void Figure(Canvas2D c, float x, float h, Color col)
        {
            float top = 0.22f + h;
            c.Disc(x, top - 0.035f, 0.032f, col);
            for (float y = 0.22f; y < top - 0.07f; y += 0.004f)
            {
                float k = (y - 0.22f) / Mathf.Max(0.01f, top - 0.29f);
                float half = Mathf.Lerp(0.045f, 0.03f, k);
                c.Rect(x - half, y, x + half, y + 0.005f, col);
            }
        }

        private static Texture2D Photo(string name, bool faded, bool beach)
        {
            var c = new Canvas2D(256, 192, new Color(0.96f, 0.94f, 0.88f));
            // 天空、海、沙灘（色調偏暖，像放久了的相紙）。
            for (int y = 0; y < c.H; y++)
            {
                float v = y / (float)c.H;
                Color col = v > 0.52f ? Color.Lerp(new Color(0.78f, 0.82f, 0.82f), new Color(0.62f, 0.72f, 0.8f), (v - 0.52f) / 0.48f)
                    : v > 0.42f ? new Color(0.45f, 0.55f, 0.62f) : Color.Lerp(new Color(0.78f, 0.7f, 0.55f), new Color(0.7f, 0.62f, 0.48f), v / 0.42f);
                for (int x = 0; x < c.W; x++) c.Px[y * c.W + x] = col;
            }
            if (!beach)
            {
                // 母女並肩：母親較高、女兒靠著她。
                Figure(c, 0.44f, 0.5f, new Color(0.22f, 0.18f, 0.17f));
                var second = new Color(0.3f, 0.22f, 0.2f, faded ? 0.18f : 1f);
                Figure(c, 0.56f, 0.44f, second);
            }
            else
            {
                Figure(c, 0.32f, 0.3f, new Color(0.24f, 0.2f, 0.18f));
                Figure(c, 0.4f, 0.26f, new Color(0.3f, 0.22f, 0.2f));
                c.Line(0.62f, 0.44f, 0.9f, 0.48f, 1.5f, new Color(0.9f, 0.9f, 0.88f, 0.6f));
            }
            // 褪色、暗角、相紙白邊。
            for (int i = 0; i < c.Px.Length; i++)
            {
                var p = c.Px[i];
                float g = p.r * 0.3f + p.g * 0.59f + p.b * 0.11f;
                c.Px[i] = Color.Lerp(p, new Color(g * 1.08f, g * 0.98f, g * 0.82f), 0.45f);
            }
            c.Vignette(0.45f);
            c.Noise(0.05f, name.GetHashCode());
            var border = new Color(0.96f, 0.94f, 0.88f);
            c.Rect(0f, 0f, 1f, 0.06f, border);
            c.Rect(0f, 0.94f, 1f, 1f, border);
            c.Rect(0f, 0f, 0.045f, 1f, border);
            c.Rect(0.955f, 0f, 1f, 1f, border);
            return c.Done(name);
        }

        private static Texture2D Make(string name)
        {
            switch (name)
            {
                case "photo_family": return Photo(name, false, false);
                case "photo_family_faded": return Photo(name, true, false);
                case "photo_beach": return Photo(name, false, true);
                case "calendar":
                {
                    var c = new Canvas2D(256, 320, PaperC);
                    c.Rect(0f, 0.8f, 1f, 1f, new Color(0.55f, 0.18f, 0.16f));
                    for (int i = 0; i <= 7; i++) c.Line(0.06f + i * 0.125f, 0.06f, 0.06f + i * 0.125f, 0.74f, 0.6f, new Color(0.6f, 0.58f, 0.55f));
                    for (int j = 0; j <= 5; j++) c.Line(0.06f, 0.06f + j * 0.136f, 0.94f, 0.06f + j * 0.136f, 0.6f, new Color(0.6f, 0.58f, 0.55f));
                    for (int row = 0; row < 5; row++)
                    {
                        for (int col = 0; col < 7; col++) Number(c, ((row * 7 + col) % 30 + 1).ToString(), 0.075f + col * 0.125f, 0.68f - row * 0.136f, 0.035f, 0.6f, new Color(0.3f, 0.3f, 0.32f));
                        // 每個週日都用紅筆圈起來，越後面越淡。
                        c.Ring(0.1225f, 0.128f + (4 - row) * 0.136f, 0.05f, 1.6f, new Color(0.75f, 0.12f, 0.1f, Mathf.Lerp(0.95f, 0.22f, row / 4f)));
                    }
                    c.Noise(0.03f, 7);
                    return c.Done(name);
                }
                case "map":
                {
                    var c = new Canvas2D(256, 256, new Color(0.88f, 0.84f, 0.72f));
                    var street = new Color(0.74f, 0.7f, 0.6f);
                    c.Rect(0f, 0.42f, 1f, 0.56f, street);
                    c.Rect(0.08f, 0f, 0.16f, 1f, street);
                    var line = new Color(0.36f, 0.3f, 0.24f);
                    string[] nums = { "15", "16", "", "18", "19" };
                    for (int i = 0; i < 5; i++)
                    {
                        float x0 = 0.2f + i * 0.155f;
                        if (i == 2) continue;
                        c.Line(x0, 0.58f, x0 + 0.13f, 0.58f, 1f, line);
                        c.Line(x0, 0.92f, x0 + 0.13f, 0.92f, 1f, line);
                        c.Line(x0, 0.58f, x0, 0.92f, 1f, line);
                        c.Line(x0 + 0.13f, 0.58f, x0 + 0.13f, 0.92f, 1f, line);
                        Number(c, nums[i], x0 + 0.03f, 0.7f, 0.07f, 1f, line);
                    }
                    // 16 與 18 之間：鉛筆補畫的一格與「17」。
                    float px = 0.2f + 2 * 0.155f;
                    c.Line(px + 0.01f, 0.59f, px + 0.12f, 0.6f, 1.2f, Graphite);
                    c.Line(px + 0.12f, 0.6f, px + 0.125f, 0.91f, 1.2f, Graphite);
                    c.Line(px + 0.125f, 0.91f, px + 0.005f, 0.905f, 1.2f, Graphite);
                    c.Line(px + 0.005f, 0.905f, px + 0.01f, 0.59f, 1.2f, Graphite);
                    Number(c, "17", px + 0.025f, 0.69f, 0.08f, 1.4f, Graphite);
                    for (int i = 0; i < 5; i++) c.Line(0.2f + i * 0.155f, 0.38f, 0.33f + i * 0.155f, 0.38f, 0.6f, new Color(0.5f, 0.45f, 0.36f, 0.6f));
                    c.Noise(0.04f, 3);
                    c.Vignette(0.25f);
                    return c.Done(name);
                }
                case "route":
                {
                    var c = new Canvas2D(256, 192, new Color(0.9f, 0.87f, 0.78f));
                    for (float x = 0f; x < 1f; x += 0.004f) c.Line(x, 0.25f + Mathf.Sin(x * 9f) * 0.05f, x + 0.004f, 0.25f + Mathf.Sin((x + 0.004f) * 9f) * 0.05f, 1f, new Color(0.35f, 0.5f, 0.6f));
                    var red = new Color(0.72f, 0.14f, 0.12f);
                    var pts = new[] { new Vector2(0.12f, 0.82f), new Vector2(0.3f, 0.7f), new Vector2(0.45f, 0.62f), new Vector2(0.62f, 0.48f), new Vector2(0.8f, 0.34f) };
                    for (int i = 0; i + 1 < pts.Length; i++)
                        for (float t = 0f; t < 1f; t += 0.2f) c.Line(Mathf.Lerp(pts[i].x, pts[i + 1].x, t), Mathf.Lerp(pts[i].y, pts[i + 1].y, t), Mathf.Lerp(pts[i].x, pts[i + 1].x, t + 0.1f), Mathf.Lerp(pts[i].y, pts[i + 1].y, t + 0.1f), 1.4f, red);
                    c.Ring(0.8f, 0.34f, 0.035f, 1.6f, red);
                    c.Disc(0.12f, 0.82f, 0.015f, Ink);
                    c.Noise(0.03f, 5);
                    return c.Done(name);
                }
                case "chalk_triangle":
                {
                    var c = new Canvas2D(128, 128, new Color(1f, 1f, 1f, 0f));
                    var chalk = new Color(0.93f, 0.93f, 0.9f, 0.85f);
                    c.Line(0.2f, 0.2f, 0.8f, 0.22f, 2.2f, chalk);
                    c.Line(0.8f, 0.22f, 0.51f, 0.82f, 2.2f, chalk);
                    c.Line(0.51f, 0.82f, 0.19f, 0.19f, 2.2f, chalk);
                    var rng = new System.Random(9);
                    for (int i = 0; i < c.Px.Length; i++) if (c.Px[i].a > 0f && rng.NextDouble() < 0.35) c.Px[i].a *= 0.4f;
                    return c.Done(name);
                }
                case "sym_triangle":
                case "sym_circle":
                case "sym_square17":
                {
                    var c = new Canvas2D(128, 128, new Color(0.72f, 0.56f, 0.28f));
                    for (int y = 0; y < c.H; y++)
                        for (int x = 0; x < c.W; x++)
                        {
                            float k = 0.85f + 0.3f * (y / 128f) - 0.12f * Mathf.Abs(x / 128f - 0.5f);
                            var p = c.Px[y * c.W + x];
                            c.Px[y * c.W + x] = new Color(p.r * k, p.g * k, p.b * k, 1f);
                        }
                    var engrave = new Color(0.18f, 0.12f, 0.06f);
                    if (name == "sym_triangle")
                    {
                        c.Line(0.22f, 0.24f, 0.78f, 0.24f, 3f, engrave);
                        c.Line(0.78f, 0.24f, 0.5f, 0.8f, 3f, engrave);
                        c.Line(0.5f, 0.8f, 0.22f, 0.24f, 3f, engrave);
                    }
                    else if (name == "sym_circle") c.Ring(0.5f, 0.5f, 0.3f, 3f, engrave);
                    else
                    {
                        c.Line(0.18f, 0.2f, 0.82f, 0.2f, 3f, engrave);
                        c.Line(0.82f, 0.2f, 0.82f, 0.8f, 3f, engrave);
                        c.Line(0.82f, 0.8f, 0.18f, 0.8f, 3f, engrave);
                        c.Line(0.18f, 0.8f, 0.18f, 0.2f, 3f, engrave);
                        Number(c, "17", 0.33f, 0.34f, 0.32f, 2.6f, engrave);
                    }
                    c.Ring(0.5f, 0.5f, 0.47f, 1.5f, new Color(0.45f, 0.33f, 0.15f));
                    c.Noise(0.06f, name.GetHashCode());
                    return c.Done(name);
                }
                case "paper_ruled":
                {
                    var c = new Canvas2D(128, 160, PaperC);
                    for (float y = 0.1f; y < 0.92f; y += 0.075f) c.Line(0.06f, y, 0.94f, y, 0.5f, new Color(0.62f, 0.7f, 0.82f, 0.7f));
                    c.Line(0.14f, 0f, 0.14f, 1f, 0.5f, new Color(0.8f, 0.4f, 0.4f, 0.6f));
                    c.Noise(0.04f, 11);
                    return c.Done(name);
                }
                case "enamel":
                {
                    // 搪瓷牌：深綠底、米白細邊框（字由世界畫布另外寫，保持清晰）。
                    var c = new Canvas2D(384, 128, new Color(0.12f, 0.24f, 0.2f));
                    c.Rect(0.025f, 0.08f, 0.975f, 0.92f, new Color(0.86f, 0.82f, 0.7f));
                    c.Rect(0.035f, 0.11f, 0.965f, 0.89f, new Color(0.14f, 0.29f, 0.24f));
                    c.Noise(0.03f, 13);
                    c.Vignette(0.35f);
                    return c.Done(name);
                }
                default:
                {
                    var c = new Canvas2D(128, 128, PaperC);
                    c.Noise(0.05f, name.GetHashCode());
                    c.Vignette(0.2f);
                    return c.Done(name);
                }
            }
        }

        // ---------------- 載體 ----------------

        /// <summary>貼在物件表面的一張圖（紙、相片、牌）。forward＝面向觀看者的方向。</summary>
        public static Transform Sheet(Transform parent, string name, Vector3 pos, Vector3 forward, Vector2 size, Material m, float tiltDeg = 0f)
        {
            var q = ArenaArt.Prim(PrimitiveType.Quad, parent, Vector3.zero, new Vector3(size.x, size.y, 1f), m, null, name, false, false);
            q.position = pos;
            q.rotation = Quaternion.LookRotation(-forward.normalized, Mathf.Abs(forward.normalized.y) > 0.9f ? Vector3.forward : Vector3.up) * Quaternion.Euler(0f, 0f, tiltDeg);
            q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return q;
        }

        public static Material Mat(Case01Palette p, string tex, float smooth = 0.1f)
        {
            var m = p.Own(ArenaArt.Mat(Color.white, smooth));
            m.mainTexture = Tex(tex);
            return m;
        }

        public static Material Decal(Case01Palette p, string tex)
        {
            var m = p.Own(ArenaArt.FadeMat(Color.white, Color.black));
            m.mainTexture = Tex(tex);
            return m;
        }

        /// <summary>
        /// 在一張載體（Sheet）上寫字：畫布放在載體前 1 mm，字依載體實際尺寸縮到裝得下（留邊）。回傳實際字高（公尺，每行）。
        /// </summary>
        public static float Write(Transform sheet, string text, Color color, float margin = 0.08f, bool handwriting = true, int size = 28, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject("Writing", typeof(RectTransform), typeof(Canvas));
            var rt = (RectTransform)go.transform;
            rt.SetParent(sheet, false);
            rt.localPosition = new Vector3(0f, 0f, -0.002f / Mathf.Max(1e-3f, sheet.lossyScale.z));
            rt.localRotation = Quaternion.identity;
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var tgo = new GameObject("Text", typeof(RectTransform));
            var t = tgo.AddComponent<Text>();
            ((RectTransform)tgo.transform).SetParent(rt, false);
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            t.font = handwriting ? Hand : Case01Hud.UiFont;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.supportRichText = true;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            // 世界尺寸：載體（Quad 縮放＝寬高）內扣邊。父物件有縮放，所以換算回本地。
            var s = sheet.lossyScale;
            float maxW = s.x * (1f - 2f * margin), maxH = s.y * (1f - 2f * margin);
            float pw = Mathf.Max(1f, t.preferredWidth) + 2f, ph = Mathf.Max(1f, t.preferredHeight) + 2f;
            rt.sizeDelta = new Vector2(pw, ph);
            float k = Mathf.Min(maxW / pw, maxH / ph);
            rt.localScale = new Vector3(k / Mathf.Max(1e-4f, s.x), k / Mathf.Max(1e-4f, s.y), 1f);
            int lines = Mathf.Max(1, text.Split('\n').Length);
            return ph * k / lines;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 提燈「顯示真實」的共用規則與材質工廠（原型）。
    /// 光圈＝世界空間的一顆球：球內顯示真實、球外顯示異域。所有使用 LanternReveal shader 的物件依同一個全域值判斷。
    /// </summary>
    public static class LanternReveal
    {
        public const string ShaderName = "Shepherd/Prototype/LanternReveal";
        public const string ShaderResource = "LanternReveal";
        public static readonly int SphereId = Shader.PropertyToID("_ShepRevealSphere");

        public enum Mode { Dual = 0, RealOnly = 1, AnomalyOnly = 2 }

        private static Shader shader;

        public static Shader Shader
        {
            get
            {
                if (shader == null) shader = Resources.Load<Shader>(ShaderResource);
                if (shader == null) shader = Shader.Find(ShaderName);
                return shader;
            }
        }

        public static readonly int ExtraId = Shader.PropertyToID("_ShepRevealExtra");
        public const int ExtraCount = 8;
        /// <summary>前 4 格給暫時的撞擊泡泡，後 4 格給永久的穩定區。</summary>
        public const int TempSlots = 4;

        public static readonly int ForwardId = Shader.PropertyToID("_ShepRevealForward");
        public static readonly int ShapeId = Shader.PropertyToID("_ShepRevealShape");
        public static Vector3 Center { get; private set; }
        public static float Radius { get; private set; }
        public static Vector3 Forward { get; private set; } = Vector3.forward;
        /// <summary>(前方倍率, 側面倍率, 後方倍率)；(1,1,1)＝圓球。</summary>
        public static Vector3 Shape { get; private set; } = Vector3.one;
        /// <summary>側面寬度每公尺增加的倍率（錐形）。</summary>
        public static float Spread { get; private set; }

        /// <summary>設定提燈光的形狀（往前偏的蛋形；聚光時變成細長光束）。</summary>
        public static void SetShape(Vector3 forward, Vector3 shape, float spread = 0f)
        {
            Forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            Shape = shape;
            Spread = spread;
            Shader.SetGlobalVector(ForwardId, Forward);
            Shader.SetGlobalVector(ShapeId, new Vector4(shape.x, shape.y, shape.z, spread));
        }

        /// <summary>與 shader 相同的「蛋形距離」（以半徑為單位前的公尺數）。</summary>
        public static float ShapedDistance(Vector3 p)
        {
            var v = p - Center;
            float along = Vector3.Dot(v, Forward);
            var perp = v - Forward * along;
            float a = along / Mathf.Max(along > 0f ? Shape.x : Shape.z, 0.05f);
            float s = Mathf.Max(Shape.y + Mathf.Max(along, 0f) * Spread, 0.05f);
            return Mathf.Sqrt(a * a + perp.sqrMagnitude / (s * s));
        }

        private static readonly Vector4[] extra = new Vector4[ExtraCount];
        private static readonly float[] life = new float[ExtraCount];
        private static readonly float[] maxLife = new float[ExtraCount];
        private static readonly float[] fullRadius = new float[ExtraCount];

        /// <summary>設定提燈光圈（外圈半徑）；radius ≤ 0 代表提燈關閉。</summary>
        public static void SetSphere(Vector3 center, float radius)
        {
            Center = center;
            Radius = radius;
            Shader.SetGlobalVector(SphereId, new Vector4(center.x, center.y, center.z, radius));
        }

        public static void Clear()
        {
            SetSphere(Vector3.zero, 0f);
            SetShape(Vector3.forward, Vector3.one);
            for (int i = 0; i < ExtraCount; i++) { extra[i] = Vector4.zero; life[i] = 0f; }
            PushExtra();
        }

        private static void PushExtra() => Shader.SetGlobalVectorArray(ExtraId, extra);

        /// <summary>暫時的現實泡泡（穩定能量撞擊點）：展開、維持、最後收合。回傳格子編號。</summary>
        public static int AddBubble(Vector3 pos, float radius, float seconds)
        {
            int best = 0;
            for (int i = 0; i < TempSlots; i++)
                if (extra[i].w <= 0f) { best = i; break; }
                else if (life[i] < life[best]) best = i;
            extra[best] = new Vector4(pos.x, pos.y, pos.z, 0.01f);
            fullRadius[best] = radius;
            life[best] = maxLife[best] = seconds;
            PushExtra();
            return best;
        }

        /// <summary>永久的穩定區（已復原的地方）。</summary>
        public static int AddAnchor(Vector3 pos, float radius)
        {
            for (int i = TempSlots; i < ExtraCount; i++)
                if (extra[i].w <= 0f)
                {
                    extra[i] = new Vector4(pos.x, pos.y, pos.z, radius);
                    fullRadius[i] = radius;
                    life[i] = -1f;
                    PushExtra();
                    return i;
                }
            return -1;
        }

        public static Vector4 Extra(int i) => extra[i];

        public static int ActiveBubbles
        {
            get
            {
                int n = 0;
                for (int i = 0; i < TempSlots; i++) if (extra[i].w > 0f) n++;
                return n;
            }
        }

        /// <summary>每個影格：暫時泡泡展開（0.25 s）、維持、收合（最後 0.6 s）。</summary>
        public static void Tick(float dt)
        {
            for (int i = 0; i < TempSlots; i++)
            {
                if (extra[i].w <= 0f) continue;
                life[i] -= dt;
                if (life[i] <= 0f) { extra[i].w = 0f; continue; }
                float age = maxLife[i] - life[i];
                float k = Mathf.Min(Mathf.Clamp01(age / 0.25f), Mathf.Clamp01(life[i] / 0.6f));
                extra[i].w = Mathf.Max(0.01f, fullRadius[i] * Mathf.SmoothStep(0f, 1f, k));
            }
            PushExtra();
        }

        /// <summary>與 shader 相同的判斷（不含邊緣雜訊；遮罩過半＝真實）：這個點目前是否顯示真實。</summary>
        public static bool IsReal(Vector3 p)
        {
            if (Radius > 0f && ShapedDistance(p) < Radius * 0.725f) return true;
            for (int i = 0; i < ExtraCount; i++)
                if (extra[i].w > 0f && Vector3.Distance(p, extra[i]) < extra[i].w * 0.8f) return true;
            return false;
        }

        // ---------------- 特效材質 ----------------

        private static Material additive;

        /// <summary>URP 粒子加亮材質（干擾粒子、光線）。</summary>
        public static Material Additive
        {
            get
            {
                if (additive != null) return additive;
                var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                additive = new Material(sh != null ? sh : Shader.Find("Sprites/Default")) { name = "LanternAdditive", hideFlags = HideFlags.DontSave };
                additive.SetFloat("_Surface", 1f);
                additive.SetFloat("_Blend", 2f);
                additive.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                additive.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                additive.SetFloat("_ZWrite", 0f);
                additive.SetOverrideTag("RenderType", "Transparent");
                additive.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                additive.renderQueue = 3100;
                additive.SetTexture("_BaseMap", Shepherd.Battle.UI.Arena.ArenaArt.SoftDot);
                return additive;
            }
        }

        // ---------------- 材質 ----------------

        public static Material Make(string name, Mode mode, Color real, Color anomaly, Texture realTex = null, Texture anomalyTex = null,
            float realSmooth = 0.3f, float anomalySmooth = 0.5f, float wobble = 0.015f)
        {
            var m = new Material(Shader) { name = name };
            m.SetFloat("_Mode", (float)mode);
            m.SetColor("_BaseColor", real);
            m.SetColor("_AnomalyColor", anomaly);
            if (realTex != null) m.SetTexture("_BaseMap", realTex);
            if (anomalyTex != null) m.SetTexture("_AnomalyMap", anomalyTex);
            m.SetFloat("_Smoothness", realSmooth);
            m.SetFloat("_AnomalySmoothness", anomalySmooth);
            m.SetFloat("_Wobble", wobble);
            return m;
        }

        public static Mode GetMode(Material m) => (Mode)Mathf.RoundToInt(m.GetFloat("_Mode"));

        /// <summary>
        /// Poly Haven（CC0）材質：顏色＋法線＋粗糙度，套用我們的色調。異域外觀由同一張貼圖去飽和、偏冷而來。
        /// metersPerTile＞0 時用世界座標 UV（牆、地板），否則用模型自己的 UV。
        /// </summary>
        public static Material MakePH(string name, string tex, string folder, float metersPerTile, Color tint, Color anomalyTint, float smooth = 0.8f, float anomalySat = 0.2f, float wobble = 0.01f)
        {
            var m = new Material(Shader) { name = name };
            m.SetFloat("_Mode", 0f);
            var diff = Resources.Load<Texture2D>("PH/" + folder + "/" + tex + "_diff");
            var nor = Resources.Load<Texture2D>("PH/" + folder + "/" + tex + "_nor");
            var rough = Resources.Load<Texture2D>("PH/" + folder + "/" + tex + "_rough");
            if (diff != null) m.SetTexture("_BaseMap", diff);
            if (nor != null) m.SetTexture("_BumpMap", nor);
            if (rough != null) { m.SetTexture("_RoughMap", rough); m.SetFloat("_UseRoughMap", 1f); }
            m.SetColor("_BaseColor", tint);
            m.SetColor("_AnomalyColor", anomalyTint);
            m.SetFloat("_AnomalyFromBase", 1f);
            m.SetFloat("_AnomalySat", anomalySat);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_AnomalySmoothness", 0.35f);
            m.SetFloat("_Wobble", wobble);
            m.SetFloat("_BumpScale", 1f);
            if (metersPerTile > 0f)
            {
                m.SetFloat("_WorldUV", 1f);
                m.SetTextureScale("_BaseMap", Vector2.one / metersPerTile);
            }
            return m;
        }

        public static bool HasPH(string tex, string folder) => Resources.Load<Texture2D>("PH/" + folder + "/" + tex + "_diff") != null;

        // ---------------- 程式產生的灰盒貼圖 ----------------

        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        private static Texture2D New(string key, int w, int h, out bool cached)
        {
            Texture2D t;
            cached = cache.TryGetValue(key, out t) && t != null;
            if (cached) return t;
            t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = key, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            cache[key] = t;
            return t;
        }

        /// <summary>灰盒格線（每格 1 m，細線 0.25 m），用來讀尺度。</summary>
        public static Texture2D Grid
        {
            get
            {
                bool c;
                var t = New("LanternGrid", 256, 256, out c);
                if (c) return t;
                var px = new Color32[256 * 256];
                for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    bool major = x < 3 || y < 3;
                    bool minor = x % 64 < 1 || y % 64 < 1;
                    byte v = (byte)(major ? 150 : minor ? 205 : 228);
                    px[y * 256 + x] = new Color32(v, v, v, 255);
                }
                t.SetPixels32(px);
                t.Apply(true);
                return t;
            }
        }

        private static void Fill(Color32[] px, int w, int h, Color32 c)
        {
            for (int i = 0; i < px.Length; i++) px[i] = c;
        }

        private static void Rect(Color32[] px, int w, int h, int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(h, y1); y++)
            for (int x = Mathf.Max(0, x0); x < Mathf.Min(w, x1); x++)
                px[y * w + x] = c;
        }

        private static void Disc(Color32[] px, int w, int h, int cx, int cy, int r, Color32 c)
        {
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
                if (x >= 0 && y >= 0 && x < w && y < h && (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) px[y * w + x] = c;
        }

        private static void Figure(Color32[] px, int w, int h, int cx, int baseY, int height, Color32 c)
        {
            int head = height / 7;
            Rect(px, w, h, cx - head, baseY, cx + head, baseY + height * 5 / 7, c);
            Disc(px, w, h, cx, baseY + height * 5 / 7 + head, head, c);
        }

        /// <summary>全家福（灰盒）：真實＝母女兩人；異域＝只剩母親，女兒的位置是一塊被抹去的空白。</summary>
        public static Texture2D Photo(bool real)
        {
            bool c;
            var t = New(real ? "LanternPhotoReal" : "LanternPhotoAnomaly", 256, 192, out c);
            if (c) return t;
            const int w = 256, h = 192;
            var px = new Color32[w * h];
            Fill(px, w, h, new Color32(214, 196, 160, 255));
            Rect(px, w, h, 0, 0, w, 70, new Color32(160, 140, 110, 255));
            Figure(px, w, h, 100, 40, 120, new Color32(70, 55, 50, 255));
            if (real) Figure(px, w, h, 160, 40, 96, new Color32(90, 60, 45, 255));
            else Rect(px, w, h, 140, 40, 182, 140, new Color32(236, 230, 214, 255));
            Rect(px, w, h, 0, 0, w, 8, new Color32(245, 240, 228, 255));
            Rect(px, w, h, 0, h - 8, w, h, new Color32(245, 240, 228, 255));
            Rect(px, w, h, 0, 0, 8, h, new Color32(245, 240, 228, 255));
            Rect(px, w, h, w - 8, 0, w, h, new Color32(245, 240, 228, 255));
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        private static readonly int[] Seg = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };

        private static void Digit(Color32[] px, int w, int h, int x, int y, int s, int d, Color32 c)
        {
            int m = Seg[d], t = Mathf.Max(2, s / 5);
            if ((m & 1) != 0) Rect(px, w, h, x, y + 2 * s, x + s, y + 2 * s + t, c);
            if ((m & 2) != 0) Rect(px, w, h, x + s - t, y + s, x + s, y + 2 * s, c);
            if ((m & 4) != 0) Rect(px, w, h, x + s - t, y, x + s, y + s, c);
            if ((m & 8) != 0) Rect(px, w, h, x, y, x + s, y + t, c);
            if ((m & 16) != 0) Rect(px, w, h, x, y, x + t, y + s, c);
            if ((m & 32) != 0) Rect(px, w, h, x, y + s, x + t, y + 2 * s, c);
            if ((m & 64) != 0) Rect(px, w, h, x, y + s - t / 2, x + s, y + s + t / 2, c);
        }

        /// <summary>門牌／日曆（灰盒）：真實＝17；異域＝16（數字錯位）。</summary>
        public static Texture2D Number(int n, Color32 paper, Color32 ink)
        {
            bool c;
            var t = New("LanternNumber" + n + paper.r, 128, 128, out c);
            if (c) return t;
            const int w = 128, h = 128;
            var px = new Color32[w * h];
            Fill(px, w, h, paper);
            Digit(px, w, h, 22, 24, 36, (n / 10) % 10, ink);
            Digit(px, w, h, 70, 24, 36, n % 10, ink);
            Rect(px, w, h, 0, h - 18, w, h, new Color32((byte)(ink.r / 2 + 90), 40, 40, 255));
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        /// <summary>舊照片（灰盒）：女孩踩在椅子上、伸手拿高櫃上的罐子；地上看得到椅子四隻腳的位置。</summary>
        public static Texture2D ChairPhoto
        {
            get
            {
                bool c;
                var t = New("LanternChairPhoto", 192, 256, out c);
                if (c) return t;
                const int w = 192, h = 256;
                var px = new Color32[w * h];
                Fill(px, w, h, new Color32(206, 190, 160, 255));
                Rect(px, w, h, 0, 0, w, 40, new Color32(150, 128, 100, 255));
                Rect(px, w, h, 120, 40, 176, 230, new Color32(110, 80, 55, 255));
                Rect(px, w, h, 132, 232, 150, 246, new Color32(170, 160, 150, 255));
                Rect(px, w, h, 50, 40, 54, 92, new Color32(60, 70, 120, 255));
                Rect(px, w, h, 96, 40, 100, 92, new Color32(60, 70, 120, 255));
                Rect(px, w, h, 46, 90, 104, 100, new Color32(60, 70, 140, 255));
                Figure(px, w, h, 76, 100, 112, new Color32(90, 60, 45, 255));
                Rect(px, w, h, 84, 186, 128, 194, new Color32(90, 60, 45, 255));
                Rect(px, w, h, 0, 0, w, 8, new Color32(245, 240, 228, 255));
                Rect(px, w, h, 0, h - 8, w, h, new Color32(245, 240, 228, 255));
                Rect(px, w, h, 0, 0, 8, h, new Color32(245, 240, 228, 255));
                Rect(px, w, h, w - 8, 0, w, h, new Color32(245, 240, 228, 255));
                t.SetPixels32(px);
                t.Apply(true);
                return t;
            }
        }

        /// <summary>牆上的畫（灰盒）：真實＝山丘與房子；異域＝一隻凝視的眼睛。</summary>
        public static Texture2D Painting(bool real)
        {
            bool c;
            var t = New(real ? "LanternPaintReal" : "LanternPaintAnomaly", 128, 96, out c);
            if (c) return t;
            const int w = 128, h = 96;
            var px = new Color32[w * h];
            if (real)
            {
                Fill(px, w, h, new Color32(150, 180, 205, 255));
                for (int x = 0; x < w; x++)
                {
                    int top = 30 + (int)(Mathf.Sin(x * 0.07f) * 8f);
                    Rect(px, w, h, x, 0, x + 1, top, new Color32(95, 130, 80, 255));
                }
                Rect(px, w, h, 70, 30, 92, 48, new Color32(200, 190, 170, 255));
                Rect(px, w, h, 68, 48, 94, 54, new Color32(120, 60, 50, 255));
            }
            else
            {
                Fill(px, w, h, new Color32(30, 34, 36, 255));
                Disc(px, w, h, 64, 48, 30, new Color32(200, 205, 190, 255));
                Disc(px, w, h, 64, 48, 13, new Color32(60, 120, 110, 255));
                Disc(px, w, h, 64, 48, 6, new Color32(5, 5, 5, 255));
            }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }
    }
}

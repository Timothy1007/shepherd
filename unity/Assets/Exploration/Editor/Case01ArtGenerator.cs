using System;
using System.Collections.Generic;
using System.IO;
using Shepherd.Exploration.Case01;
using UnityEditor;
using UnityEngine;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// 產生 Case01 室內美術素材（全部程序生成、可重現，無第三方授權問題）：
    /// 貼圖（PNG，含法線貼圖）→ Material（Shepherd/Case01/Triplanar）→ Case01ArtSet（Resources）。
    /// 選單：Shepherd/Case01/重建美術素材；batchmode：-executeMethod Shepherd.Exploration.Editor.Case01ArtGenerator.Generate
    /// 同樣的種子每次產生相同結果；修改後重新執行即可覆寫。
    /// </summary>
    public static class Case01ArtGenerator
    {
        public const string Root = "Assets/Exploration/Case01Art";
        public const string TexDir = Root + "/Textures";
        public const string MatDir = Root + "/Materials";
        public const string ResDir = Root + "/Resources";
        public const string ShaderName = "Shepherd/Case01/Triplanar";

        [MenuItem("Shepherd/Case01/重建美術素材")]
        public static void Generate()
        {
            Directory.CreateDirectory(TexDir);
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(ResDir);

            // ---------------- 貼圖 ----------------
            var tex = new Dictionary<string, string>();
            Write(tex, "T_WoodFloor", WoodFloor(1024), false);
            Write(tex, "T_WoodFloor_N", NormalFrom(WoodFloorHeight, 1024, 3.0f), true);
            Write(tex, "T_WoodGrain", WoodGrain(512), false);
            Write(tex, "T_WoodGrain_N", NormalFrom(WoodGrainHeight, 512, 1.6f), true);
            Write(tex, "T_Plaster", Plaster(1024), false);
            Write(tex, "T_Plaster_N", NormalFrom(PlasterHeight, 1024, 1.4f), true);
            Write(tex, "T_Wallpaper", Wallpaper(512), false);
            Write(tex, "T_TrimPaint", TrimPaint(512), false);
            Write(tex, "T_Fabric", Fabric(512), false);
            Write(tex, "T_Fabric_N", NormalFrom(FabricHeight, 512, 2.0f), true);
            Write(tex, "T_Paper", Paper(512), false);
            Write(tex, "T_BookCloth", BookCloth(256), false);
            Write(tex, "T_Cardboard", Cardboard(512), false);
            Write(tex, "T_StoneTile", StoneTile(1024), false);
            Write(tex, "T_StoneTile_N", NormalFrom(StoneTileHeight, 1024, 3.0f), true);
            Write(tex, "T_Brick", Brick(512), false);
            Write(tex, "T_Brick_N", NormalFrom(BrickHeight, 512, 3.0f), true);
            Write(tex, "T_DarkPlaster", DarkPlaster(512), false);
            Write(tex, "T_Cracks", Cracks(512), false);
            Write(tex, "T_CoreVeins", CoreVeins(512, false), false);
            Write(tex, "T_CoreVeins_E", CoreVeins(512, true), false);
            Write(tex, "T_Glaze", Glaze(256), false);
            AssetDatabase.Refresh();
            foreach (var kv in tex) Import(kv.Value, kv.Key.EndsWith("_N"));

            // ---------------- 材質 ----------------
            var shader = Shader.Find(ShaderName);
            if (shader == null) throw new InvalidOperationException("找不到 shader " + ShaderName);
            var set = LoadOrCreateSet();
            set.Materials.Clear();
            Func<string, Texture2D> T = n => AssetDatabase.LoadAssetAtPath<Texture2D>(tex[n]);

            // 地板、牆、天花板、木作。
            Add(set, Mat(shader, "floor_wood", T("T_WoodFloor"), T("T_WoodFloor_N"), new Color(1f, 0.95f, 0.9f), 0.5f, 0.75f, 0.8f));
            Add(set, Mat(shader, "floor_wood_up", T("T_WoodFloor"), T("T_WoodFloor_N"), new Color(0.88f, 0.82f, 0.78f), 0.5f, 0.7f, 0.8f));
            Add(set, Mat(shader, "wall_plaster", T("T_Plaster"), T("T_Plaster_N"), new Color(0.93f, 0.89f, 0.82f), 0.35f, 0.5f, 0.22f));
            Add(set, Mat(shader, "wall_paper", T("T_Wallpaper"), T("T_Plaster_N"), Color.white, 0.8f, 0.4f, 0.15f));
            Add(set, Mat(shader, "ceiling", T("T_Plaster"), T("T_Plaster_N"), new Color(0.98f, 0.96f, 0.92f), 0.3f, 0.3f, 0.15f));
            Add(set, Mat(shader, "trim_paint", T("T_TrimPaint"), T("T_WoodGrain_N"), new Color(0.95f, 0.92f, 0.85f), 1.2f, 0.7f, 0.35f));
            Add(set, Mat(shader, "wood_dark", T("T_WoodGrain"), T("T_WoodGrain_N"), new Color(0.55f, 0.4f, 0.3f), 1.4f, 0.7f, 0.7f));
            Add(set, Mat(shader, "wood_warm", T("T_WoodGrain"), T("T_WoodGrain_N"), new Color(1f, 0.85f, 0.68f), 1.4f, 0.65f, 0.7f));
            Add(set, Mat(shader, "frame_wood", T("T_WoodGrain"), T("T_WoodGrain_N"), new Color(0.42f, 0.3f, 0.22f), 3f, 0.8f, 0.6f));
            // 布料（同一張織紋，著色區分）。
            Add(set, Mat(shader, "fabric_blue", T("T_Fabric"), T("T_Fabric_N"), new Color(0.42f, 0.5f, 0.62f), 3f, 0.3f, 1f));
            Add(set, Mat(shader, "fabric_red", T("T_Fabric"), T("T_Fabric_N"), new Color(0.72f, 0.42f, 0.36f), 3f, 0.3f, 1f));
            Add(set, Mat(shader, "fabric_green", T("T_Fabric"), T("T_Fabric_N"), new Color(0.5f, 0.6f, 0.48f), 3f, 0.3f, 1f));
            Add(set, Mat(shader, "fabric_cream", T("T_Fabric"), T("T_Fabric_N"), new Color(0.92f, 0.88f, 0.8f), 3f, 0.3f, 1f));
            // 紙、書、紙箱。
            Add(set, Mat(shader, "paper", T("T_Paper"), null, new Color(1f, 0.98f, 0.93f), 4f, 0.2f, 0f, new Color(0.06f, 0.06f, 0.05f)));
            Add(set, Mat(shader, "paper_old", T("T_Paper"), null, new Color(0.9f, 0.8f, 0.6f), 4f, 0.2f, 0f, new Color(0.04f, 0.035f, 0.02f)));
            Color[] books =
            {
                new Color(0.62f, 0.16f, 0.13f), new Color(0.2f, 0.3f, 0.52f), new Color(0.25f, 0.46f, 0.3f), new Color(0.82f, 0.66f, 0.32f),
                new Color(0.4f, 0.35f, 0.37f), new Color(0.88f, 0.85f, 0.76f), new Color(0.5f, 0.25f, 0.45f),
            };
            for (int i = 0; i < books.Length; i++) Add(set, Mat(shader, "book_" + i, T("T_BookCloth"), null, books[i], 5f, 0.45f, 0f));
            Add(set, Mat(shader, "cardboard", T("T_Cardboard"), null, Color.white, 2f, 0.2f, 0f));
            // 陶器、金屬、玻璃、植物。
            Add(set, Mat(shader, "ceramic", T("T_Glaze"), null, new Color(0.95f, 0.94f, 0.9f), 2f, 0.9f, 0f));
            Add(set, Mat(shader, "brass", T("T_Glaze"), null, new Color(0.78f, 0.6f, 0.32f), 2f, 0.75f, 0f, null, 0.85f));
            Add(set, Mat(shader, "metal", T("T_Glaze"), null, new Color(0.6f, 0.61f, 0.63f), 2f, 0.7f, 0f, null, 0.75f));
            Add(set, Mat(shader, "glass_night", T("T_Glaze"), null, new Color(0.05f, 0.07f, 0.12f), 1f, 1f, 0f, new Color(0.04f, 0.07f, 0.14f)));
            Add(set, Mat(shader, "pot", T("T_Plaster"), T("T_Plaster_N"), new Color(0.72f, 0.4f, 0.26f), 3f, 0.3f, 0.8f));
            Add(set, Mat(shader, "leaf", T("T_Fabric"), null, new Color(0.3f, 0.5f, 0.26f), 6f, 0.45f, 0f));
            Add(set, Mat(shader, "lamp_shade", T("T_Fabric"), T("T_Fabric_N"), new Color(0.95f, 0.88f, 0.72f), 4f, 0.2f, 0.6f, new Color(1f, 0.72f, 0.38f) * 0.85f));
            Add(set, Mat(shader, "glow", T("T_Glaze"), null, new Color(1f, 0.9f, 0.7f), 2f, 0.6f, 0f, new Color(1f, 0.72f, 0.38f) * 1.8f));
            // 夾層與中庭：暗色本體＋裂隙光。
            var rift = new Color(0.62f, 0.25f, 1f);
            Add(set, Mat(shader, "mezz_floor", T("T_StoneTile"), T("T_StoneTile_N"), new Color(0.42f, 0.38f, 0.45f), 0.5f, 0.55f, 0.8f, rift * 0.22f, 0f, T("T_Cracks")));
            Add(set, Mat(shader, "mezz_wall", T("T_DarkPlaster"), T("T_Plaster_N"), Color.white, 0.22f, 0.35f, 0.5f, rift * 0.35f, 0f, T("T_Cracks")));
            Add(set, Mat(shader, "core_body", T("T_CoreVeins"), null, Color.white, 2.5f, 0.95f, 0f, rift * 2.2f, 0.2f, T("T_CoreVeins_E")));
            Add(set, Mat(shader, "rift", T("T_Glaze"), null, new Color(0.15f, 0.05f, 0.24f), 2f, 0.9f, 0f, rift * 2.4f));
            Add(set, Mat(shader, "frame_glow", T("T_Glaze"), null, new Color(0.22f, 0.1f, 0.32f), 2f, 0.8f, 0f, rift * 1.6f));
            Add(set, Mat(shader, "crystal", T("T_CoreVeins"), null, new Color(0.6f, 0.4f, 0.8f), 3f, 0.95f, 0f, rift * 1.3f, 0.2f, T("T_CoreVeins_E")));
            Add(set, Mat(shader, "court_floor", T("T_StoneTile"), T("T_StoneTile_N"), new Color(0.62f, 0.6f, 0.62f), 0.35f, 0.5f, 1f, rift * 0.18f, 0f, T("T_Cracks")));
            Add(set, Mat(shader, "court_wall", T("T_Brick"), T("T_Brick_N"), new Color(0.75f, 0.65f, 0.62f), 0.7f, 0.3f, 1f));

            set.PaperTexture = T("T_Paper");
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log("[Case01Art] 已產生 " + tex.Count + " 張貼圖、" + set.Materials.Count + " 個材質 → " + ResDir + "/Case01ArtSet.asset");
        }

        // ---------------- 資產工具 ----------------

        private static Case01ArtSet LoadOrCreateSet()
        {
            string path = ResDir + "/Case01ArtSet.asset";
            var set = AssetDatabase.LoadAssetAtPath<Case01ArtSet>(path);
            if (set != null) return set;
            set = ScriptableObject.CreateInstance<Case01ArtSet>();
            AssetDatabase.CreateAsset(set, path);
            return set;
        }

        private static void Add(Case01ArtSet set, Material m)
        {
            set.Materials.Add(new Case01ArtSet.Entry { Key = m.name.Substring(2), Material = m });
        }

        /// <summary>建立或更新 M_&lt;key&gt;.mat（保留 GUID）。</summary>
        private static Material Mat(Shader shader, string key, Texture2D albedo, Texture2D normal, Color tint, float tilesPerMeter, float smooth, float bump,
            Color? emission = null, float metallic = 0f, Texture2D emissionMap = null)
        {
            string path = MatDir + "/M_" + key + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = m == null;
            if (create) m = new Material(shader);
            m.shader = shader;
            m.name = "M_" + key;
            m.SetColor("_Color", tint);
            m.SetTexture("_MainTex", albedo);
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", normal != null ? bump : 0f);
            m.SetFloat("_Scale", tilesPerMeter);
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Metallic", metallic);
            m.SetTexture("_EmissionMap", emissionMap);
            m.SetColor("_EmissionColor", emission ?? Color.black);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            if (create) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }

        private static void Write(Dictionary<string, string> map, string name, Color[] px, bool normal)
        {
            int n = (int)Mathf.Sqrt(px.Length);
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.SetPixels(px);
            t.Apply();
            string path = TexDir + "/" + name + ".png";
            File.WriteAllBytes(path, t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
            map[name] = path;
        }

        private static void Import(string path, bool normal)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = !normal;
            imp.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = false;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.mipmapEnabled = true;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 4;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }

        // ---------------- 雜訊（可平鋪） ----------------

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7fffffff) / (float)0x7fffffff;
            }
        }

        private static int Mod(int a, int p) { int r = a % p; return r < 0 ? r + p : r; }

        /// <summary>週期為 period 格的值雜訊（u、v 為 0～1，可無縫平鋪）。</summary>
        private static float Noise(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(Mod(x0, period), Mod(y0, period), seed), b = Hash(Mod(x0 + 1, period), Mod(y0, period), seed);
            float c = Hash(Mod(x0, period), Mod(y0 + 1, period), seed), d = Hash(Mod(x0 + 1, period), Mod(y0 + 1, period), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Fbm(float u, float v, int period, int seed, int octaves = 4)
        {
            float s = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                s += Noise(u, v, period << o, seed + o * 17) * amp;
                norm += amp;
                amp *= 0.5f;
            }
            return s / norm;
        }

        private delegate float HeightFn(float u, float v);

        private static Color[] Fill(int n, Func<float, float, Color> f)
        {
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                px[y * n + x] = f((x + 0.5f) / n, (y + 0.5f) / n);
            return px;
        }

        private static Color[] NormalFrom(HeightFn h, int n, float strength)
        {
            var hs = new float[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                hs[y * n + x] = h((x + 0.5f) / n, (y + 0.5f) / n);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float l = hs[y * n + Mod(x - 1, n)], r = hs[y * n + Mod(x + 1, n)];
                float d = hs[Mod(y - 1, n) * n + x], u = hs[Mod(y + 1, n) * n + x];
                var nv = new Vector3((l - r) * strength * n / 256f, (d - u) * strength * n / 256f, 1f).normalized;
                px[y * n + x] = new Color(nv.x * 0.5f + 0.5f, nv.y * 0.5f + 0.5f, nv.z * 0.5f + 0.5f, 1f);
            }
            return px;
        }

        private static Color C(float r, float g, float b, float a) { return new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), Mathf.Clamp01(a)); }

        // ---------------- 木地板：11 列木板沿 u 方向，接縫、木紋、色差 ----------------

        private const int Rows = 11;

        private static void Board(float u, float v, out int row, out int board, out float seamU, out float seamV)
        {
            row = Mathf.Min(Rows - 1, Mathf.FloorToInt(v * Rows));
            float rv = v * Rows - row;
            seamV = Mathf.Min(rv, 1f - rv) / Rows;
            // 每列 3 片木板，邊界在 0～1 間循環（可平鋪）。
            float off = Hash(row, 3, 91);
            float uu = Mathf.Repeat(u + off, 1f);
            float b1 = 0.28f + Hash(row, 5, 91) * 0.12f, b2 = 0.62f + Hash(row, 7, 91) * 0.12f;
            board = uu < b1 ? 0 : uu < b2 ? 1 : 2;
            float d0 = Mathf.Min(Mathf.Abs(uu - b1), Mathf.Abs(uu - b2));
            d0 = Mathf.Min(d0, Mathf.Min(uu, 1f - uu));
            seamU = d0;
        }

        private static float WoodFloorHeight(float u, float v)
        {
            int row, board;
            float su, sv;
            Board(u, v, out row, out board, out su, out sv);
            float grain = Noise(u, v * 0.4f + row * 0.3f, 64, row * 13 + board) * 0.15f;
            float seam = Mathf.Clamp01(Mathf.Min(su * 600f, sv * 1100f));
            return seam * 0.8f + grain;
        }

        private static Color[] WoodFloor(int n)
        {
            return Fill(n, (u, v) =>
            {
                int row, board;
                float su, sv;
                Board(u, v, out row, out board, out su, out sv);
                int id = row * 3 + board;
                float tone = 0.8f + Hash(id, 1, 7) * 0.35f;
                float warp = Fbm(u, v, 4, id + 3, 3) * 6f;
                float lines = 0.5f + 0.5f * Mathf.Sin((v * Rows * 14f + warp) * Mathf.PI);
                float fine = Noise(u * 8f, v, 128, id) * 0.5f + 0.5f;
                float g = 0.82f + lines * 0.1f + fine * 0.08f;
                float knot = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(Hash(id, 9, 3), (row + 0.5f) / Rows)) * 40f);
                g -= knot * 0.25f;
                float seam = Mathf.Clamp01(Mathf.Min(su * 600f, sv * 1100f));
                float shade = Mathf.Lerp(0.45f, 1f, seam) * tone * g;
                float wear = Fbm(u, v, 3, 77, 3);
                return C(0.62f * shade, 0.44f * shade, 0.29f * shade, 0.35f + wear * 0.35f);
            });
        }

        // ---------------- 家具木紋 ----------------

        private static float WoodGrainHeight(float u, float v)
        {
            float warp = Fbm(u, v, 4, 11, 3) * 2.5f;
            return 0.5f + 0.5f * Mathf.Sin((v * 30f + warp) * Mathf.PI) * 0.25f + Noise(u * 6f, v, 128, 5) * 0.2f;
        }

        private static Color[] WoodGrain(int n)
        {
            return Fill(n, (u, v) =>
            {
                float warp = Fbm(u, v, 4, 11, 3) * 2.5f;
                float rings = 0.5f + 0.5f * Mathf.Sin((v * 30f + warp) * Mathf.PI);
                float fine = Noise(u * 6f, v, 128, 5);
                float g = 0.82f + rings * 0.08f + fine * 0.1f;
                return C(0.6f * g, 0.43f * g, 0.3f * g, 0.45f + rings * 0.2f);
            });
        }

        private static Color[] TrimPaint(int n)
        {
            return Fill(n, (u, v) =>
            {
                float brush = Noise(u * 0.5f, v * 8f, 16, 31) * 0.06f;
                float chip = Fbm(u, v, 8, 33, 3) > 0.74f ? 0.15f : 0f;
                float g = 0.92f + brush - chip;
                return C(g, g * 0.97f, g * 0.9f, 0.55f - chip);
            });
        }

        // ---------------- 牆面 ----------------

        private static float PlasterHeight(float u, float v)
        {
            return Fbm(u, v, 16, 21, 4) * 0.6f + Noise(u, v, 128, 23) * 0.2f;
        }

        private static Color[] Plaster(int n)
        {
            return Fill(n, (u, v) =>
            {
                float blot = Fbm(u, v, 4, 25, 4);
                float speck = Noise(u, v, 256, 27);
                float patch = Fbm(u, v, 3, 29, 2) > 0.62f ? 0.05f : 0f;
                float g = 0.86f + (blot - 0.5f) * 0.14f + speck * 0.04f + patch;
                return C(g, g * 0.98f, g * 0.94f, 0.25f + blot * 0.15f);
            });
        }

        private static Color[] Wallpaper(int n)
        {
            return Fill(n, (u, v) =>
            {
                float stripe = Mathf.Repeat(u * 6f, 1f) < 0.5f ? 1f : 0.93f;
                float cu = Mathf.Repeat(u * 12f, 1f) - 0.5f, cv = Mathf.Repeat(v * 8f, 1f) - 0.5f;
                float motif = Mathf.Abs(cu) + Mathf.Abs(cv) < 0.18f ? 0.82f : 1f;
                float age = Fbm(u, v, 4, 41, 4);
                float g = stripe * motif * (0.86f + (age - 0.5f) * 0.18f);
                return C(0.72f * g, 0.74f * g, 0.66f * g, 0.25f);
            });
        }

        // ---------------- 布料、紙、書、紙箱 ----------------

        private static float FabricHeight(float u, float v)
        {
            float a = Mathf.Sin(u * 128f * Mathf.PI), b = Mathf.Sin(v * 128f * Mathf.PI);
            return 0.5f + 0.25f * (Mathf.Repeat(Mathf.Floor(u * 64f) + Mathf.Floor(v * 64f), 2f) < 1f ? a : b) + Noise(u, v, 32, 51) * 0.2f;
        }

        private static Color[] Fabric(int n)
        {
            return Fill(n, (u, v) =>
            {
                float w = FabricHeight(u, v);
                float g = 0.82f + w * 0.18f + (Fbm(u, v, 8, 53, 3) - 0.5f) * 0.1f;
                return C(g, g, g, 0.15f);
            });
        }

        private static Color[] Paper(int n)
        {
            return Fill(n, (u, v) =>
            {
                float fiber = Noise(u * 3f, v, 128, 61) * 0.05f + Noise(u, v, 256, 63) * 0.04f;
                float stain = Fbm(u, v, 4, 65, 3) > 0.7f ? 0.06f : 0f;
                float g = 0.95f - fiber - stain;
                return C(g, g * 0.98f, g * 0.93f, 0.2f);
            });
        }

        private static Color[] BookCloth(int n)
        {
            return Fill(n, (u, v) =>
            {
                float band = Mathf.Abs(Mathf.Repeat(v * 3f, 1f) - 0.18f) < 0.03f ? 1.2f : 1f;
                float wear = Noise(u, v, 64, 71) * 0.12f;
                float g = (0.82f - wear) * band;
                return C(g, g, g, 0.35f);
            });
        }

        private static Color[] Cardboard(int n)
        {
            return Fill(n, (u, v) =>
            {
                float corr = Mathf.Sin(u * 160f * Mathf.PI) * 0.03f;
                float tape = Mathf.Abs(v - 0.5f) < 0.06f ? 0.08f : 0f;
                float g = 0.62f + corr + (Fbm(u, v, 8, 81, 3) - 0.5f) * 0.1f + tape;
                return C(g, g * 0.78f, g * 0.52f, 0.15f);
            });
        }

        // ---------------- 夾層／中庭 ----------------

        private static float StoneTileHeight(float u, float v)
        {
            float tu = Mathf.Repeat(u * 4f, 1f), tv = Mathf.Repeat(v * 4f, 1f);
            float grout = Mathf.Min(Mathf.Min(tu, 1f - tu), Mathf.Min(tv, 1f - tv));
            return Mathf.Clamp01(grout * 40f) * 0.7f + Fbm(u, v, 16, 91, 3) * 0.3f;
        }

        private static Color[] StoneTile(int n)
        {
            return Fill(n, (u, v) =>
            {
                int ti = Mathf.FloorToInt(u * 4f), tj = Mathf.FloorToInt(v * 4f);
                float tone = 0.75f + Hash(ti, tj, 93) * 0.25f;
                float h = StoneTileHeight(u, v);
                float g = tone * (0.55f + h * 0.45f);
                return C(g, g * 0.97f, g * 1.02f, 0.25f + h * 0.2f);
            });
        }

        private static float BrickHeight(float u, float v)
        {
            float rows = 8f;
            int r = Mathf.FloorToInt(v * rows);
            float bu = Mathf.Repeat(u * 4f + (r % 2) * 0.5f, 1f), bv = Mathf.Repeat(v * rows, 1f);
            float mortar = Mathf.Min(Mathf.Min(bu, 1f - bu) * 4f, Mathf.Min(bv, 1f - bv));
            return Mathf.Clamp01(mortar * 12f) * 0.8f + Noise(u, v, 64, 95) * 0.2f;
        }

        private static Color[] Brick(int n)
        {
            return Fill(n, (u, v) =>
            {
                int r = Mathf.FloorToInt(v * 8f), c = Mathf.FloorToInt(u * 4f + (r % 2) * 0.5f);
                float tone = 0.7f + Hash(c, r, 97) * 0.3f;
                float h = BrickHeight(u, v);
                float g = h < 0.4f ? 0.55f : tone;
                return C(0.62f * g, 0.38f * g, 0.3f * g, 0.2f);
            });
        }

        private static Color[] DarkPlaster(int n)
        {
            return Fill(n, (u, v) =>
            {
                float g = 0.32f + (Fbm(u, v, 4, 101, 4) - 0.5f) * 0.12f;
                return C(g, g * 0.95f, g * 1.08f, 0.3f);
            });
        }

        /// <summary>裂隙遮罩（白＝裂縫）：脊狀雜訊取窄帶。</summary>
        private static float CrackValue(float u, float v, int seed)
        {
            float r = 1f - Mathf.Abs(Fbm(u, v, 3, seed, 4) * 2f - 1f);
            return Mathf.Clamp01((r - 0.975f) * 40f);
        }

        private static Color[] Cracks(int n)
        {
            return Fill(n, (u, v) =>
            {
                float c = Mathf.Max(CrackValue(u, v, 111), CrackValue(u, v, 113) * 0.7f);
                return C(c, c, c, 1f);
            });
        }

        private static Color[] CoreVeins(int n, bool emission)
        {
            return Fill(n, (u, v) =>
            {
                float vein = Mathf.Max(CrackValue(u, v, 121), CrackValue(u, v, 123));
                float body = 0.12f + Fbm(u, v, 8, 125, 3) * 0.1f;
                if (emission) return C(vein, vein, vein, 1f);
                return C(body * 0.9f, body * 0.75f, body * 1.2f, 0.9f - vein * 0.4f);
            });
        }

        private static Color[] Glaze(int n)
        {
            return Fill(n, (u, v) =>
            {
                float g = 0.95f + (Noise(u, v, 32, 131) - 0.5f) * 0.06f;
                return C(g, g, g, 0.95f);
            });
        }
    }
}

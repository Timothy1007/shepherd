using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// 沉浸式住宅探索 QA 的場景加工（只在 Case01ImmersiveHouseQA 建立；在 Phase 1.9 R3 住宅與 1.8 美術之上）：
    /// - 玄關：脫在長凳前的皮鞋、靠牆的短靴、衣架上的兩件大衣、傘架裡的長柄傘、鞋櫃上的鑰匙碗與一疊信、門墊旁剛投進來的信。
    /// - 客廳：茶几上的馬克杯與遙控器、沙發上被靠過的抱枕、扶手椅上的報紙；壁爐架的合照換成高解析版本，並可拿起檢視。
    /// - 樓梯：車削欄杆柱、起步柱、蘑菇形扶手、紅色長毯與黃銅壓毯棍、牆上的畫與壁燈；二樓樓梯井改成同樣的欄杆。
    /// - 二樓走廊：兩盞壁燈、長毯、畫框。
    /// - 照明：吊燈大幅調暗（客廳靠壁爐、檯燈；玄關靠門玻璃與電話桌小燈；樓梯與二樓靠壁燈），壁燈有陰影。
    /// 所有新模型由 tools/blender/case01_immersive 程序產生（沒有外部素材）。
    /// </summary>
    public sealed class Case01ImmersiveHouse
    {
        public const string Folder = "Case01Immersive/";

        public Transform Root { get; private set; }
        public readonly List<Transform> Props = new List<Transform>();
        public readonly List<Light> Sconces = new List<Light>();
        public readonly List<Transform> Balusters = new List<Transform>();
        public Case01InspectItem Photo { get; private set; }
        public Material PhotoMaterial { get; private set; }
        public Texture2D PhotoNormal { get; private set; }
        public Texture2D PhotoAnomaly { get; private set; }
        public Material LensMat { get; private set; }
        public int Missing { get; private set; }

        private readonly Case01HouseLayout L;
        private readonly Case01Palette p;
        private readonly Case01ArtKit kit;
        private readonly Case01ImmersiveArt art;

        public static bool Available { get { return Resources.Load<GameObject>(Folder + "Baluster") != null && Resources.Load<Texture2D>(Folder + "IH_FamilyPhoto") != null; } }

        public Case01ImmersiveHouse(Case01HouseLayout layout, Case01Palette palette, Case01ImmersionWorld world)
        {
            L = layout;
            p = palette;
            art = world != null ? world.Art : null;
            kit = art != null ? art.Kit : null;
            Root = ArenaArt.Group("ImmersiveHouseQA", L.Root, Vector3.zero);
            Root.position = Vector3.zero;
            LensMat = p.Own(ArenaArt.Mat(new Color(0.85f, 0.88f, 0.9f), 0.95f));
            Lighting();
            Entrance();
            Living();
            Stairs();
            Upstairs();
            PhotoSetup(world);
            Physics.SyncTransforms();
        }

        // ---------------- 材質 ----------------

        private static readonly Dictionary<Case01Palette, Dictionary<string, Material>> ihCache = new Dictionary<Case01Palette, Dictionary<string, Material>>();

        /// <summary>IH_* 材質（Blender 只命名）：紙、牛皮紙、橡膠、麂皮、傘布、新聞紙、長毯、鏡片。</summary>
        public static void RemapIh(GameObject go, Case01Palette p, Material lens)
        {
            Dictionary<string, Material> d;
            if (!ihCache.TryGetValue(p, out d))
            {
                d = new Dictionary<string, Material>();
                ihCache[p] = d;
            }
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    if (ms[i] == null) continue;
                    var n = ms[i].name.Replace(" (Instance)", "");
                    // 客廳套件裡有貼圖、但 Case01ArtKit 沒建材質的（例如燈罩 LR_Shade）：直接用那張貼圖。
                    if (n.StartsWith("LR_") && ms[i].mainTexture == null)
                    {
                        Material lm;
                        if (!d.TryGetValue(n, out lm) || lm == null)
                        {
                            var tex = Resources.Load<Texture2D>(Case01ArtKit.LivingFolder + n);
                            if (tex == null) continue;
                            lm = p.Own(ArenaArt.Mat(Color.white, 0.12f));
                            lm.name = n;
                            lm.mainTexture = tex;
                            d[n] = lm;
                        }
                        ms[i] = lm;
                        continue;
                    }
                    if (!n.StartsWith("IH_")) continue;
                    if (n == "IH_Lens" && lens != null)
                    {
                        ms[i] = lens;
                        continue;
                    }
                    Material m;
                    if (!d.TryGetValue(n, out m) || m == null)
                    {
                        float smooth = n == "IH_Rubber" ? 0.28f : n == "IH_Umbrella" ? 0.22f : n == "IH_Suede" ? 0.06f : 0.1f;
                        m = p.Own(ArenaArt.Mat(Color.white, smooth));
                        m.name = n;
                        m.mainTexture = Resources.Load<Texture2D>(Folder + n);
                        d[n] = m;
                    }
                    ms[i] = m;
                }
                r.sharedMaterials = ms;
            }
        }

        private Transform Prop(string model, Vector3 pos, float yaw, float scale = 1f, Transform parent = null)
        {
            var prefab = Resources.Load<GameObject>(Folder + model);
            Transform t;
            // Blender 匯出的這組模型正面朝本地 −Z（實測）：轉 180° 讓 yaw 表示「正面朝向」。
            float extra = prefab != null ? 180f : 0f;
            if (prefab != null)
            {
                t = Object.Instantiate(prefab).transform;
                t.name = model;
                if (kit != null) kit.Remap(t.gameObject);
                RemapIh(t.gameObject, p, LensMat);
            }
            else if (kit != null && (t = kit.Place(model, null, Vector3.zero, 0f)) != null)
            {
            }
            else
            {
                Missing++;
                return null;
            }
            t.SetParent(parent ?? Root, true);
            t.position = pos;
            t.rotation = Quaternion.Euler(0f, yaw + extra, 0f);
            t.localScale = t.localScale * scale;
            foreach (var x in t.GetComponentsInChildren<Transform>(true)) x.gameObject.layer = Root.gameObject.layer;
            Props.Add(t);
            return t;
        }

        private static Bounds? BoundsOf(Transform t)
        {
            if (t == null) return null;
            Bounds? b = null;
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                if (b == null) b = r.bounds;
                else
                {
                    var bb = b.Value;
                    bb.Encapsulate(r.bounds);
                    b = bb;
                }
            }
            return b;
        }

        private Transform Piece(string key)
        {
            if (art == null) return null;
            Transform t;
            return art.Pieces.TryGetValue(key, out t) ? t : null;
        }

        private Transform Find(string name)
        {
            foreach (var t in L.Root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        /// <summary>從物件中心朝走廊中線（x＝8.2）的水平方向。</summary>
        private static Vector3 TowardHall(Vector3 c)
        {
            var d = new Vector3(8.2f - c.x, 0f, 0f);
            return d.sqrMagnitude < 1e-4f ? Vector3.back : d.normalized;
        }

        private static float YawOf(Vector3 dir) { return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg; }

        // ---------------- 照明 ----------------

        private void Lighting()
        {
            System.Action<Case01Room, float> dim = (room, k) =>
            {
                if (L.RoomLightIntensity.ContainsKey(room)) L.RoomLightIntensity[room] *= k;
            };
            // 吊燈幾乎都關著：生活燈（壁爐、檯燈、電話桌小燈、壁燈）才是主光。
            dim(Case01Room.Entrance, 0.35f);
            dim(Case01Room.Living, 0.2f);
            dim(Case01Room.Landing, 0.15f);
            dim(Case01Room.Dining, 0.6f);
            float y2 = Case01HouseBuilder.FloorHeight;
            // 樓梯牆的壁燈（樓梯中段，靠東牆）。
            Sconce(new Vector3(Case01HouseBuilder.R17.StairX1 - 0.005f, 3.15f, 8.65f), -90f, true);
            // 二樓走廊：西牆兩盞（書房門與臥室門之間、第三扇門附近）。
            Sconce(new Vector3(Case01HouseBuilder.R17.HallX + 0.08f, y2 + 1.65f, 7.4f), 90f, false);
            Sconce(new Vector3(Case01HouseBuilder.R17.HallX + 0.08f, y2 + 1.65f, 1.9f), 90f, false);
        }

        private void Sconce(Vector3 wallPos, float yaw, bool shadows)
        {
            var s = Prop("WallSconce", wallPos, yaw);
            var fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var bulb = wallPos + fwd * 0.16f + Vector3.up * 0.17f;
            var l = ArenaArt.PointLight(Root, bulb, new Color(1f, 0.74f, 0.48f), 4.2f, 1.05f, shadows);
            l.name = "SconceLight";
            if (shadows)
            {
                l.shadowBias = 0.05f;
                l.shadowNormalBias = 0.4f;
                l.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Low;
            }
            L.LampLights.Add(l);
            Sconces.Add(l);
            // 牆上的暖色光暈（燈罩往上打的光）：小點光貼牆。
            var wash = ArenaArt.PointLight(Root, wallPos + fwd * 0.05f + Vector3.up * 0.45f, new Color(1f, 0.78f, 0.55f), 1.3f, 0.6f, false);
            wash.name = "SconceWash";
            L.LampLights.Add(wash);
        }

        // ---------------- 玄關 ----------------

        private void Entrance()
        {
            var bench = BoundsOf(Piece("HallBench"));
            if (bench.HasValue)
            {
                var c = bench.Value.center;
                var f = TowardHall(c);
                float half = Mathf.Abs(f.x) > 0.5f ? bench.Value.extents.x : bench.Value.extents.z;
                Prop("ShoesPair", new Vector3(c.x, 0.002f, c.z) + f * (half + 0.18f), YawOf(-f) + 8f);
            }
            var cab = BoundsOf(Piece("ShoeCabinet"));
            if (cab.HasValue)
            {
                var b = cab.Value;
                var f = TowardHall(b.center);
                bool alongZ = Mathf.Abs(f.x) > 0.5f;
                var side = alongZ ? Vector3.forward : Vector3.right;
                float len = alongZ ? b.extents.z : b.extents.x;
                Prop("KeyBowl", new Vector3(b.center.x, b.max.y + 0.001f, b.center.z) - side * len * 0.45f + f * 0.02f, 0f);
                Prop("LetterStack", new Vector3(b.center.x, b.max.y + 0.001f, b.center.z) + side * len * 0.3f, YawOf(f) + 12f);
                Prop("BootsPair", new Vector3(b.center.x, 0.002f, b.center.z) + side * (len + 0.22f) + f * 0.05f, YawOf(-f));
            }
            var rack = BoundsOf(Piece("CoatRack"));
            if (rack.HasValue)
            {
                var b = rack.Value;
                var f = TowardHall(b.center);
                bool wall = Mathf.Min(b.size.x, b.size.z) < 0.25f;
                var hook = new Vector3(b.center.x, b.max.y - 0.08f, b.center.z) + (wall ? f * 0.06f : Vector3.zero);
                var side = Mathf.Abs(f.x) > 0.5f ? Vector3.forward : Vector3.right;
                Prop("CoatHanging", hook - side * 0.14f, YawOf(f) - 6f);
                Prop("CoatHangingRed", hook + side * 0.16f + f * 0.03f, YawOf(f) + 9f);
            }
            var stand = BoundsOf(Piece("UmbrellaStand"));
            if (stand.HasValue)
            {
                var umb = Prop("Umbrella", new Vector3(stand.Value.center.x, stand.Value.min.y + 0.03f, stand.Value.center.z), 30f);
                if (umb != null) umb.rotation = Quaternion.Euler(7f, 30f, 3f);
            }
            // 剛投進門縫的信（門墊旁、偏一邊，不擋路）。
            var mail = Prop("LetterStack", new Vector3(Case01HouseBuilder.R17.FrontDoorX + 0.38f, 0.003f, 0.42f), 64f);
            if (mail != null) mail.localScale = new Vector3(1f, 0.5f, 1f);
        }

        // ---------------- 客廳 ----------------

        private void Living()
        {
            var table = BoundsOf(Piece("CoffeeTable"));
            if (table.HasValue)
            {
                var b = table.Value;
                float top = b.max.y + 0.001f;
                Prop("Mug", new Vector3(b.center.x + b.extents.x * 0.35f, top, b.center.z - b.extents.z * 0.3f), 140f);
                Prop("Remote", new Vector3(b.center.x - b.extents.x * 0.25f, top, b.center.z + b.extents.z * 0.25f), 75f);
            }
            var sofa = BoundsOf(Piece("Sofa"));
            if (sofa.HasValue)
            {
                var b = sofa.Value;
                bool alongZ = b.size.z > b.size.x;
                var side = alongZ ? Vector3.forward : Vector3.right;
                float len = alongZ ? b.extents.z : b.extents.x;
                // 沙發面朝客廳中央（壁爐）：抱枕斜靠在一端的扶手邊。
                var toFire = new Vector3(0.8f - b.center.x, 0f, 2.9f - b.center.z).normalized;
                var cushion = Prop("CushionTilted", new Vector3(b.center.x, b.min.y + 0.42f, b.center.z) + side * len * 0.62f - toFire * 0.12f, YawOf(toFire));
                if (cushion != null) cushion.rotation = Quaternion.LookRotation(toFire) * Quaternion.Euler(-18f, 0f, 24f);
            }
            var chair = BoundsOf(Piece("Armchair"));
            if (chair.HasValue)
            {
                var b = chair.Value;
                var np = Prop("Newspaper", new Vector3(b.center.x, b.min.y + 0.46f, b.center.z), 200f);
                if (np != null) np.rotation = Quaternion.Euler(-6f, 200f, 4f);
            }
        }

        // ---------------- 樓梯 ----------------

        private void Stairs()
        {
            float zLow = Case01HouseBuilder.R17.StairFootZ, zHigh = Case01HouseBuilder.R17.StairTopZ, H = Case01HouseBuilder.FloorHeight;
            int n = Case01HouseBuilder.R17.StairSteps;
            float rise = H / n, depth = (zLow - zHigh) / n;
            float x0 = Case01HouseBuilder.R17.StairX0, x1 = Case01HouseBuilder.R17.StairX1, cx = (x0 + x1) * 0.5f;
            // 長毯（每一階的踏面＋踢面）與黃銅壓毯棍。
            var runner = p.Own(ArenaArt.Mat(Color.white, 0.05f));
            runner.mainTexture = Resources.Load<Texture2D>(Folder + "IH_Runner");
            float w = 0.68f;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float v = 0f;
            for (int i = 0; i < n; i++)
            {
                float zf = zLow - i * depth, zb = zLow - (i + 1) * depth, yb = i * rise, yt = (i + 1) * rise + 0.004f;
                // 踢面（面向樓梯腳 +z）。
                AddQuad(verts, uvs, tris, new Vector3(cx - w / 2, yb, zf + 0.004f), new Vector3(cx + w / 2, yb, zf + 0.004f), new Vector3(cx + w / 2, yt, zf + 0.004f), new Vector3(cx - w / 2, yt, zf + 0.004f), v, v + rise / w);
                v += rise / w;
                // 踏面（朝上）。
                AddQuad(verts, uvs, tris, new Vector3(cx - w / 2, yt, zf + 0.004f), new Vector3(cx + w / 2, yt, zf + 0.004f), new Vector3(cx + w / 2, yt, zb), new Vector3(cx - w / 2, yt, zb), v, v + depth / w);
                v += depth / w;
                if (i > 0)
                {
                    var rod = Prop("StairRod", new Vector3(cx, yb + 0.004f, zf + 0.012f), 0f, 1f);
                    if (rod != null) rod.localScale = new Vector3(w + 0.06f, 1f, 1f);
                }
            }
            var go = new GameObject("StairRunner");
            go.transform.SetParent(Root, false);
            var mesh = new Mesh { name = "StairRunner" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = runner;

            // 欄杆：原本浮著的方塊扶手與方柱隱藏（碰撞保留），換成車削欄杆柱、起步柱與蘑菇形扶手。
            foreach (var nm in new[] { "Handrail", "Newel" })
            {
                var t = Find(nm);
                if (t != null && t.GetComponent<Renderer>() != null) t.GetComponent<Renderer>().enabled = false;
            }
            System.Func<float, float> railY = z => (zLow - z) / (zLow - zHigh) * H + 0.9f;
            float bx = x0 + 0.035f;
            Prop("NewelPost", new Vector3(bx + 0.02f, 0f, zLow + 0.1f), 0f);
            for (int i = 0; i < n; i++)
            {
                for (int k = 0; k < 2; k++)
                {
                    float z = zLow - (i + 0.28f + k * 0.45f) * depth;
                    float baseY = (i + 1) * rise;
                    float h = railY(z) - baseY - 0.03f;
                    var b = Prop("Baluster", new Vector3(bx, baseY, z), 0f);
                    if (b != null)
                    {
                        b.localScale = new Vector3(1f, h / 0.86f, 1f);
                        Balusters.Add(b);
                    }
                }
            }
            float len = Mathf.Sqrt((zLow - zHigh) * (zLow - zHigh) + H * H);
            var rail = Prop("Handrail", new Vector3(bx, railY(zLow + 0.05f), zLow + 0.05f), 180f);
            if (rail != null)
            {
                // 扶手模型沿本地 −Z 延伸：讓 −Z 指向樓梯上方。
                rail.rotation = Quaternion.LookRotation(-new Vector3(0f, H, -(zLow - zHigh)).normalized, Vector3.up);
                rail.localScale = new Vector3(1f, 1f, len + 0.05f);
            }
            // 樓梯牆上的兩幅畫（沿著樓梯往上錯開）。
            Prop("PaintingFrame", new Vector3(x1 - 0.01f, 2.05f, 9.75f), -90f, 0.75f);
            Prop("WallFrame", new Vector3(x1 - 0.01f, 3.55f, 7.55f), -90f, 0.9f);
        }

        private static void AddQuad(List<Vector3> v, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float v0, float v1)
        {
            int s = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            uv.Add(new Vector2(0f, v0)); uv.Add(new Vector2(1f, v0)); uv.Add(new Vector2(1f, v1)); uv.Add(new Vector2(0f, v1));
            // 單面、順時針（踏面朝上、踢面朝樓梯腳）。
            t.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
        }

        // ---------------- 二樓走廊 ----------------

        private void Upstairs()
        {
            float y2 = Case01HouseBuilder.FloorHeight;
            float zLow = Case01HouseBuilder.R17.StairFootZ, zHigh = Case01HouseBuilder.R17.StairTopZ;
            float sx = Case01HouseBuilder.R17.StairX0;
            // 樓梯井的方塊欄杆換成欄杆柱＋扶手（碰撞保留）。
            foreach (var t in L.Root.GetComponentsInChildren<Transform>(true))
                if (t.name == "Rail" && t.position.y > y2 && t.GetComponent<Renderer>() != null) t.GetComponent<Renderer>().enabled = false;
            for (float z = zHigh + 0.06f; z < zLow - 0.02f; z += 0.13f)
            {
                var b = Prop("Baluster", new Vector3(sx, y2, z), 0f);
                if (b != null) Balusters.Add(b);
            }
            for (float x = sx + 0.13f; x < Case01HouseBuilder.R17.Width - 0.1f; x += 0.13f)
            {
                var b = Prop("Baluster", new Vector3(x, y2, zLow), 0f);
                if (b != null) Balusters.Add(b);
            }
            var r1 = Prop("Handrail", new Vector3(sx, y2 + 0.9f, zHigh), 0f);
            if (r1 != null) r1.localScale = new Vector3(1f, 1f, zLow - zHigh);
            var r2 = Prop("Handrail", new Vector3(sx, y2 + 0.9f, zLow), 90f);
            if (r2 != null) r2.localScale = new Vector3(1f, 1f, Case01HouseBuilder.R17.Width - 0.08f - sx);
            Prop("NewelPost", new Vector3(sx, y2, zLow), 0f, 0.85f);
            Prop("NewelPost", new Vector3(sx, y2, zHigh), 0f, 0.85f);
            // 走廊長毯與牆上的畫。
            var runner = p.Own(ArenaArt.Mat(Color.white, 0.05f));
            runner.mainTexture = Resources.Load<Texture2D>(Folder + "IH_Runner");
            runner.mainTextureScale = new Vector2(1f, 5f);
            ArenaArt.Prim(PrimitiveType.Quad, Root, new Vector3(7.75f, y2 + 0.006f, 3.7f), new Vector3(0.8f, 5.6f, 1f), runner, new Vector3(90f, 0f, 0f), "LandingRunner", false, false);
            Prop("PaintingFrame", new Vector3(Case01HouseBuilder.R17.HallX + 0.08f, y2 + 1.55f, 4.65f), 90f, 0.7f);
        }

        // ---------------- 合照 ----------------

        private void PhotoSetup(Case01ImmersionWorld world)
        {
            PhotoNormal = Resources.Load<Texture2D>(Folder + "IH_FamilyPhoto");
            PhotoAnomaly = Resources.Load<Texture2D>(Folder + "IH_FamilyPhotoAnomaly");
            Transform sheet = null;
            if (world != null && world.P19 != null)
                foreach (var g in world.P19.Graphics)
                    if (g.Name.Contains("合照")) sheet = g.T;
            if (sheet == null) sheet = Find("PhotoPrint");
            if (sheet == null) return;
            var r = sheet.GetComponent<Renderer>();
            // Phase 1.9 每影格會在「正常／淡掉」兩個材質之間切換；直接換這個材質的貼圖，兩者都看得到高解析照片。
            PhotoMaterial = r.sharedMaterial;
            PhotoMaterial.mainTexture = PhotoNormal;
            PhotoMaterial.SetFloat("_Glossiness", 0.55f);
            Photo = new Case01InspectItem
            {
                Id = Case01Ids.Photo,
                Title = "壁爐架上的合照",
                WorldCarrier = r,
                Back = Resources.Load<Texture2D>(Folder + "IH_PhotoBack"),
                BackWriting = "週日\n十七號門口",
                Size = new Vector2(sheet.lossyScale.x, sheet.lossyScale.x * 0.75f),
            };
            Photo.Fronts.Add(PhotoNormal);
            Photo.Fronts.Add(PhotoAnomaly);
        }

        public void SetPhotoVariant(int v)
        {
            if (Photo == null) return;
            Photo.Variant = Mathf.Clamp(v, 0, Photo.Fronts.Count - 1);
            if (PhotoMaterial != null) PhotoMaterial.mainTexture = Photo.Front;
        }
    }
}

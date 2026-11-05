using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shepherd.Battle.UI.Arena
{
    /// <summary>
    /// 3D Combat Lab 的 Graybox 美術：只用 Unity 內建 Primitive 與 Standard 材質（內建渲染管線），不使用外部素材。
    /// 場景：倫敦風格街區中被異域侵蝕的廢棄廣場。
    /// </summary>
    public static class ArenaArt
    {
        public static readonly Color Rift = new Color(0.62f, 0.25f, 1f, 1f);
        public static readonly Color LampWarm = new Color(1f, 0.72f, 0.38f, 1f);
        public static readonly Color FogColor = new Color(0.07f, 0.07f, 0.11f, 1f);

        public static Color ElementColor(Core.ElementKind e)
        {
            switch (e)
            {
                case Core.ElementKind.Ember: return new Color(1f, 0.45f, 0.15f, 1f);
                case Core.ElementKind.Base: return new Color(0.35f, 0.65f, 1f, 1f);
                default: return new Color(0.35f, 1f, 0.6f, 1f);
            }
        }

        // ---------------- 材質 ----------------

        private static Shader standard;

        private static Shader Standard
        {
            get
            {
                if (standard == null) standard = Shader.Find("Standard");
                return standard;
            }
        }

        public static Material Mat(Color color, float smoothness = 0.15f, Color? emission = null, float metallic = 0f)
        {
            var m = new Material(Standard);
            m.color = color;
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emission.HasValue) SetEmission(m, emission.Value);
            return m;
        }

        public static void SetEmission(Material m, Color emission)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", emission);
        }

        /// <summary>半透明（Standard 的 Fade 模式），用於護盾、選取環等。</summary>
        public static Material FadeMat(Color color, Color? emission = null)
        {
            var m = Mat(color, 0.8f, emission);
            m.SetFloat("_Mode", 2f);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        private static Texture2D softDot;

        /// <summary>程式產生的柔邊圓點貼圖，給粒子使用。</summary>
        public static Texture2D SoftDot
        {
            get
            {
                if (softDot != null) return softDot;
                const int n = 32;
                softDot = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "ArenaSoftDot" };
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    softDot.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
                softDot.Apply();
                return softDot;
            }
        }

        // ---------------- 幾何 ----------------

        /// <summary>建立 Primitive；預設移除碰撞體（只有敵人需要被滑鼠點選）。</summary>
        public static Transform Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat,
            Vector3? euler = null, string name = null, bool keepCollider = false, bool shadows = true)
        {
            var go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            if (!keepCollider) Kill(go.GetComponent<Collider>());
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localScale = scale;
            t.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return t;
        }

        public static Transform Group(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        public static Light PointLight(Transform parent, Vector3 pos, Color color, float range, float intensity, bool shadows)
        {
            var go = new GameObject("PointLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            return l;
        }

        public static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        // ---------------- 環境 ----------------

        /// <summary>建立廣場、建築、路燈、異域侵蝕物。回傳需要緩慢漂浮動畫的碎片。</summary>
        public static List<Transform> BuildEnvironment(Transform root)
        {
            var floating = new List<Transform>();
            var rng = new System.Random(1105);
            System.Func<float, float, float> R = (a, b) => a + (float)rng.NextDouble() * (b - a);

            var env = Group("Environment", root, Vector3.zero);
            var asphalt = Mat(new Color(0.13f, 0.13f, 0.15f), 0.35f);
            var cobble = Mat(new Color(0.24f, 0.23f, 0.24f), 0.25f);
            var curb = Mat(new Color(0.36f, 0.35f, 0.34f), 0.1f);
            var brick = Mat(new Color(0.27f, 0.17f, 0.14f), 0.05f);
            var brickDark = Mat(new Color(0.17f, 0.13f, 0.13f), 0.05f);
            var stone = Mat(new Color(0.40f, 0.38f, 0.35f), 0.05f);
            var iron = Mat(new Color(0.08f, 0.09f, 0.10f), 0.5f, null, 0.6f);
            var windowLit = Mat(new Color(0.2f, 0.15f, 0.08f), 0.6f, LampWarm * 1.4f);
            var windowDark = Mat(new Color(0.05f, 0.06f, 0.08f), 0.8f);
            var lampGlow = Mat(LampWarm, 0.5f, LampWarm * 2.2f);
            var riftMat = Mat(new Color(0.12f, 0.03f, 0.2f), 0.9f, Rift * 2.6f);
            var crystal = Mat(new Color(0.2f, 0.06f, 0.3f), 0.95f, Rift * 1.3f, 0.2f);
            var red = Mat(new Color(0.55f, 0.06f, 0.05f), 0.4f);

            // 地面：柏油街道＋石板廣場＋路緣。
            Prim(PrimitiveType.Cube, env, new Vector3(0, -0.05f, 2f), new Vector3(60, 0.1f, 50), asphalt, null, "Street");
            Prim(PrimitiveType.Cube, env, new Vector3(0, 0.01f, 0.5f), new Vector3(15, 0.06f, 12), cobble, null, "Plaza");
            Prim(PrimitiveType.Cube, env, new Vector3(0, 0.06f, -5.6f), new Vector3(15.4f, 0.12f, 0.3f), curb, null, "CurbFront");
            Prim(PrimitiveType.Cube, env, new Vector3(0, 0.06f, 6.6f), new Vector3(15.4f, 0.12f, 0.3f), curb, null, "CurbBack");
            Prim(PrimitiveType.Cube, env, new Vector3(-7.6f, 0.06f, 0.5f), new Vector3(0.3f, 0.12f, 12.4f), curb, null, "CurbLeft");
            Prim(PrimitiveType.Cube, env, new Vector3(7.6f, 0.06f, 0.5f), new Vector3(0.3f, 0.12f, 12.4f), curb, null, "CurbRight");

            // 建築：後方一排、左右兩側（連棟磚造，窗戶部分亮燈）。
            var buildings = Group("Buildings", env, Vector3.zero);
            for (int i = 0; i < 7; i++)
            {
                float w = R(3.2f, 4.6f), h = R(7f, 12f), x = -13f + i * 4.4f;
                BuildFacade(buildings, new Vector3(x, 0, 12.5f + R(-0.4f, 0.6f)), new Vector3(w, h, 4f), i % 2 == 0 ? brick : brickDark,
                    windowLit, windowDark, rng, 0f);
            }
            for (int i = 0; i < 4; i++)
            {
                float h = R(6f, 10f);
                BuildFacade(buildings, new Vector3(-13.5f, 0, -4f + i * 4.6f), new Vector3(4.4f, h, 4f), i % 2 == 0 ? brickDark : stone,
                    windowLit, windowDark, rng, -90f);
                BuildFacade(buildings, new Vector3(13.5f, 0, -4f + i * 4.6f), new Vector3(4.4f, R(6f, 10f), 4f), i % 2 == 0 ? brick : brickDark,
                    windowLit, windowDark, rng, 90f);
            }

            // 殘破的矮牆與欄杆。
            var walls = Group("Walls", env, Vector3.zero);
            Prim(PrimitiveType.Cube, walls, new Vector3(-6.2f, 0.6f, 3.5f), new Vector3(0.4f, 1.2f, 4.2f), stone, null, "WallLeft");
            Prim(PrimitiveType.Cube, walls, new Vector3(-6.2f, 1.45f, 2.6f), new Vector3(0.4f, 0.5f, 1.6f), stone, new Vector3(0, 0, 8f), "WallLeftBroken");
            Prim(PrimitiveType.Cube, walls, new Vector3(6.4f, 0.45f, 4.6f), new Vector3(3.2f, 0.9f, 0.4f), stone, new Vector3(0, -12f, 0), "WallRight");
            for (int i = 0; i < 9; i++)
                Prim(PrimitiveType.Cube, walls, new Vector3(-4f + i, 0.55f, 6.9f), new Vector3(0.06f, 1.1f, 0.06f), iron, null, "Railing");
            Prim(PrimitiveType.Cube, walls, new Vector3(0, 1.05f, 6.9f), new Vector3(8.2f, 0.06f, 0.06f), iron, null, "RailTop");

            // 路燈（維多利亞式）：只有兩盞投射陰影。
            var lamps = Group("StreetLamps", env, Vector3.zero);
            Vector3[] lampPos = { new Vector3(-6.4f, 0, -3.8f), new Vector3(6.4f, 0, -3.8f), new Vector3(-6.6f, 0, 6f), new Vector3(6.6f, 0, 6.2f) };
            for (int i = 0; i < lampPos.Length; i++)
            {
                var lamp = Group("Lamp_" + i, lamps, lampPos[i]);
                Prim(PrimitiveType.Cylinder, lamp, new Vector3(0, 0.15f, 0), new Vector3(0.36f, 0.15f, 0.36f), iron);
                Prim(PrimitiveType.Cylinder, lamp, new Vector3(0, 1.9f, 0), new Vector3(0.12f, 1.9f, 0.12f), iron);
                Prim(PrimitiveType.Cube, lamp, new Vector3(0, 3.95f, 0), new Vector3(0.42f, 0.5f, 0.42f), lampGlow, null, "LampHead", false, false);
                Prim(PrimitiveType.Cube, lamp, new Vector3(0, 4.25f, 0), new Vector3(0.56f, 0.08f, 0.56f), iron);
                PointLight(lamp, new Vector3(0, 3.7f, 0), LampWarm, 9f, i < 2 ? 1.5f : 1.1f, i < 2);
            }

            // 英式紅色電話亭與長椅、路障。
            var props = Group("Props", env, Vector3.zero);
            var booth = Group("PhoneBox", props, new Vector3(-8.6f, 0, -1.2f));
            Prim(PrimitiveType.Cube, booth, new Vector3(0, 1.25f, 0), new Vector3(1f, 2.5f, 1f), red);
            Prim(PrimitiveType.Cube, booth, new Vector3(0.51f, 1.4f, 0), new Vector3(0.02f, 1.5f, 0.7f), windowLit, null, "Glass", false, false);
            Prim(PrimitiveType.Cube, booth, new Vector3(0, 2.6f, 0), new Vector3(1.1f, 0.2f, 1.1f), red);
            var bench = Group("Bench", props, new Vector3(5.2f, 0, -2.4f));
            bench.localRotation = Quaternion.Euler(0, -70f, 0);
            Prim(PrimitiveType.Cube, bench, new Vector3(0, 0.45f, 0), new Vector3(1.8f, 0.08f, 0.5f), Mat(new Color(0.25f, 0.17f, 0.1f)));
            Prim(PrimitiveType.Cube, bench, new Vector3(0, 0.8f, 0.22f), new Vector3(1.8f, 0.5f, 0.06f), Mat(new Color(0.25f, 0.17f, 0.1f)));
            Prim(PrimitiveType.Cube, bench, new Vector3(-0.8f, 0.22f, 0), new Vector3(0.08f, 0.45f, 0.5f), iron);
            Prim(PrimitiveType.Cube, bench, new Vector3(0.8f, 0.22f, 0), new Vector3(0.08f, 0.45f, 0.5f), iron);
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cylinder, props, new Vector3(-3f + i * 2f, 0.4f, -6.1f), new Vector3(0.22f, 0.4f, 0.22f), iron, null, "Bollard");

            // 異域侵蝕：地面裂痕、晶簇、背景裂隙、漂浮碎片。
            var anomaly = Group("Anomaly", env, Vector3.zero);
            for (int i = 0; i < 9; i++)
            {
                float len = R(1.5f, 4f);
                Prim(PrimitiveType.Cube, anomaly, new Vector3(R(-5f, 5f), 0.05f, R(0.5f, 6f)), new Vector3(R(0.05f, 0.14f), 0.02f, len),
                    riftMat, new Vector3(0, R(-80f, 80f), 0), "Crack", false, false);
            }
            Vector3[] clusters = { new Vector3(-4.6f, 0, 4.8f), new Vector3(4.4f, 0, 5.4f), new Vector3(-3f, 0, 7.6f), new Vector3(9f, 0, 9.5f),
                new Vector3(-9.5f, 0, 8.2f), new Vector3(1.8f, 0, 7.8f) };
            foreach (var c in clusters)
            {
                var g = Group("CrystalCluster", anomaly, c);
                int n = rng.Next(3, 6);
                for (int k = 0; k < n; k++)
                {
                    float h = R(0.6f, 2.1f);
                    Prim(PrimitiveType.Cube, g, new Vector3(R(-0.5f, 0.5f), h * 0.4f, R(-0.5f, 0.5f)), new Vector3(R(0.18f, 0.35f), h, R(0.18f, 0.35f)),
                        crystal, new Vector3(R(-25f, 25f), R(0, 90f), R(-25f, 25f)), "Crystal");
                }
            }
            var rift = Group("Rift", anomaly, new Vector3(0.6f, 0, 9.2f));
            Prim(PrimitiveType.Cube, rift, new Vector3(0, 3.4f, 0), new Vector3(0.35f, 6.8f, 0.1f), riftMat, new Vector3(0, 0, 4f), "RiftCore", false, false);
            Prim(PrimitiveType.Cube, rift, new Vector3(0.3f, 2.2f, 0), new Vector3(0.12f, 3.2f, 0.08f), riftMat, new Vector3(0, 0, -18f), "RiftBranch", false, false);
            Prim(PrimitiveType.Cube, rift, new Vector3(-0.35f, 4.6f, 0), new Vector3(0.1f, 2.4f, 0.08f), riftMat, new Vector3(0, 0, 22f), "RiftBranch", false, false);
            PointLight(rift, new Vector3(0, 2.5f, -1.5f), Rift, 11f, 2.2f, false);
            for (int i = 0; i < 14; i++)
            {
                var shard = Prim(PrimitiveType.Cube, anomaly, new Vector3(R(-6f, 6f), R(1.5f, 5.5f), R(3f, 9f)), Vector3.one * R(0.12f, 0.45f),
                    crystal, new Vector3(R(0, 360f), R(0, 360f), R(0, 360f)), "FloatingShard", false, false);
                floating.Add(shard);
            }
            return floating;
        }

        private static void BuildFacade(Transform parent, Vector3 pos, Vector3 size, Material wall, Material lit, Material dark,
            System.Random rng, float yaw)
        {
            var b = Group("Building", parent, pos);
            b.localRotation = Quaternion.Euler(0, yaw, 0);
            Prim(PrimitiveType.Cube, b, new Vector3(0, size.y * 0.5f, 0), size, wall);
            // 屋簷與窗。
            Prim(PrimitiveType.Cube, b, new Vector3(0, size.y + 0.1f, -0.1f), new Vector3(size.x + 0.3f, 0.2f, size.z + 0.3f), wall);
            int cols = Mathf.Max(2, Mathf.FloorToInt(size.x / 1.3f));
            int rows = Mathf.Max(2, Mathf.FloorToInt((size.y - 1.5f) / 1.8f));
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                float x = (c - (cols - 1) * 0.5f) * (size.x / cols);
                float y = 1.6f + r * 1.8f;
                bool on = rng.NextDouble() < 0.22;
                Prim(PrimitiveType.Cube, b, new Vector3(x, y, -size.z * 0.5f - 0.02f), new Vector3(0.6f, 1f, 0.04f), on ? lit : dark,
                    null, "Window", false, false);
            }
        }

        // ---------------- 角色 ----------------

        /// <summary>玩家：穿長大衣、戴高帽、持提燈牧杖的人形佔位。</summary>
        public static Transform BuildPlayer(Transform parent, Vector3 pos, out Renderer[] bodyRenderers, out Light lantern)
        {
            var root = Group("Player", parent, pos);
            var coat = Mat(new Color(0.18f, 0.2f, 0.26f), 0.25f);
            var skin = Mat(new Color(0.75f, 0.62f, 0.52f), 0.2f);
            var dark = Mat(new Color(0.06f, 0.06f, 0.07f), 0.3f);
            var scarf = Mat(new Color(0.55f, 0.42f, 0.18f), 0.2f);
            var wood = Mat(new Color(0.3f, 0.2f, 0.12f), 0.2f);
            var glow = Mat(LampWarm, 0.5f, LampWarm * 2.5f);
            var body = Group("Body", root, Vector3.zero);
            var parts = new List<Transform>
            {
                Prim(PrimitiveType.Capsule, body, new Vector3(-0.14f, 0.42f, 0), new Vector3(0.2f, 0.42f, 0.2f), dark, null, "LegL"),
                Prim(PrimitiveType.Capsule, body, new Vector3(0.14f, 0.42f, 0), new Vector3(0.2f, 0.42f, 0.2f), dark, null, "LegR"),
                Prim(PrimitiveType.Cylinder, body, new Vector3(0, 0.95f, 0), new Vector3(0.62f, 0.4f, 0.5f), coat, null, "CoatSkirt"),
                Prim(PrimitiveType.Capsule, body, new Vector3(0, 1.38f, 0), new Vector3(0.58f, 0.5f, 0.42f), coat, null, "Torso"),
                Prim(PrimitiveType.Cylinder, body, new Vector3(0, 1.78f, 0), new Vector3(0.36f, 0.06f, 0.36f), scarf, null, "Scarf"),
                Prim(PrimitiveType.Sphere, body, new Vector3(0, 2.02f, 0), new Vector3(0.34f, 0.36f, 0.34f), skin, null, "Head"),
                Prim(PrimitiveType.Cylinder, body, new Vector3(0, 2.17f, 0), new Vector3(0.52f, 0.015f, 0.52f), dark, null, "HatBrim"),
                Prim(PrimitiveType.Cylinder, body, new Vector3(0, 2.33f, 0), new Vector3(0.3f, 0.16f, 0.3f), dark, null, "HatCrown"),
                Prim(PrimitiveType.Capsule, body, new Vector3(-0.38f, 1.32f, 0), new Vector3(0.16f, 0.4f, 0.16f), coat, new Vector3(0, 0, -8f), "ArmL"),
                Prim(PrimitiveType.Capsule, body, new Vector3(0.4f, 1.35f, 0.12f), new Vector3(0.16f, 0.38f, 0.16f), coat, new Vector3(-30f, 0, 10f), "ArmR"),
                Prim(PrimitiveType.Cylinder, body, new Vector3(0.55f, 1.2f, 0.3f), new Vector3(0.05f, 1.2f, 0.05f), wood, null, "Staff"),
                Prim(PrimitiveType.Sphere, body, new Vector3(0.55f, 2.45f, 0.3f), new Vector3(0.2f, 0.24f, 0.2f), glow, null, "Lantern", false, false),
            };
            lantern = PointLight(body, new Vector3(0.55f, 2.45f, 0.1f), LampWarm, 4.5f, 1.2f, false);
            var renderers = new List<Renderer>();
            foreach (var p in parts) renderers.Add(p.GetComponent<Renderer>());
            bodyRenderers = renderers.ToArray();
            return root;
        }

        /// <summary>失序體：細長扭曲的抽象人形，身上有發光裂紋，周圍環繞碎片。</summary>
        public static Transform BuildEnemy(Transform parent, Vector3 pos, float height, int variant, out Renderer[] bodyRenderers,
            out Material coreMat, out List<Transform> orbiters)
        {
            var root = Group("Enemy", parent, pos);
            var body = Group("Body", root, Vector3.zero);
            body.localScale = Vector3.one * height;
            var shell = Mat(variant == 0 ? new Color(0.09f, 0.07f, 0.11f) : new Color(0.12f, 0.1f, 0.09f), 0.7f);
            coreMat = Mat(new Color(0.3f, 0.1f, 0.4f), 0.9f, Rift * 2f);
            var crack = Mat(new Color(0.2f, 0.05f, 0.3f), 0.9f, Rift * 1.6f);
            var parts = new List<Transform>
            {
                Prim(PrimitiveType.Capsule, body, new Vector3(-0.16f, 0.5f, 0), new Vector3(0.16f, 0.5f, 0.16f), shell, new Vector3(0, 0, 4f), "LegL"),
                Prim(PrimitiveType.Capsule, body, new Vector3(0.16f, 0.5f, 0), new Vector3(0.16f, 0.5f, 0.16f), shell, new Vector3(0, 0, -6f), "LegR"),
                Prim(PrimitiveType.Capsule, body, new Vector3(0, 1.45f, 0), new Vector3(0.5f, 0.62f, 0.34f), shell, new Vector3(0, 0, variant == 0 ? 6f : -5f), "Torso"),
                Prim(PrimitiveType.Cube, body, new Vector3(0.02f, 2.2f, 0), new Vector3(0.34f, 0.42f, 0.32f), shell, new Vector3(12f, 25f, 18f), "Head"),
                Prim(PrimitiveType.Capsule, body, new Vector3(-0.42f, 1.25f, 0.05f), new Vector3(0.13f, 0.62f, 0.13f), shell, new Vector3(-10f, 0, -14f), "ArmL"),
                Prim(PrimitiveType.Capsule, body, new Vector3(0.44f, 1.2f, 0.05f), new Vector3(0.13f, 0.7f, 0.13f), shell, new Vector3(-6f, 0, 18f), "ArmR"),
                Prim(PrimitiveType.Sphere, body, new Vector3(0, 1.55f, -0.16f), new Vector3(0.22f, 0.22f, 0.12f), coreMat, null, "Core", false, false),
                Prim(PrimitiveType.Cube, body, new Vector3(0.08f, 1.3f, -0.17f), new Vector3(0.03f, 0.55f, 0.02f), crack, new Vector3(0, 0, 20f), "Crack", false, false),
                Prim(PrimitiveType.Cube, body, new Vector3(-0.1f, 1.75f, -0.17f), new Vector3(0.03f, 0.35f, 0.02f), crack, new Vector3(0, 0, -30f), "Crack", false, false),
                Prim(PrimitiveType.Cube, body, new Vector3(0.05f, 2.22f, -0.17f), new Vector3(0.16f, 0.03f, 0.02f), crack, null, "Eye", false, false),
            };
            orbiters = new List<Transform>();
            for (int i = 0; i < 5; i++)
                orbiters.Add(Prim(PrimitiveType.Cube, body, Vector3.zero, Vector3.one * (0.09f + 0.03f * (i % 3)), crack,
                    new Vector3(i * 40f, i * 70f, 0), "Orbiter", false, false));
            var renderers = new List<Renderer>();
            foreach (var p in parts) renderers.Add(p.GetComponent<Renderer>());
            bodyRenderers = renderers.ToArray();

            // 供滑鼠點選的碰撞體（只有這一個）。
            var col = root.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 1.25f * height, 0);
            col.height = 2.6f * height;
            col.radius = 0.6f * height;
            return root;
        }
    }
}

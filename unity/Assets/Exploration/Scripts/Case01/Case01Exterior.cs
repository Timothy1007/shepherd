using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// v0.5 十七號周圍的簡化夜景（窗外看得到的世界，也是街區場景的基礎）：前院小徑與矮牆、人行道、馬路、
    /// 對面一排英式聯排住宅（部分窗戶亮著）、左右鄰居 16／18 號、後院圍籬與後排房屋、路燈、月亮。
    /// 只用 Primitive；外面沒有真正的點光源（避免光穿過室內牆），亮處用自發光表現。
    /// 不是任何真實地標；這是 graybox 等級的背景，不是正式街景美術。
    /// 座標：十七號佔 x0～10、z0～10，前門在南牆 x7.1，街道在南（z＜0）。
    /// </summary>
    public static class Case01Exterior
    {
        public const float PavementNorth = -2f;
        public const float RoadNorth = -4f;
        public const float RoadSouth = -11f;
        public const float OppositeFacade = -13f;

        public sealed class Result
        {
            public Transform Root;
            public readonly List<Renderer> LitWindows = new List<Renderer>();
            public readonly List<Transform> Lamps = new List<Transform>();
            /// <summary>16／18 號的立面根物件（街區場景用來放門牌）。</summary>
            public Transform House16, House18;
        }

        /// <param name="houseDepth">Phase 1.7：十七號（與左右鄰居）的縱深；預設 10 m＝原版。較深的格局把後院圍籬與樹往後移，鄰居同深（連棟住宅同深度）。</param>
        public static Result Build(Transform parent, Case01Palette p, bool withHouse17Facade = false, float houseDepth = 10f)
        {
            float extra = Mathf.Max(0f, houseDepth - 10f);
            var r = new Result();
            var g = ArenaArt.Group("Exterior", parent, Vector3.zero);
            r.Root = g;
            var grass = Own(p, new Color(0.13f, 0.16f, 0.12f), 0.05f);
            var pave = p.Tint("court_floor", new Color(0.55f, 0.55f, 0.57f));
            var asphalt = Own(p, new Color(0.09f, 0.09f, 0.1f), 0.25f);
            var kerb = Own(p, new Color(0.42f, 0.42f, 0.43f), 0.1f);
            var line = Own(p, new Color(0.8f, 0.78f, 0.7f), 0.1f, new Color(0.12f, 0.12f, 0.1f));
            var brickA = p.Tint("court_wall", new Color(0.78f, 0.55f, 0.45f));
            var brickB = p.Tint("court_wall", new Color(0.62f, 0.5f, 0.46f));
            var render = Own(p, new Color(0.62f, 0.6f, 0.55f), 0.1f, new Color(0.035f, 0.035f, 0.04f));
            var roof = Own(p, new Color(0.13f, 0.13f, 0.15f), 0.3f);
            var trimM = Own(p, new Color(0.85f, 0.84f, 0.8f), 0.2f, new Color(0.05f, 0.05f, 0.05f));
            var dark = Own(p, new Color(0.04f, 0.045f, 0.06f), 0.6f);
            var lit = Own(p, new Color(0.9f, 0.7f, 0.45f), 0.3f, new Color(1.1f, 0.78f, 0.45f));
            var litCool = Own(p, new Color(0.6f, 0.7f, 0.85f), 0.3f, new Color(0.45f, 0.55f, 0.75f));
            var fence = p.Get("wood_dark");
            var leaf = p.Get("leaf");
            var metal = Own(p, new Color(0.08f, 0.08f, 0.09f), 0.5f, null, 0.6f);
            var lampHead = Own(p, new Color(1f, 0.85f, 0.6f), 0.2f, new Color(2.2f, 1.6f, 0.9f));
            var glowMat = p.Own(ArenaArt.FadeMat(new Color(1f, 0.8f, 0.5f, 0.12f), new Color(0.35f, 0.25f, 0.12f)));
            var moon = Own(p, new Color(0.9f, 0.92f, 1f), 0.1f, new Color(1.2f, 1.25f, 1.35f));

            // 地面：草地、前院、人行道、路緣、馬路、對面人行道。
            Box(g, new Vector3(5f, -0.06f, 10f), new Vector3(110f, 0.06f, 90f), grass, "Ground");
            Box(g, new Vector3(7.1f, -0.025f, -1f), new Vector3(1.3f, 0.03f, 2f), pave, "FrontPath");
            Box(g, new Vector3(5f, -0.02f, (PavementNorth + RoadNorth) * 0.5f), new Vector3(110f, 0.04f, PavementNorth - RoadNorth), pave, "Pavement");
            Box(g, new Vector3(5f, 0.02f, RoadNorth + 0.08f), new Vector3(110f, 0.12f, 0.16f), kerb, "Kerb");
            Box(g, new Vector3(5f, -0.05f, (RoadNorth + RoadSouth) * 0.5f), new Vector3(110f, 0.04f, RoadNorth - RoadSouth), asphalt, "Road");
            for (float x = -45f; x < 55f; x += 4f)
                Box(g, new Vector3(x, -0.025f, (RoadNorth + RoadSouth) * 0.5f), new Vector3(2f, 0.01f, 0.14f), line, "RoadLine");
            Box(g, new Vector3(5f, 0.02f, RoadSouth - 0.08f), new Vector3(110f, 0.12f, 0.16f), kerb, "Kerb");
            Box(g, new Vector3(5f, -0.02f, (RoadSouth + OppositeFacade) * 0.5f), new Vector3(110f, 0.04f, OppositeFacade - RoadSouth), pave, "PavementOpposite");

            // 前院矮牆（中間留出入口）。
            Box(g, new Vector3(2.9f, 0.35f, PavementNorth + 0.1f), new Vector3(7.6f, 0.7f, 0.22f), brickB, "FrontWall");
            Box(g, new Vector3(9.15f, 0.35f, PavementNorth + 0.1f), new Vector3(2.5f, 0.7f, 0.22f), brickB, "FrontWall");

            // 對面一排聯排住宅（朝北）。
            var rng = new System.Random(1717);
            for (int i = 0; i < 14; i++)
            {
                float x0 = -40f + i * 6.4f;
                Terrace(g, new Vector3(x0 + 3.2f, 0f, OppositeFacade - 4f), 180f, 6.2f, 8f, i % 2 == 0 ? brickA : brickB, render, roof, trimM, dark,
                    rng.NextDouble() < 0.45 ? lit : rng.NextDouble() < 0.3 ? litCool : dark, rng.NextDouble() < 0.3 ? lit : dark, r, i % 3 == 0);
            }

            // 左右鄰居（側邊留出通道，所以側窗看得到圍籬與鄰居的側牆）。
            r.House16 = Terrace(g, new Vector3(-6.3f, 0f, houseDepth * 0.5f), 0f, 6.4f, houseDepth, brickB, render, roof, trimM, dark, lit, dark, r, false);
            r.House18 = Terrace(g, new Vector3(16.3f, 0f, houseDepth * 0.5f), 0f, 6.4f, houseDepth, brickA, render, roof, trimM, dark, dark, litCool, r, true);
            if (withHouse17Facade) Terrace(g, new Vector3(5f, 0f, 5f), 0f, 10f, 10f, brickA, render, roof, trimM, dark, dark, dark, r, false);

            // 側邊與後院的圍籬、後排房屋、幾棵樹。
            foreach (var x in new[] { -1.6f, 11.6f })
                Box(g, new Vector3(x, 0.9f, 7.5f + extra * 0.5f), new Vector3(0.06f, 1.8f, 19f + extra), fence, "SideFence");
            Box(g, new Vector3(5f, 0.9f, 17f + extra), new Vector3(30f, 1.8f, 0.06f), fence, "BackFence");
            // 後排只到 x≈14（不伸進 x25 以東的異常中庭）。
            for (int i = 0; i < 5; i++)
            {
                float x0 = -18f + i * 6.4f;
                Terrace(g, new Vector3(x0 + 3.2f, 0f, 28f + extra), 180f, 6.2f, 8f, i % 2 == 0 ? brickB : brickA, render, roof, trimM, dark,
                    rng.NextDouble() < 0.35 ? lit : dark, dark, r, false);
            }
            foreach (var t in new[] { new Vector3(1.5f, 0f, 14f + extra), new Vector3(8.5f, 0f, 15f + extra), new Vector3(-12f, 0f, -12.2f), new Vector3(22f, 0f, -12.2f) })
            {
                Box(g, t + new Vector3(0f, 1.2f, 0f), new Vector3(0.22f, 2.4f, 0.22f), fence, "TreeTrunk");
                Sphere(g, t + new Vector3(0f, 3.2f, 0f), new Vector3(2.6f, 2.4f, 2.6f), leaf, "TreeCrown");
            }

            // 路燈（自發光燈頭＋地上淡淡的光暈；沒有真正的點光源）。
            foreach (var x in new[] { -8f, 4.2f, 16f, 28f })
            {
                var lamp = ArenaArt.Group("StreetLamp", g, new Vector3(x, 0f, RoadNorth + 0.45f));
                Box(lamp, new Vector3(0f, 2.2f, 0f), new Vector3(0.12f, 4.4f, 0.12f), metal, "Post");
                Box(lamp, new Vector3(0f, 4.35f, -0.35f), new Vector3(0.08f, 0.08f, 0.8f), metal, "Arm");
                Box(lamp, new Vector3(0f, 4.22f, -0.72f), new Vector3(0.36f, 0.16f, 0.36f), lampHead, "Head");
                var glow = ArenaArt.Prim(PrimitiveType.Cylinder, lamp, new Vector3(0f, 0.01f, -0.8f), new Vector3(4.2f, 0.005f, 4.2f), glowMat, null, "Pool", false, false);
                glow.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.Lamps.Add(lamp);
            }
            Sphere(g, new Vector3(-38f, 42f, 70f), Vector3.one * 4f, moon, "Moon");
            return r;
        }

        /// <summary>一戶英式聯排住宅：磚牆立面、白色窗框、門與門楣、斜屋頂、兩層窗戶。yaw 0＝朝南（立面在 -z 側）。</summary>
        private static Transform Terrace(Transform g, Vector3 center, float yaw, float width, float depth, Material brick, Material render, Material roof,
            Material trim, Material dark, Material upperWin, Material lowerWin, Result r, bool bay)
        {
            var h = ArenaArt.Group("Terrace", g, center);
            h.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float half = depth * 0.5f;
            Box(h, new Vector3(0f, 3.1f, 0f), new Vector3(width - 0.1f, 6.2f, depth), brick, "Body");
            Box(h, new Vector3(0f, 0.35f, -half - 0.02f), new Vector3(width - 0.1f, 0.7f, 0.06f), render, "Plinth");
            // 斜屋頂（兩片）＋煙囪。
            for (int s = -1; s <= 1; s += 2)
            {
                var rp = ArenaArt.Prim(PrimitiveType.Cube, h, new Vector3(0f, 7.2f, s * half * 0.5f), new Vector3(width, 0.12f, half * 1.2f), roof, new Vector3(s * -32f, 0f, 0f), "Roof", false, true);
                rp.localPosition = new Vector3(0f, 6.9f, s * half * 0.48f);
            }
            Box(h, new Vector3(width * 0.3f, 7.8f, 0f), new Vector3(0.6f, 1.4f, 0.8f), brick, "Chimney");
            // 門（左）與窗。
            float doorX = -width * 0.28f;
            Box(h, new Vector3(doorX, 1.1f, -half - 0.04f), new Vector3(1.0f, 2.2f, 0.06f), dark, "Door");
            Box(h, new Vector3(doorX, 2.35f, -half - 0.05f), new Vector3(1.2f, 0.12f, 0.1f), trim, "DoorHead");
            Win(h, new Vector3(width * 0.18f, 1.55f, -half), bay ? 1.6f : 1.2f, 1.3f, lowerWin, trim, r);
            Win(h, new Vector3(doorX, 4.4f, -half), 0.9f, 1.2f, upperWin, trim, r);
            Win(h, new Vector3(width * 0.18f, 4.4f, -half), 1.2f, 1.2f, upperWin, trim, r);
            return h;
        }

        private static void Win(Transform h, Vector3 pos, float w, float hgt, Material glass, Material trim, Result r)
        {
            var gl = Box(h, pos + new Vector3(0f, 0f, -0.03f), new Vector3(w, hgt, 0.04f), glass, "Window");
            if (glass.IsKeywordEnabled("_EMISSION") && glass.GetColor("_EmissionColor").maxColorComponent > 0.3f) r.LitWindows.Add(gl.GetComponent<Renderer>());
            Box(h, pos + new Vector3(0f, -hgt * 0.5f - 0.05f, -0.07f), new Vector3(w + 0.2f, 0.08f, 0.14f), trim, "Sill");
            Box(h, pos + new Vector3(0f, hgt * 0.5f + 0.04f, -0.05f), new Vector3(w + 0.1f, 0.06f, 0.08f), trim, "Lintel");
            Box(h, pos + new Vector3(0f, 0f, -0.055f), new Vector3(0.04f, hgt, 0.03f), trim, "Mullion");
        }

        private static Material Own(Case01Palette p, Color c, float smooth, Color? emission = null, float metallic = 0f)
        {
            return p.Own(ArenaArt.Mat(c, smooth, emission, metallic));
        }

        private static Transform Box(Transform p, Vector3 pos, Vector3 scale, Material m, string name)
        {
            return ArenaArt.Prim(PrimitiveType.Cube, p, pos, scale, m, null, name, false, false);
        }

        private static Transform Sphere(Transform p, Vector3 pos, Vector3 scale, Material m, string name)
        {
            return ArenaArt.Prim(PrimitiveType.Sphere, p, pos, scale, m, null, name, false, false);
        }
    }
}

using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// v0.5 失序門「不穩定的邊界」：門洞四周由許多短段組成的發光框（左右各 9 段、上方 5 段），
    /// 每段依雜訊沿門面局部位移、偶爾短暫缺口、在紅／綠／藍／紫／青之間跳色；另有兩道漂移、淡出的殘影框。
    /// 只是外觀：沒有碰撞體，不動門洞平面、Portal.Crossed、互動熱區或門上的記號。
    /// 校正後收斂成對齊、灰色、穩定的門框；降低閃爍時位移與跳色都變慢、缺口關閉、顏色較灰。
    /// </summary>
    public sealed class Case01PortalEdge
    {
        public const int JambSegments = 9;
        public const int HeadSegments = 5;

        private sealed class Seg
        {
            public Transform T;
            public Renderer R;
            public Vector3 Home;
            /// <summary>允許位移的方向（門面內、垂直於這一段）。</summary>
            public Vector3 Drift;
            public int Seed;
        }

        private readonly List<Seg> segs = new List<Seg>();
        private readonly List<Transform> ghosts = new List<Transform>();
        private readonly List<Vector3> ghostHome = new List<Vector3>();
        private Material[] colors;
        private Material stable;
        private Material ghostMat;
        private Vector3 right;

        public Transform Root { get; private set; }
        /// <summary>目前隱藏中的段數（缺口；測試用）。</summary>
        public int Gaps { get; private set; }
        /// <summary>各段離原位的最大距離（測試用）。</summary>
        public float MaxOffset { get; private set; }
        /// <summary>目前使用的不同顏色數（測試用）。</summary>
        public int ColorsInUse { get; private set; }
        public int SegmentCount { get { return segs.Count; } }

        public static Case01PortalEdge Build(Transform parent, Case01Portal p, Case01Palette pal)
        {
            var e = new Case01PortalEdge();
            e.right = p.Right;
            e.Root = ArenaArt.Group("PortalEdgeV05_" + p.Door, parent, Vector3.zero);
            e.colors = new[]
            {
                pal.Own(ArenaArt.Mat(new Color(0.08f, 0.02f, 0.02f), 0.2f, new Color(1.6f, 0.15f, 0.12f))),
                pal.Own(ArenaArt.Mat(new Color(0.02f, 0.08f, 0.02f), 0.2f, new Color(0.15f, 1.4f, 0.25f))),
                pal.Own(ArenaArt.Mat(new Color(0.02f, 0.02f, 0.08f), 0.2f, new Color(0.2f, 0.35f, 1.7f))),
                pal.Own(ArenaArt.Mat(new Color(0.07f, 0.02f, 0.07f), 0.2f, new Color(1.3f, 0.2f, 1.4f))),
                pal.Own(ArenaArt.Mat(new Color(0.02f, 0.07f, 0.07f), 0.2f, new Color(0.15f, 1.3f, 1.4f))),
                pal.Own(ArenaArt.Mat(new Color(0.08f, 0.08f, 0.08f), 0.2f, new Color(0.9f, 0.92f, 1f))),
            };
            e.stable = pal.Own(ArenaArt.Mat(new Color(0.22f, 0.22f, 0.24f), 0.25f, new Color(0.12f, 0.13f, 0.15f)));
            e.ghostMat = pal.Own(ArenaArt.FadeMat(new Color(0.6f, 0.4f, 0.9f, 0.18f), new Color(0.35f, 0.2f, 0.5f)));

            float hw = p.HalfWidth, h = p.Height;
            // 門洞來源側（玩家走來的那一面）稍微往外，蓋住牆的門邊。
            var basePos = p.PlanePoint - p.Normal * 0.1f;
            var rot = Quaternion.LookRotation(p.Normal, Vector3.up);
            float seg = h / JambSegments;
            int n = 0;
            foreach (var side in new[] { -1f, 1f })
            {
                for (int i = 0; i < JambSegments; i++)
                {
                    var pos = basePos + p.Right * side * (hw + 0.045f) + Vector3.up * (seg * (i + 0.5f));
                    e.Add(pos, rot, new Vector3(0.07f, seg * 0.86f, 0.06f), p.Right * side, n++);
                }
            }
            float hs = (2f * hw + 0.18f) / HeadSegments;
            for (int i = 0; i < HeadSegments; i++)
            {
                var pos = basePos + p.Right * (-hw - 0.09f + hs * (i + 0.5f)) + Vector3.up * (h + 0.045f);
                e.Add(pos, rot, new Vector3(hs * 0.86f, 0.07f, 0.06f), Vector3.up, n++);
            }
            // 兩道殘影框（淡、無陰影）。
            for (int g = 0; g < 2; g++)
            {
                var gr = ArenaArt.Group("Ghost_" + g, e.Root, basePos - p.Normal * (0.04f + 0.03f * g));
                gr.rotation = rot;
                ArenaArt.Prim(PrimitiveType.Cube, gr, new Vector3(-hw - 0.05f, h * 0.5f, 0f), new Vector3(0.05f, h, 0.01f), e.ghostMat, null, "GhostJamb", false, false);
                ArenaArt.Prim(PrimitiveType.Cube, gr, new Vector3(hw + 0.05f, h * 0.5f, 0f), new Vector3(0.05f, h, 0.01f), e.ghostMat, null, "GhostJamb", false, false);
                ArenaArt.Prim(PrimitiveType.Cube, gr, new Vector3(0f, h + 0.05f, 0f), new Vector3(2f * hw + 0.15f, 0.05f, 0.01f), e.ghostMat, null, "GhostHead", false, false);
                e.ghosts.Add(gr);
                e.ghostHome.Add(gr.position);
            }
            e.Root.gameObject.SetActive(false);
            return e;
        }

        private void Add(Vector3 pos, Quaternion rot, Vector3 scale, Vector3 drift, int seed)
        {
            var t = ArenaArt.Prim(PrimitiveType.Cube, Root, pos, scale, colors[seed % colors.Length], null, "EdgeSegment", false, false);
            t.rotation = rot;
            segs.Add(new Seg { T = t, R = t.GetComponent<Renderer>(), Home = t.position, Drift = drift, Seed = seed });
        }

        private static float Hash(int a, int b)
        {
            unchecked
            {
                int h = a * 374761393 + b * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        /// <summary>每影格更新（不建立新材質，只換 sharedMaterial）。</summary>
        public void Tick(float clock, Case01PortalState state, bool reduceFlicker, bool visible)
        {
            bool on = visible && state != Case01PortalState.Dormant;
            if (Root.gameObject.activeSelf != on) Root.gameObject.SetActive(on);
            Gaps = 0;
            MaxOffset = 0f;
            ColorsInUse = 0;
            if (!on) return;
            float glitch = state == Case01PortalState.Wrong ? 1f : state == Case01PortalState.Unstable ? 0.65f : state == Case01PortalState.Anchored ? 0.2f : 0f;
            float speed = reduceFlicker ? 0.25f : 1f;
            int slice = Mathf.FloorToInt(clock * (reduceFlicker ? 2.5f : 9f));
            int used = 0;
            foreach (var s in segs)
            {
                if (glitch <= 0f)
                {
                    // 校正後：對齊、灰色、穩定。
                    s.T.position = s.Home;
                    s.R.enabled = true;
                    s.R.sharedMaterial = stable;
                    continue;
                }
                // 流動：平滑雜訊位移＋偶發的跳格。
                float flow = Mathf.Clamp((Mathf.PerlinNoise(clock * 1.3f * speed + s.Seed * 0.37f, s.Seed * 1.7f) - 0.5f) * 2f, -1f, 1f);
                float jump = Hash(slice, s.Seed) < 0.12f * glitch ? (Hash(slice + 7, s.Seed) - 0.5f) * 0.22f : 0f;
                float amount = (flow * 0.06f + (reduceFlicker ? 0f : jump)) * glitch;
                s.T.position = s.Home + s.Drift * amount;
                MaxOffset = Mathf.Max(MaxOffset, Mathf.Abs(amount));
                // 局部缺口（降低閃爍時不做）。
                bool gap = !reduceFlicker && Hash(slice + 31, s.Seed) < 0.14f * glitch;
                s.R.enabled = !gap;
                if (gap) Gaps++;
                // 跳色：低狀態時多半是白，錯接時紅綠藍紫青。降低閃爍時偏向灰白。
                int ci = Hash(slice / 2 + 3, s.Seed) < (reduceFlicker ? 0.7f : 0.25f) * (1.2f - glitch) ? colors.Length - 1 : (int)(Hash(slice / 2, s.Seed) * (colors.Length - 1));
                s.R.sharedMaterial = colors[ci];
                if (!gap) used |= 1 << ci;
            }
            for (int b = used; b != 0; b &= b - 1) ColorsInUse++;
            for (int g = 0; g < ghosts.Count; g++)
            {
                var gr = ghosts[g];
                bool show = glitch > 0.3f;
                if (gr.gameObject.activeSelf != show) gr.gameObject.SetActive(show);
                if (!show) continue;
                float k = Mathf.Repeat(clock * 0.45f * speed + g * 0.5f, 1f);
                gr.position = ghostHome[g] + right * ((k - 0.5f) * 0.18f * (g == 0 ? 1f : -1f)) + Vector3.up * (Mathf.Sin(clock * 0.8f + g) * 0.03f);
            }
        }
    }
}

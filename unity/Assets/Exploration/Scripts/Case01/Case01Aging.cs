using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// v0.4 房屋舊化痕跡（「被現實遺忘」）：牆角積塵、牆面污痕、小塊剝落、地上散落紙張。
    /// 全部是無碰撞體的薄片，貼地／貼牆、放在角落附近（避開門洞、窗、樓梯井與調查熱區），不遮擋任何互動。
    /// 程度由 <see cref="Case01Mood.AgingLevel"/> 決定：1＝局部灰塵；2＝灰塵＋污痕＋剝落＋紙張；3（H3）＝灰塵／污痕／剝落保留、紙張收走。
    /// 位置以固定亂數種子產生，同一次建置、F1～F6 重建都一樣。
    /// </summary>
    public sealed class Case01Aging
    {
        private sealed class RoomSet
        {
            public readonly List<GameObject> Dust1 = new List<GameObject>();
            public readonly List<GameObject> Dust2 = new List<GameObject>();
            public readonly List<GameObject> Marks = new List<GameObject>();
            public readonly List<GameObject> Papers = new List<GameObject>();
            public int Level;
        }

        private readonly Dictionary<Case01Room, RoomSet> rooms = new Dictionary<Case01Room, RoomSet>();
        public readonly List<Case01Room> Rooms = new List<Case01Room>();

        public int LevelOf(Case01Room r)
        {
            RoomSet s;
            return rooms.TryGetValue(r, out s) ? s.Level : 0;
        }

        /// <summary>某房間目前看得見的痕跡數量（測試用）。</summary>
        public int VisibleCount(Case01Room r)
        {
            RoomSet s;
            if (!rooms.TryGetValue(r, out s)) return 0;
            int n = 0;
            foreach (var l in new[] { s.Dust1, s.Dust2, s.Marks, s.Papers })
                foreach (var g in l)
                    if (g.activeSelf) n++;
            return n;
        }

        public int PaperCount(Case01Room r)
        {
            RoomSet s;
            if (!rooms.TryGetValue(r, out s)) return 0;
            int n = 0;
            foreach (var g in s.Papers) if (g.activeSelf) n++;
            return n;
        }

        public void Apply(Case01Room r, int level)
        {
            RoomSet s;
            if (!rooms.TryGetValue(r, out s)) return;
            s.Level = level;
            foreach (var g in s.Dust1) g.SetActive(level >= 1);
            foreach (var g in s.Dust2) g.SetActive(level >= 2);
            foreach (var g in s.Marks) g.SetActive(level >= 2);
            foreach (var g in s.Papers) g.SetActive(level == 2);
        }

        private static readonly Case01Room[] AgingRooms =
        {
            Case01Room.Entrance, Case01Room.Kitchen, Case01Room.Living, Case01Room.Dining, Case01Room.Landing, Case01Room.Study, Case01Room.Bedroom,
        };

        public static Case01Aging Build(Case01HouseLayout L, Transform g, Case01Palette p)
        {
            var a = new Case01Aging();
            var dust = p.Own(ArenaArt.FadeMat(new Color(0.16f, 0.14f, 0.12f, 0.45f), Color.black));
            dust.mainTexture = Blot;
            var stain = p.Own(ArenaArt.FadeMat(new Color(0.22f, 0.19f, 0.15f, 0.4f), Color.black));
            stain.mainTexture = Blot;
            var peel = p.Own(ArenaArt.FadeMat(new Color(0.78f, 0.75f, 0.68f, 0.85f), Color.black));
            peel.mainTexture = Blot;
            var paper = p.Get("paper");
            foreach (var room in AgingRooms)
            {
                Bounds b = default(Bounds);
                bool found = false;
                foreach (var v in L.Rooms)
                    if (v.Room == room)
                    {
                        b = v.Bounds;
                        found = true;
                    }
                if (!found) continue;
                var set = new RoomSet();
                a.rooms[room] = set;
                a.Rooms.Add(room);
                var rg = ArenaArt.Group("Aging_" + room, g, Vector3.zero);
                var rng = new System.Random(4100 + (int)room * 37);
                System.Func<float, float, float> R = (lo, hi) => lo + (float)rng.NextDouble() * (hi - lo);

                bool upstairs = b.min.y > 1f;
                float floorY = (upstairs ? Case01HouseBuilder.FloorHeight : 0f) + 0.004f;
                float x0 = b.min.x + 0.075f, x1 = b.max.x - 0.075f;
                float z0 = Mathf.Max(b.min.z, 0f) + 0.075f, z1 = b.max.z - 0.075f;
                // 樓梯井（x8.8～10、z1.0～4.6）上方與樓梯本身不放東西。
                if (room == Case01Room.Entrance || room == Case01Room.Landing) x1 = Mathf.Min(x1, Case01HouseBuilder.StairX0 - 0.2f);

                // 四個角落的積塵：前兩個屬於 H1 的局部灰塵，其餘 H2 起出現。
                var corners = new[] { new Vector2(x0, z0), new Vector2(x1, z1), new Vector2(x0, z1), new Vector2(x1, z0) };
                for (int i = 0; i < corners.Length; i++)
                {
                    var c = corners[i];
                    float sx = Mathf.Sign((x0 + x1) * 0.5f - c.x), sz = Mathf.Sign((z0 + z1) * 0.5f - c.y);
                    var pos = new Vector3(c.x + sx * R(0.25f, 0.45f), floorY, c.y + sz * R(0.25f, 0.45f));
                    var d = Flat(rg, pos, new Vector2(R(0.5f, 0.9f), R(0.35f, 0.6f)), R(0f, 180f), dust, "Dust");
                    (i < 2 ? set.Dust1 : set.Dust2).Add(d);
                }
                // 牆面污痕與一小塊剝落：靠近牆角的低處（不在門洞、窗戶的位置）。
                float h0 = upstairs ? Case01HouseBuilder.FloorHeight : 0f;
                set.Marks.Add(Wall(rg, new Vector3(x0 + 0.005f, h0 + R(0.25f, 0.7f), z0 + R(0.5f, 0.9f)), 90f, new Vector2(R(0.3f, 0.55f), R(0.25f, 0.5f)), stain, "Stain"));
                set.Marks.Add(Wall(rg, new Vector3(x0 + R(0.5f, 0.9f), h0 + R(0.3f, 0.9f), z1 - 0.005f), 180f, new Vector2(R(0.25f, 0.45f), R(0.3f, 0.6f)), stain, "Stain"));
                set.Marks.Add(Wall(rg, new Vector3(x0 + 0.006f, h0 + R(1.1f, 1.5f), z1 - R(0.5f, 0.8f)), 90f, new Vector2(R(0.12f, 0.2f), R(0.08f, 0.14f)), peel, "Peel"));
                // 地上散落的紙張（只在 H2；H3 收走）。
                for (int i = 0; i < 3; i++)
                {
                    var c = corners[(i + 1) % 4];
                    float sx = Mathf.Sign((x0 + x1) * 0.5f - c.x), sz = Mathf.Sign((z0 + z1) * 0.5f - c.y);
                    var pos = new Vector3(c.x + sx * R(0.5f, 1.1f), floorY + 0.002f + i * 0.001f, c.y + sz * R(0.4f, 1.0f));
                    set.Papers.Add(Flat(rg, pos, new Vector2(0.21f, 0.29f), R(0f, 360f), paper, "Paper"));
                }
                a.Apply(room, 0);
            }
            return a;
        }

        private static Texture2D blot;

        /// <summary>柔邊不規則的污漬遮罩（程序產生、只建一次；不是方形色塊）。</summary>
        public static Texture2D Blot
        {
            get
            {
                if (blot != null) return blot;
                const int n = 64;
                blot = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Case01AgingBlot", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                        float ang = Mathf.Atan2(v, u);
                        float edge = 0.72f + 0.16f * Mathf.Sin(ang * 3f + 1.3f) + 0.08f * Mathf.Sin(ang * 7f);
                        float r = Mathf.Sqrt(u * u + v * v) / edge;
                        float grain = Mathf.PerlinNoise(x * 0.21f, y * 0.21f);
                        float alpha = Mathf.Clamp01((1f - r) * 2.2f) * (0.55f + 0.45f * grain);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                    }
                blot.SetPixels32(px);
                blot.Apply(false, true);
                return blot;
            }
        }

        private static GameObject Flat(Transform g, Vector3 pos, Vector2 size, float yaw, Material m, string name)
        {
            var q = ArenaArt.Prim(PrimitiveType.Quad, g, pos, new Vector3(size.x, size.y, 1f), m, null, name, false, false);
            q.localRotation = Quaternion.Euler(90f, yaw, 0f);
            return q.gameObject;
        }

        private static GameObject Wall(Transform g, Vector3 pos, float yaw, Vector2 size, Material m, string name)
        {
            // Quad 正面朝 -z；yaw 讓它面向房間內側。
            var q = ArenaArt.Prim(PrimitiveType.Quad, g, pos, new Vector3(size.x, size.y, 1f), m, null, name, false, false);
            q.localRotation = Quaternion.Euler(0f, yaw + 180f, 0f);
            return q.gameObject;
        }
    }
}

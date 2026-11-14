using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;
using Mode = Shepherd.Exploration.Lantern.LanternReveal.Mode;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 能力小原型（docs/design/CORE_LOOP_AND_ABILITIES_V1.md 第 10 節）：獨立的三個小房間，白天、褪色風格、沒有屋頂。
    /// 一、到高處：樓梯塌了一半（記憶中是完整的）；另有一大一小兩個木箱可以墊腳。
    /// 二、過缺口：地板破了一個洞（記憶中有木板，其中一塊記憶有偏差）；另有一塊真的木板可以搬過去，木箱也能丟進洞裡墊著。
    /// 三、打開封住的門：門靠壓板打開；壓板在矮牆圍住的角落，記憶中上面放著一袋米（照＋定），也可以把木箱抬過矮牆放上去。
    /// 不動 Case01；按 N 進來（重來）。
    /// </summary>
    public sealed partial class LanternGraybox
    {
        public static readonly Vector3 SandboxOrigin = new Vector3(-130f, 0f, 0f);
        public Transform SandboxRoot { get; private set; }
        public readonly List<LanternMemory> Memories = new List<LanternMemory>();
        public Bounds SandboxBounds { get; private set; }
        public Bounds SandboxPlatform { get; private set; }
        public float SandboxGapX0 { get; private set; }
        public float SandboxGapX1 { get; private set; }
        public Transform SandboxDoor { get; private set; }
        public Vector3 SandboxDoorClosed { get; private set; }
        public Vector3 SandboxPlate { get; private set; }
        public LanternMemory SandboxSack { get; private set; }
        public LanternTetherable SandboxBoard { get; private set; }
        public LanternTetherable SandboxCrateSmall { get; private set; }
        public LanternTetherable SandboxCrateBig { get; private set; }

        private Vector3 SB(float x, float y, float z) => SandboxOrigin + new Vector3(x, y, z);

        public bool InSandbox(Vector3 p) => SandboxRoot != null && SandboxBounds.Contains(p + Vector3.up * 0.5f);

        public void BuildSandbox()
        {
            if (SandboxRoot != null)
            {
                var old = SandboxRoot;
                Memories.RemoveAll(m => m == null || m.transform.IsChildOf(old));
                Tetherables.RemoveAll(t => t == null || t.transform.IsChildOf(old));
                ArenaArt.Kill(old.gameObject);
            }
            Memories.RemoveAll(m => m == null);
            SandboxRoot = new GameObject("AbilitySandbox").transform;
            SandboxRoot.SetParent(Root, false);
            var saved = into;
            into = SandboxRoot;
            try { SandboxGeometry(); }
            finally { into = saved; }
            Physics.SyncTransforms();
        }

        private LanternMemory MemoryBox(string name, string label, Vector3 center, Vector3 size, Material m, bool biased = false, Vector3? euler = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Into, false);
            go.transform.position = center;
            if (euler.HasValue) go.transform.rotation = Quaternion.Euler(euler.Value);
            var col = go.AddComponent<BoxCollider>();
            col.size = size;
            Box(name + "Mesh", center, size, m, false, euler, go.transform);
            var mem = go.AddComponent<LanternMemory>();
            mem.Label = label;
            mem.Biased = biased;
            var h = size * 0.5f;
            mem.Probes = new[] { Vector3.zero, new Vector3(h.x * 0.8f, 0f, 0f), new Vector3(-h.x * 0.8f, 0f, 0f), new Vector3(0f, 0f, h.z * 0.8f), new Vector3(0f, 0f, -h.z * 0.8f) };
            mem.Init(col);
            Memories.Add(mem);
            return mem;
        }

        private void SandboxGeometry()
        {
            var floor = Mat("SB_Floor", Mode.Dual, new Color(0.74f, 0.58f, 0.42f), new Color(0.5f, 0.5f, 0.5f), null, null, 0.45f, 0.4f, 0.004f, true);
            var wallM = Mat("SB_Wall", Mode.Dual, new Color(0.88f, 0.82f, 0.7f), new Color(0.5f, 0.5f, 0.5f), null, null, 0.2f, 0.2f, 0.004f, true);
            var trim = Mat("SB_Trim", Mode.Dual, new Color(0.42f, 0.27f, 0.16f), new Color(0.4f, 0.4f, 0.4f), null, null, 0.4f, 0.4f, 0f);
            var stone = Mat("SB_Stone", Mode.Dual, new Color(0.55f, 0.53f, 0.5f), new Color(0.45f, 0.45f, 0.45f), null, null, 0.3f, 0.3f, 0.004f, true);
            var memWood = Mat("SB_MemoryWood", Mode.RealOnly, new Color(0.82f, 0.56f, 0.3f), new Color(0.82f, 0.56f, 0.3f), null, null, 0.4f, 0.4f, 0.006f);
            var memCloth = Mat("SB_MemorySack", Mode.RealOnly, new Color(0.86f, 0.78f, 0.55f), new Color(0.86f, 0.78f, 0.55f), null, null, 0.2f, 0.2f, 0.006f);
            var crateM = Mat("SB_Crate", Mode.Dual, new Color(0.7f, 0.52f, 0.3f), new Color(0.5f, 0.5f, 0.5f), null, null, 0.3f, 0.3f, 0f);
            var boardM = Mat("SB_Board", Mode.Dual, new Color(0.6f, 0.42f, 0.26f), new Color(0.5f, 0.5f, 0.5f), null, null, 0.3f, 0.3f, 0f);
            var brass = Mat("SB_Plate", Mode.Dual, new Color(0.8f, 0.62f, 0.28f), new Color(0.5f, 0.5f, 0.5f), null, null, 0.7f, 0.5f, 0f);
            var doorM = Mat("SB_Door", Mode.Dual, new Color(0.18f, 0.38f, 0.3f), new Color(0.4f, 0.4f, 0.4f), null, null, 0.4f, 0.4f, 0f);
            float H = 3.2f;

            // 地板：中間的房間有一道 1.6 m 寬、0.66 m 深的缺口（靠這一側有一階可以爬回來，不會卡死）。
            SandboxGapX0 = SandboxOrigin.x + 8.2f;
            SandboxGapX1 = SandboxOrigin.x + 9.8f;
            Box("SBFloorL", SB(4.1f, -0.43f, 4f), new Vector3(8.2f, 0.86f, 8f), floor);
            Box("SBPitFloor", SB(9f, -0.76f, 4f), new Vector3(1.6f, 0.2f, 8f), stone);
            Box("SBPitLedge", SB(8.4f, -0.495f, 4f), new Vector3(0.4f, 0.33f, 8f), stone);
            Box("SBFloorR", SB(13.9f, -0.43f, 4f), new Vector3(8.2f, 0.86f, 8f), floor);
            // 外牆（沒有屋頂，陽光照得進來）、兩道隔間牆（中間各留 2 m 的門洞）。
            Box("SBWallS", SB(9f, H / 2f, -0.1f), new Vector3(18.4f, H, 0.2f), wallM);
            Box("SBWallN", SB(9f, H / 2f, 8.1f), new Vector3(18.4f, H, 0.2f), wallM);
            Box("SBWallW", SB(-0.1f, H / 2f, 4f), new Vector3(0.2f, H, 8.4f), wallM);
            Box("SBWallE1", SB(18.1f, H / 2f, 5.2f), new Vector3(0.2f, H, 5.6f), wallM);
            Box("SBWallE2", SB(18.1f, H / 2f, 0.3f), new Vector3(0.2f, H, 0.6f), wallM);
            Box("SBWallE3", SB(18.1f, H - 0.4f, 1.6f), new Vector3(0.2f, 0.8f, 1.6f), wallM);
            foreach (float px in new[] { 6f, 12f })
            {
                Box("SBPartA", SB(px, H / 2f, 1.5f), new Vector3(0.2f, H, 3f), wallM);
                Box("SBPartB", SB(px, H / 2f, 6.5f), new Vector3(0.2f, H, 3f), wallM);
                Box("SBPartTop", SB(px, H - 0.3f, 4f), new Vector3(0.2f, 0.6f, 2f), wallM);
            }

            // 一、到高處：高台（1.0 m）、塌掉一半的樓梯（第一階是真的，上面兩階只在記憶中）、一大一小兩個木箱。
            Box("SBPlatform", SB(3f, 0.5f, 7f), new Vector3(6f, 1f, 2f), trim);
            var plat = new Bounds();
            plat.SetMinMax(SB(0f, 0.85f, 6f), SB(6f, 2.6f, 8f));
            SandboxPlatform = plat;
            Box("SBStep1", SB(5.2f, 0.165f, 5.0f), new Vector3(0.8f, 0.33f, 0.4f), trim);
            MemoryBox("SBStep2", "記憶中的樓梯", SB(5.2f, 0.335f, 5.4f), new Vector3(0.8f, 0.67f, 0.4f), memWood);
            MemoryBox("SBStep3", "記憶中的樓梯", SB(5.2f, 0.5f, 5.8f), new Vector3(0.8f, 1.0f, 0.4f), memWood);
            Box("SBStairDebris", SB(4.7f, 0.05f, 4.6f), new Vector3(0.9f, 0.1f, 0.35f), trim, false, new Vector3(0f, 25f, 0f));
            Box("SBGoalFrame", SB(1.2f, 1.45f, 7.95f), new Vector3(0.5f, 0.6f, 0.04f), brass, false);
            SandboxCrateSmall = Tether("SBCrateSmall", "小木箱", SB(1.4f, 0f, 2.2f), new Vector3(0.55f, 0.33f, 0.55f), 2f);
            Box("SBCrateSmallMesh", SandboxCrateSmall.transform.position + Vector3.up * 0.165f, new Vector3(0.55f, 0.33f, 0.55f), crateM, false, null, SandboxCrateSmall.transform);
            SandboxCrateBig = Tether("SBCrateBig", "大木箱", SB(2.4f, 0f, 1.8f), new Vector3(0.62f, 0.66f, 0.62f), 4f);
            Box("SBCrateBigMesh", SandboxCrateBig.transform.position + Vector3.up * 0.33f, new Vector3(0.62f, 0.66f, 0.62f), crateM, false, null, SandboxCrateBig.transform);

            // 二、過缺口：四塊記憶中的木板（第三塊記憶有偏差），一塊真的木板靠在牆邊。
            for (int i = 0; i < 4; i++)
                MemoryBox("SBPlank" + i, i == 2 ? "記憶中的木板（晃動）" : "記憶中的木板", SB(9f, -0.03f, 3.3f + i * 0.42f), new Vector3(2.0f, 0.06f, 0.38f), memWood, i == 2);
            SandboxBoard = Tether("SBBoard", "木板", SB(7.1f, 0f, 6.9f), new Vector3(2.0f, 0.06f, 0.5f), 3f);
            Box("SBBoardMesh", SandboxBoard.transform.position + Vector3.up * 0.03f, new Vector3(2.0f, 0.06f, 0.5f), boardM, false, null, SandboxBoard.transform);

            // 三、打開封住的門：矮牆（1.2 m，人跨不過去）圍住的角落裡有一塊壓板；記憶中壓板上放著一袋米。門在東牆。
            Box("SBLowWallX", SB(13.2f, 0.6f, 5.6f), new Vector3(2.4f, 1.2f, 0.15f), wallM);
            Box("SBLowWallZ", SB(14.4f, 0.6f, 6.85f), new Vector3(0.15f, 1.2f, 2.5f), wallM);
            SandboxPlate = SB(13.3f, 0.03f, 6.9f);
            Box("SBPlate", SandboxPlate, new Vector3(0.9f, 0.06f, 0.9f), brass, false);
            SandboxSack = MemoryBox("SBSack", "記憶中的一袋米", SB(13.3f, 0.33f, 6.9f), new Vector3(0.6f, 0.55f, 0.5f), memCloth);
            var door = Box("SBDoor", SB(18.1f, 1.2f, 1.6f), new Vector3(0.14f, 2.4f, 1.6f), doorM, true);
            SandboxDoor = door;
            SandboxDoorClosed = door.position;
            Box("SBExitFloor", SB(19.2f, -0.1f, 1.6f), new Vector3(2.0f, 0.2f, 2.2f), floor);
            Box("SBExitWallN", SB(19.2f, H / 2f, 2.75f), new Vector3(2.2f, H, 0.2f), wallM);
            Box("SBExitWallS", SB(19.2f, H / 2f, 0.45f), new Vector3(2.2f, H, 0.2f), wallM);
            Box("SBExitWallE", SB(20.3f, H / 2f, 1.6f), new Vector3(0.2f, H, 2.4f), wallM);

            // 記憶的位置有灰塵在光裡飄（告訴玩家「這裡有東西」）。
            Dust("SBDustStairs", SB(5.2f, 0.9f, 5.6f), new Vector3(0.8f, 0.8f, 0.8f));
            Dust("SBDustGap", SB(9f, 0.4f, 3.9f), new Vector3(1.6f, 0.6f, 1.6f));
            Dust("SBDustSack", SB(13.3f, 0.6f, 6.9f), new Vector3(0.6f, 0.6f, 0.6f));

            // 牆上的說明（暫定文字）。
            var paper = Mat("SB_Paper", Mode.AlwaysReal, new Color(0.93f, 0.9f, 0.82f), new Color(0.93f, 0.9f, 0.82f), null, null, 0.2f, 0.2f, 0f);
            // 說明貼在高台正面（一進來就看得到）；三個目標的牌子貼在北牆。
            var sign = Box("SBRules", SB(1.8f, 0.52f, 5.98f), new Vector3(1.3f, 0.8f, 0.02f), paper, false);
            WallText(sign, "能力原型\n\n照（F 提燈）：光裡的東西是實的\n定（Q）：固定一個東西 25 秒\n牽（E）：抓起、放下、丟（左鍵）",
                Quaternion.identity, new Vector3(0f, 0f, -0.015f), 0.0072f, new Color(0.2f, 0.14f, 0.1f));
            foreach (var (x, text) in new[] { (3f, "一、到高處"), (9f, "二、過缺口"), (15f, "三、打開封住的門") })
            {
                var s = Box("SBGoalSign", SB(x, 2.5f, 7.98f), new Vector3(1.3f, 0.34f, 0.02f), paper, false);
                WallText(s, text, Quaternion.identity, new Vector3(0f, 0f, -0.015f), 0.012f, new Color(0.2f, 0.14f, 0.1f));
            }

            SandboxBounds = new Bounds(SB(10f, 1.5f, 4f), new Vector3(21f, 6f, 9f));
            Mark("Sandbox", SB(1.0f, 0.05f, 1.0f), SB(3f, 1.1f, 6f));
        }
    }
}

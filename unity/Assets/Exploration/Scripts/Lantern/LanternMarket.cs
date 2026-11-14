using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;
using Mode = Shepherd.Exploration.Lantern.LanternReveal.Mode;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 記憶空間 A｜家鄉的老街市場 灰盒 v0.1（docs/level/CASE01_SPACE_STRUCTURE_V1.md 第 8 節）。
    /// 從新房子側院後牆的門進來；母親小時候和自己的母親擺攤的地方。下午、褪色，攤位在光裡才有顏色、貨只在記憶中。
    /// 一、越過倒下的攤子；二、上到騎樓；三、過天橋；四、找到家裡的攤位（拿到信物：捲尺）。
    /// </summary>
    public sealed partial class LanternGraybox
    {
        public static readonly Vector3 MarketOrigin = new Vector3(-130f, 0f, 60f);
        public Transform MarketRoot { get; private set; }
        public Bounds MarketBounds { get; private set; }
        public float MarketBarrierZ { get; private set; }
        public Bounds MarketBalcony { get; private set; }
        public float MarketBridgeGapX0 { get; private set; }
        public float MarketBridgeGapX1 { get; private set; }
        public Bounds MarketTerrace { get; private set; }
        public Transform MarketShutter { get; private set; }
        public Vector3 MarketShutterClosed { get; private set; }
        public Vector3 MarketPlate { get; private set; }
        public LanternMemory MarketSack { get; private set; }
        public LanternUsable MarketKeepsake { get; private set; }
        public LanternUsable MarketExit { get; private set; }
        public LanternUsable YardDoor { get; private set; }

        private Vector3 MK(float x, float y, float z) => MarketOrigin + new Vector3(x, y, z);

        public bool InMarket(Vector3 p) => MarketRoot != null && MarketBounds.Contains(p + Vector3.up * 0.5f);
        public bool InAbilitySpace(Vector3 p) => InSandbox(p) || InMarket(p);

        public void BuildMarket()
        {
            if (MarketRoot != null)
            {
                var old = MarketRoot;
                Memories.RemoveAll(m => m == null || m.transform.IsChildOf(old));
                Tetherables.RemoveAll(t => t == null || t.transform.IsChildOf(old));
                Usables.RemoveAll(u => u == null || u.transform.IsChildOf(old));
                ArenaArt.Kill(old.gameObject);
            }
            MarketRoot = new GameObject("MemoryMarket").transform;
            MarketRoot.SetParent(Root, false);
            var saved = into;
            into = MarketRoot;
            try { MarketGeometry(); }
            finally { into = saved; }
            Physics.SyncTransforms();
        }

        private LanternTetherable MarketTether(string name, string label, Vector3 pos, Vector3 size, float mass, Material m)
        {
            var t = Tether(name, label, pos, size, mass);
            Box(name + "Mesh", pos + Vector3.up * size.y * 0.5f, size, m, false, null, t.transform);
            return t;
        }

        private void MarketGeometry()
        {
            Material D(string n, Color c, float sm = 0.3f) => Mat(n, Mode.Dual, c, new Color(0.5f, 0.5f, 0.5f), null, null, sm, 0.3f, 0.004f, true);
            Material R(string n, Color c) => Mat(n, Mode.RealOnly, c, c, null, null, 0.35f, 0.35f, 0.006f);
            var ground = D("MK_Ground", new Color(0.62f, 0.58f, 0.52f));
            var plaster = D("MK_Plaster", new Color(0.9f, 0.84f, 0.72f));
            var plaster2 = D("MK_Plaster2", new Color(0.82f, 0.72f, 0.6f));
            var wood = D("MK_Wood", new Color(0.5f, 0.32f, 0.18f));
            var awnRed = D("MK_AwningRed", new Color(0.78f, 0.2f, 0.14f));
            var awnYel = D("MK_AwningYellow", new Color(0.92f, 0.74f, 0.25f));
            var awnGreen = D("MK_AwningGreen", new Color(0.2f, 0.46f, 0.34f));
            var metal = D("MK_Shutter", new Color(0.55f, 0.58f, 0.6f), 0.5f);
            var brass = D("MK_Plate", new Color(0.8f, 0.62f, 0.28f), 0.6f);
            var cartM = D("MK_Cart", new Color(0.36f, 0.5f, 0.62f));
            var crateM = D("MK_Crate", new Color(0.7f, 0.52f, 0.3f));
            var memWood = R("MK_MemWood", new Color(0.86f, 0.58f, 0.3f));
            var memAwning = R("MK_MemAwning", new Color(0.9f, 0.3f, 0.18f));
            var memBasket = R("MK_MemBasket", new Color(0.82f, 0.66f, 0.38f));
            var memSack = R("MK_MemSack", new Color(0.88f, 0.8f, 0.58f));
            var goods = new[] { R("MK_GoodsRed", new Color(0.9f, 0.2f, 0.15f)), R("MK_GoodsGreen", new Color(0.3f, 0.65f, 0.3f)), R("MK_GoodsYellow", new Color(0.95f, 0.8f, 0.3f)), R("MK_GoodsCloth", new Color(0.35f, 0.45f, 0.75f)) };
            var lanternRed = Mat("MK_Lantern", Mode.Dual, new Color(0.86f, 0.12f, 0.08f), new Color(0.5f, 0.5f, 0.5f), null, null, 0.3f, 0.3f, 0f);
            var paper = Mat("MK_Paper", Mode.AlwaysReal, new Color(0.93f, 0.9f, 0.82f), new Color(0.93f, 0.9f, 0.82f), null, null, 0.2f, 0.2f, 0f);
            float H = 6f;

            // 街與兩側的樓（西：一樓攤位；東：一樓店面＋2 m 高的騎樓）。南端是進來的門，北端是牆。
            Box("MKStreet", MK(5f, -0.1f, 15f), new Vector3(12.4f, 0.2f, 32.4f), ground);
            Box("MKWallW", MK(-1.1f, H / 2f, 15f), new Vector3(0.2f, H, 32.4f), plaster);
            Box("MKWallE", MK(11.1f, H / 2f, 15f), new Vector3(0.2f, H, 32.4f), plaster2);
            Box("MKWallS", MK(5f, H / 2f, -1.1f), new Vector3(12.4f, H, 0.2f), plaster);
            Box("MKWallN", MK(5f, H / 2f, 31.1f), new Vector3(12.4f, H, 0.2f), plaster2);
            Box("MKEntryFrame", MK(5f, 1.2f, -0.98f), new Vector3(1.4f, 2.4f, 0.05f), wood, false);

            // 一、倒下的攤子：1 m 高的一堆，橫過整條街。南邊：記憶中疊好的竹籃（照）、推車與小木箱（牽）。北邊有固定的兩階（掉回來不會卡死）。
            MarketBarrierZ = MarketOrigin.z + 7f;
            Box("MKRubble", MK(4.9f, 0.5f, 7f), new Vector3(12f, 1.0f, 0.9f), wood);
            Box("MKRubbleBoard", MK(3f, 1.05f, 7.1f), new Vector3(2.4f, 0.1f, 0.4f), awnRed, false, new Vector3(0f, 12f, 6f));
            MemoryBox("MKBasket1", "記憶中疊好的竹籃", MK(2.2f, 0.165f, 6.15f), new Vector3(0.7f, 0.33f, 0.6f), memBasket);
            MemoryBox("MKBasket2", "記憶中疊好的竹籃", MK(2.2f, 0.33f, 6.3f), new Vector3(0.7f, 0.66f, 0.3f), memBasket);
            Box("MKBackStep1", MK(8.6f, 0.165f, 7.85f), new Vector3(0.8f, 0.33f, 0.4f), wood);
            Box("MKBackStep2", MK(8.6f, 0.33f, 7.55f), new Vector3(0.8f, 0.66f, 0.3f), wood);
            MarketTether("MKCart", "推車", MK(5.5f, 0f, 3.2f), new Vector3(0.9f, 0.66f, 0.6f), 4f, cartM);
            MarketTether("MKCrate", "小木箱", MK(6.8f, 0f, 2.2f), new Vector3(0.55f, 0.33f, 0.55f), 2f, crateM);
            MarketTether("MKCrate2", "小木箱", MK(1.0f, 0f, 1.6f), new Vector3(0.55f, 0.33f, 0.55f), 2f, crateM);

            // 壓板與鐵捲門（給「上到騎樓」的第二種解法）：壓板在西側矮牆圍住的角落；記憶中上面放著一袋米。
            Box("MKLowWallZ", MK(1.7f, 0.6f, 5.2f), new Vector3(0.15f, 1.2f, 2.6f), plaster);
            Box("MKLowWallX", MK(0.3f, 0.6f, 3.9f), new Vector3(2.8f, 1.2f, 0.15f), plaster);
            Box("MKLowWallX2", MK(0.3f, 0.6f, 6.5f), new Vector3(2.8f, 1.2f, 0.15f), plaster);
            MarketPlate = MK(0.3f, 0.03f, 5.2f);
            Box("MKPlate", MarketPlate, new Vector3(0.9f, 0.06f, 0.9f), brass, false);
            MarketSack = MemoryBox("MKSack", "記憶中的一袋米", MK(0.3f, 0.33f, 5.2f), new Vector3(0.6f, 0.55f, 0.5f), memSack);

            // 東側：騎樓（2 m 高、x 8.5～11、z 10～27）。下面是店面的牆。
            Box("MKBalcony", MK(9.75f, 1.0f, 18.5f), new Vector3(2.5f, 2.0f, 17f), plaster2);
            var bal = new Bounds();
            bal.SetMinMax(MK(8.5f, 1.85f, 10f), MK(11f, 4f, 27f));
            MarketBalcony = bal;
            for (int i = 0; i < 4; i++)
            {
                float z = 12f + i * 4f;
                Box("MKShopfront", MK(8.48f, 1.0f, z), new Vector3(0.04f, 1.8f, 3.2f), i % 2 == 0 ? awnGreen : awnRed, false);
            }
            Box("MKRail", MK(8.55f, 2.45f, 21.5f), new Vector3(0.08f, 0.9f, 7f), wood);
            // 記憶中的遮雨棚：一道 38° 的斜坡，從街面接到騎樓（光裡走得上去）。
            MemoryBox("MKAwningRamp", "記憶中的遮雨棚", MK(7.25f, 1.0f, 12.6f), new Vector3(3.2f, 0.08f, 1.5f), memAwning, false, new Vector3(0f, 0f, 38.7f));
            // 鐵捲門後的真樓梯（z 7.9～10 往北升到騎樓）。
            for (int s = 0; s < 6; s++)
                Box("MKStair", MK(9.75f, 0.165f + s * 0.165f, 8.15f + s * 0.33f), new Vector3(2.5f, 0.33f * (s + 1), 0.33f), wood);
            var shutter = Box("MKShutter", MK(9.75f, 1.25f, 7.75f), new Vector3(2.5f, 2.5f, 0.08f), metal, true);
            MarketShutter = shutter;
            MarketShutterClosed = shutter.position;

            // 三、天橋（y 2.0，z 26～27.4，從騎樓往西到天台）：中間斷了 3 m。
            Box("MKBridgeE", MK(7.5f, 1.9f, 26.7f), new Vector3(2.0f, 0.2f, 1.4f), wood);
            Box("MKBridgeW", MK(2.5f, 1.9f, 26.7f), new Vector3(2.0f, 0.2f, 1.4f), wood);
            MarketBridgeGapX0 = MarketOrigin.x + 3.5f;
            MarketBridgeGapX1 = MarketOrigin.x + 6.5f;
            MemoryBox("MKBridgeMem", "記憶中的橋面", MK(5f, 1.94f, 26.7f), new Vector3(3.3f, 0.12f, 1.2f), memWood);
            // 北邊另一條「記憶有偏差」的走道：看起來比較寬、比較近，但時有時無、定不住。
            Box("MKWalkE", MK(9.75f, 1.0f, 29f), new Vector3(2.5f, 2.0f, 4f), plaster2);
            MemoryBox("MKWalkBiased", "記憶中的走道（晃動）", MK(4.75f, 1.94f, 29.2f), new Vector3(7.0f, 0.12f, 1.6f), memWood, true);
            MarketTether("MKLongBoard", "長木板", MK(9.7f, 2.0f, 23.5f), new Vector3(0.5f, 0.06f, 3.6f), 3f, wood);
            for (int i = 0; i < 2; i++) Box("MKBridgeRail", MK(i == 0 ? 7.5f : 2.5f, 2.45f, 27.35f), new Vector3(2.0f, 0.9f, 0.06f), wood);

            // 四、西側天台（y 2.0，x -1～1.5，z 22～31）：母親家的攤位只在光裡；捲尺是信物；牆上的老照片。
            Box("MKTerrace", MK(0.25f, 1.0f, 26.5f), new Vector3(2.5f, 2.0f, 9f), plaster);
            var ter = new Bounds();
            ter.SetMinMax(MK(-1f, 1.85f, 22f), MK(1.5f, 4f, 31f));
            MarketTerrace = ter;
            MemoryBox("MKFamilyStall", "記憶中家裡的攤子", MK(0.1f, 2.45f, 29.6f), new Vector3(1.6f, 0.9f, 0.8f), memWood);
            MemoryBox("MKFamilyAwning", "記憶中家裡的遮雨棚", MK(0.1f, 3.6f, 29.4f), new Vector3(1.9f, 0.06f, 1.3f), memAwning, false, new Vector3(-12f, 0f, 0f));
            var signBoard = Box("MKFamilySign", MK(0.1f, 3.1f, 30.95f), new Vector3(1.2f, 0.4f, 0.04f), paper, false);
            WallText(signBoard, "林記布疋", Quaternion.identity, new Vector3(0f, 0f, -0.03f), 0.02f, new Color(0.55f, 0.08f, 0.06f));
            var tape = Box("MKTapeMeasure", MK(0.1f, 2.95f, 29.5f), new Vector3(0.16f, 0.06f, 0.16f), awnYel, true);
            MarketKeepsake = tape.gameObject.AddComponent<LanternUsable>();
            MarketKeepsake.Label = "捲尺";
            MarketKeepsake.PromptFn = () => "E 拿起一卷舊捲尺";
            Usables.Add(MarketKeepsake);
            var photo = Box("MKOldPhoto", MK(-0.98f, 2.9f, 27.5f), new Vector3(0.03f, 0.5f, 0.7f), paper, true);
            AddInspect(photo.gameObject, LanternInspectable.Kind.Pages, "攤位前的老照片", null, null, new[]
            {
                "（暫定）黑白照片：一個小女孩站在布攤前，旁邊是她的母親。\n\n攤位的招牌寫著「陳記布疋」。",
            }, "");
            // 出口：天台牆上的門，回到房子的側院。
            var exit = Box("MKExitDoor", MK(-0.98f, 3.1f, 24f), new Vector3(0.06f, 2.0f, 1.0f), wood, true);
            MarketExit = exit.gameObject.AddComponent<LanternUsable>();
            MarketExit.Label = "門";
            MarketExit.PromptFn = () => "E 推開門，回到房子";
            Usables.Add(MarketExit);

            // 西側一樓的攤位（灰盒）：桌子、遮雨棚；貨只在記憶中（光裡才看得到）。
            var awnings = new[] { awnRed, awnYel, awnGreen };
            for (int i = 0; i < 4; i++)
            {
                float z = 10.5f + i * 2.6f;
                Box("MKStallTable", MK(0.2f, 0.4f, z), new Vector3(1.4f, 0.8f, 2.0f), wood);
                Box("MKStallAwning", MK(0.6f, 2.3f, z), new Vector3(2.2f, 0.06f, 2.2f), awnings[i % 3], false, new Vector3(0f, 0f, -14f));
                for (int g = 0; g < 3; g++)
                    MemoryBox("MKGoods", "記憶中的貨", MK(0.2f, 0.92f, z - 0.6f + g * 0.6f), new Vector3(0.5f, 0.24f, 0.45f), goods[(i + g) % goods.Length]);
            }
            // 燈籠串（兩排）。
            foreach (float z in new[] { 14f, 20f })
                for (int i = 0; i < 6; i++)
                    Ball("MKLantern", MK(0.8f + i * 1.7f, 4.6f - 0.3f * Mathf.Sin(Mathf.PI * i / 5f), z), new Vector3(0.32f, 0.38f, 0.32f), lanternRed);
            // 灰塵：告訴玩家「這裡有記憶」。
            Dust("MKDustBaskets", MK(2.2f, 0.6f, 6.2f), new Vector3(0.8f, 0.6f, 0.8f));
            Dust("MKDustRamp", MK(7.25f, 1.1f, 12.6f), new Vector3(2.4f, 1.8f, 1.4f));
            Dust("MKDustSack", MK(0.3f, 0.6f, 5.2f), new Vector3(0.6f, 0.6f, 0.6f));
            Dust("MKDustBridge", MK(5f, 2.2f, 26.7f), new Vector3(3f, 0.6f, 1.2f));
            Dust("MKDustStall", MK(0.1f, 2.6f, 29.6f), new Vector3(1.6f, 1f, 1f));

            MarketBounds = new Bounds(MK(5f, 2.5f, 15f), new Vector3(13f, 8f, 33f));
            Mark("Market", MK(5f, 0.05f, 0.6f), MK(5f, 1.2f, 8f));
        }

        /// <summary>新房子側院後牆的一扇門（通往市場的記憶）。在 BuildHouseV2 之後呼叫。</summary>
        private void YardDoorToMarket()
        {
            var saved = into; var savedOrigin = hpOrigin; var savedScale = HouseScale;
            into = V2Root; hpOrigin = V2Origin; HouseScale = V2Scale;
            try
            {
                var doorM = Mat("V2_YardDoor", Mode.Dual, new Color(0.45f, 0.3f, 0.18f), new Color(0.5f, 0.5f, 0.5f), null, null, 0.3f, 0.3f, 0f);
                var d = Box("V2YardBackDoor", HP(5.25f, 0f, V2Depth) + new Vector3(0f, 1.0f, -0.14f), new Vector3(0.9f, 2.0f, 0.06f), doorM, true);
                YardDoor = d.gameObject.AddComponent<LanternUsable>();
                YardDoor.Label = "後門";
                YardDoor.PromptFn = () => "E 推開側院的後門";
                Usables.Add(YardDoor);
                Dust("V2YardDoorDust", d.position + Vector3.back * 0.4f, new Vector3(0.9f, 1.6f, 0.4f));
            }
            finally { into = saved; hpOrigin = savedOrigin; HouseScale = savedScale; }
        }
    }
}

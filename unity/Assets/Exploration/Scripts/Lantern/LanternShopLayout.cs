using System.Collections.Generic;
using UnityEngine;
using Mode = Shepherd.Exploration.Lantern.LanternReveal.Mode;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 服飾店（家庭改衣店）布局：2026-11-16 使用者試玩後採用「A｜生活機能優先」為正式布局（B 已移除）。
    /// 程式仍保留原本的擺法（layout 0）以便對照與回復，沒有切換入口；正式遊戲建好房子後套用 A（LanternPrototypeQA.UseConfirmedLayouts）。
    /// 牆上的謎題與線索（S1 電箱、S2 外套與吊牌、衣架後的照片、影子殘影的牆）不動；S4 縫紉機在窗下（自然光）。
    /// 只移動家具、開關各套專屬的物件；不重建房子。平面座標：x＝左牆 0～右牆 6.5，z＝前牆 0～店後牆 5（世界尺寸×1.3）。
    /// S3 抽屜訂單簿目前正式遊戲還沒有實作（只在文件裡）；A、B 都在櫃台的店主側預留位置，不放物件、不加劇情。
    /// </summary>
    public sealed partial class LanternGraybox
    {
        public static readonly string[] ShopLayoutNames = { "原本的擺法", "A｜生活機能優先（正式）" };
        /// <summary>0＝原本的擺法、1＝A（正式）。</summary>
        public int ShopLayout { get; private set; }

        private struct ShopPose { public Vector3 Pos; public Quaternion Rot; }
        private sealed class ShopMove { public Transform T; public ShopPose[] Poses = new ShopPose[2]; }
        private readonly List<ShopMove> shopMoves = new List<ShopMove>();
        private readonly List<GameObject>[] shopOnly = { new List<GameObject>(), new List<GameObject>() };
        private bool shopRegistered;
        private Transform shopRoot;   // 建店面時的父物件（之後 Into 會換成別的房子）

        /// <summary>切換服飾店布局。只移動、顯示或隱藏物件。</summary>
        public void SetShopLayout(int layout)
        {
            RegisterShopLayouts();
            ShopLayout = Mathf.Clamp(layout, 0, 1);
            foreach (var m in shopMoves)
            {
                if (m.T == null) continue;
                m.T.SetPositionAndRotation(m.Poses[ShopLayout].Pos, m.Poses[ShopLayout].Rot);
            }
            for (int i = 0; i < 2; i++)
                foreach (var g in shopOnly[i]) if (g != null) g.SetActive(i == ShopLayout);
            Physics.SyncTransforms();
        }

        /// <summary>A／B 專屬的物件（在 V2Shop 最後建立，預設隱藏）。</summary>
        private void BuildShopLayoutPieces()
        {
            for (int i = 0; i < 2; i++) shopOnly[i].Clear();
            shopMoves.Clear();
            shopRegistered = false;
            ShopLayout = 0;
            shopRoot = Into;
            float f = FurnitureScale;
            var curtain = Mat("V2_ShopCurtain", Mode.Dual, new Color(0.62f, 0.28f, 0.24f), new Color(0.45f, 0.45f, 0.47f), null, null, 0.2f, 0.3f, 0.006f);
            var tableTop = Mat("V2_CutTable", Mode.Dual, new Color(0.72f, 0.6f, 0.44f), new Color(0.5f, 0.5f, 0.5f), null, null, 0.35f, 0.3f, 0.004f);

            // ---------------- A：生活機能優先 ----------------
            // 進門左手邊就是接待櫃台（櫃台本身由 shopMoves 移過來）；櫃台後是店主的工作區：一張裁剪桌（量布、畫線、燙平）。
            var cutA = new GameObject("ShopA_CutTable").transform; cutA.SetParent(Into, false);
            Fur("ShopA_CutTableTop", 2.3f, 2.85f, 0.86f, 1.6f, 0.05f, 0.85f, tableTop, true).SetParent(cutA, true);
            foreach (var (dx, dz) in new[] { (-0.58f, -0.28f), (0.58f, -0.28f), (-0.58f, 0.28f), (0.58f, 0.28f) })
                Fur("ShopA_CutTableLeg", 2.3f + dx, 2.85f + dz, 0f, 0.06f, 0.86f, 0.06f, hWood, false).SetParent(cutA, true);
            Fur("ShopA_CutTableShelf", 2.3f, 2.85f, 0.18f, 1.5f, 0.03f, 0.75f, hWood, false).SetParent(cutA, true);   // 下層放布捲
            Fur("ShopA_FabricRoll", 2.0f, 2.85f, 0.22f, 0.9f, 0.12f, 0.12f, curtain, false, 90f).SetParent(cutA, true);
            shopOnly[1].Add(cutA.gameObject);
            // 試衣間：右後角（原本布料架的位置，布料架移到後牆）；簾子半開，裡面一張小凳。
            shopOnly[1].Add(FittingBooth("ShopA_Fitting", 5.62f, 6.5f, 3.42f, 4.38f, curtain).gameObject);
        }

        /// <summary>試衣間：靠牆的轉角，兩根柱子＋頂上的簾桿＋側板＋半開的布簾（布簾和側板有碰撞，人進不去）。</summary>
        private void SetShopLayoutPiecesHidden()
        {
            foreach (var g in shopOnly[1]) if (g != null) g.SetActive(false);
        }

        private Transform FittingBooth(string name, float x0, float x1, float z0, float z1, Material curtain)
        {
            var root = new GameObject(name).transform;
            root.SetParent(Into, false);
            float h = 2.05f;
            // 側板（靠前／靠後那一面，木框＋布）
            Fur(name + "_Side", (x0 + x1) / 2f, z0 + 0.02f, 0f, (x1 - x0) * V2Scale / FurnitureScale, h, 0.04f, hWood, true).SetParent(root, true);
            Fur(name + "_Back", (x0 + x1) / 2f, z1 - 0.02f, 0f, (x1 - x0) * V2Scale / FurnitureScale, h, 0.04f, hWood, true).SetParent(root, true);
            // 柱子與簾桿
            Fur(name + "_Post", x0 + 0.02f, z0 + 0.02f, 0f, 0.05f, h, 0.05f, hWood, false).SetParent(root, true);
            Fur(name + "_Post", x0 + 0.02f, z1 - 0.02f, 0f, 0.05f, h, 0.05f, hWood, false).SetParent(root, true);
            Fur(name + "_Rod", x0 + 0.02f, (z0 + z1) / 2f, h - 0.04f, 0.04f, 0.04f, (z1 - z0) * V2Scale / FurnitureScale, hTrim, false).SetParent(root, true);
            // 布簾：拉上三分之二，留一個縫看得到裡面的凳子和鏡子一角
            float zc = z0 + (z1 - z0) * 0.36f;
            Fur(name + "_Curtain", x0 + 0.03f, zc, 0.12f, 0.03f, h - 0.2f, (z1 - z0) * 0.68f * V2Scale / FurnitureScale, curtain, true).SetParent(root, true);
            Fur(name + "_Stool", (x0 + x1) / 2f + 0.1f, z1 - 0.3f, 0f, 0.35f, 0.45f, 0.35f, hWood, false).SetParent(root, true);
            Fur(name + "_Hook", x1 - 0.06f, (z0 + z1) / 2f, 1.6f, 0.06f, 0.06f, 0.3f, hTrim, false).SetParent(root, true);
            return root;
        }

        /// <summary>第一次切換時登記每件會移動的家具在三套布局的位置（櫃台燈在 V2Lights 才建立，所以延後登記）。</summary>
        private void RegisterShopLayouts()
        {
            if (shopRegistered) return;
            shopRegistered = true;
            Transform Find(string n)
            {
                var root = shopRoot != null ? shopRoot : Into;
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == n && t.parent == root) return t;
                return null;
            }
            // 移動一件家具：給 A、B 兩套的「外框中心」與朝向；followers（櫃台上的東西、櫃台燈）跟著做同樣的剛體移動。
            void Place(string name, Vector2? a, float yawA, params string[] followers)
            {
                var t = Find(name);
                if (t == null) return;
                var rs = t.GetComponentsInChildren<Renderer>(true);
                var bc = t.position;
                if (rs.Length > 0) { var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds); bc = bb.center; }
                var fol = new List<Transform> { t };
                foreach (var fn in followers) { var ft = Find(fn); if (ft != null) fol.Add(ft); }
                foreach (var ft in fol)
                {
                    var m = new ShopMove { T = ft };
                    m.Poses[0] = new ShopPose { Pos = ft.position, Rot = ft.rotation };
                    if (a == null) m.Poses[1] = m.Poses[0];
                    else
                    {
                        var q = Quaternion.Euler(0f, yawA, 0f) * Quaternion.Inverse(t.rotation);
                        var c = HPv2(a.Value.x, a.Value.y);
                        c.y = bc.y;
                        m.Poses[1] = new ShopPose { Pos = c + q * (ft.position - bc), Rot = q * ft.rotation };
                    }
                    shopMoves.Add(m);
                }
            }
            // 櫃台：進門左手邊、面向通道（接待／收件）。
            Place("V2Counter", new Vector2(3.95f, 1.9f), 90f,
                "V2Register", "V2CounterClutter", "V2CounterLampBase", "V2CounterLampStem", "V2CounterBulb", "V2CounterLight");
            // 布料架：移到後牆（電箱右邊），右後角讓給試衣間。
            // 2026-11-17：再往右挪一點，電箱前空出約 0.77 m，紙箱、凳子塞得進去。
            Place("V2FabricShelf", new Vector2(2.95f, 4.72f), 180f);
            // 紙箱：移到鈕扣抽屜櫃旁邊（電箱下方留出站的位置）。
            Place("V2Boxes", new Vector2(1.28f, 4.72f), 180f);   // 擺正（斜著放的外框會比箱子大），也往左一點
            // 鏡子：留在試衣間前（不動）。
        }

        /// <summary>V2 房子的平面座標 → 世界座標（地面）。</summary>
        private Vector3 HPv2(float x, float z) => V2Origin + new Vector3(x * V2Scale, 0f, z * V2Scale);
    }
}

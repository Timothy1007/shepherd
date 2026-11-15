using System;
using System.Collections.Generic;
using UnityEngine;
using Mode = Shepherd.Exploration.Lantern.LanternReveal.Mode;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 餐廳「空間層次與探索構圖」QA 的 B 改造版（docs/level/CASE01_DINING_SPATIAL_DEPTH_QA_PLAN.md）。
    /// 正式遊戲一律是 A（現況）；只有 QA 場景會切到 B。B 只是把既有物件換位置／朝向，加上少量有建築或生活理由的遮擋：
    ///   1. 矮書櫃轉 90°、從南牆伸出來：隔出看電視的角落，書朝向扶手椅那一側（坐著伸手就拿得到）；
    ///      從走廊門進來時，它在左前方擋住一部分起居角（前景遮擋），走過去才看見電視與扶手椅。
    ///   2. 扶手椅和小茶几往角落裡收、斜對電視：一個人看電視、看報紙的位置，不再和牆平行。
    ///   3. 電視上方的兩張家庭照移到壁爐旁的壁龕牆（暖氣片上方）：壁爐、壁爐架上的相框和掛鐘成為「家庭記憶」的次焦點。
    ///      電視上方原本那張（劇情物件）不動。
    ///   4. 打通的開口掛一道門簾（橫樑下的銅桿、兩片綁在短牆邊的布簾）：英國舊屋常用來擋風保暖；
    ///      它框住從起居區看向用餐區的畫面，走動時和後面的餐桌、壁爐產生前後視差。
    ///   5. （可另外開關）光照引導：壁爐架上方一盞小射燈、廚房門透出一點偏冷的光——讓視線從餐桌被帶到壁爐和廚房方向。
    /// 不動：門洞、橫樑、短牆、壁柱與壁爐、餐桌與椅子（含被推開的那張）、碗櫃、月曆、桌上那頓飯、身高線、提燈。
    /// </summary>
    public sealed partial class LanternGraybox
    {
        private struct LayoutMove { public Transform T; public Vector3 PA, PB; public Quaternion RA, RB; }
        private readonly List<LayoutMove> layoutMoves = new List<LayoutMove>();
        private readonly List<GameObject> layoutOnlyB = new List<GameObject>();
        private readonly List<Light> focusLights = new List<Light>();

        /// <summary>目前是不是 B 改造版。</summary>
        public bool DiningLayoutIsB { get; private set; }
        /// <summary>B 的光照引導是否打開（A 時永遠不亮）。</summary>
        public bool DiningFocusLightsOn { get; private set; }

        /// <summary>切換 A（現況）／B（構圖改造）。只移動物件與開關 B 專屬的物件，不重建房子。</summary>
        public void SetDiningLayout(bool b)
        {
            DiningLayoutIsB = b;
            foreach (var m in layoutMoves)
            {
                if (m.T == null) continue;
                m.T.position = b ? m.PB : m.PA;
                m.T.rotation = b ? m.RB : m.RA;
            }
            foreach (var g in layoutOnlyB) if (g != null) g.SetActive(b);
            foreach (var l in focusLights) if (l != null) l.enabled = b && DiningFocusLightsOn;
            Physics.SyncTransforms();
        }

        /// <summary>B 的光照引導（只有 B 會亮）。</summary>
        public void SetDiningFocusLights(bool on)
        {
            DiningFocusLightsOn = on;
            foreach (var l in focusLights) if (l != null) l.enabled = DiningLayoutIsB && on;
        }

        /// <summary>登記一件物件的 B 位置；followers（接觸陰影、拖鞋、燈）跟著做同樣的剛體移動。</summary>
        private void MoveInB(Transform t, Vector3 posB, float yawB, params Transform[] followers)
        {
            if (t == null) return;
            var ra = t.rotation; var pa = t.position;
            var rb = Quaternion.Euler(0f, yawB, 0f);
            var q = rb * Quaternion.Inverse(ra);
            layoutMoves.Add(new LayoutMove { T = t, PA = pa, RA = ra, PB = posB, RB = rb });
            foreach (var f in followers)
            {
                if (f == null) continue;
                layoutMoves.Add(new LayoutMove { T = f, PA = f.position, RA = f.rotation, PB = posB + q * (f.position - pa), RB = q * f.rotation });
            }
        }

        private void DiningLayoutB(Func<float, float> Xw, Func<float, float> Ys, float H, float zc, float bx, float zb0, float zb1, float nibW, float nibE,
            Transform books, Transform booksBlob, Transform arm, Transform armBlob, Transform slippers, Transform side, Transform sideBlob,
            List<(Transform f, Transform p)> famFrames, Material plaster, Material wood, Material brass)
        {
            layoutMoves.Clear(); layoutOnlyB.Clear(); focusLights.Clear();
            DiningLayoutIsB = false;

            // 1. 矮書櫃：背板貼著 X 3.92，從南牆伸出約 1.1 m，書朝西（扶手椅那側）。
            MoveInB(books, new Vector3(Xw(3.92f), 0f, Ys(0.6f)), -90f, booksBlob);
            // 2. 扶手椅斜對電視、小茶几在它和電視之間的西側；茶几上的檯燈光跟著茶几。
            Transform sideLamp = null;
            foreach (var t in Into.GetComponentsInChildren<Transform>(true)) if (t.name == "DN_SideLampLight") { sideLamp = t; break; }
            MoveInB(arm, new Vector3(Xw(1.95f), 0f, Ys(1.7f)), 200f, armBlob, slippers);
            MoveInB(side, new Vector3(Xw(1.12f), 0f, Ys(1.22f)), 20f, sideBlob, sideLamp);
            // 3. 兩張家庭照：移到壁爐南側壁龕的牆上（暖氣片上方），一高一低。
            var wallX = Xw(0f);
            var spots = new[] { (Ys(3.12f), 1.56f), (Ys(3.6f), 1.74f) };
            for (int i = 0; i < famFrames.Count && i < spots.Length; i++)
            {
                var (f, p) = famFrames[i];
                var rot = Quaternion.Euler(0f, 90f, 0f);
                layoutMoves.Add(new LayoutMove { T = f, PA = f.position, RA = f.rotation, PB = new Vector3(wallX + 0.012f, spots[i].Item2, spots[i].Item1), RB = rot });
                layoutMoves.Add(new LayoutMove { T = p, PA = p.position, RA = p.rotation, PB = new Vector3(wallX + 0.024f, spots[i].Item2, spots[i].Item1), RB = rot });
            }

            // 4. 門簾：橫樑下一根銅桿，兩片布簾各綁在短牆邊。
            var cloth = PH("DN_PortiereCloth", "quatrefoil_jacquard_fabric", "Tex", 0.55f, new Color(0.46f, 0.52f, 0.4f), new Color(0.5f, 0.5f, 0.5f), 0.1f);
            var group = new GameObject("DN_LayoutB_Portiere").transform;
            group.SetParent(Into, false);
            float zMid = (zb0 + zb1) / 2f;
            float x0 = Xw(nibW), x1 = Xw(5.795f - nibE);
            float rodY = H - 0.32f;
            Cyl("DN_PortiereRod", new Vector3((x0 + x1) / 2f, rodY, zMid), new Vector3(0.03f, (x1 - x0) / 2f + 0.04f, 0.03f), brass, new Vector3(0f, 0f, 90f), group);
            foreach (var x in new[] { x0 + 0.02f, x1 - 0.02f })
                Box("DN_PortiereBracket", new Vector3(x, rodY + 0.08f, zMid), new Vector3(0.025f, 0.16f, 0.025f), brass, false, null, group);
            void Strand(Vector3 a, Vector3 b, float r)
            {
                var d = b - a;
                Cyl("DN_PortiereFold", (a + b) / 2f, new Vector3(r * 2f, d.magnitude / 2f, r * 2f), cloth, Quaternion.FromToRotation(Vector3.up, d.normalized).eulerAngles, group);
            }
            foreach (var side01 in new[] { 0, 1 })
            {
                float s = side01 == 0 ? 1f : -1f;              // 西片往東展開、東片往西展開
                float edge = side01 == 0 ? x0 + 0.03f : x1 - 0.03f;
                const int N = 6;
                for (int i = 0; i < N; i++)
                {
                    float t = i / (float)(N - 1);
                    float dz = (i % 2 == 0 ? 1f : -1f) * 0.035f;
                    var top = new Vector3(edge + s * t * 0.5f, rodY - 0.03f, zMid + dz);
                    var tie = new Vector3(edge + s * (0.02f + t * 0.07f), 1.05f, zMid + dz * 0.6f);
                    var bottom = new Vector3(edge + s * (0.01f + t * 0.34f), 0.03f, zMid + dz * 1.2f);
                    Strand(top, tie, 0.065f);
                    Strand(tie, bottom, 0.07f);
                }
                Box("DN_PortiereTieback", new Vector3(edge + s * 0.06f, 1.05f, zMid), new Vector3(0.16f, 0.05f, 0.2f), brass, false, null, group);
                // 布簾下半部的碰撞（細長的隱形盒子；布不會被穿過去）。
                var col = Box("DN_PortiereCollider", new Vector3(edge + s * 0.17f, 0.5f, zMid), new Vector3(0.34f, 1.0f, 0.2f), cloth, true, null, group);
                col.GetComponent<Renderer>().enabled = false;
            }
            layoutOnlyB.Add(group.gameObject);

            // 5. 光照引導（B 專屬、可另外開關）。
            // 射燈對準壁爐架和上面的相框（不是鏡子：打在鏡面上會變成一片白）。
            var mantelTarget = new Vector3(bx, 1.05f, zc);
            var from = new Vector3(bx + 1.4f, H - 0.15f, zc);
            var mantelSpot = AddLight("DN_Focus_MantelSpot", LightType.Spot, from, new Color(1f, 0.8f, 0.6f), 1.9f, 4.8f, false, LightShadows.None, 34f, (mantelTarget - from).normalized);
            mantelSpot.innerSpotAngle = 18f;
            var kitchenSpill = AddLight("DN_Focus_KitchenSpill", LightType.Point, new Vector3(Xw(2.0f), 1.7f, Ys(6.31f) - 0.35f), new Color(0.74f, 0.84f, 1f), 0.65f, 3.2f, false, LightShadows.None);
            focusLights.Add(mantelSpot); focusLights.Add(kitchenSpill);

            // 預設 A（正式遊戲）。
            SetDiningLayout(false);
        }
    }
}

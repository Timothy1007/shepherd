using System.Collections.Generic;
using UnityEngine;
using Mode = Shepherd.Exploration.Lantern.LanternReveal.Mode;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 廚房 ↔ 側院的通路（廚房東牆的門洞，平面 z 13.4～14.2）。設計總綱 §10（2026-11-16）：
    /// 一開始兩側都是完整的牆（沒有門框、沒有碰撞縫，提燈也照不出來，因為補牆用的是兩個世界都存在的同一種牆面材質）；
    /// 玩家經過走廊→餐廳的成長、進到廚房之後，這處通路恢復，之後一直開著、兩邊都能走。
    /// 走廊→側院的普通後門不受影響，從頭到尾都能走。
    /// </summary>
    public sealed partial class LanternGraybox
    {
        public const float KitchenYardZ0 = 13.4f, KitchenYardZ1 = 14.2f;
        /// <summary>通路是否已經恢復。</summary>
        public bool KitchenYardOpen { get; private set; }
        /// <summary>封住時補上的那段牆（含兩側踢腳板）。</summary>
        public Transform KitchenYardSeal { get; private set; }
        private readonly List<GameObject> kitchenYardFrame = new List<GameObject>();
        /// <summary>通路正在慢慢顯現（補牆還在、碰撞還在，顯現完才拿掉）。</summary>
        public bool KitchenYardRevealing { get; private set; }
        /// <summary>從門洞中央往外顯現的速度（m/s）；約 2 秒顯現完。</summary>
        public const float KitchenYardRevealSpeed = 0.9f;
        private int kitchenYardRoom = -1;
        private float kitchenYardRevealNeed;
        /// <summary>QA 用：顯現進度（目前擴散半徑／需要的半徑、格子）。</summary>
        public string KitchenYardRevealDebug => "slot " + kitchenYardRoom + " spread " + (kitchenYardRoom >= 0 ? LanternReveal.RoomSpread(kitchenYardRoom) : -1f).ToString("0.00") + " need " + kitchenYardRevealNeed.ToString("0.00");
        private readonly List<(Renderer r, Material m)> kitchenYardSwapped = new List<(Renderer, Material)>();

        /// <summary>在 BuildHouseV2 裡（V2 座標生效時）呼叫：建好補牆、找出門洞的門框，預設封住。</summary>
        private void V2KitchenYardPassage()
        {
            if (kitchenYardRoom >= 0) LanternReveal.RemoveRoom(kitchenYardRoom);   // 房子重建：收回上一輪的顯現範圍
            kitchenYardRoom = -1;
            KitchenYardRevealing = false;
            kitchenYardSwapped.Clear();
            kitchenYardFrame.Clear();
            const float doorH = 2.25f;   // 和 V2Shell 的門洞高度一樣
            var a = HP(V2ExtWidth, 0f, KitchenYardZ0);
            var b = HP(V2ExtWidth, 0f, KitchenYardZ1);
            foreach (Transform t in Into)
                if (t.name == "DoorTrim" && Mathf.Abs(t.position.x - a.x) < 0.3f && t.position.z > a.z - 0.3f && t.position.z < b.z + 0.3f)
                    kitchenYardFrame.Add(t.gameObject);
            var root = new GameObject("V2KitchenYardSeal").transform;
            root.SetParent(Into, false);
            var saved = into;
            into = root;
            try
            {
                Box("Wall", HP(V2ExtWidth, doorH / 2f, (KitchenYardZ0 + KitchenYardZ1) / 2f),
                    new Vector3(ExtT, doorH * HouseHeightScale, (KitchenYardZ1 - KitchenYardZ0) * HouseScale), hWallKitchen);
                SkirtZ(V2ExtWidth, KitchenYardZ0, KitchenYardZ1, 0f, ExtT);
            }
            finally { into = saved; }
            KitchenYardSeal = root;
            SetKitchenYardPassage(false);
        }

        /// <summary>
        /// 劇情觸發：通路慢慢顯現，效果和「提燈照到才出現的東西」一樣——
        /// 補牆改成只存在於異域的牆、門框改成只存在於真實的門框，再從門洞中央讓一塊小範圍慢慢恢復真實（邊緣發光）。
        /// 提燈照到的地方也會提早看見。顯現完才拿掉補牆（含碰撞），門框換回一般材質。
        /// </summary>
        public void RevealKitchenYardPassage()
        {
            if (KitchenYardOpen || KitchenYardSeal == null) return;
            KitchenYardOpen = true;
            KitchenYardRevealing = true;
            kitchenYardSwapped.Clear();
            void Swap(Renderer r, Mode mode)
            {
                var m = new Material(r.sharedMaterial) { name = r.sharedMaterial.name + (mode == Mode.AnomalyOnly ? "_AnomalyOnly" : "_RealOnly") };
                m.SetFloat("_Mode", (float)mode);
                kitchenYardSwapped.Add((r, r.sharedMaterial));
                r.sharedMaterial = m;
            }
            foreach (var r in KitchenYardSeal.GetComponentsInChildren<Renderer>()) Swap(r, Mode.AnomalyOnly);
            foreach (var g in kitchenYardFrame)
            {
                if (g == null) continue;
                g.SetActive(true);
                foreach (var r in g.GetComponentsInChildren<Renderer>()) Swap(r, Mode.RealOnly);
            }
            // 顯現的範圍只包住門洞（牆厚＋門框），不會把旁邊的牆也變成真實的樣子。
            // （注意：這裡是遊戲中呼叫，HP() 已經不是 V2 的座標，所以直接用物件的外框。）
            var sealRs = KitchenYardSeal.GetComponentsInChildren<Renderer>();
            var b = sealRs[0].bounds;
            foreach (var r in sealRs) b.Encapsulate(r.bounds);
            foreach (var g in kitchenYardFrame) if (g != null) foreach (var r in g.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            b.Expand(new Vector3(0.12f, 0.1f, 0.06f));
            var seed = new Vector3(b.center.x, b.min.y + 1.2f, b.center.z);
            kitchenYardRevealNeed = 0f;
            foreach (var cx in new[] { b.min.x, b.max.x }) foreach (var cy in new[] { b.min.y, b.max.y }) foreach (var cz in new[] { b.min.z, b.max.z })
                kitchenYardRevealNeed = Mathf.Max(kitchenYardRevealNeed, Vector3.Distance(seed, new Vector3(cx, cy, cz)));
            kitchenYardRevealNeed += 0.7f;   // 遮罩邊緣（與 IsReal 判斷的 0.6 m 一致，再留一點）
            kitchenYardRoom = LanternReveal.AddRoom(b, seed, false, KitchenYardRevealSpeed);
            if (kitchenYardRoom < 0) FinishKitchenYardReveal();   // 沒有空格：直接打開
        }

        /// <summary>每個影格：顯現完了就拿掉補牆與碰撞。</summary>
        public void TickKitchenYardPassage()
        {
            if (KitchenYardRevealing && LanternReveal.RoomSpread(kitchenYardRoom) >= kitchenYardRevealNeed) FinishKitchenYardReveal();
        }

        private void FinishKitchenYardReveal()
        {
            KitchenYardRevealing = false;
            foreach (var (r, m) in kitchenYardSwapped) if (r != null) r.sharedMaterial = m;
            kitchenYardSwapped.Clear();
            if (KitchenYardSeal != null) KitchenYardSeal.gameObject.SetActive(false);
            Physics.SyncTransforms();
        }

        /// <summary>直接開／關廚房↔側院的通路（開＝門洞與門框出現；關＝完整的牆）。測試、截圖、重建用；遊戲裡用 RevealKitchenYardPassage。</summary>
        public void SetKitchenYardPassage(bool open)
        {
            if (KitchenYardRevealing) FinishKitchenYardReveal();
            if (!open && kitchenYardRoom >= 0) { LanternReveal.RemoveRoom(kitchenYardRoom); kitchenYardRoom = -1; }
            KitchenYardOpen = open;
            if (KitchenYardSeal != null) KitchenYardSeal.gameObject.SetActive(!open);
            foreach (var g in kitchenYardFrame) if (g != null) g.SetActive(open);
            Physics.SyncTransforms();
        }

        /// <summary>側院（一樓、不含門洞本身）。</summary>
        public bool InV2Yard(Vector3 feet)
        {
            var d = (feet - V2Origin) / V2Scale;
            return d.x > V2ExtWidth + 0.15f && d.x < V2Width && d.z > V2Main + 0.15f && d.z < V2Depth && feet.y - V2Origin.y < V2Ground * V2Scale;
        }
    }
}

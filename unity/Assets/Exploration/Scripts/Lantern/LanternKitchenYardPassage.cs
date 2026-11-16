using System.Collections.Generic;
using UnityEngine;

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

        /// <summary>在 BuildHouseV2 裡（V2 座標生效時）呼叫：建好補牆、找出門洞的門框，預設封住。</summary>
        private void V2KitchenYardPassage()
        {
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

        /// <summary>開／關廚房↔側院的通路（開＝門洞與門框出現；關＝完整的牆）。</summary>
        public void SetKitchenYardPassage(bool open)
        {
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

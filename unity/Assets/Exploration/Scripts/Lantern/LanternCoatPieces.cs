using System.Linq;
using UnityEngine;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// S2「那件外套」的新玩法（使用者 2026-11-17）：取下外套 → 攤在裁剪桌上 → 提燈照它，外套自己打開 → 近看內側的標籤。
    /// 這裡只建物件：桌上的外套（合起來／打開）、抱在手上的外套、內側標籤（可以近看）、裁剪桌與外套上的互動點。流程在 LanternV2Puzzles。
    /// </summary>
    public sealed partial class LanternGraybox
    {
        public Transform V2CoatOnTable { get; private set; }
        public Transform V2CoatOpened { get; private set; }
        public Transform V2CoatHeld { get; private set; }
        public LanternInspectable V2CoatLabel { get; private set; }
        public LanternUsable V2CoatUse { get; private set; }
        public LanternUsable V2CoatTableUse { get; private set; }
        /// <summary>取外套時手要伸到的高度（世界 y，大約衣領）。</summary>
        public float V2CoatReachY { get; private set; }

        /// <summary>內側標籤上的字（沿用現有劇情：沒有名字，只有一年一年被塗改的尺寸；最後的文字待定）。</summary>
        public static readonly string[] CoatLabelPages =
        {
            "（暫定）標籤上沒有名字。\n\n只有一行一行的尺寸，寫上去，又被劃掉。",
            "（暫定）每一年都比前一年大一點。\n\n最後一行的字跡，和前面的不一樣。",
        };

        private void BuildCoatPieces()
        {
            Transform Child(string n) => V2Root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n && t.parent == V2Root);
            var coat = Child("V2HangingCoat");
            if (coat == null) return;
            V2CoatReachY = coat.position.y + 1.38f * FurnitureScale;
            // 外套本身：E 取下（流程由謎題控制器接上）
            V2CoatUse = coat.gameObject.GetComponent<LanternUsable>() ?? coat.gameObject.AddComponent<LanternUsable>();
            V2CoatUse.Label = "那件外套";
            if (!Usables.Contains(V2CoatUse)) Usables.Add(V2CoatUse);
            // 桌面：裁剪桌（布局 A）；沒有裁剪桌時用櫃台。
            var table = Child("ShopA_CutTable");
            Transform tableModel = table != null && table.gameObject.activeInHierarchy ? table : Child("V2Counter");
            if (tableModel == null) return;
            var rs = tableModel.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var top = new Vector3(b.center.x, b.max.y + 0.002f, b.center.z);
            float yaw = tableModel == table ? 0f : tableModel.eulerAngles.y + 90f;
            V2CoatOnTable = CustomModel("V2CoatOnTable", "v3_coat_flat", top, yaw, false);
            V2CoatOpened = CustomModel("V2CoatOpened", "v3_coat_open", top, yaw, false);
            V2CoatHeld = CustomModel("V2CoatHeld", "v3_coat_flat", top, 0f, false);
            if (V2CoatOnTable != null) V2CoatOnTable.gameObject.SetActive(false);
            if (V2CoatHeld != null) V2CoatHeld.gameObject.SetActive(false);
            if (V2CoatOpened != null)
            {
                var lab = V2CoatOpened.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("CoatInnerLabel"));
                if (lab != null)
                {
                    var col = lab.gameObject.AddComponent<BoxCollider>();
                    col.size = new Vector3(0.14f, 0.03f, 0.1f);   // 近看要瞄得到（標籤很薄，碰撞做厚一點）
                    V2CoatLabel = AddInspect(lab.gameObject, LanternInspectable.Kind.Pages, "外套內側的標籤", null, null, CoatLabelPages, "");
                }
                V2CoatOpened.gameObject.SetActive(false);
            }
            // 桌子：抱著外套時 E 攤在桌上
            var tableHit = tableModel.GetComponentInChildren<Collider>();
            var host = tableHit != null ? tableHit.gameObject : tableModel.gameObject;
            V2CoatTableUse = host.GetComponent<LanternUsable>() ?? host.AddComponent<LanternUsable>();
            V2CoatTableUse.Label = "裁剪桌";
            if (!Usables.Contains(V2CoatTableUse)) Usables.Add(V2CoatTableUse);
        }
    }
}

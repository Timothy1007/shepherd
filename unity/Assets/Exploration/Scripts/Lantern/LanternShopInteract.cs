using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mode = Shepherd.Exploration.Lantern.LanternReveal.Mode;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 可以搬動、站上去的東西（改衣店：紙箱堆最上面那個箱子、縫紉機的凳子）。
    /// 搬動用和其他箱子一樣的「牽引」（E 抓起、E 放下、左鍵丟出；放在任何放得下的地方），站上去由 LanternCarry 處理。
    /// 不屬於任何一套美術（之後褪色版／恢復版是兩套場景，這些互動物件獨立存在，位置記在進度裡）。
    /// </summary>
    public sealed class LanternMovable : MonoBehaviour
    {
        public string Label = "箱子";
        public BoxCollider Box;
        public Vector3 HomePosition;
        public Quaternion HomeRotation;
        public LanternTetherable Tether;
        /// <summary>能不能爬上去站著。凳子不行：只有縫紉時（拿到線、放好提燈之後）才坐上去（使用者 2026-11-17）。</summary>
        public bool Climbable = true;
        /// <summary>正被牽引著。</summary>
        public bool Grabbed => Tether != null && Tether.Held;
        /// <summary>頂面比根物件高多少（建立時量好；碰撞關掉時也能用）。</summary>
        public float TopOffset;
        /// <summary>底面比根物件低多少。</summary>
        public float BottomOffset;
        /// <summary>頂面的高度（世界 y）。</summary>
        public float TopY => Box != null && Box.enabled && Box.gameObject.activeInHierarchy ? Box.bounds.max.y : transform.position.y + TopOffset;
        public float BottomY => transform.position.y - BottomOffset;
    }

    /// <summary>
    /// 改衣店的互動物件（設計總綱 §11 與 2026-11-16 使用者討論）：
    ///   可搬的紙箱（從紙箱堆拆出最上面那個）、可搬的凳子；縫紉機桌上放提燈的位置；縫紉機上的小燈（有電才亮）；
    ///   放在桌上的提燈（灰盒）。
    /// 在套用正式布局之後建立（紙箱堆會先被布局 A 移到電箱旁邊）。
    /// </summary>
    public sealed partial class LanternGraybox
    {
        public readonly List<LanternMovable> Movables = new List<LanternMovable>();
        public LanternMovable V2MovableBox { get; private set; }
        public LanternMovable V2StoolMovable { get; private set; }
        /// <summary>縫紉機桌上放提燈的位置（桌面左邊空的那一段）。</summary>
        public Vector3 V2SewLanternSpot { get; private set; }
        /// <summary>坐下縫紉時凳子的位置（地面）與朝向（面向縫紉機）。</summary>
        public Vector3 V2SeatSpot { get; private set; }
        public float V2SeatYaw { get; private set; }
        /// <summary>縫紉機的小燈（有電才亮，亮了比較好穿針）。</summary>
        public Light V2SewingLamp { get; private set; }
        /// <summary>放在桌上的提燈（灰盒）。</summary>
        public Transform V2PlacedLantern { get; private set; }
        public LanternUsable V2PlacedLanternUse { get; private set; }
        private int shopInteractRoot;

        public void BuildShopInteractables()
        {
            if (V2Root == null || shopInteractRoot == V2Root.GetInstanceID()) return;
            shopInteractRoot = V2Root.GetInstanceID();
            Movables.RemoveAll(m => m == null);
            var saved = into;
            into = V2Root;
            try
            {
                Transform Child(string n) => V2Root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n && t.parent == V2Root);
                // ---- 紙箱：把紙箱堆最上面那個（BoxB＋封箱膠帶）拆出來，變成可以搬的箱子 ----
                var stack = Child("V2Boxes");
                if (stack != null)
                {
                    var parts = stack.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "BoxB" || r.name == "BoxTape").ToList();
                    if (parts.Count > 0)
                    {
                        var b = parts[0].bounds; foreach (var r in parts) b.Encapsulate(r.bounds);
                        var root = new GameObject("V2MovableBox").transform;
                        root.SetParent(V2Root, false);
                        root.SetPositionAndRotation(new Vector3(b.center.x, b.min.y, b.center.z), stack.rotation);
                        root.localScale = Vector3.one;
                        foreach (var r in parts) r.transform.SetParent(root, true);
                        V2MovableBox = MakeMovable(root, "紙箱", b);
                        FitCollider(stack);   // 紙箱堆剩下的部分（下面的箱子＋布袋）重新算碰撞
                    }
                }
                // ---- 凳子 ----
                var stool = Child("V2Stool");
                if (stool != null)
                {
                    var rs = stool.GetComponentsInChildren<Renderer>(true);
                    var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    foreach (var c in stool.GetComponents<BoxCollider>()) Object.DestroyImmediate(c);
                    V2StoolMovable = MakeMovable(stool, "凳子", b);
                    V2StoolMovable.Climbable = false;
                }
                // ---- 縫紉機：桌上放提燈的位置、凳子的座位、小燈 ----
                if (V2SewingMachine != null)
                {
                    // 桌面：舊的灰盒是 TableTop；美術風格 QA 的桌子是一整塊（桌面在最上面），縫紉機在另一側。
                    var top = V2SewingMachine.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "TableTop" || r.name == "SQA_SewingTable");
                    var tb = top != null ? top.bounds : new Bounds(V2SewingMachine.position + Vector3.up * 0.8f, new Vector3(1f, 0.04f, 0.45f));
                    V2SewLanternSpot = StylizedSewingInUse
                        ? new Vector3(tb.min.x + 0.24f, tb.max.y, tb.center.z + 0.05f)    // 桌面空的那一側（坐著看是右手邊）
                        : new Vector3(tb.min.x + 0.13f, tb.max.y, tb.center.z + 0.04f);
                    var seat = V2StoolMovable != null ? V2StoolMovable.HomePosition : V2SewingMachine.position + Vector3.forward * 0.6f;
                    V2SeatSpot = new Vector3(seat.x, V2Origin.y, seat.z);
                    var toMachine = new Vector3(tb.center.x - seat.x, 0f, tb.center.z - seat.z);
                    V2SeatYaw = Mathf.Atan2(toMachine.x, toMachine.z) * Mathf.Rad2Deg;
                    var head = V2SewingMachine.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "Head" || r.name == "SQA_SewingMachine");
                    var lampAt = head == null ? tb.center + Vector3.up * 0.3f
                        : head.name == "Head" ? head.bounds.center + new Vector3(0f, -0.02f, 0.08f)
                        : new Vector3(head.bounds.center.x, head.bounds.max.y - 0.04f, head.bounds.max.z + 0.06f);
                    var go = new GameObject("V2SewingLamp");
                    go.transform.SetParent(V2Root, false);
                    go.transform.position = lampAt;
                    V2SewingLamp = go.AddComponent<Light>();
                    V2SewingLamp.type = LightType.Point;
                    V2SewingLamp.color = new Color(0.92f, 0.94f, 1f);   // 褪色態：冷白
                    V2SewingLamp.range = 1.0f;
                    V2SewingLamp.intensity = 0.35f;   // 微微亮就好（使用者 2026-11-17）
                    V2SewingLamp.shadows = LightShadows.None;
                    V2SewingLamp.enabled = false;
                    V2PlacedLantern = BuildPlacedLantern(V2SewLanternSpot);
                }
            }
            finally { into = saved; }
        }

        private LanternMovable MakeMovable(Transform root, string label, Bounds worldBounds)
        {
            var box = root.gameObject.AddComponent<BoxCollider>();
            box.center = root.InverseTransformPoint(worldBounds.center);
            var ls = root.lossyScale;
            var size = Quaternion.Inverse(root.rotation) * worldBounds.size;
            box.size = new Vector3(Mathf.Abs(size.x) / ls.x, Mathf.Abs(size.y) / ls.y, Mathf.Abs(size.z) / ls.z);
            var m = root.gameObject.AddComponent<LanternMovable>();
            m.Label = label;
            m.Box = box;
            m.TopOffset = worldBounds.max.y - root.position.y;
            m.BottomOffset = root.position.y - worldBounds.min.y;
            m.HomePosition = root.position;
            m.HomeRotation = root.rotation;
            // 牽引：和其他可以搬的箱子一樣（剛體；只轉水平方向，不會翻倒）。拉近一點比較好控制。
            var rb = root.gameObject.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            var tt = root.gameObject.AddComponent<LanternTetherable>();
            tt.Label = label;
            tt.Init(label == "凳子" ? 2.5f : 3f);
            tt.HoldDistance = 1.15f;
            m.Tether = tt;
            Tetherables.Add(tt);
            Movables.Add(m);
            return m;
        }

        /// <summary>依目前的子物件重新算外框碰撞（世界軸對齊的外框換成物件座標）。</summary>
        private static void FitCollider(Transform root)
        {
            var rs = root.GetComponentsInChildren<Renderer>(true);
            var box = root.GetComponent<BoxCollider>();
            if (rs.Length == 0 || box == null) return;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            box.center = root.InverseTransformPoint(b.center);
            var ls = root.lossyScale;
            var size = Quaternion.Inverse(root.rotation) * b.size;
            box.size = new Vector3(Mathf.Abs(size.x) / ls.x, Mathf.Abs(size.y) / ls.y, Mathf.Abs(size.z) / ls.z);
        }

        /// <summary>
        /// 放在桌上的提燈：用手上那盞提燈的模型（LanternViewmodel 裡的 LanternPivot），底部貼齊桌面。找不到模型才用灰盒。預設隱藏。
        /// </summary>
        private Transform BuildPlacedLantern(Vector3 at)
        {
            var prefab = Resources.Load<GameObject>(LanternViewmodel.PrefabPath);
            if (prefab != null)
            {
                var inst = Object.Instantiate(prefab);
                var pivot = inst.transform.Find("LanternPivot");
                if (pivot != null)
                {
                    var root = new GameObject("V2PlacedLantern").transform;
                    root.SetParent(V2Root, false);
                    root.position = at;
                    pivot.SetParent(root, true);
                    pivot.localRotation = Quaternion.identity;
                    pivot.localPosition = Vector3.zero;
                    Object.DestroyImmediate(inst);
                    var rs = pivot.GetComponentsInChildren<Renderer>(true);
                    foreach (var t in pivot.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
                    foreach (var r in rs) { r.enabled = true; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
                    if (rs.Length > 0)
                    {
                        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                        // 實際大小：提燈約 30 cm 高（第一人稱模型的比例可能不同，量一次再縮放）。
                        float h = b.size.y;
                        if (h > 0.01f) pivot.localScale *= 0.3f / h;
                        b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                        pivot.position += new Vector3(at.x - b.center.x, at.y - b.min.y, at.z - b.center.z);
                    }
                    var col = root.gameObject.AddComponent<BoxCollider>();
                    col.center = new Vector3(0f, 0.15f, 0f);
                    col.size = new Vector3(0.2f, 0.3f, 0.2f);
                    col.isTrigger = true;
                    V2PlacedLanternUse = root.gameObject.AddComponent<LanternUsable>();
                    V2PlacedLanternUse.Label = "提燈";
                    V2PlacedLanternUse.PromptFn = () => "E 把提燈拿回來";
                    Usables.Add(V2PlacedLanternUse);
                    root.gameObject.SetActive(false);
                    return root;
                }
                Object.DestroyImmediate(inst);
            }
            return BuildPlacedLanternGraybox(at);
        }

        private Transform BuildPlacedLanternGraybox(Vector3 at)
        {
            var root = new GameObject("V2PlacedLantern").transform;
            root.SetParent(V2Root, false);
            root.position = at;
            var glass = Mat("V2_PlacedLanternGlass", Mode.Dual, new Color(1f, 0.85f, 0.6f), new Color(1f, 0.85f, 0.6f), null, null, 0.8f, 0.8f, 0f);
            glass.SetColor("_EmissionColor", new Color(2.4f, 1.6f, 0.8f));
            glass.SetColor("_AnomalyEmission", new Color(2.4f, 1.6f, 0.8f));
            Cyl("V2PlacedLanternBase", at + Vector3.up * 0.015f, new Vector3(0.1f, 0.015f, 0.1f), hDark, null, root);
            Cyl("V2PlacedLanternGlass", at + Vector3.up * 0.1f, new Vector3(0.075f, 0.07f, 0.075f), glass, null, root);
            Cyl("V2PlacedLanternCap", at + Vector3.up * 0.18f, new Vector3(0.09f, 0.012f, 0.09f), hDark, null, root);
            Box("V2PlacedLanternHandle", at + Vector3.up * 0.24f, new Vector3(0.1f, 0.012f, 0.012f), hDark, false, null, root);
            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.13f, 0f);
            col.size = new Vector3(0.2f, 0.26f, 0.2f);
            col.isTrigger = true;   // 只給瞄準用，不擋路
            V2PlacedLanternUse = root.gameObject.AddComponent<LanternUsable>();
            V2PlacedLanternUse.Label = "提燈";
            V2PlacedLanternUse.PromptFn = () => "E 把提燈拿回來";
            Usables.Add(V2PlacedLanternUse);
            root.gameObject.SetActive(false);
            return root;
        }
    }
}

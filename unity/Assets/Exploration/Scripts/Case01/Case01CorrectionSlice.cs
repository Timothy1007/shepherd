using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration.Case01
{
    public enum Case01CabinetState
    {
        /// <summary>倒在拱口前，擋住通往後廚的路。</summary>
        Fallen,
        Rising,
        /// <summary>校正中：暫時直立，倒數結束就倒回去（最後 1.5 秒顫動預警）。</summary>
        Upright,
        Falling,
        /// <summary>局部穩定：維持條件已滿足，不再倒下。</summary>
        Stable,
    }

    public enum Case01CorrectResult { NotAvailable, Cooldown, Miss, Restored, Refreshed, Blocked, Stabilized, AlreadyStable }

    public enum Case01TetherResult { NotAvailable, Miss, Grabbed, Placed, PlacedOnMark, Denied, Locked }

    /// <summary>
    /// 現實校正 × 沉浸式操作垂直切片（只在「現實校正沉浸式玩法 QA」建立；借用 Phase 1.9 R3 的廚房與後廚）。
    /// 廚房＋後廚是一個局部的失序區域：靠隔牆的高櫃反覆倒下，橫在通往後廚的拱口前（真的碰撞，走不過去）。
    /// - 現實校正（只在失序區域內可用）：瞄準櫃子短按左鍵；命中時櫃子暫時回到直立（碰撞同步改變、拱口通行），
    ///   幾秒後（可調，候選 4～6 秒）倒回去。立起與倒下前都先檢查擺動路徑：有玩家或椅子在路徑上就等待／把椅子推開，絕不壓住人。
    /// - 維持條件：後廚牆上的舊照片——女孩踩著那張廚房椅、椅子靠在高櫃旁；高櫃旁的地板有四個磨亮的椅腳凹痕。
    /// - 牽引（右鍵）：瞄準那張椅子拾取，它跟著視線在身前移動（會被牆與家具擋住，不穿模），地上的框顯示落點（紅＝不能放、綠＝可以、金＝放在凹痕上），
    ///   右鍵放下、Q 取消（放回原處）。離開區域、被牆擋住太久會自動放下或歸位，不會卡住。
    /// - 椅子放在凹痕上再校正一次：高櫃局部穩定，通路保持暢通；嗡聲退去、燈回暖、樓上傳來一次敲擊。R 可以隨時重置。
    /// 這只驗證「觀察 → 短暫校正 → 理解維持條件 → 牽引 → 局部穩定」，不代表所有異域的修復方式，也不改主線。
    /// </summary>
    public sealed class Case01CorrectionSlice
    {
        // ---------------- 可調參數 ----------------
        /// <summary>校正維持秒數（候選 4～6）。</summary>
        public float Duration = 5f;
        public float Cooldown = 0.6f;
        public float CorrectRange = 9f;
        public float TetherRange = 4f;
        public float MarkTolerance = 0.3f;
        public const float RiseTime = 0.5f, FallTime = 0.42f, WarnTime = 1.5f;
        public const float MinHold = 1.0f, MaxHold = 2.2f;

        // ---------------- 空間 ----------------
        /// <summary>失序區域：R3 的廚房＋後廚（一樓 x0～10、z12～18）。</summary>
        public static readonly Bounds Zone = new Bounds(new Vector3(5f, 1f, 15f), new Vector3(9.8f, 2.4f, 5.9f));
        /// <summary>高櫃的轉軸（靠隔牆、南側底邊）；直立時佔 z 15.0～16.2，倒下時橫在 z 12.9～15.0（拱口 z 12.8～14.0 前）。</summary>
        public static readonly Vector3 Pivot = new Vector3(6.69f, 0f, 15.0f);
        public static readonly Vector3 CabinetSize = new Vector3(0.45f, 2.1f, 1.2f);
        public static readonly Vector3 ChairStart = new Vector3(0.75f, 0f, 13.4f);
        /// <summary>椅子原本的位置（地上的凹痕）：高櫃北側。</summary>
        public static readonly Vector3 MarkPos = new Vector3(6.6f, 0f, 16.85f);
        public static readonly Vector3 PhotoPos = new Vector3(7.085f, 1.45f, 15.6f);
        public static readonly Quaternion FallenRot = Quaternion.Euler(-90f, 0f, 0f);

        public Case01CabinetState State { get; private set; }
        public float TimeLeft { get; private set; }
        public bool Holding { get; private set; }
        public bool Stable { get { return State == Case01CabinetState.Stable; } }
        public Transform Root { get; private set; }
        public Transform Cabinet { get; private set; }
        public BoxCollider CabinetCollider { get; private set; }
        public Transform Chair { get; private set; }
        public BoxCollider ChairCollider { get; private set; }
        public Case01CorrectResult LastCorrect { get; private set; }
        public Case01TetherResult LastTether { get; private set; }
        public string AimHint { get; private set; }
        public bool PlayerInZone { get; private set; }
        public bool GhostValid { get; private set; }
        public bool GhostOnMark { get; private set; }
        public int Corrections { get; private set; }
        public int FallsBlockedByPlayer { get; private set; }
        public int ChairPushes { get; private set; }
        public bool RisePending { get; private set; }
        public bool PhotoSeen { get; private set; }
        public bool MarksSeen { get; private set; }
        public readonly List<string> Log = new List<string>();

        private readonly Case01HouseLayout L;
        private readonly Case01Palette p;
        private readonly Case01ArtKit kit;
        private FirstPersonController player;
        private float angle = 90f; // 0＝直立，90＝倒下
        private float animFrom, animTo, animT, cooldownT;
        private bool stabilizeAfterRise;
        private readonly List<KeyValuePair<Material, Color>> cabinetMats = new List<KeyValuePair<Material, Color>>();
        private Transform shelfGoods, spill;
        private Light coldLight;
        private Material marksMat, rippleMat;
        private Transform ripple;
        private float rippleT = -1f, stableT = -1f, shakeT;
        private AudioSource cabAudio, chairAudio, zoneAudio, uiAudio;
        private Vector3 grabOriginPos;
        private Quaternion grabOriginRot;
        private float holdDist, chairYawOffset, blockedT;
        private LineRenderer ghost, beam, tetherLine;
        private float beamT = -1f;
        private Vector3 beamEnd;
        private Transform device;
        private Material lensMat;
        private float lensFlash;
        private bool audioOn = true;

        public static readonly Color Cold = new Color(0.62f, 0.68f, 0.82f);

        public Case01CorrectionSlice(Case01HouseLayout layout, Case01Palette palette, Case01ArtKit artKit)
        {
            L = layout;
            p = palette;
            kit = artKit;
            Build();
        }

        // ======================= 建立 =======================

        private void Build()
        {
            Root = ArenaArt.Group("RealityCorrectionSlice", L.Root, Vector3.zero);
            // 下面的座標都是世界座標：根放在世界原點、不旋轉（房屋根本身就在原點）。
            Root.position = Vector3.zero;
            Root.rotation = Quaternion.identity;
            // 高櫃：根在轉軸，Body 是一個 0.45×2.1×1.2 m 的高櫃（正面朝廚房 −x），盒狀碰撞＝外形。
            Cabinet = ArenaArt.Group("TallDresser", Root, Pivot);
            var body = ArenaArt.Group("Body", Cabinet, new Vector3(0f, CabinetSize.y * 0.5f, CabinetSize.z * 0.5f));
            CabinetCollider = body.gameObject.AddComponent<BoxCollider>();
            CabinetCollider.size = CabinetSize;
            var model = kit != null ? kit.Place("Bookcase", body, Vector3.zero, -90f) : null;
            if (model != null) Fit(model, body, CabinetSize);
            else ArenaArt.Prim(PrimitiveType.Cube, body, Vector3.zero, CabinetSize, p.Get("wood_dark"), null, "DresserBox", false, true);
            // 層板上的罐子與盤子（直立時在架上，倒下時散在地上）。
            shelfGoods = ArenaArt.Group("ShelfGoods", body, Vector3.zero);
            var ceramic = p.Get("ceramic");
            var glass = p.Own(ArenaArt.Mat(new Color(0.78f, 0.84f, 0.8f), 0.7f));
            for (int s = 0; s < 3; s++)
                for (int k = 0; k < 3; k++)
                    ArenaArt.Prim(PrimitiveType.Cylinder, shelfGoods, new Vector3(-0.04f, -0.55f + s * 0.52f, -0.38f + k * 0.36f), new Vector3(0.1f, 0.07f, 0.1f), k == 1 ? ceramic : glass, null, "Jar", false, false);
            spill = ArenaArt.Group("Spill", Root, Vector3.zero);
            var rng = new System.Random(17);
            for (int i = 0; i < 7; i++)
            {
                var pos = new Vector3(6.05f + (float)rng.NextDouble() * 0.35f, 0.05f, 13.1f + (float)rng.NextDouble() * 1.8f);
                ArenaArt.Prim(PrimitiveType.Cylinder, spill, pos, new Vector3(0.1f, 0.05f, 0.1f), i % 3 == 0 ? ceramic : glass, new Vector3(90f, (float)rng.NextDouble() * 180f, 0f), "SpilledJar", false, false);
            }
            foreach (var r in Cabinet.GetComponentsInChildren<Renderer>(true))
            {
                // 自己的材質副本（不改到共用材質；編輯模式也不會洩漏 renderer.material 的實例）。
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    mats[i] = p.Own(new Material(mats[i]));
                    cabinetMats.Add(new KeyValuePair<Material, Color>(mats[i], mats[i].HasProperty("_Color") ? mats[i].color : Color.white));
                }
                r.sharedMaterials = mats;
            }
            var cabHot = new GameObject("Interact_rc_cabinet");
            cabHot.transform.SetParent(body, false);
            var hb = cabHot.AddComponent<BoxCollider>();
            hb.isTrigger = true;
            hb.size = CabinetSize + new Vector3(0.12f, 0.05f, 0.05f);
            var cabIt = cabHot.AddComponent<Case01Interactable>();
            cabIt.Id = "rc_cabinet";
            cabIt.Prompt = "高櫃";

            // 那張椅子（可牽引）：既有的餐椅模型，加一個褪色的藍坐墊（和餐桌那幾張紅坐墊的椅子區分）。
            Chair = ArenaArt.Group("HerChair", Root, ChairStart);
            var chairModel = kit != null ? kit.Place("DiningChairArt", Chair, Vector3.zero, 0f) : null;
            Vector3 chairSize = new Vector3(0.46f, 0.92f, 0.5f);
            if (chairModel != null)
            {
                // 碰撞盒＝模型外形（量測時椅子還沒旋轉）。
                var rs = chairModel.GetComponentsInChildren<Renderer>(true);
                if (rs.Length > 0)
                {
                    var cb = rs[0].bounds;
                    foreach (var r in rs) cb.Encapsulate(r.bounds);
                    chairSize = new Vector3(Mathf.Clamp(cb.size.x, 0.3f, 0.7f), Mathf.Clamp(cb.size.y, 0.6f, 1.2f), Mathf.Clamp(cb.size.z, 0.3f, 0.7f));
                    chairModel.position += new Vector3(Chair.position.x - cb.center.x, Chair.position.y - cb.min.y, Chair.position.z - cb.center.z);
                }
            }
            if (chairModel == null) ArenaArt.Prim(PrimitiveType.Cube, Chair, new Vector3(0f, 0.23f, 0f), new Vector3(0.44f, 0.46f, 0.46f), p.Get("wood_warm"), null, "ChairBox", false, true);
            ArenaArt.Prim(PrimitiveType.Cube, Chair, new Vector3(0f, 0.5f, 0.02f), new Vector3(0.38f, 0.04f, 0.36f), p.Own(ArenaArt.Mat(new Color(0.42f, 0.6f, 0.66f), 0.15f)), null, "Cushion", false, true);
            ChairCollider = Chair.gameObject.AddComponent<BoxCollider>();
            ChairCollider.center = new Vector3(0f, chairSize.y * 0.5f, 0f);
            ChairCollider.size = chairSize;
            Chair.rotation = Quaternion.Euler(0f, 90f, 0f);

            // 維持條件的線索：高櫃旁地上的椅腳凹痕；後廚牆上的舊照片。
            marksMat = Case01Graphics.Decal(p, "scuff_marks");
            var marks = Case01Graphics.Sheet(Root, "ChairMarks", MarkPos + Vector3.up * 0.012f, Vector3.up, new Vector2(0.62f, 0.62f), marksMat);
            Hotspot("rc_marks", "地上的痕跡", MarkPos + Vector3.up * 0.15f, new Vector3(0.7f, 0.3f, 0.7f));
            var photo = Case01Graphics.Sheet(Root, "ChairPhoto", PhotoPos, Vector3.right, new Vector2(0.2f, 0.15f), Case01Graphics.Mat(p, "photo_chair"), 3f);
            ArenaArt.Prim(PrimitiveType.Sphere, Root, PhotoPos + new Vector3(0.006f, 0.065f, 0f), Vector3.one * 0.012f, p.Get("brass"), null, "Pin", false, false);
            Hotspot("rc_photo", "舊照片", PhotoPos, new Vector3(0.12f, 0.3f, 0.36f));

            coldLight = ArenaArt.PointLight(Root, new Vector3(6.0f, 2.1f, 14.6f), new Color(0.6f, 0.68f, 0.95f), 4.5f, 0.7f, false);

            rippleMat = p.Own(ArenaArt.FadeMat(new Color(1f, 0.86f, 0.6f, 0f), new Color(1f, 0.7f, 0.35f)));
            ripple = ArenaArt.Prim(PrimitiveType.Sphere, Root, Vector3.zero, Vector3.one, rippleMat, null, "CorrectionRipple", false, false);
            ripple.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ripple.gameObject.SetActive(false);

            cabAudio = Source("CabinetAudio", Pivot + new Vector3(0f, 1f, 0f), 1f, 16f);
            chairAudio = Source("ChairAudio", ChairStart + Vector3.up * 0.5f, 1f, 10f);
            chairAudio.transform.SetParent(Chair, true);
            zoneAudio = Source("ZoneAudio", new Vector3(5f, 1.6f, 15f), 2f, 12f);
            zoneAudio.clip = Case01CorrectionSfx.Get("rc_zone_loop");
            zoneAudio.loop = true;
            zoneAudio.volume = 0f;

            ghost = Line("PlacementGhost", 0.012f);
            ghost.loop = false;
            beam = Line("CorrectionBeam", 0.01f);
            tetherLine = Line("TetherLine", 0.008f);
            Apply(90f);
            SetTint(0f);
            State = Case01CabinetState.Fallen;
            Physics.SyncTransforms();
        }

        private void Hotspot(string id, string prompt, Vector3 pos, Vector3 size)
        {
            var go = new GameObject("Interact_" + id);
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            var b = go.AddComponent<BoxCollider>();
            b.isTrigger = true;
            b.size = size;
            var it = go.AddComponent<Case01Interactable>();
            it.Id = id;
            it.Prompt = prompt;
        }

        private AudioSource Source(string name, Vector3 pos, float min, float max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = min;
            a.maxDistance = max;
            return a;
        }

        private LineRenderer Line(string name, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.widthMultiplier = width;
            lr.positionCount = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sharedMaterial = p.Own(ArenaArt.FadeMat(new Color(1f, 0.85f, 0.55f, 0.9f), new Color(1f, 0.7f, 0.35f)));
            return lr;
        }

        /// <summary>把模型縮放、對齊到盒子（盒子中心＝body 原點）。</summary>
        private static void Fit(Transform model, Transform body, Vector3 size)
        {
            var rs = model.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            // yaw −90：模型本地 x 對到世界 z、本地 z 對到世界 x。
            var s = model.localScale;
            model.localScale = new Vector3(s.x * size.z / Mathf.Max(0.01f, b.size.z), s.y * size.y / Mathf.Max(0.01f, b.size.y), s.z * size.x / Mathf.Max(0.01f, b.size.x));
            b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            model.position += body.position - b.center;
        }

        /// <summary>第一人稱的校正器（黃銅環＋鏡片，握在左手側）；只有在失序區域裡出現。</summary>
        public void AttachDevice(Camera cam)
        {
            if (device != null) ArenaArt.Kill(device.gameObject);
            device = ArenaArt.Group("CorrectionDevice", cam.transform, Vector3.zero);
            device.localPosition = new Vector3(-0.22f, -0.21f, 0.46f);
            device.localRotation = Quaternion.Euler(-25f, 28f, -10f);
            // 黃銅自帶一點微光：室內暗處也看得出手上的校正器，不會變成一團黑影。
            var brass = p.Own(ArenaArt.Mat(new Color(0.78f, 0.6f, 0.3f), 0.7f, new Color(0.18f, 0.12f, 0.05f), 0.6f));
            lensMat = p.Own(ArenaArt.Mat(new Color(0.75f, 0.85f, 0.9f), 0.9f, new Color(0.15f, 0.2f, 0.25f)));
            ArenaArt.Prim(PrimitiveType.Cylinder, device, new Vector3(0f, -0.06f, -0.03f), new Vector3(0.028f, 0.07f, 0.028f), p.Get("wood_dark"), new Vector3(20f, 0f, 0f), "Grip", false, false);
            ArenaArt.Prim(PrimitiveType.Cylinder, device, new Vector3(0f, 0.03f, 0f), new Vector3(0.085f, 0.006f, 0.085f), brass, new Vector3(90f, 0f, 0f), "Ring", false, false);
            ArenaArt.Prim(PrimitiveType.Cylinder, device, new Vector3(0f, 0.03f, 0.002f), new Vector3(0.07f, 0.004f, 0.07f), lensMat, new Vector3(90f, 0f, 0f), "Lens", false, false);
            foreach (var r in device.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Case01HouseBuilder.SetLayer(device, cam.gameObject.layer);
            device.gameObject.SetActive(false);
            if (uiAudio == null)
            {
                uiAudio = cam.gameObject.AddComponent<AudioSource>();
                uiAudio.playOnAwake = false;
                uiAudio.spatialBlend = 0f;
            }
        }

        public bool DeviceVisible { get { return device != null && device.gameObject.activeSelf; } }
        public Vector3 DevicePos { get { return device != null ? device.position + device.up * 0.03f : Vector3.zero; } }

        public void Dispose()
        {
            if (device != null) ArenaArt.Kill(device.gameObject);
            device = null;
            if (uiAudio != null) ArenaArt.Kill(uiAudio);
            uiAudio = null;
        }

        // ======================= 狀態 =======================

        public bool InZone(Vector3 feet)
        {
            return feet.y < 1.2f && feet.x > Zone.min.x && feet.x < Zone.max.x && feet.z > Zone.min.z && feet.z < Zone.max.z;
        }

        public bool ChairOnMark
        {
            get
            {
                if (Holding) return false;
                var c = Chair.position;
                return new Vector2(c.x - MarkPos.x, c.z - MarkPos.z).magnitude <= MarkTolerance && c.y < 0.1f;
            }
        }

        public bool Passable { get { return angle < 5f; } }

        private void Apply(float a)
        {
            angle = a;
            Cabinet.rotation = Quaternion.Slerp(Quaternion.identity, FallenRot, a / 90f);
            bool down = a > 80f;
            shelfGoods.gameObject.SetActive(a < 30f);
            spill.gameObject.SetActive(down);
            // 碰撞立刻跟上外形（玩家的 CharacterController 下一次移動就以新的姿勢為準）。
            Physics.SyncTransforms();
        }

        private void SetTint(float warm)
        {
            foreach (var kv in cabinetMats)
                if (kv.Key != null && kv.Key.HasProperty("_Color")) kv.Key.color = Color.Lerp(kv.Value * Cold, kv.Value, warm);
        }

        /// <summary>櫃子在 a 度時的盒子（世界）。</summary>
        private void BoxAt(float a, out Vector3 center, out Vector3 half, out Quaternion rot)
        {
            rot = Quaternion.Slerp(Quaternion.identity, FallenRot, a / 90f);
            center = Pivot + rot * new Vector3(0f, CabinetSize.y * 0.5f, CabinetSize.z * 0.5f);
            half = CabinetSize * 0.5f;
        }

        /// <summary>從 a0 擺到 a1 的路徑上，有沒有玩家（或椅子）。</summary>
        private bool SweepHits(float a0, float a1, bool chairToo, out bool chairHit)
        {
            chairHit = false;
            bool hit = false;
            bool was = CabinetCollider.enabled;
            CabinetCollider.enabled = false;
            Physics.SyncTransforms();
            float lo = Mathf.Min(a0, a1), hi = Mathf.Max(a0, a1);
            for (float a = lo; a <= hi + 0.01f; a += 7.5f)
            {
                Vector3 c, h;
                Quaternion r;
                BoxAt(a, out c, out h, out r);
                foreach (var col in Physics.OverlapBox(c, h - Vector3.one * 0.01f, r, ~0, QueryTriggerInteraction.Collide))
                {
                    if (player != null && col == player.Body) hit = true;
                    if (chairToo && col == ChairCollider) chairHit = true;
                }
                if (player != null)
                {
                    // 保險：直接用玩家膠囊對盒子的距離。
                    var local = Quaternion.Inverse(r) * (player.transform.position + Vector3.up * 0.9f - c);
                    var q = new Vector3(Mathf.Max(0f, Mathf.Abs(local.x) - h.x), Mathf.Max(0f, Mathf.Abs(local.y) - h.y - 0.55f), Mathf.Max(0f, Mathf.Abs(local.z) - h.z));
                    if (q.magnitude < FirstPersonController.Radius - 0.02f) hit = true;
                }
            }
            CabinetCollider.enabled = was;
            return hit;
        }

        private void Play(AudioSource a, string clip, float vol)
        {
            if (a == null || !audioOn || !a.isActiveAndEnabled) return;
            var c = Case01CorrectionSfx.Get(clip);
            if (c != null) a.PlayOneShot(c, vol);
        }

        private void Note(string s)
        {
            Log.Add(s);
            if (Log.Count > 40) Log.RemoveAt(0);
        }

        // ======================= 校正 =======================

        /// <summary>左鍵：從眼睛沿視線校正。只有玩家在失序區域內才有作用。</summary>
        public Case01CorrectResult TryCorrect(Vector3 eye, Vector3 dir)
        {
            if (player == null || !InZone(player.transform.position)) return LastCorrect = Case01CorrectResult.NotAvailable;
            if (cooldownT > 0f) return LastCorrect = Case01CorrectResult.Cooldown;
            cooldownT = Cooldown;
            Vector3 hitPoint;
            if (!AimAtCabinet(eye, dir, out hitPoint))
            {
                Play(uiAudio, "rc_fizzle", 0.5f);
                lensFlash = -0.6f;
                Note("校正落空");
                return LastCorrect = Case01CorrectResult.Miss;
            }
            Corrections++;
            beamT = 0f;
            beamEnd = hitPoint;
            lensFlash = 1f;
            rippleT = 0f;
            ripple.position = hitPoint;
            Play(uiAudio, "rc_correct", 0.6f);
            if (State == Case01CabinetState.Stable)
            {
                Note("已經穩定");
                return LastCorrect = Case01CorrectResult.AlreadyStable;
            }
            bool condition = ChairOnMark;
            if (State == Case01CabinetState.Upright || State == Case01CabinetState.Rising)
            {
                if (condition)
                {
                    Stabilize();
                    return LastCorrect = Case01CorrectResult.Stabilized;
                }
                TimeLeft = Duration;
                Note("校正延長");
                return LastCorrect = Case01CorrectResult.Refreshed;
            }
            // 倒下（或正在倒）：立起來——路徑上有人就不立（不把人頂開），等玩家讓開再試。
            bool chairHit;
            if (SweepHits(angle, 0f, false, out chairHit))
            {
                RisePending = true;
                shakeT = 0.4f;
                Play(cabAudio, "rc_deny", 0.7f);
                Note("校正命中，但擺動路徑上有人：櫃子只是晃了一下");
                return LastCorrect = Case01CorrectResult.Blocked;
            }
            RisePending = false;
            stabilizeAfterRise = condition;
            State = Case01CabinetState.Rising;
            animFrom = angle;
            animTo = 0f;
            animT = 0f;
            Play(cabAudio, "rc_rise", 0.8f);
            Note(condition ? "校正：櫃子立起（維持條件已滿足）" : "校正：櫃子暫時立起");
            return LastCorrect = condition ? Case01CorrectResult.Stabilized : Case01CorrectResult.Restored;
        }

        /// <summary>視線是否命中櫃子：直接射線命中，或 2.5° 內的瞄準輔助（且視線沒被擋）。</summary>
        public bool AimAtCabinet(Vector3 eye, Vector3 dir, out Vector3 hitPoint)
        {
            hitPoint = Vector3.zero;
            RaycastHit h;
            if (Physics.Raycast(eye, dir, out h, CorrectRange, Case01SpatialMap.SightMask, QueryTriggerInteraction.Ignore))
            {
                if (h.collider == CabinetCollider)
                {
                    hitPoint = h.point;
                    return true;
                }
            }
            var c = CabinetCollider.bounds.center;
            var to = c - eye;
            if (to.magnitude > CorrectRange || Vector3.Angle(dir, to) > 2.5f) return false;
            if (Physics.Raycast(eye, to.normalized, out h, to.magnitude, Case01SpatialMap.SightMask, QueryTriggerInteraction.Ignore) && h.collider != CabinetCollider) return false;
            hitPoint = CabinetCollider.ClosestPoint(eye);
            return true;
        }

        private void Stabilize()
        {
            State = Case01CabinetState.Stable;
            TimeLeft = 0f;
            stableT = 0f;
            Play(cabAudio, "rc_stable", 0.9f);
            Note("局部穩定：椅子回到原位，高櫃不再倒下");
        }

        // ======================= 牽引 =======================

        /// <summary>右鍵：沒拿東西時瞄準椅子拾取；拿著時放下。</summary>
        public Case01TetherResult TryTether(Vector3 eye, Vector3 dir)
        {
            if (player == null || !InZone(player.transform.position)) return LastTether = Case01TetherResult.NotAvailable;
            if (Holding) return Place();
            if (!AimAtChair(eye, dir)) return LastTether = Case01TetherResult.Miss;
            if (Stable)
            {
                Play(uiAudio, "rc_deny", 0.5f);
                return LastTether = Case01TetherResult.Locked;
            }
            Holding = true;
            grabOriginPos = Chair.position;
            grabOriginRot = Chair.rotation;
            ChairCollider.isTrigger = true;
            var flat = new Vector3(Chair.position.x - eye.x, 0f, Chair.position.z - eye.z);
            holdDist = Mathf.Clamp(flat.magnitude, MinHold, MaxHold);
            chairYawOffset = Mathf.DeltaAngle(player.Yaw, Chair.eulerAngles.y);
            blockedT = 0f;
            Play(chairAudio, "rc_grab", 0.7f);
            if (chairAudio != null && audioOn)
            {
                chairAudio.clip = Case01CorrectionSfx.Get("rc_tether_loop");
                chairAudio.loop = true;
                chairAudio.volume = 0.35f;
                if (chairAudio.isActiveAndEnabled) chairAudio.Play();
            }
            Note("牽引：拾起椅子");
            return LastTether = Case01TetherResult.Grabbed;
        }

        /// <summary>滑鼠滾輪：調整握持距離（1.0～2.2 m）。</summary>
        public void AdjustHold(float delta)
        {
            if (Holding) holdDist = Mathf.Clamp(holdDist + delta, MinHold, MaxHold);
        }

        public float HoldDistance { get { return holdDist; } }

        public bool AimAtChair(Vector3 eye, Vector3 dir)
        {
            RaycastHit h;
            if (Physics.Raycast(eye, dir, out h, TetherRange, Case01SpatialMap.SightMask, QueryTriggerInteraction.Collide))
                if (h.collider == ChairCollider) return true;
            var c = ChairCollider.bounds.center;
            var to = c - eye;
            if (to.magnitude > TetherRange || Vector3.Angle(dir, to) > 4f) return false;
            return !Physics.Raycast(eye, to.normalized, out h, to.magnitude, Case01SpatialMap.SightMask, QueryTriggerInteraction.Ignore) || h.collider == ChairCollider;
        }

        /// <summary>落點：椅子正下方的地板；能不能放（不和牆、家具、玩家、櫃子重疊）、是不是在凹痕上。</summary>
        private Vector3 Landing(out bool valid, out bool onMark)
        {
            var c = Chair.position;
            var landing = new Vector3(c.x, 0f, c.z);
            RaycastHit h;
            bool wasTrig = ChairCollider.enabled;
            ChairCollider.enabled = false;
            if (Physics.Raycast(c + Vector3.up * 0.4f, Vector3.down, out h, 2f, Case01SpatialMap.SightMask, QueryTriggerInteraction.Ignore)) landing.y = h.point.y;
            valid = landing.y < 0.08f && landing.y > -0.08f && Free(landing, Chair.rotation, true);
            ChairCollider.enabled = wasTrig;
            onMark = valid && new Vector2(landing.x - MarkPos.x, landing.z - MarkPos.z).magnitude <= MarkTolerance;
            return landing;
        }

        /// <summary>椅子放在 pos 會不會和其他東西重疊（玩家也算）。</summary>
        private bool Free(Vector3 pos, Quaternion rot, bool includePlayer)
        {
            var center = pos + rot * ChairCollider.center;
            var half = ChairCollider.size * 0.5f - new Vector3(0.02f, 0.03f, 0.02f);
            foreach (var col in Physics.OverlapBox(center + Vector3.up * 0.02f, half, rot, Case01SpatialMap.SightMask, QueryTriggerInteraction.Ignore))
            {
                if (col == ChairCollider) continue;
                if (player != null && col == player.Body)
                {
                    if (includePlayer) return false;
                    continue;
                }
                return false;
            }
            if (includePlayer && player != null)
            {
                var d = new Vector2(player.transform.position.x - pos.x, player.transform.position.z - pos.z).magnitude;
                if (d < FirstPersonController.Radius + 0.25f) return false;
            }
            return true;
        }

        public Case01TetherResult Place()
        {
            if (!Holding) return LastTether = Case01TetherResult.NotAvailable;
            bool valid, onMark;
            var landing = Landing(out valid, out onMark);
            if (!valid)
            {
                Play(chairAudio, "rc_deny", 0.6f);
                Note("這裡放不下");
                return LastTether = Case01TetherResult.Denied;
            }
            Drop(landing, Chair.rotation);
            Play(chairAudio, "rc_place", 0.8f);
            Note(onMark ? "椅子放回凹痕上" : "放下椅子");
            return LastTether = onMark ? Case01TetherResult.PlacedOnMark : Case01TetherResult.Placed;
        }

        /// <summary>Q：取消牽引，椅子回到拾起的地方（那裡被擋住就放在目前的落點；都不行就留在手上）。</summary>
        public bool Cancel()
        {
            if (!Holding) return false;
            ChairCollider.enabled = false;
            bool originFree = Free(grabOriginPos, grabOriginRot, true);
            ChairCollider.enabled = true;
            if (originFree)
            {
                Drop(grabOriginPos, grabOriginRot);
                Play(chairAudio, "rc_place", 0.5f);
                Note("取消牽引：椅子回到原處");
                return true;
            }
            bool valid, onMark;
            var landing = Landing(out valid, out onMark);
            if (valid)
            {
                Drop(landing, Chair.rotation);
                Note("取消牽引：原處被擋住，就地放下");
                return true;
            }
            Play(chairAudio, "rc_deny", 0.5f);
            return false;
        }

        private void Drop(Vector3 pos, Quaternion rot)
        {
            Holding = false;
            Chair.position = pos;
            Chair.rotation = rot;
            ChairCollider.isTrigger = false;
            if (chairAudio != null)
            {
                chairAudio.loop = false;
                chairAudio.Stop();
            }
            Physics.SyncTransforms();
        }

        private void TickHold(float dt, Vector3 eye, Vector3 forward)
        {
            var flat = new Vector3(forward.x, 0f, forward.z);
            if (flat.sqrMagnitude < 1e-4f) flat = player.transform.forward;
            flat.Normalize();
            var target = new Vector3(eye.x, 0.14f, eye.z) + flat * holdDist;
            var cur = Chair.position;
            // 椅子繞著玩家轉（不從玩家身上穿過）：角度以每秒 220° 追視線方向，距離往握持距離靠，每影格位移上限 6 m/s。
            var center = new Vector3(eye.x, 0f, eye.z);
            var rel = new Vector3(cur.x - center.x, 0f, cur.z - center.z);
            float r = Mathf.Max(0.5f, rel.magnitude);
            float ang = Mathf.Atan2(rel.x, rel.z) * Mathf.Rad2Deg;
            float targetAng = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            float newAng = Mathf.MoveTowardsAngle(ang, targetAng, 220f * dt);
            float newR = Mathf.MoveTowards(r, holdDist, 2.5f * dt);
            System.Func<float, float, Vector3> at = (a, rr) => center + new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), 0f, Mathf.Cos(a * Mathf.Deg2Rad)) * rr + Vector3.up * 0.14f;
            var rot = Quaternion.Euler(0f, player.Yaw + chairYawOffset, 0f);
            ChairCollider.enabled = false;
            Vector3 next = cur;
            var tries = new[] { at(newAng, newR), at(newAng, r), at(ang, newR) };
            bool moved = false;
            foreach (var cand0 in tries)
            {
                var cand = cand0;
                var d = cand - cur;
                d.y = 0f;
                if (d.magnitude > 6f * dt) cand = new Vector3(cur.x, 0.14f, cur.z) + d.normalized * 6f * dt;
                if (Free(cand, rot, false) && PlayerClear(cand))
                {
                    next = cand;
                    moved = true;
                    break;
                }
            }
            if (!moved && Free(cur, rot, false)) next = new Vector3(cur.x, 0.14f, cur.z);
            bool rotOk = Free(next, rot, false);
            ChairCollider.enabled = true;
            Chair.position = next;
            if (rotOk) Chair.rotation = rot;
            Physics.SyncTransforms();
            // 被牆擋住、拉不過去太久：自動放下（或歸位），不卡住。
            // 卡在家具旁一下不算：只有離想要的位置超過 2 m、或視線被牆完全擋住超過 1.2 秒才斷開。
            var top = new Vector3(ChairCollider.bounds.center.x, ChairCollider.bounds.max.y - 0.05f, ChairCollider.bounds.center.z);
            bool lost = new Vector2(target.x - next.x, target.z - next.z).magnitude > 2.0f || (Physics.Linecast(eye, top, Case01SpatialMap.SightMask, QueryTriggerInteraction.Ignore) && Physics.Linecast(eye, ChairCollider.bounds.center, Case01SpatialMap.SightMask, QueryTriggerInteraction.Ignore));
            blockedT = lost ? blockedT + dt : 0f;
            if (blockedT > 1.2f)
            {
                Note("牽引斷開：被擋住太久");
                AutoRelease();
            }
        }

        private bool PlayerClear(Vector3 cand)
        {
            if (player == null) return true;
            var d = new Vector2(player.transform.position.x - cand.x, player.transform.position.z - cand.z).magnitude;
            return d > FirstPersonController.Radius + 0.32f;
        }

        private void AutoRelease()
        {
            bool valid, onMark;
            var landing = Landing(out valid, out onMark);
            if (valid) Drop(landing, Chair.rotation);
            else if (!Cancel())
            {
                // 最後手段：回到起點（起點總是空的；有人站在那裡就再等一下）。
                ChairCollider.enabled = false;
                bool startFree = Free(ChairStart, Quaternion.Euler(0f, 90f, 0f), true);
                ChairCollider.enabled = true;
                if (startFree) Drop(ChairStart, Quaternion.Euler(0f, 90f, 0f));
            }
        }

        /// <summary>櫃子倒下的路徑上有椅子：把椅子推到旁邊（往廚房內側），不讓它被壓住或卡住通路。</summary>
        private void PushChairOut()
        {
            ChairCollider.enabled = false;
            foreach (var x in new[] { 5.85f, 5.5f, 5.15f })
            {
                foreach (var z in new[] { Chair.position.z, 13.6f, 15.6f, 16.6f })
                {
                    var cand = new Vector3(x, 0f, Mathf.Clamp(z, 12.6f, 17.0f));
                    if (Free(cand, Chair.rotation, true))
                    {
                        ChairCollider.enabled = true;
                        Drop(cand, Chair.rotation);
                        ChairPushes++;
                        Play(chairAudio, "rc_place", 0.6f);
                        Note("椅子被倒下的櫃子推開");
                        return;
                    }
                }
            }
            ChairCollider.enabled = true;
        }

        // ======================= 每影格 =======================

        public void Tick(float dt, FirstPersonController fp, Camera cam, bool exploring, bool audio)
        {
            player = fp;
            audioOn = audio;
            if (player == null) return;
            if (cooldownT > 0f) cooldownT -= dt;
            var feet = player.transform.position;
            PlayerInZone = InZone(feet);
            var eye = player.EyePosition;
            var fwd = player.ViewRotation * Vector3.forward;

            // 離開區域：牽引斷開（放在落點或歸位）。
            if (Holding && !PlayerInZone)
            {
                Note("離開失序區域：牽引斷開");
                AutoRelease();
            }
            if (Holding) TickHold(dt, eye, fwd);

            // 櫃子。
            switch (State)
            {
                case Case01CabinetState.Rising:
                case Case01CabinetState.Falling:
                {
                    animT += dt;
                    float dur = State == Case01CabinetState.Rising ? RiseTime : FallTime;
                    float k = Mathf.Clamp01(animT / dur);
                    float e = State == Case01CabinetState.Rising ? 1f - (1f - k) * (1f - k) : k * k;
                    Apply(Mathf.Lerp(animFrom, animTo, e));
                    SetTint(State == Case01CabinetState.Rising ? e : 1f - e);
                    if (k >= 1f)
                    {
                        if (State == Case01CabinetState.Rising)
                        {
                            if (stabilizeAfterRise && ChairOnMark) Stabilize();
                            else
                            {
                                State = Case01CabinetState.Upright;
                                TimeLeft = Duration;
                            }
                        }
                        else
                        {
                            State = Case01CabinetState.Fallen;
                            Play(cabAudio, "rc_thud", 1f);
                            Note("櫃子倒回拱口前");
                        }
                    }
                    break;
                }
                case Case01CabinetState.Upright:
                {
                    TimeLeft -= dt;
                    float warm = Mathf.Clamp01(TimeLeft / Mathf.Max(0.01f, Duration));
                    SetTint(0.45f + 0.55f * warm);
                    if (TimeLeft <= WarnTime && TimeLeft + dt > WarnTime) Play(cabAudio, "rc_warn", 0.8f);
                    float shake = TimeLeft < WarnTime ? Mathf.Sin(Time.realtimeSinceStartup * 40f) * 1.6f * (1f - TimeLeft / WarnTime) : 0f;
                    Apply(Mathf.Max(0f, shake));
                    if (TimeLeft <= 0f)
                    {
                        bool chairHit;
                        if (SweepHits(0f, 90f, true, out chairHit))
                        {
                            // 玩家在倒下的路徑上：不倒，繼續顫動，等玩家離開。
                            if (TimeLeft > -dt * 1.5f) FallsBlockedByPlayer++;
                            TimeLeft = 0f;
                            if (Random.value < dt * 1.2f) Play(cabAudio, "rc_warn", 0.5f);
                            break;
                        }
                        if (chairHit) PushChairOut();
                        State = Case01CabinetState.Falling;
                        animFrom = angle;
                        animTo = 90f;
                        animT = 0f;
                    }
                    break;
                }
                case Case01CabinetState.Fallen:
                    if (shakeT > 0f)
                    {
                        shakeT -= dt;
                        Apply(90f - Mathf.Abs(Mathf.Sin(shakeT * 30f)) * 3f * shakeT / 0.4f);
                    }
                    else if (angle != 90f) Apply(90f);
                    break;
                case Case01CabinetState.Stable:
                    if (angle != 0f) Apply(0f);
                    SetTint(1f);
                    break;
            }
            // 擺動途中被擋住的立起要求（玩家讓開後不會自己立起，要再校正一次——明確的因果）。
            if (RisePending && State != Case01CabinetState.Fallen) RisePending = false;
            CabinetCollider.enabled = true;

            // 穩定後的回饋：嗡聲退去、冷光回暖、凹痕亮一下、樓上一次敲擊。
            if (stableT >= 0f)
            {
                stableT += dt;
                float k = Mathf.Clamp01(stableT / 2f);
                coldLight.color = Color.Lerp(new Color(0.6f, 0.68f, 0.95f), new Color(1f, 0.8f, 0.55f), k);
                coldLight.intensity = Mathf.Lerp(0.7f, 0.45f, k);
                if (marksMat != null) ArenaArt.SetEmission(marksMat, new Color(0.6f, 0.45f, 0.2f) * Mathf.Max(0f, 1f - Mathf.Abs(stableT - 1f)));
                if (stableT >= 1.5f && stableT - dt < 1.5f) PlayAt("rc_knock", new Vector3(4.5f, 4.2f, 8.5f), 0.7f);
            }
            if (zoneAudio != null)
            {
                float target = audioOn && PlayerInZone && !Stable ? 0.32f : 0f;
                zoneAudio.volume = Mathf.MoveTowards(zoneAudio.volume, target, dt * 0.4f);
                if (Application.isPlaying && zoneAudio.isActiveAndEnabled && !zoneAudio.isPlaying && zoneAudio.volume > 0f) zoneAudio.Play();
            }

            // 漣漪（局部現實恢復的範圍）。
            if (rippleT >= 0f)
            {
                rippleT += dt;
                float k = Mathf.Clamp01(rippleT / 0.45f);
                ripple.gameObject.SetActive(k < 1f);
                ripple.localScale = Vector3.one * Mathf.Lerp(0.15f, 2.4f, k);
                rippleMat.color = new Color(1f, 0.86f, 0.6f, 0.4f * (1f - k));
                if (k >= 1f) rippleT = -1f;
            }

            UpdateVisuals(dt, eye, fwd, exploring);
        }

        private void PlayAt(string clip, Vector3 pos, float vol)
        {
            if (!audioOn || !Application.isPlaying) return;
            var c = Case01CorrectionSfx.Get(clip);
            if (c != null) AudioSource.PlayClipAtPoint(c, pos, vol);
        }

        private void UpdateVisuals(float dt, Vector3 eye, Vector3 fwd, bool exploring)
        {
            bool show = PlayerInZone && exploring;
            if (device != null)
            {
                if (device.gameObject.activeSelf != show) device.gameObject.SetActive(show);
                lensFlash = Mathf.MoveTowards(lensFlash, 0f, dt * 2.5f);
                if (lensMat != null)
                {
                    var em = lensFlash >= 0f ? Color.Lerp(new Color(0.15f, 0.2f, 0.25f), new Color(1.6f, 1.2f, 0.7f), lensFlash) : Color.Lerp(new Color(0.15f, 0.2f, 0.25f), new Color(0.5f, 0.05f, 0.05f), -lensFlash);
                    ArenaArt.SetEmission(lensMat, em);
                }
                // 命中時鏡片往前一推（手的回饋）。
                float kick = Mathf.Max(0f, lensFlash) * 0.04f;
                device.localPosition = new Vector3(-0.22f, -0.21f, 0.46f + kick);
            }
            // 校正光束（0.2 秒）。
            if (beamT >= 0f && device != null)
            {
                beamT += dt;
                beam.positionCount = 2;
                beam.SetPosition(0, DevicePos);
                beam.SetPosition(1, beamEnd);
                beam.widthMultiplier = 0.012f * (1f - beamT / 0.2f);
                if (beamT > 0.2f)
                {
                    beamT = -1f;
                    beam.positionCount = 0;
                }
            }
            // 牽引線與落點框。
            if (Holding)
            {
                bool valid, onMark;
                var landing = Landing(out valid, out onMark);
                GhostValid = valid;
                GhostOnMark = onMark;
                DrawGhost(landing, Chair.rotation, onMark ? new Color(1f, 0.82f, 0.35f) : valid ? new Color(0.5f, 0.95f, 0.6f) : new Color(1f, 0.35f, 0.3f));
                var a = device != null && device.gameObject.activeSelf ? DevicePos : eye + fwd * 0.4f - Vector3.up * 0.25f;
                var b = ChairCollider.bounds.center;
                tetherLine.positionCount = 12;
                for (int i = 0; i < 12; i++)
                {
                    float t = i / 11f;
                    var pt = Vector3.Lerp(a, b, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.06f + Vector3.right * Mathf.Sin(Time.realtimeSinceStartup * 9f + t * 8f) * 0.01f;
                    tetherLine.SetPosition(i, pt);
                }
            }
            else
            {
                GhostValid = GhostOnMark = false;
                ghost.positionCount = 0;
                tetherLine.positionCount = 0;
            }
            // 準星提示。
            AimHint = null;
            if (show)
            {
                Vector3 hp;
                if (Holding) AimHint = GhostOnMark ? "右鍵 放回凹痕｜Q 取消" : GhostValid ? "右鍵 放下｜Q 取消" : "這裡放不下｜Q 取消";
                else if (AimAtChair(eye, fwd) && !Stable) AimHint = "右鍵 牽引";
                else if (AimAtCabinet(eye, fwd, out hp)) AimHint = Stable ? "（已穩定）" : "左鍵 校正";
            }
        }

        private void DrawGhost(Vector3 pos, Quaternion rot, Color col)
        {
            var h = ChairCollider.size * 0.5f;
            var c = pos + Vector3.up * 0.02f;
            var pts = new[] { new Vector3(-h.x, 0f, -h.z), new Vector3(h.x, 0f, -h.z), new Vector3(h.x, 0f, h.z), new Vector3(-h.x, 0f, h.z), new Vector3(-h.x, 0f, -h.z) };
            ghost.positionCount = pts.Length;
            for (int i = 0; i < pts.Length; i++) ghost.SetPosition(i, c + rot * pts[i]);
            ghost.startColor = ghost.endColor = col;
            ghost.sharedMaterial.color = col;
        }

        // ======================= 調查（E） =======================

        public string Describe(string id)
        {
            switch (id)
            {
                case "rc_photo":
                    PhotoSeen = true;
                    return "釘在牆上的舊照片：她踩在那張鋪著藍色舊坐墊的椅子上，伸手去拿高櫃最上層的罐子。椅子就靠在櫃子旁邊。";
                case "rc_marks":
                    MarksSeen = true;
                    return "高櫃旁的地板上有四個磨亮的小凹痕，還有椅子被拖動的弧形刮痕。這裡原本總是放著一張椅子。";
                case "rc_cabinet":
                    if (Stable) return "高櫃靠著牆站穩了。旁邊那張椅子回到了它的位置。";
                    if (State == Case01CabinetState.Upright || State == Case01CabinetState.Rising) return "它現在是站著的——木頭在輕輕發抖，撐不了多久。";
                    return "高櫃從牆邊倒下來，正好橫在通往後廚的拱口前。櫃門鬆開，罐子滾了一地。它不像是第一次倒下。";
            }
            return null;
        }

        // ======================= 重置 =======================

        public void ResetPuzzle()
        {
            if (Holding)
            {
                Holding = false;
                ChairCollider.isTrigger = false;
                if (chairAudio != null) chairAudio.Stop();
            }
            Chair.position = ChairStart;
            Chair.rotation = Quaternion.Euler(0f, 90f, 0f);
            State = Case01CabinetState.Fallen;
            TimeLeft = 0f;
            RisePending = false;
            stabilizeAfterRise = false;
            stableT = -1f;
            cooldownT = 0f;
            coldLight.color = new Color(0.6f, 0.68f, 0.95f);
            coldLight.intensity = 0.7f;
            if (marksMat != null) ArenaArt.SetEmission(marksMat, Color.black);
            Apply(90f);
            SetTint(0f);
            Physics.SyncTransforms();
            Note("重置：櫃子倒下、椅子回到窗邊");
        }

        /// <summary>QA：玩家目前是否和櫃子或椅子的碰撞重疊（不應該發生）。</summary>
        public bool PlayerOverlapsSlice()
        {
            if (player == null) return false;
            var pos = player.transform.position;
            var a = pos + Vector3.up * (FirstPersonController.Radius);
            var b = pos + Vector3.up * (FirstPersonController.Height - FirstPersonController.Radius);
            var cc = player.Body;
            bool was = cc.enabled;
            cc.enabled = false;
            bool hit = false;
            foreach (var c in Physics.OverlapCapsule(a, b, FirstPersonController.Radius - 0.04f, ~0, QueryTriggerInteraction.Ignore))
                if (c == CabinetCollider || (c == ChairCollider && !ChairCollider.isTrigger)) hit = true;
            cc.enabled = was;
            return hit;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 點按判定（穿針）：指針來回擺動，進到亮的範圍時按一下＝成功；要成功 Need 次，失誤會退一格。
    /// </summary>
    public sealed class LanternTapJudge
    {
        public bool Active { get; private set; }
        public float Pointer { get; private set; }
        public float ZoneCenter { get; private set; } = 0.5f;
        public float ZoneWidth = 0.16f;
        public float Speed = 0.9f;
        public int Need = 3;
        public int Hits { get; private set; }
        public int Misses { get; private set; }
        public bool Succeeded { get; private set; }
        public float Flash { get; private set; }
        public bool LastWasHit { get; private set; }
        private float t;
        private System.Random rng = new System.Random(7);

        public void Begin()
        {
            Active = true; Succeeded = false; Hits = 0; Misses = 0; t = 0f;
            NewZone();
        }

        public void Cancel() { Active = false; }

        public bool InZone => Mathf.Abs(Pointer - ZoneCenter) <= ZoneWidth / 2f;

        private void NewZone() { ZoneCenter = 0.2f + (float)rng.NextDouble() * 0.6f; }

        /// <summary>回傳這一格是否剛完成。</summary>
        public bool Step(float dt, bool tap)
        {
            if (!Active) return false;
            t += dt * Speed;
            Pointer = Mathf.PingPong(t, 1f);
            Flash = Mathf.MoveTowards(Flash, 0f, dt * 3f);
            if (!tap) return false;
            Flash = 1f;
            if (InZone)
            {
                Hits++; LastWasHit = true;
                NewZone();
                if (Hits >= Need) { Active = false; Succeeded = true; return true; }
            }
            else { Misses++; LastWasHit = false; Hits = Mathf.Max(0, Hits - 1); }
            return false;
        }
    }

    /// <summary>
    /// 保持平衡（像釣魚遊戲）：標記會被晃來晃去，用左右（A／D）把它留在中間的穩定區，累積站穩 Need 秒就成功；晃出橫條就失敗（差點摔下來）。
    /// 幼年取外套用（穩定區窄、要站久、晃得兇）；青春期踮腳用（比較簡單）。之後手機可以換成一個滑桿。
    /// </summary>
    public sealed class LanternBalance
    {
        public bool Active { get; private set; }
        public bool Succeeded { get; private set; }
        public bool Failed { get; private set; }
        /// <summary>-1～1；超出就失敗。</summary>
        public float Marker { get; private set; }
        public float Velocity { get; private set; }
        public float Zone { get; private set; } = 0.3f;
        public float Need { get; private set; } = 3f;
        public float Hold { get; private set; }
        public float Drift { get; private set; } = 1f;
        public int Attempts { get; private set; }
        private float t, phase;
        public bool InZone => Mathf.Abs(Marker) <= Zone;

        public void Begin(float zone, float need, float drift)
        {
            Zone = zone; Need = need; Drift = drift;
            Marker = 0f; Velocity = 0f; Hold = 0f; t = 0f;
            phase = (Attempts * 7.31f) % 50f + 3f;
            Attempts++;
            Active = true; Succeeded = Failed = false;
        }

        public void Cancel() { Active = false; }

        /// <summary>回傳 1＝成功、-1＝失敗、0＝還在進行。input＝-1～1（A／D）。</summary>
        public int Step(float dt, float input)
        {
            if (!Active) return 0;
            t += dt;
            float push = (Mathf.PerlinNoise(t * 0.9f, phase) - 0.5f) * 2f * Drift + Mathf.Sin(t * 1.7f + phase) * 0.35f * Drift;
            Velocity += (push + Mathf.Clamp(input, -1f, 1f) * 2.2f - Marker * 0.3f) * dt;
            Velocity *= Mathf.Exp(-1.6f * dt);
            Marker += Velocity * dt;
            if (InZone) Hold += dt; else Hold = Mathf.Max(0f, Hold - dt * 0.6f);
            if (Mathf.Abs(Marker) >= 1f) { Active = false; Failed = true; return -1; }
            if (Hold >= Need) { Active = false; Succeeded = true; return 1; }
            return 0;
        }
    }

    /// <summary>
    /// 新房子一樓第一批謎題（設計見 docs/level/CASE01_GROUND_FLOOR_ESCAPE_ROOM_V0_1.md）：
    /// S1 電箱、S2 外套吊牌（聚光）、Y1 側院的紅線、S4 縫紉機穿針（點按判定）、秘密（衣架後的照片）。
    /// 2026-11-16 使用者決定：
    ///   ・開燈和縫紉機拆開：腳踏縫紉機不用電，拿到線就能縫；有電時縫紉機的小燈會亮，穿針比較容易（不是必要條件）。
    ///   ・任務清單不排順序（恢復供電、看清吊牌、找到線、把外套縫好）。
    ///   ・縫紉：先把提燈放在桌上 → 幼年爬上凳子／青春期、成人拉開凳子坐下 → 穿針；幼年判定最難。
    ///   ・店面完成不再讓整間房永久恢復真實（之後大部分時間都是褪色態，只有提燈照到的地方是記憶中的樣子）；
    ///     完成時刻＝影子殘影、筆記更新。
    /// 所有文字暫定。
    /// </summary>
    public sealed class LanternV2Puzzles
    {
        public bool Powered { get; private set; }
        public bool TagRead { get; private set; }
        public float TagFocus { get; private set; }
        public bool HasThread { get; private set; }
        public bool Sewn { get; private set; }
        public bool ShopRepaired { get; private set; }
        public readonly List<string> Notebook = new List<string>();
        public readonly LanternTapJudge Tap = new LanternTapJudge();
        public LanternEcho ShopEcho { get; private set; }
        /// <summary>提燈放在縫紉機桌上。</summary>
        public bool LanternPlaced { get; private set; }
        /// <summary>坐在（幼年：爬上）縫紉機前的凳子上。</summary>
        public bool Seated { get; private set; }
        /// <summary>目前的記憶年齡（0＝幼年、1＝青春期、2＝成人）；由玩家那一側每格設定。</summary>
        public int SewAge = 2;
        /// <summary>坐下／站起來的那一格要求玩家那一側移動位置（坐下＝座位、站起來＝凳子旁邊）。</summary>
        public bool SeatMoveRequested { get; private set; }
        public void ConsumeSeatMove() => SeatMoveRequested = false;
        /// <summary>坐好了、穿針判定已經開始（坐上去的動作播完才開始）。</summary>
        public bool SewingStarted { get; private set; }
        private bool lanternAwayWarned;

        // ---------------- S2 那件外套（使用者 2026-11-17：取下 → 攤在裁剪桌上 → 提燈照，外套打開 → 近看內側標籤） ----------------
        public enum CoatState { OnPeg, Held, OnTable, Open }
        public CoatState Coat { get; private set; }
        public bool CoatHeld => Coat == CoatState.Held;
        public readonly LanternBalance Balance = new LanternBalance();
        /// <summary>手能伸到的高度（世界 y）；由玩家那一側設定。</summary>
        public System.Func<float> ReachTop;
        /// <summary>這一格的左右輸入（保持平衡用）。</summary>
        public float BalanceInput;
        public float CoatOpenProgress { get; private set; }
        private bool CanReachCoat => ReachTop == null || ReachTop() >= world.V2CoatReachY;
        /// <summary>玩家腳的位置（世界座標）；由玩家那一側設定。</summary>
        public System.Func<Vector3> PlayerFeet;
        /// <summary>提燈放在哪裡（縫紉機桌上或地上）。</summary>
        public Vector3 LanternSpot { get; private set; }
        public bool LanternAtSewTable => LanternPlaced && (LanternSpot - world.V2SewLanternSpot).sqrMagnitude < 0.01f;
        /// <summary>幼年站在凳子上（使用者 2026-11-17：紙箱不夠高，要用凳子）。</summary>
        public bool StandingOnStool
        {
            get
            {
                var s = world.V2StoolMovable;
                if (s == null || PlayerFeet == null) return false;
                var p = PlayerFeet();
                float top = s.transform.position.y + s.TopOffset;
                var d = new Vector2(p.x - s.transform.position.x, p.z - s.transform.position.z);
                return p.y > top - 0.08f && d.magnitude < 0.5f;
            }
        }
        private bool StandingOnBox
        {
            get
            {
                var b = world.V2MovableBox;
                if (b == null || PlayerFeet == null) return false;
                var p = PlayerFeet();
                var d = new Vector2(p.x - b.transform.position.x, p.z - b.transform.position.z);
                return p.y > b.transform.position.y + b.TopOffset - 0.08f && d.magnitude < 0.5f;
            }
        }
        /// <summary>看得到裁剪桌的桌面：青春期、成人直接看得到；幼年要站在桌邊的凳子上。</summary>
        public bool CanSeeTable
        {
            get
            {
                if (SewAge != 0) return true;
                if (!StandingOnStool || PlayerFeet == null) return false;
                var p = PlayerFeet(); var c = world.V2CoatTableCenter;
                return new Vector2(p.x - c.x, p.z - c.z).magnitude < 1.5f;
            }
        }
        private bool seeTableWarned;
        public int RepairMoments { get; private set; }

        private readonly LanternGraybox world;
        private readonly LanternRig rig;
        private readonly Camera cam;
        private readonly LanternFlow flow;
        private readonly LanternAudio audio;
        private bool tapWas;

        public LanternV2Puzzles(LanternGraybox world, LanternRig rig, Camera cam, LanternFlow flow, LanternAudio audio)
        {
            this.world = world; this.rig = rig; this.cam = cam; this.flow = flow; this.audio = audio;
            Rebind();
        }

        /// <summary>新房子重建後重新接上謎題物件。</summary>
        public void Rebind()
        {
            Powered = TagRead = HasThread = Sewn = ShopRepaired = false;
            LanternPlaced = Seated = SeatMoveRequested = lanternAwayWarned = SewingStarted = false;
            Coat = CoatState.OnPeg; CoatOpenProgress = 0f; Balance.Cancel(); seeTableWarned = false;
            rig.PlacedAt = null;
            TagFocus = 0f;
            Notebook.Clear();
            Notebook.Add("她的外套，我又改了一次。");
            Tap.Cancel();
            if (world.V2SewingMachine != null)
            {
                var u = world.V2SewingMachine.gameObject.GetComponent<LanternUsable>() ?? world.V2SewingMachine.gameObject.AddComponent<LanternUsable>();
                u.Label = "縫紉機";
                u.PromptFn = SewingPrompt;
                u.OnUse = UseSewing;
                if (!world.Usables.Contains(u)) world.Usables.Add(u);
            }
            if (world.V2ThreadSpool != null)
            {
                world.V2ThreadSpool.gameObject.SetActive(true);
                var u = world.V2ThreadSpool.gameObject.GetComponent<LanternUsable>() ?? world.V2ThreadSpool.gameObject.AddComponent<LanternUsable>();
                u.Label = "一捲紅線";
                u.PromptFn = () => "E 拿起一捲紅線";
                u.OnUse = () =>
                {
                    HasThread = true;
                    world.V2ThreadSpool.gameObject.SetActive(false);
                    Note("洗衣籃裡有一捲紅線。她的縫紉機用的就是這種。");
                    flow.ShowToast("（拿到一捲紅線）", 2.5f);
                    audio?.Play(LanternAudio.Grab, 0.6f);
                };
                if (!world.Usables.Contains(u)) world.Usables.Add(u);
            }
            if (ShopEcho != null) { world.Usables.Remove(ShopEcho.Replay); Object.DestroyImmediate(ShopEcho.gameObject); }
            ShopEcho = null;
            BindShopInteractables();
        }

        /// <summary>改衣店的互動物件建好之後（套用正式布局後）接上：放在桌上的提燈可以拿回來。</summary>
        public void BindShopInteractables()
        {
            if (world.V2CoatUse != null) { world.V2CoatUse.PromptFn = CoatPrompt; world.V2CoatUse.OnUse = UseCoat; }
            if (world.V2CoatTableUse != null) { world.V2CoatTableUse.PromptFn = TablePrompt; world.V2CoatTableUse.OnUse = () => { if (Coat == CoatState.Held) PlaceCoatOnTable(); }; }
            ShowCoat();
            if (world.V2PlacedLanternUse != null)
            {
                world.V2PlacedLanternUse.OnUse = PickUpLantern;
                world.V2PlacedLanternUse.PromptFn = () => Seated ? "" : CoatHeld ? "（手上抱著外套，拿不了提燈）" : "E 把提燈拿回來";
            }
            if (world.V2PlacedLantern != null) world.V2PlacedLantern.gameObject.SetActive(LanternPlaced);
        }

        private string CoatPrompt()
        {
            if (Balance.Active) return "";
            if (Coat == CoatState.Held) return "E 把外套掛回去";
            if (Coat != CoatState.OnPeg) return "";
            if (SewAge == 0 && !StandingOnStool) return StandingOnBox ? "（還是搆不到……紙箱不夠高）" : "（外套掛得太高了，也太重了）";
            if (!LanternPlaced) return "E 先把提燈放下（兩隻手才抱得動外套）";
            if (SewAge == 2) return "E 取下那件外套";
            if (SewAge == 1) return "E 踮起腳，取下那件外套";
            return "E 試著取下外套（要站穩）";
        }

        private string TablePrompt()
        {
            if (Coat == CoatState.Held) return SewAge == 0 ? "E 把外套丟到" + world.V2CoatTableUse.Label + "上" : "E 把外套攤在" + world.V2CoatTableUse.Label + "上";
            if (Coat == CoatState.OnTable && !CanSeeTable) return "（看不到桌上……要站高一點）";
            return "";
        }

        /// <summary>把提燈放在地上（取外套前；使用者 2026-11-17：和縫紉一樣，手要空出來，不然會穿模）。</summary>
        private void PutLanternDown()
        {
            if (cam == null) return;
            var f = cam.transform.forward; f.y = 0f; f = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            var r = Vector3.Cross(Vector3.up, f);
            var feet = PlayerFeet != null ? PlayerFeet() : cam.transform.position - Vector3.up * 1.5f;
            // 腳邊、右手邊的地上；離紙箱、凳子遠一點（不然之後很難對準提燈），右邊不行就放左邊、後面。
            Vector3 best = feet + r * 0.5f; float bestClear = -1f;
            foreach (var off in new[] { r * 0.5f + f * 0.1f, -r * 0.5f + f * 0.1f, r * 0.45f - f * 0.35f, -r * 0.45f - f * 0.35f, -f * 0.55f })
            {
                var p = feet + off;
                float clear = 9f;
                foreach (var m in world.Movables) if (m != null) clear = Mathf.Min(clear, new Vector2(p.x - m.transform.position.x, p.z - m.transform.position.z).magnitude);
                if (clear > bestClear) { bestClear = clear; best = p; }
                if (clear > 0.6f) break;
            }
            var from = new Vector3(best.x, feet.y + 0.3f, best.z);
            float y = Physics.Raycast(from, Vector3.down, out var hit, 4f, ~(1 << LanternRig.ViewLayer), QueryTriggerInteraction.Ignore) ? hit.point.y : feet.y;
            PlaceLanternAt(new Vector3(best.x, y, best.z), "（把提燈放在腳邊，兩隻手空了出來）");
        }

        private void UseCoat()
        {
            if (Balance.Active) return;
            if (Coat == CoatState.Held) { Coat = CoatState.OnPeg; ShowCoat(); audio?.Play(LanternAudio.Drop, 0.4f); return; }
            if (Coat != CoatState.OnPeg) return;
            if (SewAge == 0 && !StandingOnStool)
            {
                flow.ShowToast(StandingOnBox ? "（踮起腳也搆不到。紙箱太矮了……要更高的東西，像那張凳子）" : "（外套掛得太高了，也太重了。要是站得高一點……）", 3f);
                return;
            }
            if (!LanternPlaced) { PutLanternDown(); return; }
            if (SewAge == 2) { TakeCoat(); return; }
            if (SewAge == 1) Balance.Begin(0.42f, 2f, 0.8f);
            else Balance.Begin(0.26f, 3f, 1.35f);
            flow.ShowToast(SewAge == 0 ? "（外套好重……用 A／D 站穩）" : "（踮著腳……用 A／D 保持平衡）", 3f);
        }

        private void TakeCoat()
        {
            Coat = CoatState.Held;
            ShowCoat();
            audio?.Play(LanternAudio.Grab, 0.6f);
            flow.ShowToast("（取下了那件外套。找個地方攤開來看看）", 3f);
        }

        private void PlaceCoatOnTable()
        {
            Coat = CoatState.OnTable;
            CoatOpenProgress = 0f;
            seeTableWarned = false;
            ShowCoat();
            audio?.Play(LanternAudio.Placed, 0.5f);
            flow.ShowToast(SewAge == 0 ? "（用力把外套丟上桌子。從下面看不到桌上……）" : "（外套攤在桌上。去拿提燈來照照看）", 3f);
        }

        /// <summary>依目前的狀態顯示／隱藏：牆上的外套、手上的外套、桌上的外套（合起來／打開）。</summary>
        private void ShowCoat()
        {
            var peg = world.V2CoatUse != null ? world.V2CoatUse.transform : null;
            if (peg != null) foreach (var r in peg.GetComponentsInChildren<Renderer>(true)) r.enabled = Coat == CoatState.OnPeg;
            if (world.V2CoatHeld != null)
            {
                bool held = Coat == CoatState.Held && cam != null;
                world.V2CoatHeld.gameObject.SetActive(held);
                if (held && world.V2CoatHeld.parent != cam.transform)
                {
                    world.V2CoatHeld.SetParent(cam.transform, false);
                    world.V2CoatHeld.localPosition = new Vector3(0.04f, -0.31f, 0.56f);  // 抱在胸前：畫面下方看得到
                    world.V2CoatHeld.localRotation = Quaternion.Euler(-50f, 0f, 0f);     // 朝向自己，看得到外套的正面
                    bool folded = world.V2CoatHeld.Find("v3_coat_folded") != null;
                    world.V2CoatHeld.localScale = Vector3.one * (folded ? 0.6f : 0.5f);   // 摺起來抱著
                    foreach (var t in world.V2CoatHeld.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LanternRig.ViewLayer;
                    foreach (var r in world.V2CoatHeld.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            if (world.V2CoatOnTable != null) world.V2CoatOnTable.gameObject.SetActive(Coat == CoatState.OnTable);
            if (world.V2CoatOpened != null) world.V2CoatOpened.gameObject.SetActive(Coat == CoatState.Open);
        }

        /// <summary>任務清單（不排順序）。</summary>
        public IEnumerable<(string task, bool done)> ShopTasks
        {
            get
            {
                yield return ("讓店裡有電", Powered);
                yield return ("看看那件外套裡面", TagRead);
                yield return ("找到縫紉機用的線", HasThread);
                yield return ("用縫紉機把外套改好", Sewn);
            }
        }

        private bool StoolAtMachine => world.V2StoolMovable == null ||
            Vector2.Distance(new Vector2(world.V2StoolMovable.transform.position.x, world.V2StoolMovable.transform.position.z), new Vector2(world.V2SeatSpot.x, world.V2SeatSpot.z)) < 0.9f;

        private string SewingPrompt()
        {
            if (Sewn) return "（縫紉機停了下來）";
            if (Seated) return "";
            if (!HasThread) return "E 檢查縫紉機（少了線）";
            if (LanternPlaced && !LanternAtSewTable) return "（提燈放在別的地方，先去拿回來）";
            if (!LanternPlaced) return "E 把提燈放在縫紉機桌上";
            if (!StoolAtMachine) return "（凳子不在縫紉機前面）";
            return SewAge == 0 ? "E 爬上凳子" : "E 拉開凳子坐下";
        }

        private void PlaceLantern() => PlaceLanternAt(world.V2SewLanternSpot, "（把提燈放在桌上，兩隻手空了出來）");

        private void PlaceLanternAt(Vector3 spot, string toast)
        {
            LanternPlaced = true;
            LanternSpot = spot;
            lanternAwayWarned = false;
            rig.PlacedAt = spot + Vector3.up * 0.12f;
            if (world.V2PlacedLantern != null) { world.V2PlacedLantern.position = spot; world.V2PlacedLantern.gameObject.SetActive(true); }
            Physics.SyncTransforms();
            audio?.Play(LanternAudio.Placed, 0.5f);
            flow.ShowToast(toast, 2.5f);
        }

        public void PickUpLantern()
        {
            if (!LanternPlaced || Seated) return;
            if (CoatHeld) { flow.ShowToast("（手上抱著外套，拿不了提燈）", 2f); return; }
            LanternPlaced = false;
            rig.PlacedAt = null;
            if (world.V2PlacedLantern != null) world.V2PlacedLantern.gameObject.SetActive(false);
            audio?.Play(LanternAudio.Grab, 0.5f);
        }

        private void SitDown()
        {
            Seated = true;
            SewingStarted = false;
            SeatMoveRequested = true;   // 玩家那一側播「爬上／坐上凳子」的動作，播完才呼叫 BeginSewing
            var s = world.V2StoolMovable;
            if (s != null)
            {
                var rb = s.Tether != null ? s.Tether.Body : null;
                if (rb != null) { rb.velocity = Vector3.zero; rb.isKinematic = true; }
                s.transform.SetPositionAndRotation(s.HomePosition, s.HomeRotation);   // 把凳子拉正
                s.Box.enabled = false;
            }
        }

        /// <summary>坐好之後才開始穿針（使用者 2026-11-16：要先坐上去，才開始判定）。</summary>
        public void BeginSewing()
        {
            if (!Seated || SewingStarted) return;
            SewingStarted = true;
            // 穿針難度：幼年最難（判定窄、指針快、要多穿一次），成人最容易；有電（縫紉機的小燈亮著）再寬一點。
            float zone = SewAge == 0 ? 0.10f : SewAge == 1 ? 0.13f : 0.17f;
            Tap.ZoneWidth = zone + (Powered ? 0.03f : 0f);
            Tap.Speed = SewAge == 0 ? 1.2f : SewAge == 1 ? 1.0f : 0.85f;
            Tap.Need = SewAge == 0 ? 4 : 3;
            Tap.Begin();
            tapWas = true;   // 開始那一下不算一次點按。
            string feel = SewAge == 0 ? "（腳只搆得到踏板的邊，要用整個身體去踩……指針進到亮的那一段時，按 E 穿針）"
                        : SewAge == 1 ? "（腳剛好踩得到踏板。指針進到亮的那一段時，按 E 穿針）"
                        : "（熟悉的節奏。指針進到亮的那一段時，按 E 穿針）";
            flow.ShowToast(Powered ? feel : feel + "\n（沒有電，縫紉機的小燈不亮，看不太清楚針眼）", 4f);
        }

        /// <summary>站起來（縫好了、或中途離開）：凳子恢復碰撞，提燈拿回手上。</summary>
        public void StandUp()
        {
            if (!Seated) return;
            Seated = false;
            SeatMoveRequested = true;
            Tap.Cancel();
            SewingStarted = false;
            if (world.V2StoolMovable != null)
            {
                world.V2StoolMovable.Box.enabled = true;
                var rb = world.V2StoolMovable.Tether != null ? world.V2StoolMovable.Tether.Body : null;
                if (rb != null) rb.isKinematic = false;
            }
            if (LanternPlaced) { LanternPlaced = false; rig.PlacedAt = null; if (world.V2PlacedLantern != null) world.V2PlacedLantern.gameObject.SetActive(false); }
        }

        private void Note(string line) { if (!Notebook.Contains(line)) Notebook.Add(line); }

        private void UseSewing()
        {
            if (Sewn || Seated) return;
            if (!HasThread) { Note("縫紉機沒有線。"); flow.ShowToast("（縫紉機上沒有線……）", 3f); return; }
            if (LanternPlaced && !LanternAtSewTable) { flow.ShowToast("（提燈放在別的地方。先去拿回來）", 2.5f); return; }
            if (!LanternPlaced) { PlaceLantern(); return; }
            if (!StoolAtMachine) { flow.ShowToast("（凳子被搬走了。得先把它搬回縫紉機前面）", 3f); return; }
            SitDown();
        }

        /// <summary>每個影格。tapInput＝玩家這一格按著 E（或左鍵）。</summary>
        public void Step(float dt, bool tapInput, bool skip)
        {
            if (world.V2Root == null) return;
            // S1 電箱。
            if (!Powered && world.V2PowerLever != null && world.V2PowerLever.Done)
            {
                Powered = true;
                world.V2Powered = true;
                Note("電來了。燈不再閃了，可是光是冷的，店裡還是灰的。");
                flow.ShowToast("（電來了，燈穩了下來。光是冷的，店裡還是灰的）", 3.5f);
            }
            // S2 那件外套：保持平衡 → 取下 → 攤在桌上 → 提燈照，外套打開 → 近看內側的標籤。
            if (Balance.Active)
            {
                int res = Balance.Step(dt, BalanceInput);
                if (res > 0) { TakeCoat(); flow.ShowToast(SewAge == 0 ? "（站穩了……把外套抱了下來。好重）" : "（踮著腳，把外套取了下來）", 3f); }
                else if (res < 0) { audio?.Play(LanternAudio.Drop, 0.5f); flow.ShowToast(SewAge == 0 ? "（腳下一晃，差點摔下來。外套還掛在那裡）" : "（腳一軟，沒拿到）", 3f); }
            }
            if (Coat == CoatState.OnTable && world.V2CoatOnTable != null)
            {
                var c = world.V2CoatTableCenter + Vector3.up * 0.05f;
                bool lit = (rig.On || rig.PlacedAt.HasValue) && LanternReveal.IsReal(c);
                if (lit && !CanSeeTable)
                {
                    // 幼年站在地上：提燈照得到，可是看不到桌上（使用者 2026-11-17：要踩凳子才能照）。
                    lit = false;
                    if (!seeTableWarned) { seeTableWarned = true; flow.ShowToast("（桌子太高，看不到上面的外套。要站在凳子上才看得到）", 3f); }
                }
                CoatOpenProgress = lit ? CoatOpenProgress + dt : Mathf.Max(0f, CoatOpenProgress - dt * 0.5f);
                if (CoatOpenProgress >= 1f)
                {
                    Coat = CoatState.Open;
                    ShowCoat();
                    LanternReveal.AddBubble(c, 0.9f, 1.8f);   // 和提燈照出東西一樣的發光邊緣
                    audio?.Play(LanternAudio.RestoreDone, 0.7f);
                    flow.ShowToast("（提燈的光一照，外套自己攤開了。內側縫著一張標籤）", 4f);
                }
            }
            if (Coat == CoatState.Open && !TagRead && world.V2CoatLabel != null && world.V2CoatLabel.Opened > 0)
            {
                TagRead = true;
                TagFocus = 1f;
                Note("標籤上沒有名字。只有一行一行被塗改的尺寸，每一年都比前一年大一點。");
                Note("每件衣服都有名字。只有這件沒有。");
                audio?.Play(LanternAudio.Placed, 0.6f);
            }
            // S4 穿針（點按判定）。
            bool tap = tapInput && !tapWas;
            tapWas = tapInput;
            if (world.V2SewingLamp != null) world.V2SewingLamp.enabled = Powered;
            if (Seated && SewingStarted && !Tap.Active) StandUp();   // 縫好了、或穿針被取消
            if (LanternPlaced && !Seated && !lanternAwayWarned && cam != null && Vector3.Distance(cam.transform.position, LanternSpot) > 4.5f)
            {
                lanternAwayWarned = true;
                flow.ShowToast(LanternAtSewTable ? "（提燈還放在縫紉機的桌上）" : "（提燈還放在地上）", 3f);
            }
            if (Tap.Active)
            {
                if (Tap.Step(dt, tap))
                {
                    Sewn = true;
                    Note("縫紉機自己動了起來，在布上縫出一筆——像是一個名字的開頭。");
                    flow.ShowToast(SewAge == 0 ? "（腳都踩痠了。縫紉機終於動了起來……縫出了一筆）" : "（縫紉機動了起來……縫出了一筆）", 3.5f);
                    audio?.Play(LanternAudio.RestoreDone, 0.8f);
                }
                else if (tap)
                {
                    audio?.Play(Tap.LastWasHit ? LanternAudio.Toggle : LanternAudio.Drop, 0.5f);
                    if (!Tap.LastWasHit && SewAge == 0 && Tap.Misses % 2 == 1) flow.ShowToast("（踏板好重，踩空了一下）", 1.8f);
                }
            }
            // 店面的事都做完了（不排順序）→ 完成時刻。
            if (!ShopRepaired && Powered && TagRead && Sewn) RepairShop();
            if (ShopEcho != null)
            {
                if (ShopEcho.Armed)
                {
                    // 看過去才開始演；太久沒看，再提醒一次。
                    armedTime += dt;
                    if (EchoInView()) ShopEcho.Play();
                    else if (armedTime > 12f && !reminded) { reminded = true; flow.ShowToast("（鏡子旁邊的牆上，好像有人影……）", 4f); }
                }
                if (ShopEcho.Playing) ShopEcho.Step(dt, skip);
            }
        }

        private float armedTime;
        private bool reminded;

        /// <summary>牆上的影子在畫面中間附近、不太遠。</summary>
        public bool EchoInView()
        {
            if (ShopEcho == null || cam == null) return false;
            var d = ShopEcho.transform.position - cam.transform.position;
            return d.magnitude < 9f && Vector3.Angle(cam.transform.forward, d) < 30f;
        }

        /// <summary>店面還差幾件事（筆記用，不寫答案）。</summary>
        public int ShopRemaining => (Powered ? 0 : 1) + (TagRead ? 0 : 1) + (Sewn ? 0 : 1);

        public bool InputCaptured => Tap.Active || Seated || Balance.Active;

        /// <summary>截圖與除錯用：直接設定進度（下一格 Step 會照常觸發修復時刻）。</summary>
        public void DebugForce(bool power, bool tag, bool thread, bool sewn)
        {
            if (power) { Powered = true; world.V2Powered = true; }
            if (tag) { TagRead = true; TagFocus = 1f; }
            if (thread) { HasThread = true; if (world.V2ThreadSpool != null) world.V2ThreadSpool.gameObject.SetActive(false); }
            if (sewn) Sewn = true;
        }

        private void RepairShop()
        {
            ShopRepaired = true;
            RepairMoments++;
            // 不再讓整間店永久恢復真實（使用者 2026-11-16）：店裡維持褪色，只有提燈照到的地方是記憶中的樣子。
            var safe = world.V2ShopRoom;
            safe.Expand(new Vector3(-0.3f, 0f, -0.3f));
            world.SafeRooms.Add(safe);
            audio?.Play(LanternAudio.RestoreDone, 1f);
            Note("外套改好了。吊牌上那一行行尺寸，和布上縫出的那一筆對上了。牆上，有兩個人的影子。");
            flow.ShowToast("（外套改好了……鏡子旁邊的牆上，有影子在動）", 4.5f);
            armedTime = 0f; reminded = false;
            ShopEcho = LanternEcho.Create(world.V2Root, world.V2ShopEchoAt, world.V2ShopEchoRot, new[]
            {
                new LanternEcho.Beat { T = 0.5f, MotherX = 0.9f, GirlX = -0.35f, MotherPose = 0, GirlPose = 0, Who = "媽媽", Line = "（暫定）站好，別動。" },
                new LanternEcho.Beat { T = 3.5f, MotherX = 0.25f, GirlX = -0.35f, MotherPose = 1, GirlPose = 0, Who = "媽媽", Line = "又長高了。" },
                new LanternEcho.Beat { T = 7.0f, MotherX = 0.25f, GirlX = -0.45f, MotherPose = 1, GirlPose = 1, Who = "女兒", Line = "媽，你都用看的，不用尺嗎？" },
                new LanternEcho.Beat { T = 10.5f, MotherX = 0.35f, GirlX = -0.45f, MotherPose = 0, GirlPose = 1, Who = "媽媽", Line = "我的眼睛比尺準。" },
                new LanternEcho.Beat { T = 14.0f, MotherX = 0.35f, GirlX = -0.3f, MotherPose = 0, GirlPose = 0, Who = "女兒", Line = "那明年呢？" },
                new LanternEcho.Beat { T = 17.0f, MotherX = 0.45f, GirlX = -0.3f, MotherPose = 1, GirlPose = 0, Who = "媽媽", Line = "明年再改。" },
            }, 21f);
            ShopEcho.Arm();
            if (!world.Usables.Contains(ShopEcho.Replay)) world.Usables.Add(ShopEcho.Replay);
        }
    }
}

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
    /// 新房子一樓第一批謎題（設計見 docs/level/CASE01_GROUND_FLOOR_ESCAPE_ROOM_V0_1.md）：
    /// S1 電箱、S2 外套吊牌（聚光）、Y1 側院的紅線、S4 縫紉機穿針（點按判定）、秘密（衣架後的照片）。
    /// 店面修好（S2＋S4；第二批再加 S3 訂單簿）→ 修復時刻：真實擴散、暖燈常亮、牆上的影子殘影、筆記更新。
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
            TagFocus = 0f;
            Notebook.Clear();
            Notebook.Add("她的外套，我又改了一次。");
            Tap.Cancel();
            if (world.V2SewingMachine != null)
            {
                var u = world.V2SewingMachine.gameObject.GetComponent<LanternUsable>() ?? world.V2SewingMachine.gameObject.AddComponent<LanternUsable>();
                u.Label = "縫紉機";
                u.PromptFn = () => Sewn ? "（縫紉機停了下來）" : !Powered ? "E 踩踏板（沒有電，燈也不亮）" : !HasThread ? "E 檢查縫紉機（少了線）" : "E 穿針";
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
            if (ShopEcho != null) Object.DestroyImmediate(ShopEcho.gameObject);
            ShopEcho = null;
        }

        private void Note(string line) { if (!Notebook.Contains(line)) Notebook.Add(line); }

        private void UseSewing()
        {
            if (Sewn) return;
            if (!Powered) { flow.ShowToast("（踏板踩不動。櫃台後面好像有電箱）", 3f); return; }
            if (!HasThread) { Note("縫紉機沒有線。"); flow.ShowToast("（縫紉機上沒有線……）", 3f); return; }
            Tap.Begin();
            tapWas = true; // 開始那一下不算一次點按。
            flow.ShowToast("（指針進到亮的那一段時，按 E 穿針）", 3f);
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
                Note("電來了。燈亮了，照出來的卻是一間廢墟。");
                flow.ShowToast("（電來了——店裡的燈穩了下來）", 3f);
            }
            // S2 外套吊牌：聚光照著它。
            if (!TagRead && world.V2CoatTag != null && cam != null)
            {
                var d = world.V2CoatTag.position - cam.transform.position;
                if (rig.FocusBlend > 0.7f && d.magnitude < 4.5f && Vector3.Angle(cam.transform.forward, d) < 12f)
                {
                    TagFocus = Mathf.Clamp01(TagFocus + dt / 1.2f);
                    if (TagFocus >= 1f)
                    {
                        TagRead = true;
                        Note("吊牌上沒有名字。只有一行一行被塗改的尺寸，每一年都比前一年大一點。");
                        Note("每件衣服都有名字。只有這件沒有。");
                        flow.ShowToast("（吊牌上沒有名字……只有一行一行被塗改的尺寸）", 4f);
                        audio?.Play(LanternAudio.Placed, 0.6f);
                    }
                }
            }
            // S4 穿針（點按判定）。
            bool tap = tapInput && !tapWas;
            tapWas = tapInput;
            if (Tap.Active && cam != null && world.V2SewingMachine != null && Vector3.Distance(cam.transform.position, world.V2SewingMachine.position) > 3.2f)
                Tap.Cancel(); // 走開就放棄穿針。
            if (Tap.Active)
            {
                if (Tap.Step(dt, tap))
                {
                    Sewn = true;
                    Note("縫紉機自己動了起來，在布上縫出一筆——像是一個名字的開頭。");
                    flow.ShowToast("（縫紉機動了起來……縫出了一筆）", 3.5f);
                    audio?.Play(LanternAudio.RestoreDone, 0.8f);
                }
                else if (tap) audio?.Play(Tap.LastWasHit ? LanternAudio.Toggle : LanternAudio.Drop, 0.5f);
            }
            // 店面修好 → 修復時刻。
            if (!ShopRepaired && TagRead && Sewn) RepairShop();
            if (ShopEcho != null && ShopEcho.Playing) ShopEcho.Step(dt, skip);
        }

        public bool InputCaptured => Tap.Active;

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
            var seed = world.V2SewingMachine != null ? world.V2SewingMachine.position + Vector3.up : world.V2ShopRoom.center;
            LanternReveal.AddRoom(world.V2ShopRoom, seed);
            foreach (var l in world.V2ShopLamps) if (l != null) world.V2SafeLamps.Add(l);
            var safe = world.V2ShopRoom;
            safe.Expand(new Vector3(-0.3f, 0f, -0.3f));
            world.SafeRooms.Add(safe);
            audio?.Play(LanternAudio.RestoreDone, 1f);
            Note("店變回了原本的樣子。牆上，有兩個人的影子。");
            flow.ShowToast("（店變回了原本的樣子）", 3f);
            ShopEcho = LanternEcho.Create(world.V2Root, world.V2ShopEchoAt, world.V2ShopEchoRot, new[]
            {
                new LanternEcho.Beat { T = 0.5f, MotherX = 0.9f, GirlX = -0.35f, MotherPose = 0, GirlPose = 0, Line = "（暫定）站好，別動。" },
                new LanternEcho.Beat { T = 3.5f, MotherX = 0.25f, GirlX = -0.35f, MotherPose = 1, GirlPose = 0, Line = "又長高了。" },
                new LanternEcho.Beat { T = 7.0f, MotherX = 0.25f, GirlX = -0.45f, MotherPose = 1, GirlPose = 1, Line = "媽，你都用看的，不用尺嗎？" },
                new LanternEcho.Beat { T = 10.5f, MotherX = 0.35f, GirlX = -0.45f, MotherPose = 0, GirlPose = 1, Line = "我的眼睛比尺準。" },
                new LanternEcho.Beat { T = 14.0f, MotherX = 0.35f, GirlX = -0.3f, MotherPose = 0, GirlPose = 0, Line = "那明年呢？" },
                new LanternEcho.Beat { T = 17.0f, MotherX = 0.45f, GirlX = -0.3f, MotherPose = 1, GirlPose = 0, Line = "明年再改。" },
            }, 21f);
            ShopEcho.Play();
        }
    }
}

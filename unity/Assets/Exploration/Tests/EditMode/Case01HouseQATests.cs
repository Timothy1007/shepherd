using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Shepherd.Battle.Core;
using Shepherd.Battle.UI;
using Shepherd.Exploration.Case01;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// 〈第十七號門〉室內 QA 場景：用真正的移動輸入（WASD／滑鼠視角）在碰撞體中走動、以準星＋E 調查，
    /// 驗證房間與門洞可通行、錯接門的安全傳送、異域反制 → 既有卡牌戰鬥 → 戰後修復 → 出口。
    /// </summary>
    public sealed class Case01HouseQATests
    {
        private const float Dt = 1f / 30f;
        private readonly List<GameObject> roots = new List<GameObject>();

        private Case01HouseQA NewQa(CombatLabScenario scenario = null, int? seed = null, Case01Checkpoint? cp = null)
        {
            var go = new GameObject("Case01HouseQATest");
            roots.Add(go);
            var qa = go.AddComponent<Case01HouseQA>();
            qa.Build(false, scenario, seed, cp);
            return qa;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r);
            roots.Clear();
        }

        private static CombatLabScenario QuickVictory()
        {
            var s = new CombatLabScenario { Name = "quick-victory" };
            s.Enemies.Add(new EnemyState { Id = 1, Name = "E1 失序體・甲", Value = 1, Corrections = 1, AttackPattern = new[] { 12 } });
            s.Enemies.Add(new EnemyState { Id = 2, Name = "E2 失序體・乙", Value = 1, Corrections = 1, AttackPattern = new[] { 6 } });
            return s;
        }

        private static CombatLabScenario QuickDefeat()
        {
            var s = new CombatLabScenario { Name = "quick-defeat" };
            s.Enemies.Add(new EnemyState { Id = 1, Name = "E1 失序體・甲", Value = 500, AttackPattern = new[] { 999 } });
            return s;
        }

        // ---------------- 腳本化輸入 ----------------

        private static void Tick(Case01HouseQA qa, Case01Input input) { qa.Tick(Dt, input); }

        private static void Run(Case01HouseQA qa, float seconds)
        {
            for (float t = 0; t < seconds; t += Dt) qa.Tick(Dt, Case01Input.None);
        }

        private static void RunUntil(Case01HouseQA qa, System.Func<bool> done, string what, float maxSeconds = 20f)
        {
            for (float t = 0; t < maxSeconds && !done(); t += Dt) qa.Tick(Dt, Case01Input.None);
            Assert.IsTrue(done(), "逾時：" + what + "（模式 " + qa.Mode + "，位置 " + qa.Player.transform.position + "）");
        }

        private static void Settle(Case01HouseQA qa) { RunUntil(qa, () => qa.Mode == Case01Mode.Exploring, "傳送結束", 5f); }

        /// <summary>把視線轉向目標所需的滑鼠位移（與 FirstPersonController 的換算相同）。</summary>
        private static Vector2 LookDelta(FirstPersonController p, Vector3 target, bool level)
        {
            var d = target - p.EyePosition;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = level ? 0f : -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            float dy = Mathf.DeltaAngle(p.Yaw, yaw);
            float dp = pitch - p.Pitch;
            return new Vector2(dy / FirstPersonController.LookSensitivity, -dp / FirstPersonController.LookSensitivity);
        }

        /// <summary>用 W＋滑鼠走到目標（水平距離 0.25 m 內）；若途中觸發傳送則提前結束並回傳 false。</summary>
        private static bool WalkTo(Case01HouseQA qa, float x, float z, float maxSeconds = 12f)
        {
            var target = new Vector3(x, 0f, z);
            for (float t = 0; t < maxSeconds; t += Dt)
            {
                if (qa.Mode != Case01Mode.Exploring) return false;
                var p = qa.Player.transform.position;
                var flat = new Vector2(target.x - p.x, target.z - p.z);
                if (flat.magnitude < 0.25f) return true;
                var aim = new Vector3(x, qa.Player.EyePosition.y, z);
                Tick(qa, Case01Input.From(new ExploreInput { Move = new Vector2(0f, 1f), Look = LookDelta(qa.Player, aim, true) }));
            }
            Assert.Fail("走不到 (" + x + ", " + z + ")，卡在 " + qa.Player.transform.position + "，房間 " + qa.CurrentRoom);
            return false;
        }

        /// <summary>走向門洞另一側：必須觸發傳送，並等傳送完成。</summary>
        private static void WalkThrough(Case01HouseQA qa, float x, float z, Case01Room expect)
        {
            Assert.IsFalse(WalkTo(qa, x, z), "應該在門洞觸發傳送");
            Settle(qa);
            Assert.AreEqual(expect, qa.CurrentRoom, "通路的目的地");
        }

        private static void Aim(Case01HouseQA qa, string id)
        {
            var it = qa.Layout.Interactables[id];
            var c = it.GetComponent<BoxCollider>();
            var target = it.transform.TransformPoint(c.center);
            for (int i = 0; i < 2; i++) Tick(qa, Case01Input.From(new ExploreInput { Look = LookDelta(qa.Player, target, false) }));
        }

        /// <summary>準星對準物件並按 E。</summary>
        private static void Interact(Case01HouseQA qa, string id)
        {
            Aim(qa, id);
            Assert.IsNotNull(qa.CurrentTarget, "準星對到 " + id + "（位置 " + qa.Player.transform.position + "）");
            Assert.AreEqual(id, qa.CurrentTarget.Id);
            // 同一物件的面板還開著：第一次 E 是關閉。
            if (qa.Hud.PanelVisible && qa.PanelSourceId == id) Tick(qa, Case01Input.From(new ExploreInput { Interact = true }));
            Tick(qa, Case01Input.From(new ExploreInput { Interact = true }));
        }

        private static void Choose(Case01HouseQA qa, int n)
        {
            Assert.IsNotNull(qa.Hud.ActiveChoice, "有選項可選");
            Tick(qa, new Case01Input { Choice = n });
        }

        private static void CloseText(Case01HouseQA qa)
        {
            if (qa.Hud.PanelVisible) Tick(qa, Case01Input.From(new ExploreInput { Interact = true }));
        }

        // 常用路線（座標見 Case01HouseBuilder）。
        private static void EntranceToDining(Case01HouseQA qa)
        {
            WalkTo(qa, 6.5f, 4.4f);
            WalkTo(qa, 6.5f, 5.8f);
            WalkTo(qa, 6.6f, 6.2f);
        }

        private static void DiningToEntrance(Case01HouseQA qa)
        {
            WalkTo(qa, 6.5f, 5.6f);
            WalkTo(qa, 6.5f, 4.2f);
        }

        private static void EntranceToLanding(Case01HouseQA qa)
        {
            WalkTo(qa, 8.2f, 1.35f);
            WalkTo(qa, 9.4f, 1.35f);
            WalkTo(qa, 9.4f, 4.3f);
            WalkTo(qa, 8.3f, 4.5f);
            WalkTo(qa, 6.95f, 3.6f);
            Assert.AreEqual(Case01Room.Landing, qa.CurrentRoom, "爬上樓梯到二樓平台");
            Assert.Greater(qa.Player.transform.position.y, 2.9f);
        }

        private static void LandingToStudy(Case01HouseQA qa)
        {
            WalkTo(qa, 6.95f, 4.3f);
            WalkTo(qa, 6.95f, 5.8f);
            Assert.AreEqual(Case01Room.Study, qa.CurrentRoom);
        }

        private static void StudyToLanding(Case01HouseQA qa)
        {
            WalkTo(qa, 6.95f, 6.0f);
            WalkTo(qa, 6.95f, 4.2f);
            Assert.AreEqual(Case01Room.Landing, qa.CurrentRoom);
        }

        private static void LandingToMezzanine(Case01HouseQA qa)
        {
            WalkTo(qa, 6.95f, 2.0f);
            WalkThrough(qa, 6.95f, -0.6f, Case01Room.Mezzanine);
        }

        // ---------------- 建立 ----------------

        [Test]
        public void Build_StartsAtEntrance_WithHouseRoomsDoorsAndInteractables()
        {
            var qa = NewQa();
            Assert.AreEqual(Case01Mode.Exploring, qa.Mode);
            Assert.AreEqual(Case01Room.Entrance, qa.CurrentRoom, "從一樓玄關開始");
            Assert.AreEqual(Case01Stage.EnteredHome, qa.Session.Stage);
            Assert.AreEqual(7, qa.Layout.Portals.Count);
            foreach (var id in new[]
                     {
                         Case01Ids.Table, Case01Ids.Photo, Case01Ids.SundayNote, Case01Ids.Tea, Case01Ids.Calendar, Case01Ids.FrontDoor, Case01Ids.Map,
                         Case01Ids.EllieNotes, Case01Ids.OldRecord, Case01Ids.Postcard, Case01Ids.StorageDoor, Case01Ids.StudyMark, Case01Ids.ThirdDoor,
                         Case01Ids.Core, Case01Ids.MezzTriangleMark, Case01Ids.MezzCircleMark, Case01Ids.Wall17,
                     })
                Assert.IsTrue(qa.Layout.Interactables.ContainsKey(id), id);
            foreach (var room in new[] { Case01Room.Entrance, Case01Room.Dining, Case01Room.Study, Case01Room.Landing, Case01Room.Mezzanine, Case01Room.Courtyard })
                Assert.AreEqual(room, qa.Layout.RoomAt(qa.Layout.Arrivals[room].Position), "到達點在正確的房間：" + room);
            Assert.IsTrue(qa.Layout.DiningSecondSet.activeSelf, "一開始有兩副餐具");
            Assert.IsFalse(qa.Layout.ThirdDoorFrame.activeSelf, "第三扇門還沒出現");
            Assert.IsFalse(qa.EncounterFiguresVisible, "開局不戰鬥");
            Assert.IsTrue(qa.Hud.Visible);
            StringAssert.Contains("QA", qa.Hud.QaText.text);
            StringAssert.Contains("F1", qa.Hud.Base.ViewMode.text);
            StringAssert.Contains("J 日誌", qa.Hud.Base.Controls.text);
        }

        [Test]
        public void Build_InTestMode_DoesNotChangeRenderSettings()
        {
            var fog = RenderSettings.fog;
            var ambient = RenderSettings.ambientLight;
            NewQa();
            Assert.AreEqual(fog, RenderSettings.fog);
            Assert.AreEqual(ambient, RenderSettings.ambientLight);
        }

        // ---------------- S00／S10 ----------------

        [Test]
        public void Dining_TwoSets_RewrittenOnlyWhenOutOfSight_RevisitShowsOneAndJournalKeepsTwo()
        {
            var qa = NewQa();
            EntranceToDining(qa);
            Assert.AreEqual(Case01Room.Dining, qa.CurrentRoom, "走得進餐廳");
            Interact(qa, Case01Ids.Table);
            StringAssert.Contains("兩人份", qa.Hud.Base.InspectText.text);
            Assert.IsTrue(qa.Session.Has(Case01Flags.TableTwo));
            Interact(qa, Case01Ids.Table);
            Interact(qa, Case01Ids.Table);
            Assert.AreEqual(1, qa.Session.TimesSet(Case01Flags.TableTwo));

            // 退到玄關但一直看著餐桌：不改寫。
            qa.Player.Teleport(new Vector3(6.5f, 0.05f, 3.6f), 0f);
            Run(qa, 0.3f);
            Assert.IsTrue(qa.TableVisible());
            Assert.IsTrue(qa.Flow.DiningRewritePending);
            Assert.IsTrue(qa.Layout.DiningSecondSet.activeSelf, "直視時不硬閃");
            // 轉身：改寫。
            qa.Player.Teleport(new Vector3(6.5f, 0.05f, 3.6f), 180f);
            Run(qa, 0.2f);
            Assert.IsFalse(qa.Layout.DiningSecondSet.activeSelf);
            Assert.IsTrue(qa.Layout.DiningTrace.activeSelf, "桌面痕跡不同");

            EntranceToDining(qa);
            Assert.IsFalse(qa.Hud.SubtitleVisible, "v0.4：第一章不使用同伴通訊");
            StringAssert.Contains("兩副餐具", qa.Hud.MonologueLog[qa.Hud.MonologueLog.Count - 1], "主角自語");
            Assert.AreEqual(Case01Stage.DiningContradiction, qa.Session.Stage);
            Interact(qa, Case01Ids.Table);
            StringAssert.Contains("只剩一副", qa.Hud.Base.InspectText.text);
            Assert.IsTrue(qa.Session.HasJournal("table_two"));

            Tick(qa, new Case01Input { Journal = true });
            Assert.IsTrue(qa.Hud.JournalVisible);
            StringAssert.Contains("兩副", qa.Hud.JournalText.text, "日誌可重讀原始觀測");
            Tick(qa, new Case01Input { Journal = true });
            Assert.IsFalse(qa.Hud.JournalVisible);
        }

        [Test]
        public void Stairs_AreWalkable_AndRoomsConnectByDoorways()
        {
            var qa = NewQa();
            WalkTo(qa, 5.8f, 2.5f);
            WalkTo(qa, 3.5f, 2.5f);
            Assert.AreEqual(Case01Room.Kitchen, qa.CurrentRoom);
            WalkTo(qa, 2.5f, 4.2f);
            WalkTo(qa, 2.5f, 6.4f);
            Assert.AreEqual(Case01Room.Living, qa.CurrentRoom, "廚房↔客廳");
            WalkTo(qa, 4.2f, 7.0f);
            WalkTo(qa, 4.2f, 7.5f);
            WalkTo(qa, 6.0f, 7.5f);
            Assert.AreEqual(Case01Room.Dining, qa.CurrentRoom, "客廳↔餐廳");
            WalkTo(qa, 6.5f, 6.0f);
            DiningToEntrance(qa);
            EntranceToLanding(qa);
            LandingToStudy(qa);
            WalkTo(qa, 5.8f, 7.5f);
            WalkTo(qa, 3.8f, 7.5f);
            Assert.AreEqual(Case01Room.Bedroom, qa.CurrentRoom, "書房↔臥室");
            // 走回一樓。
            WalkTo(qa, 5.8f, 7.5f);
            StudyToLanding(qa);
            WalkTo(qa, 8.3f, 4.5f);
            WalkTo(qa, 9.4f, 4.3f);
            WalkTo(qa, 9.4f, 1.3f);
            WalkTo(qa, 7.5f, 1.4f);
            Assert.AreEqual(Case01Room.Entrance, qa.CurrentRoom);
            Assert.Less(qa.Player.transform.position.y, 0.3f);
        }

        // ---------------- 完整流程 ----------------

        [Test]
        public void FullPlaythrough_FromEntranceToExit_WithRealMovementAndExistingBattle()
        {
            var qa = NewQa(QuickVictory(), 1);

            // S00／S10。
            EntranceToDining(qa);
            Interact(qa, Case01Ids.Table);
            WalkTo(qa, 8.8f, 6.0f);
            WalkTo(qa, 8.8f, 7.6f);
            Interact(qa, Case01Ids.Photo);
            Assert.IsTrue(qa.Session.Has(Case01Flags.PhotoObserved));
            WalkTo(qa, 8.8f, 6.0f);
            DiningToEntrance(qa);
            Run(qa, 0.1f);
            EntranceToDining(qa);
            Assert.IsTrue(qa.Session.Has(Case01Flags.TableRewritten));
            DiningToEntrance(qa);

            // S20：書房。
            EntranceToLanding(qa);
            Assert.IsFalse(qa.Flow.PortalActive(Case01Door.StudyDoor));
            LandingToStudy(qa);
            WalkTo(qa, 6.7f, 8.2f);
            Interact(qa, Case01Ids.Map);
            Interact(qa, Case01Ids.EllieNotes);
            StringAssert.Contains("追的是失序本身", qa.Hud.Base.InspectText.text, "手記：艾琳主動研究失序");
            StringAssert.Contains("不知道誰寄來的資料", qa.Hud.Base.InspectText.text, "收到不明來歷的引導資料");
            WalkTo(qa, 7.5f, 7.7f);
            WalkTo(qa, 8.7f, 7.5f);
            Interact(qa, Case01Ids.OldRecord);
            Assert.IsTrue(qa.Flow.WrongConnectionArmed);
            Assert.IsTrue(qa.Layout.ThirdDoorFrame.activeSelf, "第三扇門的門框浮現（視覺提示）");
            CloseText(qa);

            // S30：樓梯錯接。
            WalkTo(qa, 7.5f, 7.0f);
            StudyToLanding(qa);
            Assert.AreEqual(Case01Stage.WrongConnection, qa.Session.Stage);
            Assert.IsTrue(qa.Layout.Portal(Case01Door.StudyDoor).Membrane.activeSelf);
            WalkTo(qa, 6.95f, 4.3f);
            WalkThrough(qa, 6.95f, 5.8f, Case01Room.Dining);
            Assert.IsTrue(qa.Session.Has(Case01Flags.RouteMismatchSeen));
            Assert.Less(qa.Player.transform.position.y, 0.5f, "安全地落在一樓餐廳");
            DiningToEntrance(qa);
            EntranceToLanding(qa);
            WalkTo(qa, 6.3f, 3.9f);
            Interact(qa, Case01Ids.StudyMark);
            Choose(qa, 3);
            Assert.AreEqual(Case01Room.Entrance, qa.Flow.Destination(Case01Door.StudyDoor), "錯誤覆寫可以再改");
            Interact(qa, Case01Ids.StudyMark);
            Choose(qa, 1);
            Assert.IsTrue(qa.Flow.StairRouteRestored);
            Assert.IsFalse(qa.Layout.Portal(Case01Door.ThirdDoor).Seal.activeSelf, "第三扇門打開");
            CloseText(qa);
            LandingToMezzanine(qa);
            Assert.AreEqual(Case01Stage.MezzanineDiscovered, qa.Session.Stage);

            // S40：碰核 → 反制 → 中庭 → 既有戰鬥。
            WalkTo(qa, 40f, -0.4f);
            Interact(qa, Case01Ids.Core);
            Assert.AreEqual(Case01Mode.Defense, qa.Mode);
            RunUntil(qa, () => qa.Mode == Case01Mode.Exploring && qa.CurrentRoom == Case01Room.Courtyard && qa.Manifested, "被拉進異常中庭並看完失序體形成", 12f);
            Assert.IsTrue(qa.EncounterFiguresVisible);
            Assert.IsFalse(qa.Session.Has(Case01Flags.BattleStarted), "看到敵人還不算開戰");
            Assert.IsFalse(WalkTo(qa, 40f, 46f, 15f), "走近即遭遇");
            Assert.AreEqual(Case01Mode.EnteringBattle, qa.Mode);
            var returnPos = qa.Arena.transform.position;
            Assert.AreEqual(Case01HouseBuilder.EncounterAnchorPosition, returnPos, "戰場原點在中庭");
            RunUntil(qa, () => qa.Mode == Case01Mode.Battle, "鏡頭過場");
            var arena = qa.Arena;
            Assert.IsTrue(arena.ArenaCamera.enabled);
            Assert.IsFalse(qa.ExploreCamera.enabled);
            Assert.IsFalse(qa.Hud.Visible);
            Assert.AreEqual(7, arena.Session.Snapshot().Hand.Count, "沿用既有規則：7 張手牌");
            Assert.AreEqual(3, arena.Session.ApMax, "3 AP");
            arena.CommitButton.onClick.Invoke();
            RunUntil(qa, () => !arena.IsPlaying, "事件播放");
            Assert.AreEqual(BattleOutcome.Victory, arena.Session.Snapshot().Outcome);
            Assert.AreEqual("返回中庭", arena.BannerButtonText);
            arena.BannerButton.onClick.Invoke();
            RunUntil(qa, () => qa.Mode == Case01Mode.Exploring, "返回探索");
            Assert.IsTrue(qa.Arena == null);
            Assert.AreEqual(Case01Room.Courtyard, qa.CurrentRoom, "回到原本的探索位置");
            Assert.Greater(qa.Player.transform.position.z, 40f);
            Assert.AreEqual(1, qa.Session.TimesSet(Case01Flags.BattleWon));
            Assert.IsFalse(qa.EncounterFiguresVisible, "敵人不重生");
            Assert.IsFalse(qa.Flow.CoreStabilized, "戰勝不等於修復");
            Run(qa, 1f);
            Assert.AreEqual(1, qa.Encounter.TriggerCount, "不再觸發");

            // S50：回到核，三段修復。
            WalkTo(qa, 40f, 25.6f, 20f);
            WalkThrough(qa, 40f, 23.4f, Case01Room.Mezzanine);
            WalkTo(qa, 40f, -0.4f);
            Interact(qa, Case01Ids.Core);
            Assert.IsTrue(qa.Flow.Door17Open);
            Assert.IsFalse(qa.Layout.Portal(Case01Door.Mezz17).Seal.activeSelf, "牆上出現真正可走的門");
            CloseText(qa);
            Interact(qa, Case01Ids.Core);
            Assert.IsNull(qa.Hud.ActiveChoice, "先要親自確認通道");
            CloseText(qa);
            WalkTo(qa, 44f, 0f);
            WalkThrough(qa, 46.6f, 0f, Case01Room.Study);
            StudyToLanding(qa);
            LandingToMezzanine(qa);
            WalkTo(qa, 40f, -0.4f);
            Interact(qa, Case01Ids.Core);
            Choose(qa, 2);
            Settle(qa);
            Assert.AreEqual(Case01Room.Dining, qa.CurrentRoom, "錨點錯誤：送到安全房間");
            DiningToEntrance(qa);
            EntranceToLanding(qa);
            LandingToMezzanine(qa);
            WalkTo(qa, 40f, -0.4f);
            Interact(qa, Case01Ids.Core);
            Choose(qa, 1);
            Assert.IsTrue(qa.Session.Has(Case01Flags.RouteAnchorSet));
            CloseText(qa);
            WalkTo(qa, 36f, 0f);
            WalkThrough(qa, 33.4f, 0f, Case01Room.Study);
            StudyToLanding(qa);
            LandingToMezzanine(qa);
            WalkTo(qa, 38.5f, 0f);
            WalkTo(qa, 38.5f, 4.5f);
            WalkTo(qa, 40f, 4.6f);
            WalkThrough(qa, 40f, 6.7f, Case01Room.Entrance);
            EntranceToLanding(qa);
            LandingToMezzanine(qa);
            WalkTo(qa, 40f, -0.4f);
            Interact(qa, Case01Ids.Core);
            Assert.IsTrue(qa.Flow.CoreStabilized);
            Assert.AreEqual(Case01Session.HouseTemporarilyStable, qa.Session.Get(Case01Session.HouseStateKey));
            Run(qa, 2f);
            foreach (var f in qa.Layout.Overlaps) Assert.Less(Quaternion.Angle(f.localRotation, Quaternion.identity), 3f, "房間輪廓復位");
            Assert.AreEqual("離開十七號", qa.Layout.FrontDoorLabel.text, "出口真正改變");

            // S60：回玄關，出口。
            CloseText(qa);
            WalkTo(qa, 38.5f, 0f);
            WalkTo(qa, 38.5f, 4.5f);
            WalkTo(qa, 40f, 4.6f);
            WalkThrough(qa, 40f, 6.7f, Case01Room.Entrance);
            Interact(qa, Case01Ids.FrontDoor);
            Choose(qa, 1);
            Assert.AreEqual(Case01Mode.Ended, qa.Mode);
            Assert.IsTrue(qa.Hud.EndVisible);
            StringAssert.Contains("下落未明", qa.Hud.EndText.text);
            foreach (var flag in Case01Flags.All) Assert.IsTrue(qa.Session.Has(flag), flag);
            Assert.AreEqual(Case01Stage.Exited, qa.Session.Stage);

            // 重玩。
            Tick(qa, new Case01Input { Jump = 1 });
            Assert.AreEqual(Case01Mode.Exploring, qa.Mode);
            Assert.AreEqual(Case01Stage.EnteredHome, qa.Session.Stage);
            Assert.AreEqual(Case01Room.Entrance, qa.CurrentRoom);
        }

        // ---------------- 戰敗 → 檢查點 ----------------

        [Test]
        public void Defeat_ReturnsToCourtyardCheckpoint_AndBattleCanBeRetried()
        {
            var qa = NewQa(QuickDefeat(), 1, Case01Checkpoint.S40);
            Assert.AreEqual(Case01Room.Mezzanine, qa.CurrentRoom);
            WalkTo(qa, 40f, -0.4f);
            Interact(qa, Case01Ids.Core);
            RunUntil(qa, () => qa.Mode == Case01Mode.Exploring && qa.CurrentRoom == Case01Room.Courtyard && qa.Manifested, "被拉進中庭並看完失序體形成", 12f);
            WalkTo(qa, 40f, 46f, 15f);
            RunUntil(qa, () => qa.Mode == Case01Mode.Battle, "進入戰鬥");
            var arena = qa.Arena;
            Assert.IsTrue(qa.DemoHud.Tutorial.Active, "第一場戰鬥自動出現首戰教學");
            arena.CommitButton.onClick.Invoke();
            RunUntil(qa, () => !arena.IsPlaying, "事件播放");
            Assert.AreEqual(BattleOutcome.Defeat, arena.Session.Snapshot().Outcome);
            Tick(qa, Case01Input.None);
            Assert.IsFalse(qa.DemoHud.Tutorial.Active, "戰鬥結束時教學關閉，不與結果橫幅重疊");
            Assert.IsFalse(qa.DemoHud.Tutorial.NoteVisible);
            Assert.AreEqual("返回檢查點重試", arena.BannerButtonText);
            arena.BannerButton.onClick.Invoke();
            Tick(qa, Case01Input.None);
            Assert.AreEqual(Case01Mode.Exploring, qa.Mode);
            Assert.IsTrue(qa.Arena == null);
            Assert.AreEqual(Case01Room.Courtyard, qa.CurrentRoom, "回到中庭入口");
            Assert.IsTrue(qa.ExploreCamera.enabled);
            Assert.IsTrue(qa.Hud.Visible);
            Assert.IsTrue(qa.EncounterFiguresVisible, "失序體重新聚集");
            Assert.IsFalse(qa.Session.Has(Case01Flags.BattleWon));
            Assert.IsTrue(qa.Session.HasJournal("battle_lost"));
            Assert.AreEqual(1, qa.Retries);
            Assert.IsFalse(WalkTo(qa, 40f, 46f, 15f), "可以再戰");
            Assert.AreEqual(Case01Mode.EnteringBattle, qa.Mode);
            RunUntil(qa, () => qa.Mode == Case01Mode.Battle, "再次進入戰鬥");
            Assert.IsFalse(qa.DemoHud.Tutorial.Active, "重試不再自動出現教學");
            Assert.IsFalse(qa.DemoHud.Tutorial.PanelVisible);
            Assert.IsTrue(qa.DemoHud.ReplayVisible, "可按「？教學」重看");
        }

        // ---------------- QA 跳轉 ----------------

        [Test]
        public void QaJumps_F1toF4_RebuildConsistentState()
        {
            var qa = NewQa();
            Tick(qa, new Case01Input { Jump = 2 });
            Assert.AreEqual(Case01Stage.WrongConnection, qa.Session.Stage);
            Assert.AreEqual(Case01Room.Landing, qa.CurrentRoom);
            Assert.IsFalse(qa.Layout.DiningSecondSet.activeSelf);
            Assert.IsTrue(qa.Layout.ThirdDoorFrame.activeSelf);
            Assert.IsTrue(qa.Layout.Portal(Case01Door.ThirdDoor).Seal.activeSelf, "S30：第三扇門還封著");
            WalkTo(qa, 6.95f, 4.3f);
            WalkThrough(qa, 6.95f, 5.8f, Case01Room.Dining);

            Tick(qa, new Case01Input { Jump = 3 });
            Assert.AreEqual(Case01Stage.MezzanineDiscovered, qa.Session.Stage);
            Assert.AreEqual(Case01Room.Mezzanine, qa.CurrentRoom);
            Assert.IsFalse(qa.EncounterFiguresVisible);

            Tick(qa, new Case01Input { Jump = 4 });
            Assert.AreEqual(Case01Stage.PostBattle, qa.Session.Stage);
            Assert.IsFalse(qa.Layout.Portal(Case01Door.CourtyardTear).Seal.activeSelf);
            WalkTo(qa, 40f, -0.4f);
            Interact(qa, Case01Ids.Core);
            Assert.IsTrue(qa.Flow.Door17Open, "S50 直接可做第一段修復");

            Tick(qa, new Case01Input { Jump = 1 });
            Assert.AreEqual(Case01Stage.EnteredHome, qa.Session.Stage);
            Assert.AreEqual(Case01Room.Entrance, qa.CurrentRoom);
            Assert.IsTrue(qa.Layout.DiningSecondSet.activeSelf);
            Assert.AreEqual(4, qa.Jumps);
        }

        [Test]
        public void FrontDoor_BeforeStabilization_RestartsFromEntrance()
        {
            var qa = NewQa();
            EntranceToDining(qa);
            Interact(qa, Case01Ids.Table);
            DiningToEntrance(qa);
            WalkTo(qa, 7.1f, 1.4f);
            Interact(qa, Case01Ids.FrontDoor);
            StringAssert.Contains("QA 出口", qa.Hud.Base.InspectText.text);
            Choose(qa, 1);
            Tick(qa, Case01Input.None);
            Assert.AreEqual(Case01Stage.EnteredHome, qa.Session.Stage, "重新開始");
            Assert.IsFalse(qa.Session.Has(Case01Flags.TableTwo));
        }

        [Test]
        public void Interaction_IsBlockedByWalls()
        {
            var qa = NewQa();
            // 玄關看向餐桌方向但隔著牆（x=5 以西的廚房看不到）。
            qa.Player.Teleport(new Vector3(4.2f, 0.05f, 2.5f), 90f);
            Aim(qa, Case01Ids.Photo);
            Assert.IsNull(qa.CurrentTarget, "距離外／隔牆不能調查");
        }

        // ---------------- 場景檔與舊 QA ----------------

        [Test]
        public void SceneAsset_ContainsOnlyTheQaComponent_AndOldScenesKeepTheirScripts()
        {
            string Guid(string path) { return UnityEditor.AssetDatabase.AssetPathToGUID(path); }
            var scene = File.ReadAllText("Assets/Exploration/Scenes/Case01HouseInteriorQA.unity");
            var qaGuid = Guid("Assets/Exploration/Scripts/Case01/Case01HouseQA.cs");
            Assert.IsNotEmpty(qaGuid);
            StringAssert.Contains("guid: " + qaGuid, scene, "場景引用的是存在的腳本（不會 Missing Script）");
            Assert.AreEqual(1, CountOf(scene, "m_Script:"), "只有一個 MonoBehaviour");
            var street = File.ReadAllText("Assets/Exploration/Scenes/AnomalyStreetDemo.unity");
            StringAssert.Contains("guid: " + Guid("Assets/Exploration/Scripts/AnomalyStreetDemo.cs"), street, "舊 QA 場景仍指向原本的 Demo");
            StringAssert.DoesNotContain(qaGuid, street);
        }

        private static int CountOf(string s, string sub)
        {
            int n = 0, i = 0;
            while ((i = s.IndexOf(sub, i, System.StringComparison.Ordinal)) >= 0)
            {
                n++;
                i += sub.Length;
            }
            return n;
        }
    }
}

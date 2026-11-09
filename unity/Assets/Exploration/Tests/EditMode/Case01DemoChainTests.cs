using System.Collections.Generic;
using NUnit.Framework;
using Shepherd.Battle.Core;
using Shepherd.Battle.UI;
using Shepherd.Exploration.Case01;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// Demo Phase 1（v0.7）P0-C：在正式 Demo（不是獨立 QA）中跑完整條關鍵路徑：
    /// 事務所接案 → 街區查名冊 → 進 17 號 → 線索、錯接、第三扇門 → 核 → 形成演出 → 戰鬥 → 核校正 v0.3 → 前門回街區 → 事務所結案；
    /// 檢查每段的區域、HUD 沒有 QA 字樣、跨區域狀態（委託、修核、勝利）不遺失。戰鬥用快速勝利情境（規則不變）。
    /// </summary>
    public sealed class Case01DemoChainTests
    {
        private const float Dt = 1f / 30f;
        private readonly List<GameObject> roots = new List<GameObject>();
        private Case01DemoSlice d;

        [TearDown]
        public void TearDown()
        {
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r);
            roots.Clear();
            Case01RunState.Reset();
        }

        private static CombatLabScenario QuickVictory()
        {
            var s = new CombatLabScenario { Name = "quick-victory" };
            s.Enemies.Add(new EnemyState { Id = 1, Name = "E1 失序體・甲", Value = 1, Corrections = 1, AttackPattern = new[] { 12 } });
            s.Enemies.Add(new EnemyState { Id = 2, Name = "E2 失序體・乙", Value = 1, Corrections = 1, AttackPattern = new[] { 6 } });
            return s;
        }

        private Case01HouseQA H { get { return d.House; } }

        private void Tick(Case01Input input) { d.Tick(Dt, input); }

        private void Run(float s)
        {
            for (float t = 0; t < s; t += Dt) d.Tick(Dt, Case01Input.None);
        }

        private void RunUntil(System.Func<bool> done, string what, float max = 20f)
        {
            for (float t = 0; t < max && !done(); t += Dt) d.Tick(Dt, Case01Input.None);
            Assert.IsTrue(done(), "逾時：" + what + "（區域 " + d.Area + (H != null ? "，模式 " + H.Mode + "，" + H.Player.transform.position : "") + "）");
        }

        private void Settle()
        {
            RunUntil(() => !d.Transitioning && (H == null || H.Mode == Case01Mode.Exploring), "轉場結束", 6f);
        }

        private static Vector2 LookDelta(FirstPersonController p, Vector3 target, bool level)
        {
            var dir = target - p.EyePosition;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float pitch = level ? 0f : -Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg;
            return new Vector2(Mathf.DeltaAngle(p.Yaw, yaw) / FirstPersonController.LookSensitivity, -(pitch - p.Pitch) / FirstPersonController.LookSensitivity);
        }

        private void At(float x, float y, float z, float yaw = 0f)
        {
            H.Player.Teleport(new Vector3(x, y + 0.05f, z), yaw);
            Tick(Case01Input.None);
        }

        private void Interact(string id)
        {
            var it = H.Layout.Interactables[id];
            var target = it.transform.TransformPoint(it.GetComponent<BoxCollider>().center);
            for (int i = 0; i < 2; i++) Tick(Case01Input.From(new ExploreInput { Look = LookDelta(H.Player, target, false) }));
            Assert.AreEqual(id, H.CurrentTarget != null ? H.CurrentTarget.Id : null, "準星對到 " + id + "（" + H.Player.transform.position + "）");
            if (H.Hud.PanelVisible && H.PanelSourceId == id) Tick(Case01Input.From(new ExploreInput { Interact = true }));
            Tick(Case01Input.From(new ExploreInput { Interact = true }));
        }

        private void Close()
        {
            if (H.Hud.PanelVisible) Tick(Case01Input.From(new ExploreInput { Interact = true }));
        }

        private void Choose(int n)
        {
            Assert.IsNotNull(H.Hud.ActiveChoice, "有選項");
            Tick(new Case01Input { Choice = n });
        }

        /// <summary>朝目標走直到到達（true）或被傳送打斷（false）。</summary>
        private bool WalkTo(float x, float z, float max = 12f)
        {
            for (float t = 0; t < max; t += Dt)
            {
                if (H.Mode != Case01Mode.Exploring) return false;
                var p = H.Player.transform.position;
                if (new Vector2(x - p.x, z - p.z).magnitude < 0.25f) return true;
                Tick(Case01Input.From(new ExploreInput { Move = new Vector2(0f, 1f), Look = LookDelta(H.Player, new Vector3(x, H.Player.EyePosition.y, z), true) }));
            }
            Assert.Fail("走不到 (" + x + ", " + z + ")，停在 " + H.Player.transform.position);
            return false;
        }

        private void WalkThrough(float x, float z, Case01Room expect)
        {
            Assert.IsFalse(WalkTo(x, z), "應在門洞觸發傳送");
            Settle();
            Assert.AreEqual(expect, H.CurrentRoom);
        }

        private void AssertNoQaText()
        {
            StringAssert.DoesNotContain("QA", H.Hud.Base.Objective.text, "Demo 目標文字沒有 QA 字樣");
            Assert.AreEqual("", H.Hud.Base.ViewMode.text, "Demo 沒有 QA 快捷鍵說明");
            Assert.IsFalse(H.Hud.QaText.transform.parent.gameObject.activeSelf, "Demo 沒有 QA 狀態列");
        }

        [Test]
        public void Demo_FullChain_Office_Street_RealHouseRun_Street_Office()
        {
            var go = new GameObject("Case01DemoChain");
            roots.Add(go);
            d = go.AddComponent<Case01DemoSlice>();
            d.TestScenario = QuickVictory();
            d.Build(false, Case01DemoArea.Office, Case01Floorplan.Original);

            // 事務所 → 街區 → 17 號。
            d.Interact(Case01DemoIds.OfficeCase);
            d.Hud.HidePanel();
            d.Interact(Case01DemoIds.OfficeDoor);
            Settle();
            Assert.AreEqual(Case01DemoArea.Street, d.Area);
            d.Interact(Case01DemoIds.StreetListing);
            d.Hud.HidePanel();
            d.Interact(Case01DemoIds.Door17);
            Settle();
            Assert.AreEqual(Case01DemoArea.House, d.Area);
            Assert.IsTrue(H.IsDemo);
            Assert.AreEqual(Case01CorrectionMode.RevisedV03, H.Flow.Correction, "Demo 用核校正 v0.3");
            AssertNoQaText();

            // 一樓：餐桌兩副 → 合照 → 離開 → 回來只剩一副。
            At(6.5f, 0f, 6.0f, 30f);
            Interact(Case01Ids.Table);
            Close();
            At(8.7f, 0f, 7.6f, 90f);
            Interact(Case01Ids.Photo);
            Close();
            At(7.1f, 0f, 1.6f, 180f);
            Run(0.3f);
            At(6.5f, 0f, 6.0f, 30f);
            Run(0.3f);
            Assert.IsTrue(H.Session.Has(Case01Flags.TableRewritten), "餐桌二→一");

            // 二樓書房線索 → 平台 → 書房門錯接到餐廳 → 覆寫記號 → 第三扇門。
            float y = Case01HouseBuilder.FloorHeight;
            At(6.7f, y, 8.2f);
            Interact(Case01Ids.Map);
            Interact(Case01Ids.EllieNotes);
            At(8.7f, y, 7.5f, 90f);
            Interact(Case01Ids.OldRecord);
            Close();
            At(6.95f, y, 6.2f, 180f);
            At(6.95f, y, 4.2f, 0f);
            Run(0.2f);
            WalkThrough(6.95f, 5.8f, Case01Room.Dining);
            Assert.IsTrue(H.Session.Has(Case01Flags.RouteMismatchSeen), "書房門錯接到餐廳");
            At(6.3f, y, 3.9f, -90f);
            Interact(Case01Ids.StudyMark);
            Choose(1);
            Close();
            At(6.95f, y, 2.0f, 180f);
            WalkThrough(6.95f, -0.6f, Case01Room.Mezzanine);

            // 核 → 反制 → 中庭形成演出（E 跳過）→ 遭遇 → 戰鬥（快速勝利）。
            At(40f, 0f, -0.4f, 0f);
            Interact(Case01Ids.Core);
            RunUntil(() => H.Mode == Case01Mode.Manifesting, "形成演出", 12f);
            Tick(Case01Input.From(new ExploreInput { Interact = true }));
            Assert.AreEqual(Case01EnemyVisual.Manifested, H.EnemyVisual);
            Assert.IsFalse(WalkTo(40f, 46f, 15f), "走近即遭遇");
            RunUntil(() => H.Mode == Case01Mode.Battle, "進入戰鬥");
            var arena = H.Arena;
            arena.CommitButton.onClick.Invoke();
            RunUntil(() => !arena.IsPlaying, "事件播放", 30f);
            Assert.AreEqual(BattleOutcome.Victory, arena.Session.Snapshot().Outcome);
            arena.BannerButton.onClick.Invoke();
            RunUntil(() => H.Mode == Case01Mode.Exploring, "返回探索");
            Assert.IsTrue(H.Session.Has(Case01Flags.BattleWon));

            // 核校正 v0.3：回夾層，錨點 △→書房、第二關係 17→玄關，穿 17 號門寫回。
            At(40f, 0f, 25.6f, 180f);
            WalkThrough(40f, 23.4f, Case01Room.Mezzanine);
            At(40f, 0f, -0.4f, 0f);
            Interact(Case01Ids.Core);
            Choose(1);
            Interact(Case01Ids.Core);
            Choose(1);
            Close();
            At(40f, 0f, 4.6f, 0f);
            WalkThrough(40f, 6.7f, Case01Room.Entrance);
            Assert.IsTrue(H.Flow.CoreStabilized, "核校正完成");
            AssertNoQaText();

            // 前門 → 街區（狀態保留）→ 事務所結案。
            var flow = H.Flow;
            H.Player.Teleport(H.Layout.Spawn.Position + new Vector3(0f, 0f, 0.3f), 180f, 8f);
            Tick(Case01Input.None);
            Assert.AreEqual(Case01Ids.FrontDoor, H.CurrentTarget != null ? H.CurrentTarget.Id : null);
            Tick(Case01Input.From(new ExploreInput { Interact = true }));
            Settle();
            Assert.AreEqual(Case01DemoArea.Street, d.Area, "前門回街區");
            Assert.IsNull(d.House, "屋內場景已移除（不與街區同時存在）");
            Assert.AreSame(flow, Case01RunState.HouseFlow, "屋內進度跨區域保留");
            Assert.IsTrue(Case01RunState.HouseResolved);
            Assert.IsTrue(flow.Session.Has(Case01Flags.BattleWon), "勝利狀態保留");
            var cams = go.GetComponentsInChildren<Camera>(true);   // 只算 Demo 自己的（測試場景本身的 Main Camera 不算）
            Assert.AreEqual(1, cams.Length, "只有街區的相機：" + string.Join("、", System.Array.ConvertAll(cams, c => c.name + "@" + (c.transform.root != null ? c.transform.root.name : "-") + "/" + (c.transform.parent != null ? c.transform.parent.name : "-"))));
            d.Interact(Case01DemoIds.StreetBack);
            Settle();
            Assert.AreEqual(Case01DemoArea.Office, d.Area);
            d.Interact(Case01DemoIds.OfficeCase);
            Assert.IsTrue(d.Ended && Case01RunState.CaseClosed, "結案");
        }
    }
}

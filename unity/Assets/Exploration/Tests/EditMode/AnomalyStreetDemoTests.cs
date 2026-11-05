using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shepherd.Battle.Core;
using Shepherd.Battle.UI;
using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>異域探索 Demo：第一人稱移動與碰撞、遭遇判定、探索↔戰鬥切換、勝利返回、戰敗重新開始。</summary>
    public sealed class AnomalyStreetDemoTests
    {
        private const float Dt = 1f / 30f;
        private readonly List<GameObject> roots = new List<GameObject>();

        private AnomalyStreetDemo NewDemo(CombatLabScenario scenario = null, int? seed = null)
        {
            var go = new GameObject("AnomalyStreetDemoTest");
            roots.Add(go);
            var d = go.AddComponent<AnomalyStreetDemo>();
            d.Build(false, scenario, seed);
            return d;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r);
            roots.Clear();
        }

        private static void Run(AnomalyStreetDemo d, float seconds, ExploreInput input)
        {
            for (float t = 0; t < seconds; t += Dt) d.Tick(Dt, input);
        }

        private static void RunUntil(AnomalyStreetDemo d, System.Func<AnomalyStreetDemo, bool> done, ExploreInput input, float maxSeconds = 20f)
        {
            for (float t = 0; t < maxSeconds && !done(d); t += Dt) d.Tick(Dt, input);
            Assert.IsTrue(done(d), "逾時：條件未達成，目前狀態 " + d.State);
        }

        /// <summary>敵人只剩 1 點並帶 1 層校正：空序列結束回合即由核心的持續校正解除（與 CombatLab3D 截圖相同的測試情境）。</summary>
        private static CombatLabScenario QuickVictory()
        {
            var s = new CombatLabScenario { Name = "quick-victory" };
            s.Enemies.Add(new EnemyState { Id = 1, Name = "E1 失序體・甲", Value = 1, Corrections = 1, AttackPattern = new[] { 12 } });
            s.Enemies.Add(new EnemyState { Id = 2, Name = "E2 失序體・乙", Value = 1, Corrections = 1, AttackPattern = new[] { 6 } });
            return s;
        }

        /// <summary>敵人失序值極高、攻擊遠超錨定值：空序列結束回合即戰敗。</summary>
        private static CombatLabScenario QuickDefeat()
        {
            var s = new CombatLabScenario { Name = "quick-defeat" };
            s.Enemies.Add(new EnemyState { Id = 1, Name = "E1 失序體・甲", Value = 500, AttackPattern = new[] { 999 } });
            return s;
        }

        /// <summary>從廣場南側直接往北走進遭遇區，直到戰鬥開始。</summary>
        private static void WalkIntoBattle(AnomalyStreetDemo d)
        {
            d.Player.Teleport(new Vector3(0.2f, 0.05f, 2f), 0f);
            RunUntil(d, x => x.State != DemoState.Exploring, ExploreInput.Forward());
            RunUntil(d, x => x.State == DemoState.Battle, ExploreInput.None);
        }

        // ---------------- 場景 ----------------

        [Test]
        public void Build_CreatesStreetPlayerEncounterAndAnomaly()
        {
            var d = NewDemo();
            Assert.AreEqual(DemoState.Exploring, d.State);
            Assert.IsNotNull(d.Player);
            Assert.IsNotNull(d.Player.GetComponent<CharacterController>());
            Assert.IsNotNull(d.ExploreCamera);
            Assert.AreSame(d.ExploreCamera, d.ActiveCamera);
            Assert.IsNull(d.Arena, "探索時尚未建立戰鬥");
            Assert.IsTrue(d.Hud.Visible);

            var root = d.Layout.Root;
            Assert.IsNotNull(root.Find("Buildings"), "建築物");
            Assert.IsNotNull(root.Find("StreetLamps"), "街燈");
            Assert.IsNotNull(root.Find("Props"), "障礙物");
            Assert.IsNotNull(root.Find("Anomaly"), "廣場異域裂痕");
            Assert.IsNotNull(d.Layout.Anomaly, "可互動的異常物件");
            Assert.GreaterOrEqual(root.GetComponentsInChildren<Light>().Count(l => l.type == LightType.Point), 8, "街燈與異域光源");

            // 地圖約 40×40 公尺。
            var b = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var c in root.Find("Ground").GetComponentsInChildren<Collider>()) b.Encapsulate(c.bounds);
            Assert.AreEqual(44f, b.size.x, 0.5f);
            Assert.AreEqual(44f, b.size.z, 0.5f);

            // 敵人：數量與名稱來自 Combat Lab 標準情境（現有數值）。
            var scenario = CombatLabScenario.Default();
            var figures = d.Layout.Root.parent.Find("EncounterEnemies");
            Assert.IsNotNull(figures);
            Assert.AreEqual(scenario.Enemies.Count, figures.childCount);
            Assert.IsTrue(d.EncounterFiguresVisible);
            Assert.IsFalse(d.Encounter.Defeated);
        }

        [Test]
        public void SpawnPoint_IsOutsideEncounterAndWarnRadius()
        {
            var d = NewDemo();
            Assert.AreEqual(EncounterCheck.None, d.Encounter.Check(d.Player.transform.position), "出生點不應在遭遇提示範圍內");
            Run(d, 2f, ExploreInput.None);
            Assert.AreEqual(DemoState.Exploring, d.State);
        }

        // ---------------- 第一人稱操作 ----------------

        [Test]
        public void Walk_MovesForward_AndRunIsFaster()
        {
            var d = NewDemo();
            Run(d, 0.3f, ExploreInput.None); // 落地
            var start = d.Player.transform.position;
            Run(d, 1f, ExploreInput.Forward());
            var walked = d.Player.transform.position - start;
            Assert.Greater(walked.z, 2.8f, "W 往前（+z）走");
            Assert.Less(Mathf.Abs(walked.x), 0.2f);
            Assert.AreEqual(FirstPersonController.WalkSpeed, d.Player.LastHorizontalSpeed, 0.3f);

            start = d.Player.transform.position;
            Run(d, 1f, ExploreInput.Forward(true));
            var ran = d.Player.transform.position - start;
            Assert.Greater(ran.z, walked.z * 1.5f, "Shift 奔跑較快");
            Assert.AreEqual(FirstPersonController.RunSpeed, d.Player.LastHorizontalSpeed, 0.3f);
        }

        [Test]
        public void Strafe_UsesYaw()
        {
            var d = NewDemo();
            d.Player.Teleport(new Vector3(0f, 0.05f, -12f), 90f); // 面向東
            Run(d, 0.3f, ExploreInput.None);
            var start = d.Player.transform.position;
            Run(d, 0.5f, new ExploreInput { Move = new Vector2(0f, 1f) });
            Assert.Greater(d.Player.transform.position.x - start.x, 1.2f, "面向東時 W 往 +x");
            start = d.Player.transform.position;
            Run(d, 0.5f, new ExploreInput { Move = new Vector2(-1f, 0f) });
            Assert.Greater(d.Player.transform.position.z - start.z, 1.2f, "面向東時 A 往北（+z）");
        }

        [Test]
        public void MouseLook_RotatesView_AndClampsPitch()
        {
            var d = NewDemo();
            Assert.IsTrue(d.CursorCaptured);
            d.Tick(Dt, new ExploreInput { Look = new Vector2(10f, 0f) });
            Assert.AreEqual(10f * FirstPersonController.LookSensitivity, d.Player.Yaw, 0.01f);
            d.Tick(Dt, new ExploreInput { Look = new Vector2(0f, -1000f) });
            Assert.AreEqual(FirstPersonController.PitchLimit, d.Player.Pitch, 0.01f, "向下看有上限");
            d.Tick(Dt, new ExploreInput { Look = new Vector2(0f, 1000f) });
            Assert.AreEqual(-FirstPersonController.PitchLimit, d.Player.Pitch, 0.01f, "向上看有上限");
            Assert.Less(Quaternion.Angle(d.Player.ViewRotation, d.ExploreCamera.transform.rotation), 0.01f, "攝影機跟隨視角");
            Assert.AreEqual(d.Player.EyePosition.y, d.ExploreCamera.transform.position.y, 0.001f);
        }

        [Test]
        public void Escape_ReleasesCursor_AndStopsMouseLook_ClickRecaptures()
        {
            var d = NewDemo();
            d.Tick(Dt, new ExploreInput { ReleaseCursor = true });
            Assert.IsFalse(d.CursorCaptured);
            Assert.IsTrue(d.Hud.CursorHintVisible, "提示點擊畫面重新鎖定");
            float yaw = d.Player.Yaw;
            d.Tick(Dt, new ExploreInput { Look = new Vector2(20f, 5f) });
            Assert.AreEqual(yaw, d.Player.Yaw, "釋放滑鼠時不轉視角");
            d.Tick(Dt, new ExploreInput { Click = true });
            Assert.IsTrue(d.CursorCaptured);
            Assert.IsFalse(d.Hud.CursorHintVisible);
        }

        [Test]
        public void Gravity_LandsOnGround()
        {
            var d = NewDemo();
            d.Player.Teleport(new Vector3(0f, 3f, -12f), 0f);
            Run(d, 1.5f, ExploreInput.None);
            Assert.AreEqual(0f, d.Player.transform.position.y, 0.1f, "落到地面");
            Assert.IsTrue(d.Player.Grounded);
        }

        [Test]
        public void Walls_BlockMovement()
        {
            var d = NewDemo();
            // 西側建築正面在 x = -5.5；從人行道朝西直走 3 秒。
            d.Player.Teleport(new Vector3(-3f, 0.2f, -14f), -90f);
            Run(d, 3f, ExploreInput.Forward(true));
            Assert.Greater(d.Player.transform.position.x, -5.5f + FirstPersonController.Radius - 0.1f, "不可穿過建築");

            // 地圖南端（施工圍欄＋邊界）。
            d.Player.Teleport(new Vector3(0f, 0.05f, -17f), 180f);
            Run(d, 3f, ExploreInput.Forward(true));
            Assert.Greater(d.Player.transform.position.z, -StreetBuilder.HalfSize, "不可離開地圖");

            // 小巷盡頭的牆。
            d.Player.Teleport(new Vector3(-14.2f, 0.05f, -5.9f), -90f);
            Run(d, 4f, ExploreInput.Forward(true));
            Assert.Greater(d.Player.transform.position.x, -18.3f, "不可穿過小巷盡頭的牆");
        }

        [Test]
        public void Sidewalk_CurbCanBeSteppedOn()
        {
            var d = NewDemo();
            d.Player.Teleport(new Vector3(-2.5f, 0.05f, -16f), -90f);
            Run(d, 0.8f, ExploreInput.Forward());
            Assert.Less(d.Player.transform.position.x, -3.6f, "可走上 12 cm 的路緣");
            Assert.AreEqual(0.12f, d.Player.transform.position.y, 0.06f);
        }

        // ---------------- 遭遇判定 ----------------

        [Test]
        public void EncounterZone_ChecksDistanceHeightAndDefeat()
        {
            var z = new EncounterZone { Center = new Vector3(0f, 0f, 10f) };
            Assert.AreEqual(EncounterCheck.None, z.Check(new Vector3(0f, 0f, 10f - z.WarnRadius - 0.1f)));
            Assert.AreEqual(EncounterCheck.Near, z.Check(new Vector3(0f, 0f, 10f - z.WarnRadius + 0.1f)));
            Assert.AreEqual(EncounterCheck.Near, z.Check(new Vector3(0f, 0f, 10f - z.TriggerRadius - 0.1f)));
            Assert.AreEqual(EncounterCheck.Trigger, z.Check(new Vector3(0f, 0f, 10f - z.TriggerRadius + 0.1f)));
            Assert.AreEqual(EncounterCheck.Trigger, z.Check(new Vector3(z.TriggerRadius - 0.1f, 0f, 10f)), "任何方向");
            Assert.AreEqual(EncounterCheck.None, z.Check(new Vector3(0f, 5f, 10f)), "高度差過大不觸發");
            z.MarkDefeated();
            Assert.AreEqual(EncounterCheck.None, z.Check(new Vector3(0f, 0f, 10f)), "已擊敗不觸發");
        }

        [Test]
        public void Approaching_ShowsWarningFirst_ThenTriggersOnlyInsideZone()
        {
            var d = NewDemo();
            d.Player.Teleport(new Vector3(0.2f, 0.05f, -6f), 0f);
            bool warned = false;
            float warnDistance = -1f;
            for (int i = 0; i < 900 && d.State == DemoState.Exploring; i++)
            {
                d.Tick(Dt, ExploreInput.Forward());
                if (d.State != DemoState.Exploring) break;
                float dist = d.Encounter.HorizontalDistance(d.Player.transform.position);
                Assert.Greater(dist, d.Encounter.TriggerRadius - 0.3f, "在遭遇區外不應開始戰鬥");
                if (d.Hud.HintVisible && d.Hud.Hint.text.Contains("失序體") && !warned)
                {
                    warned = true;
                    warnDistance = dist;
                }
            }
            Assert.IsTrue(warned, "進入戰鬥前先顯示遭遇提示");
            Assert.LessOrEqual(warnDistance, d.Encounter.WarnRadius + 0.01f);
            Assert.AreEqual(DemoState.EnteringBattle, d.State);
            Assert.AreEqual(1, d.Encounter.TriggerCount);
            Assert.IsTrue(d.Hud.EncounterVisible, "遭遇橫幅");
            StringAssert.Contains("失序體", d.Hud.EncounterTitle.text);
        }

        [Test]
        public void WalkingElsewhere_DoesNotTrigger()
        {
            var d = NewDemo();
            // 主街往返、進小巷：都不在遭遇區內。
            Run(d, 2f, ExploreInput.Forward());
            d.Player.Teleport(new Vector3(-3f, 0.05f, -7f), -90f);
            Run(d, 4f, ExploreInput.Forward());
            Assert.AreEqual(DemoState.Exploring, d.State);
            Assert.AreEqual(0, d.Encounter.TriggerCount);
            Assert.IsNull(d.Arena);
        }

        // ---------------- 探索 → 戰鬥 ----------------

        [Test]
        public void EnteringBattle_FreezesMovement_BlendsCamera_ThenShowsBattleHud()
        {
            var d = NewDemo();
            d.Player.Teleport(new Vector3(0.2f, 0.05f, 2f), 0f);
            RunUntil(d, x => x.State != DemoState.Exploring, ExploreInput.Forward());
            Assert.AreEqual(DemoState.EnteringBattle, d.State);
            var arena = d.Arena;
            Assert.IsNotNull(arena);
            Assert.IsFalse(arena.HudVisible, "過場中尚未顯示戰鬥 HUD");
            Assert.IsFalse(arena.ArenaCamera.enabled);
            Assert.IsFalse(d.EncounterFiguresVisible, "探索中的敵人由戰鬥中的敵人接手");

            var pos = d.Player.transform.position;
            float yaw = d.Player.Yaw;
            var camStart = d.ExploreCamera.transform.position;
            d.Tick(Dt, new ExploreInput { Move = new Vector2(1f, 1f), Look = new Vector2(30f, 10f), Run = true, ToggleView = true });
            Run(d, AnomalyStreetDemo.EnterBlendSeconds * 0.4f, ExploreInput.Forward(true));
            Assert.AreEqual(pos, d.Player.transform.position, "遭遇後第一人稱移動暫停");
            Assert.AreEqual(yaw, d.Player.Yaw, "遭遇後滑鼠視角暫停");
            Assert.AreEqual(BattleViewMode.TacticalThirdPerson, d.BattleView, "過場中不能切換視角");
            var mid = d.ExploreCamera.transform.position;
            var target = arena.ArenaCamera.transform.position;
            Assert.Greater(Vector3.Distance(mid, camStart), 0.3f, "鏡頭正在移動");
            Assert.Greater(Vector3.Distance(mid, target), 0.3f, "鏡頭平順移動，不是瞬間切換");

            RunUntil(d, x => x.State == DemoState.Battle, ExploreInput.None);
            Assert.IsTrue(arena.ArenaCamera.enabled);
            Assert.IsFalse(d.ExploreCamera.enabled, "只有一台攝影機在畫");
            Assert.AreSame(arena.ArenaCamera, d.ActiveCamera);
            Assert.AreEqual(target, d.ExploreCamera.transform.position, "過場終點與戰鬥鏡頭相同，切換不跳動");
            Assert.IsTrue(arena.HudVisible);
            Assert.IsTrue(arena.InteractionEnabled);
            Assert.IsFalse(d.Hud.Visible, "探索 HUD 收起");
            Assert.IsFalse(d.CursorCaptured, "戰鬥中滑鼠可自由點選卡牌");
        }

        [Test]
        public void Battle_KeepsStreetAsBackground_AndUsesEncounterAnchor()
        {
            var d = NewDemo();
            var figures = d.Layout.Root.parent.Find("EncounterEnemies");
            var figurePositions = new List<Vector3>();
            foreach (Transform f in figures) figurePositions.Add(f.position);
            WalkIntoBattle(d);
            var arena = d.Arena;
            Assert.IsTrue(d.Layout.Root.gameObject.activeInHierarchy, "沿用 3D 街區作為戰場背景");
            Assert.IsNull(arena.transform.Find("ArenaWorld/Environment"), "嵌入時不建立 Combat Lab 的廣場");
            Assert.IsFalse(arena.GetComponentsInChildren<Light>().Any(l => l.type == LightType.Directional), "沿用街區的月光");
            var snap = arena.Session.Snapshot();
            for (int i = 0; i < snap.Enemies.Count; i++)
                Assert.AreEqual(figurePositions[i].x, arena.EnemyRoot(snap.Enemies[i].Id).position.x, 0.01f, "戰鬥中的敵人與探索時位置相同");
            foreach (var e in snap.Enemies)
            {
                var vp = arena.ArenaCamera.WorldToViewportPoint(arena.EnemyRoot(e.Id).position + Vector3.up);
                Assert.IsTrue(vp.z > 0f && vp.x > 0.1f && vp.x < 0.9f && vp.y > 0.15f && vp.y < 0.9f, e.Name + " 在戰術鏡頭畫面內");
                // 戰術鏡頭與敵人之間沒有建築遮擋。
                var from = arena.ArenaCamera.transform.position;
                var to = arena.EnemyRoot(e.Id).position + Vector3.up * 1.2f;
                var blockers = Physics.RaycastAll(from, (to - from).normalized, Vector3.Distance(from, to) - 0.8f)
                    .Where(h => h.collider.GetComponentInParent<ArenaEnemyMarker>() == null
                                && h.collider.GetComponent<CharacterController>() == null).ToArray();
                Assert.AreEqual(0, blockers.Length, e.Name + " 被擋住：" + string.Join(",", blockers.Select(h => h.collider.name)));
            }
            var pvp = arena.ArenaCamera.WorldToViewportPoint(arena.PlayerRoot.position + Vector3.up);
            Assert.IsTrue(pvp.z > 0f && pvp.x > 0.1f && pvp.x < 0.9f && pvp.y > 0.1f && pvp.y < 0.9f, "玩家在戰術鏡頭畫面內");
        }

        [Test]
        public void Battle_UsesExistingCore_SameResultsAsCombatLabSession()
        {
            var d = NewDemo();
            WalkIntoBattle(d);
            var s = d.Arena.Session;
            var expected = CombatLabScenario.Default();
            var snap = s.Snapshot();
            Assert.AreEqual(expected.Enemies.Count, snap.Enemies.Count);
            for (int i = 0; i < snap.Enemies.Count; i++)
            {
                Assert.AreEqual(expected.Enemies[i].Name, snap.Enemies[i].Name);
                Assert.AreEqual(expected.Enemies[i].Value, snap.Enemies[i].Value, "沿用現有數值");
            }

            // 同一種子、同一編排：探索 Demo 的戰鬥與獨立的 CombatLabSession（BattleController／RoundResolver）結果完全相同。
            var reference = new CombatLabSession(RuleSet.CreateDefault(), CombatLabScenario.Default(), CombatLabScenario.DefaultSeed);
            var hand = snap.Hand.Select(c => c.Id).ToArray();
            CollectionAssert.AreEqual(hand, reference.Snapshot().Hand.Select(c => c.Id).ToArray(), "同樣的手牌");
            Assert.IsTrue(s.AddCard(hand[0]));
            Assert.IsTrue(s.AddCard(hand[1]));
            Assert.IsTrue(reference.AddCard(hand[0]));
            Assert.IsTrue(reference.AddCard(hand[1]));
            d.Arena.CommitButton.onClick.Invoke();
            var expectedResult = reference.Commit();
            Assert.IsTrue(d.Arena.LastCommit.Accepted);
            CollectionAssert.AreEqual(expectedResult.Resolution.Events.Select(e => e.ToString()).ToArray(),
                d.Arena.LastCommit.Resolution.Events.Select(e => e.ToString()).ToArray());
            Assert.IsTrue(d.Arena.IsPlaying, "沿用現有的施放、受擊特效播放");
            RunUntil(d, x => !x.Arena.IsPlaying, ExploreInput.None);
            Assert.Greater(d.Arena.Stats.Casts, 0);
            Assert.AreEqual(reference.Snapshot().Anchor, s.Snapshot().Anchor);
        }

        [Test]
        public void InBattle_ExploreInputIsIgnored()
        {
            var d = NewDemo();
            WalkIntoBattle(d);
            var pos = d.Player.transform.position;
            float yaw = d.Player.Yaw;
            Run(d, 1f, new ExploreInput { Move = new Vector2(1f, 1f), Look = new Vector2(10f, 3f), Run = true, Interact = true, ToggleView = true,
                Click = true, ReleaseCursor = true });
            Assert.AreEqual(DemoState.Battle, d.State);
            Assert.AreEqual(pos, d.Player.transform.position, "戰鬥中 WASD 不移動角色");
            Assert.AreEqual(yaw, d.Player.Yaw, "戰鬥中滑鼠不轉第一人稱視角");
            Assert.IsFalse(d.CursorCaptured, "戰鬥中點擊不會鎖定滑鼠");
            Assert.AreEqual(BattleViewMode.TacticalThirdPerson, d.BattleView);
            Assert.IsFalse(d.Hud.InspectVisible);
        }

        // ---------------- 勝利 → 返回探索 ----------------

        [Test]
        public void Victory_ReturnsToExploration_AndEnemyNeverTriggersAgain()
        {
            var d = NewDemo(QuickVictory(), 1);
            WalkIntoBattle(d);
            var arena = d.Arena;
            arena.CommitButton.onClick.Invoke();
            RunUntil(d, x => !x.Arena.IsPlaying, ExploreInput.None);
            Assert.AreEqual(BattleOutcome.Victory, arena.Session.Snapshot().Outcome);
            Assert.AreEqual(BattleOutcome.Victory, d.LastOutcome);
            Assert.IsTrue(arena.BannerVisible);
            Assert.AreEqual("返回探索", arena.BannerButtonText);
            Assert.IsTrue(d.Encounter.Defeated);
            Assert.AreEqual(DemoState.Battle, d.State, "等玩家按下返回探索");

            arena.BannerButton.onClick.Invoke();
            d.Tick(Dt, ExploreInput.None);
            Assert.AreEqual(DemoState.ReturningToExplore, d.State);
            Assert.IsFalse(arena.HudVisible, "收起戰鬥 HUD");
            Assert.IsTrue(d.ExploreCamera.enabled);
            RunUntil(d, x => x.State == DemoState.Exploring, ExploreInput.None);
            Assert.IsNull(d.Arena, "戰鬥物件已移除");
            Assert.IsTrue(d.Hud.Visible);
            Assert.IsTrue(d.CursorCaptured, "回到第一人稱時重新鎖定滑鼠");
            Assert.AreEqual(d.Player.EyePosition, d.ExploreCamera.transform.position, "鏡頭回到第一人稱");
            Assert.AreEqual(AnomalyStreetDemo.ExploreFieldOfView, d.ExploreCamera.fieldOfView, 0.01f);
            Assert.IsFalse(d.EncounterFiguresVisible, "已擊敗的敵人不再出現");
            StringAssert.Contains("平息", d.Hud.ToastText);

            // 玩家仍站在遭遇區內、在區內走動：都不再觸發戰鬥。
            Assert.LessOrEqual(d.Encounter.HorizontalDistance(d.Player.transform.position), d.Encounter.TriggerRadius);
            Run(d, 1.5f, ExploreInput.Forward());
            Run(d, 1.5f, new ExploreInput { Move = new Vector2(0f, -1f) });
            Run(d, 1f, new ExploreInput { Move = new Vector2(-1f, 0f) });
            Assert.AreEqual(DemoState.Exploring, d.State);
            Assert.AreEqual(1, d.Encounter.TriggerCount, "只觸發一次");
            Assert.IsNull(d.Arena);
            Assert.IsFalse(d.StartEncounter(), "已擊敗的遭遇不能再開始");

            // 可以繼續探索：往回走到主街。
            d.Player.Teleport(d.Player.transform.position, 180f);
            var before = d.Player.transform.position;
            Run(d, 2f, ExploreInput.Forward(true));
            Assert.Less(d.Player.transform.position.z, before.z - 8f, "返回後可以自由移動");
        }

        // ---------------- 戰敗 → 重新開始 ----------------

        [Test]
        public void Defeat_OffersRestart_WhichResetsTheDemo()
        {
            var d = NewDemo(QuickDefeat(), 1);
            WalkIntoBattle(d);
            var arena = d.Arena;
            arena.CommitButton.onClick.Invoke();
            RunUntil(d, x => !x.Arena.IsPlaying, ExploreInput.None);
            Assert.AreEqual(BattleOutcome.Defeat, arena.Session.Snapshot().Outcome);
            Assert.IsTrue(arena.BannerVisible);
            Assert.AreEqual("重新開始 Demo", arena.BannerButtonText);
            Assert.IsFalse(d.Encounter.Defeated);

            arena.BannerButton.onClick.Invoke();
            d.Tick(Dt, ExploreInput.None);
            Assert.AreEqual(1, d.Restarts);
            Assert.AreEqual(DemoState.Exploring, d.State);
            Assert.IsNull(d.Arena);
            Assert.IsTrue(d.ExploreCamera.enabled);
            Assert.IsTrue(d.Hud.Visible);
            Assert.AreEqual(StreetBuilder.SpawnPoint.z, d.Player.transform.position.z, 0.1f, "回到出生點");
            Assert.IsFalse(d.Encounter.Defeated);
            Assert.AreEqual(0, d.Encounter.TriggerCount);
            Assert.IsTrue(d.EncounterFiguresVisible);
            Assert.IsFalse(d.AnomalyInvestigated);

            // 重新開始後可以再次遭遇。
            WalkIntoBattle(d);
            Assert.AreEqual(DemoState.Battle, d.State);
            Assert.AreEqual(500, d.Arena.Session.Snapshot().Enemies[0].Value, "戰鬥狀態也重新開始");
        }

        // ---------------- 異常物件 ----------------

        [Test]
        public void Anomaly_CanBeInspectedOnlyWhenCloseAndFacing()
        {
            var d = NewDemo();
            var obj = d.Layout.Anomaly.position;
            d.Player.Teleport(new Vector3(obj.x + 6f, 0.05f, obj.z), -90f);
            d.Tick(Dt, new ExploreInput { Interact = true });
            Assert.IsFalse(d.AnomalyInvestigated, "太遠");

            d.Player.Teleport(new Vector3(obj.x + 2f, 0.05f, obj.z), 90f);
            d.Tick(Dt, new ExploreInput { Interact = true });
            Assert.IsFalse(d.AnomalyInvestigated, "背對");

            d.Player.Teleport(new Vector3(obj.x + 2f, 0.05f, obj.z), -90f);
            d.Tick(Dt, ExploreInput.None);
            Assert.IsTrue(d.Hud.HintVisible);
            StringAssert.Contains("調查", d.Hud.Hint.text);
            d.Tick(Dt, new ExploreInput { Interact = true });
            Assert.IsTrue(d.AnomalyInvestigated);
            Assert.IsTrue(d.Hud.InspectVisible);
            StringAssert.Contains("異常物件", d.Hud.InspectText.text);
            d.Tick(Dt, new ExploreInput { Interact = true });
            Assert.IsFalse(d.Hud.InspectVisible, "再按 E 關閉");
            Assert.IsTrue(d.AnomalyInvestigated);
        }

        [Test]
        public void Alley_IsReachableFromSpawn()
        {
            var d = NewDemo();
            // 從出生點走到小巷入口，再往西走到異常物件前。
            d.Player.Teleport(new Vector3(0f, 0.05f, -7f), -90f);
            Run(d, 5f, ExploreInput.Forward(true));
            Assert.Less(d.Player.transform.position.x, -12f, "小巷可通行（繞過障礙物前的直線部分）");
        }

        // ---------------- 戰鬥視角（Demo 比較） ----------------

        [Test]
        public void ToggleView_SwitchesBattleCamera_ToFirstPerson()
        {
            var d = NewDemo();
            Assert.AreEqual(BattleViewMode.TacticalThirdPerson, d.BattleView, "預設第三人稱戰術鏡頭");
            d.Tick(Dt, new ExploreInput { ToggleView = true });
            Assert.AreEqual(BattleViewMode.FirstPerson, d.BattleView);
            StringAssert.Contains("第一人稱", d.Hud.ViewMode.text);
            WalkIntoBattle(d);
            var arena = d.Arena;
            Assert.IsFalse(arena.PlayerAvatarVisible, "第一人稱戰鬥隱藏玩家模型");
            var eye = arena.PlayerRoot.position + Vector3.up * FirstPersonController.EyeHeight;
            Assert.AreEqual(eye.x, arena.ArenaCamera.transform.position.x, 0.01f);
            Assert.AreEqual(eye.y, arena.ArenaCamera.transform.position.y, 0.01f);
            foreach (var e in arena.Session.Snapshot().Enemies)
            {
                var vp = arena.ArenaCamera.WorldToViewportPoint(arena.EnemyRoot(e.Id).position + Vector3.up * 1.2f);
                Assert.IsTrue(vp.z > 0f && vp.x > 0.05f && vp.x < 0.95f && vp.y > 0.1f && vp.y < 0.95f, e.Name + " 在第一人稱畫面內");
                var sp = arena.EnemyScreenPoint(e.Id);
                Assert.AreEqual(e.Id, arena.EnemyAt(sp), "第一人稱下仍可點選敵人");
            }
            // 戰鬥規則與第三人稱完全相同（只差鏡頭）。
            Assert.AreEqual(CombatLabScenario.Default().Enemies.Count, arena.EnemyCount);
        }

        // ---------------- 既有 CombatArenaView 的嵌入選項 ----------------

        [Test]
        public void CombatArena_EmbeddedOptions_DoNotChangeStandaloneDefaults()
        {
            var go = new GameObject("Standalone");
            roots.Add(go);
            var standalone = go.AddComponent<CombatArenaView>();
            standalone.Build(false);
            Assert.AreEqual(CombatArenaView.CameraPosition, standalone.ArenaCamera.transform.position, "單獨場景的鏡頭位置不變");
            Assert.IsTrue(standalone.ArenaCamera.enabled);
            Assert.IsNotNull(standalone.transform.Find("ArenaWorld/Environment"));
            Assert.AreEqual("再戰一次", standalone.BannerButtonText);

            var go2 = new GameObject("Embedded");
            roots.Add(go2);
            go2.SetActive(false);
            go2.transform.position = new Vector3(5f, 0f, 7f);
            go2.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            var embedded = go2.AddComponent<CombatArenaView>();
            embedded.Build(false, null, null, ArenaHostOptions.Embedded());
            go2.SetActive(true);
            Assert.IsFalse(embedded.ArenaCamera.enabled);
            Assert.IsNull(embedded.transform.Find("ArenaWorld/Environment"));
            var expectedCam = go2.transform.TransformPoint(CombatArenaView.CameraPosition);
            Assert.Less(Vector3.Distance(expectedCam, embedded.ArenaCamera.transform.position), 0.01f, "鏡頭隨戰場原點移動與旋轉");
            var expectedPlayer = go2.transform.TransformPoint(CombatArenaView.PlayerPosition);
            Assert.Less(Vector3.Distance(expectedPlayer, embedded.PlayerRoot.position), 0.01f);
            foreach (var e in embedded.Session.Snapshot().Enemies)
            {
                Assert.AreEqual(e.Id, embedded.EnemyAt(embedded.EnemyScreenPoint(e.Id)), "旋轉後的戰場仍可點選敵人");
            }
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Shepherd.Battle.Core;
using Shepherd.Battle.UI;
using Shepherd.Battle.UI.Arena;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shepherd.Battle.Tests
{
    /// <summary>3D Combat Lab：場景建立、3D 點選目標、HUD 同步、事件播放與版面。</summary>
    public sealed class CombatArenaTests
    {
        private readonly List<GameObject> roots = new List<GameObject>();

        private CombatArenaView NewView(CombatLabScenario scenario = null, int? seed = null)
        {
            var go = new GameObject("CombatArenaTest");
            roots.Add(go);
            var v = go.AddComponent<CombatArenaView>();
            v.Build(false, scenario, seed);
            return v;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r);
            roots.Clear();
        }

        private static string Plain(string s)
        {
            return Regex.Replace(s, "<.*?>", "");
        }

        private static void Click(Button b)
        {
            Assert.IsTrue(b.interactable, b.name + " 不可點擊");
            b.onClick.Invoke();
        }

        private static CombatLabScenario Scenario(params EnemyState[] enemies)
        {
            var s = new CombatLabScenario { Name = "test" };
            s.Enemies.AddRange(enemies);
            return s;
        }

        private static string[] Strings(IEnumerable<BattleEvent> events)
        {
            return events.Select(e => e.ToString()).ToArray();
        }

        // ---------------- 場景 ----------------

        [Test]
        public void Build_CreatesArenaMatchingBattleState()
        {
            var v = NewView();
            var snap = v.Session.Snapshot();
            Assert.IsNotNull(v.ArenaCamera, "需要固定的 3D 戰鬥攝影機");
            Assert.IsFalse(v.ArenaCamera.orthographic, "透視攝影機");
            Assert.IsNotNull(v.PlayerRoot);
            Assert.AreEqual(snap.Enemies.Count, v.EnemyCount, "3D 敵人數量與 BattleState 一致");
            foreach (var e in snap.Enemies)
            {
                var root = v.EnemyRoot(e.Id);
                Assert.IsNotNull(root, e.Name);
                Assert.AreEqual(e.Id, root.GetComponent<ArenaEnemyMarker>().EnemyId);
                Assert.IsNotNull(root.GetComponent<Collider>(), "敵人需要碰撞體才能被滑鼠點選");
                Assert.IsTrue(v.ArenaCamera.WorldToViewportPoint(root.position + Vector3.up).z > 0f);
                var vp = v.ArenaCamera.WorldToViewportPoint(root.position + Vector3.up);
                Assert.IsTrue(vp.x > 0.1f && vp.x < 0.9f && vp.y > 0.15f && vp.y < 0.9f, e.Name + " 應在攝影機畫面內");
            }
            var pvp = v.ArenaCamera.WorldToViewportPoint(v.PlayerRoot.position + Vector3.up);
            Assert.IsTrue(pvp.x > 0.1f && pvp.x < 0.9f && pvp.y > 0.15f && pvp.y < 0.9f, "玩家應在攝影機畫面內");
            var colliders = v.GetComponentsInChildren<Collider>(true);
            Assert.AreEqual(snap.Enemies.Count, colliders.Length, "只有敵人有碰撞體，環境不攔截點擊");
            var lights = v.GetComponentsInChildren<Light>(true);
            Assert.IsTrue(lights.Any(l => l.type == LightType.Directional && l.shadows != LightShadows.None), "主光源投射陰影");
            Assert.GreaterOrEqual(lights.Count(l => l.type == LightType.Point), 4, "路燈等點光源");
        }

        [Test]
        public void Build_InTestMode_DoesNotChangeRenderSettings()
        {
            bool fog = RenderSettings.fog;
            var ambient = RenderSettings.ambientLight;
            var v = NewView();
            Assert.AreEqual(fog, RenderSettings.fog);
            Assert.AreEqual(ambient, RenderSettings.ambientLight);
            v.ApplyAtmosphere();
            Assert.IsTrue(RenderSettings.fog);
            v.RestoreAtmosphere();
            Assert.AreEqual(fog, RenderSettings.fog, "離開時還原");
            Assert.AreEqual(ambient, RenderSettings.ambientLight);
        }

        // ---------------- 3D 點選 ----------------

        [Test]
        public void ClickingEnemyInScene_SelectsTarget()
        {
            var v = NewView();
            foreach (var e in v.Session.Snapshot().Enemies.Reverse())
            {
                var sp = v.EnemyScreenPoint(e.Id);
                Assert.AreEqual(e.Id, v.EnemyAt(sp), e.Name + " 射線應打到它的碰撞體");
                Assert.IsTrue(v.TrySelectEnemyAt(sp));
                Assert.AreEqual(e.Id, v.Session.SelectedTargetId);
                v.Tick(0f);
                Assert.IsTrue(v.TargetRingVisible, "被選中的敵人腳下有選取環");
                var ring = v.TargetRingPosition;
                var rootPos = v.EnemyRoot(e.Id).position;
                Assert.AreEqual(rootPos.x, ring.x, 0.01f);
                Assert.AreEqual(rootPos.z, ring.z, 0.01f);
                StringAssert.Contains("目標", Plain(v.PlateText(e.Id)));
            }
        }

        [Test]
        public void ClickingEmptyGround_KeepsTarget()
        {
            var v = NewView();
            var before = v.Session.SelectedTargetId;
            Assert.IsNull(v.EnemyAt(new Vector2(2f, 2f)));
            Assert.IsFalse(v.TrySelectEnemyAt(new Vector2(2f, 2f)));
            Assert.AreEqual(before, v.Session.SelectedTargetId);
        }

        [Test]
        public void ClickingEnemyPlate_AlsoSelectsTarget()
        {
            var v = NewView();
            int id = v.Session.Snapshot().Enemies[1].Id;
            Click(v.PlateButton(id));
            Assert.AreEqual(id, v.Session.SelectedTargetId);
        }

        // ---------------- HUD 同步 ----------------

        [Test]
        public void Hud_ShowsRealBattleState()
        {
            var v = NewView();
            var snap = v.Session.Snapshot();
            string status = Plain(v.StatusText);
            StringAssert.Contains("第 1 回合", status);
            StringAssert.Contains("錨定值 " + snap.Anchor + " / 40", status);
            StringAssert.Contains("擬態錨定 " + snap.Shield, status);
            StringAssert.Contains("AP 3 / 3", status);
            Assert.AreEqual(3, v.SchoolButtons.Count);
            CollectionAssert.AreEqual(snap.Hand.Select(c => c.Id).ToArray(), v.HandIds.ToArray());
            Assert.AreEqual(7, v.HandButtons.Count(b => b.gameObject.activeSelf));
            var f = v.Session.Forecast;
            foreach (var e in snap.Enemies)
            {
                string plate = Plain(v.PlateText(e.Id));
                StringAssert.Contains(e.Name, plate);
                StringAssert.Contains("失序值 " + e.Value + " / " + e.Value, plate);
                var ef = f.Enemy(e.Id);
                StringAssert.Contains("攻擊 " + e.ForecastAttack, plate, "頭上顯示攻擊意圖");
                StringAssert.Contains(Plain(CombatLabText.EnemyImpact(ef, f.Outcome)), plate, "意圖顯示對玩家的影響");
                Assert.AreEqual(1f, v.PlateFillRatio(e.Id), 1e-4f);
            }
            StringAssert.Contains("錨定值 " + f.AnchorBefore + " → " + f.AnchorAfter, Plain(v.ForecastText), "沒有編排也預估敵方反擊");
            Assert.Less(v.AnchorFillRatio, (float)snap.Anchor / 40f + 1e-4f);
        }

        [Test]
        public void StagingCards_UpdatesHudAndForecastImmediately()
        {
            var v = NewView();
            Click(v.HandButtons[0]);
            StringAssert.Contains("AP 2 / 3", Plain(v.StatusText));
            StringAssert.Contains("1 / 3 張", Plain(v.PlanTitle));
            Assert.IsTrue(v.SequenceButtons[0].interactable);
            StringAssert.Contains("施放後預估", Plain(v.ForecastText));
            Click(v.HandButtons[1]);
            var f = v.Session.Forecast;
            foreach (var e in f.Enemies)
            {
                float expected = Mathf.Max(e.ValueBefore, e.ValueAfter) / (float)e.ValueBefore;
                Assert.AreEqual(expected, v.PlateGhostRatio(e.Id), 1e-3f, "血條預估段");
                Assert.AreEqual(Mathf.Min(e.ValueBefore, e.ValueAfter) / (float)e.ValueBefore, v.PlateFillRatio(e.Id), 1e-3f);
            }
            Click(v.SequenceButtons[0]);
            Assert.AreEqual(1, v.Session.Staged.Count, "點擊編排欄位移除");
        }

        [Test]
        public void SchoolButtons_SwitchSchool()
        {
            var v = NewView();
            Click(v.SchoolButtons[2]);
            Assert.AreEqual(SchoolKind.Relation, v.Session.SelectedSchool);
            StringAssert.Contains("關係論", Plain(v.StatusText));
        }

        [Test]
        public void Tooltip_OnlyOnHover()
        {
            var v = NewView();
            Assert.IsFalse(v.TooltipVisible, "平常不攤開卡牌說明");
            var b = v.HandButtons[0];
            var data = new PointerEventData(null);
            ExecuteEvents.Execute(b.gameObject, data, ExecuteEvents.pointerEnterHandler);
            Assert.IsTrue(v.TooltipVisible);
            var card = v.Session.Snapshot().Hand[0];
            StringAssert.Contains(Plain(CombatLabText.EffectDetail(v.Session.Rules, v.Session.SelectedSchool, card.Element)),
                Plain(v.TooltipText));
            StringAssert.Contains("加入後本回合預估", Plain(v.TooltipText));
            Assert.IsFalse(v.Tooltip.GetComponent<CanvasGroup>().blocksRaycasts);
            ExecuteEvents.Execute(b.gameObject, data, ExecuteEvents.pointerExitHandler);
            Assert.IsFalse(v.TooltipVisible);
        }

        [Test]
        public void BattleLog_CollapsedByDefault_CanExpand()
        {
            var v = NewView();
            Assert.IsFalse(v.LogVisible, "紀錄預設收合，不佔戰場空間");
            Click(v.LogToggle);
            Assert.IsTrue(v.LogVisible);
            StringAssert.Contains(v.Session.Log.Last(), v.LogText);
            Click(v.LogToggle);
            Assert.IsFalse(v.LogVisible);
        }

        // ---------------- 確認施放與事件播放 ----------------

        [Test]
        public void Commit_PlaysExactlyTheResolverEvents_ThenSyncsWithState()
        {
            var v = NewView();
            var s = v.Session;
            Click(v.HandButtons[0]);
            Click(v.HandButtons[1]);
            var preview = s.Controller.Preview(s.BuildPlan());
            var before = s.Snapshot();

            Click(v.CommitButton);
            Assert.IsTrue(v.IsPlaying, "確認後播放回饋");
            Assert.AreEqual(before.Anchor, v.DisplayAnchor, "播放開始時仍顯示結算前的數值");
            Assert.AreEqual(before.Enemies[0].Value, v.DisplayValue(before.Enemies[0].Id));
            Assert.IsFalse(v.HandButtons[0].interactable, "播放中不能操作手牌");
            StringAssert.Contains("略過動畫", v.CommitButton.GetComponentInChildren<Text>().text);

            Assert.AreEqual(preview.Result.NewState.Fingerprint(), v.LastCommit.Resolution.NewState.Fingerprint(),
                "結算使用與預覽相同的 RoundResolver");
            v.FinishPlayback();
            Assert.IsFalse(v.IsPlaying);
            CollectionAssert.AreEqual(Strings(v.LastCommit.Resolution.Events), Strings(v.PlayedEvents),
                "播放的事件就是核心產生的事件，順序不變");

            var after = s.Snapshot();
            Assert.AreEqual(after.Anchor, v.DisplayAnchor);
            Assert.AreEqual(after.Shield, v.DisplayShield);
            foreach (var e in after.Enemies) Assert.AreEqual(e.Value, v.DisplayValue(e.Id), e.Name);
            StringAssert.Contains("第 2 回合", Plain(v.StatusText));
            CollectionAssert.AreEqual(after.Hand.Select(c => c.Id).ToArray(), v.HandIds.ToArray(), "回合結束後更新手牌");

            var ev = v.LastCommit.Resolution.Events;
            Assert.AreEqual(ev.Count(e => e.Kind == EventKind.CardResolved), v.Stats.Casts, "每張牌一次施放效果");
            Assert.AreEqual(ev.Count(e => e.Kind == EventKind.EnemyActed && e.Amount > 0), v.Stats.EnemyAttacks);
            Assert.AreEqual(ev.Count(e => e.Kind == EventKind.AnchorChanged && e.Amount < 0), v.Stats.PlayerHits, "玩家受擊回饋");
            Assert.AreEqual(ev.Count(e => (e.Kind == EventKind.Pressure || e.Kind == EventKind.Conduction)), v.Stats.EnemyHits,
                "敵人受擊回饋");
        }

        [Test]
        public void Playback_AdvancesOverTime_AndShowsDamageNumbersFromEvents()
        {
            var v = NewView();
            Click(v.HandButtons[0]);
            Click(v.CommitButton);
            int total = v.LastCommit.Resolution.Events.Count;
            v.Tick(0.2f);
            Assert.Less(v.PlayedEvents.Count, total, "事件逐一播放，不是瞬間跳到結果");
            float t = 0f;
            while (v.IsPlaying && t < 30f)
            {
                v.Tick(1f / 30f);
                t += 1f / 30f;
            }
            Assert.IsFalse(v.IsPlaying, "播放會在合理時間內結束");
            foreach (var e in v.LastCommit.Resolution.Events.Where(x => x.Kind == EventKind.AnchorChanged && x.Amount < 0))
                CollectionAssert.Contains(v.Fx.FloaterHistory, "錨定值 −" + (-e.Amount), "傷害數字來自事件");
            foreach (var e in v.LastCommit.Resolution.Events.Where(x => x.Kind == EventKind.Pressure))
                Assert.IsTrue(v.Fx.FloaterHistory.Any(f => f.EndsWith("−" + e.Value)), "壓制數字來自事件的實際減少量");
            v.Fx.FinishAll();
        }

        [Test]
        public void ShieldAbsorb_HasItsOwnFeedback()
        {
            CombatArenaView v = null;
            for (int seed = 1; seed < 300; seed++)
            {
                var candidate = NewView(null, seed);
                if (candidate.Session.Snapshot().Hand.Any(c => c.Element == ElementKind.Base)) { v = candidate; break; }
            }
            Assert.IsNotNull(v);
            int slot = v.Session.Snapshot().Hand.ToList().FindIndex(c => c.Element == ElementKind.Base);
            Click(v.HandButtons[slot]);
            Click(v.CommitButton);
            v.FinishPlayback();
            var ev = v.LastCommit.Resolution.Events;
            int absorbs = ev.Count(e => e.Kind == EventKind.ShieldChanged && e.Amount < 0 && e.Source.StartsWith("enemy:"));
            Assert.Greater(absorbs, 0, "構形的擬態錨定會吸收敵方攻擊");
            Assert.AreEqual(absorbs, v.Stats.ShieldAbsorbs, "護盾吸收有獨立的回饋");
            Assert.GreaterOrEqual(v.Stats.ShieldGains, 1);
        }

        [Test]
        public void Victory_ShowsBanner_AndDissolvesEnemy()
        {
            // 敵人失序值 1、帶 1 層校正：空序列結束回合時由持續校正解除（規則由核心結算）。
            var v = NewView(Scenario(new EnemyState { Id = 1, Name = "失序體・測", Value = 1, Corrections = 1, AttackPattern = new[] { 5 } }), 1);
            Click(v.CommitButton);
            v.FinishPlayback();
            Assert.AreEqual(BattleOutcome.Victory, v.Session.Snapshot().Outcome);
            Assert.IsTrue(v.BannerVisible);
            StringAssert.Contains("勝利", v.BannerText);
            Assert.AreEqual(1, v.Stats.Resolves, "解除效果");
            Assert.IsFalse(v.EnemyRoot(1).gameObject.activeSelf, "解除後模型消失");
            Assert.IsTrue(v.DisplayResolved(1));
            StringAssert.Contains("已解除", v.PlateText(1));
        }

        [Test]
        public void Defeat_ShowsBanner()
        {
            var v = NewView(Scenario(new EnemyState { Id = 1, Name = "失序體・測", Value = 50, AttackPattern = new[] { 99 } }), 1);
            Click(v.CommitButton);
            v.FinishPlayback();
            Assert.AreEqual(BattleOutcome.Defeat, v.Session.Snapshot().Outcome);
            Assert.IsTrue(v.BannerVisible);
            StringAssert.Contains("戰敗", v.BannerText);
            Assert.GreaterOrEqual(v.Stats.PlayerHits, 1);
            Assert.IsFalse(v.CommitButton.interactable, "戰鬥結束後不能再確認");
        }

        [Test]
        public void Reset_RestoresBattleAndEnemies()
        {
            var v = NewView(Scenario(new EnemyState { Id = 1, Name = "失序體・測", Value = 1, Corrections = 1, AttackPattern = new[] { 5 } }), 1);
            Click(v.CommitButton);
            v.FinishPlayback();
            Click(v.ResetButton);
            Assert.AreEqual(BattleOutcome.Ongoing, v.Session.Snapshot().Outcome);
            Assert.IsFalse(v.BannerVisible);
            Assert.IsTrue(v.EnemyRoot(1).gameObject.activeSelf);
            StringAssert.Contains("第 1 回合", Plain(v.StatusText));
        }

        // ---------------- 版面 ----------------

        private static void LayoutAt(CombatArenaView v, int width, int height)
        {
            var scaler = v.HudCanvas.GetComponent<CanvasScaler>();
            float scale = CombatLabView.ScaleFactorFor(scaler, width, height);
            scaler.enabled = false;
            v.HudCanvas.renderMode = RenderMode.WorldSpace;
            v.HudRoot.sizeDelta = new Vector2(width / scale, height / scale);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(v.HudRoot);
            v.Refresh();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(v.HudRoot);
        }

        private static Rect LocalRect(RectTransform root, RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            Vector2 a = root.InverseTransformPoint(c[0]), b = root.InverseTransformPoint(c[2]);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        [TestCase(1920, 1080)]
        [TestCase(1280, 720)]
        [TestCase(1366, 768)]
        [TestCase(1920, 1200)]
        public void Hud_NoOverflow_NoOverlap_AndKeepsBattlefieldClear(int width, int height)
        {
            var v = NewView();
            Click(v.HandButtons[0]);
            Click(v.HandButtons[1]);
            Click(v.LogToggle);
            LayoutAt(v, width, height);

            var problems = new List<string>();
            foreach (var t in v.HudRoot.GetComponentsInChildren<Text>())
            {
                if (string.IsNullOrEmpty(t.text) || t.horizontalOverflow == HorizontalWrapMode.Overflow) continue;
                var scroll = t.GetComponentInParent<ScrollRect>();
                if (scroll != null && t.transform.IsChildOf(scroll.content)) continue;
                Assert.GreaterOrEqual(t.fontSize, CombatLabStyle.MinFont, t.name);
                float need = LayoutUtility.GetPreferredHeight(t.rectTransform);
                if (need > t.rectTransform.rect.height + 2f)
                    problems.Add(t.transform.parent.name + "/" + t.name + " 需要 " + need.ToString("0") + "，只有 "
                        + t.rectTransform.rect.height.ToString("0") + "：" + t.text);
            }
            Assert.IsEmpty(problems, width + "×" + height + " HUD 文字超出：\n" + string.Join("\n", problems));

            var panels = v.HudPanels().Select(p => new KeyValuePair<string, Rect>(p.name, LocalRect(v.HudRoot, p))).ToList();
            var area = v.HudRoot.rect;
            foreach (var p in panels)
            {
                Assert.IsTrue(p.Value.xMin >= area.xMin - 0.5f && p.Value.xMax <= area.xMax + 0.5f
                    && p.Value.yMin >= area.yMin - 0.5f && p.Value.yMax <= area.yMax + 0.5f, p.Key + " 超出畫面");
            }
            for (int i = 0; i < panels.Count; i++)
            for (int j = i + 1; j < panels.Count; j++)
                Assert.IsFalse(panels[i].Value.Overlaps(panels[j].Value), panels[i].Key + " 與 " + panels[j].Key + " 重疊");

            // 戰場中央（寬 30%–70%、高 30%–75%）不放固定面板。
            var center = Rect.MinMaxRect(area.xMin + area.width * 0.3f, area.yMin + area.height * 0.3f,
                area.xMin + area.width * 0.7f, area.yMin + area.height * 0.75f);
            foreach (var p in panels) Assert.IsFalse(p.Value.Overlaps(center), p.Key + " 遮住戰場中央");
        }

        // ---------------- 場景檔 ----------------

        [Test]
        public void SceneFiles_FlatLabKept_3DSceneUsesArenaView()
        {
            Assert.IsTrue(File.Exists("Assets/Battle/Scenes/CombatLab.unity"), "保留平面 CombatLab 場景");
            const string path = "Assets/Battle/Scenes/CombatLab3D.unity";
            Assert.IsTrue(File.Exists(path), "需要 CombatLab3D 場景");
            string meta = File.ReadAllText("Assets/Battle/UI/Arena/CombatArenaView.cs.meta");
            string guid = Regex.Match(meta, @"guid: (\w+)").Groups[1].Value;
            StringAssert.Contains(guid, File.ReadAllText(path), "場景掛著 CombatArenaView");
            Assert.IsFalse(File.ReadAllText("Assets/Battle/Scenes/CombatLab.unity").Contains(guid), "平面場景不受影響");
        }
    }
}

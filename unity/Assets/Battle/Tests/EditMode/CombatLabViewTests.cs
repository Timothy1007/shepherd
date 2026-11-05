using System.Linq;
using NUnit.Framework;
using Shepherd.Battle.Core;
using Shepherd.Battle.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Battle.Tests
{
    /// <summary>以程式點擊 Combat Lab 介面按鈕，確認畫面與戰鬥狀態同步。</summary>
    public sealed class CombatLabViewTests
    {
        private GameObject root;
        private CombatLabView view;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("CombatLabTest");
            view = root.AddComponent<CombatLabView>();
            view.Build(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        private static void Click(Button b)
        {
            Assert.IsTrue(b.interactable, b.name + " 不可點擊");
            b.onClick.Invoke();
        }

        [Test]
        public void Build_ShowsRealBattleInfo()
        {
            var snap = view.Session.Snapshot();
            StringAssert.Contains("第 1 回合", view.TopText);
            StringAssert.Contains("錨定值 " + snap.Anchor + " / 40", view.TopText);
            StringAssert.Contains("AP 3 / 3", view.TopText);
            StringAssert.Contains("實在論", view.TopText);
            StringAssert.Contains("進行中", view.TopText);
            Assert.AreEqual(3, view.SchoolButtons.Count);
            Assert.AreEqual(snap.Enemies.Count, view.EnemyButtons.Count);
            Assert.AreEqual(7, view.HandButtons.Count(b => b.gameObject.activeSelf));
            CollectionAssert.AreEqual(snap.Hand.Select(c => c.Id).ToArray(), view.HandIds.ToArray());
            for (int i = 0; i < snap.Enemies.Count; i++)
            {
                var label = System.Text.RegularExpressions.Regex.Replace(
                    view.EnemyButtons[i].GetComponentInChildren<Text>().text, "<.*?>", "");
                StringAssert.Contains(snap.Enemies[i].Name, label);
                StringAssert.Contains("失序值 " + snap.Enemies[i].Value, label);
            }
        }

        [Test]
        public void HandClick_StagesAndUnstages()
        {
            int id = view.HandIds[0];
            Click(view.HandButtons[0]);
            Assert.AreEqual(1, view.Session.Staged.Count);
            Assert.AreEqual(id, view.Session.Staged[0].CardId);
            StringAssert.Contains("AP 2 / 3", view.TopText);
            StringAssert.Contains("1. ", view.SequenceButtons[0].GetComponentInChildren<Text>().text);
            StringAssert.Contains("第 1 張", view.HandButtons[0].GetComponentInChildren<Text>().text);
            Assert.IsTrue(view.SequenceButtons[0].interactable);
            Assert.IsFalse(view.SequenceButtons[1].interactable);

            Click(view.HandButtons[0]);
            Assert.AreEqual(0, view.Session.Staged.Count);
            StringAssert.Contains("AP 3 / 3", view.TopText);

            Click(view.HandButtons[1]);
            Click(view.HandButtons[2]);
            Click(view.SequenceButtons[0]);
            Assert.AreEqual(1, view.Session.Staged.Count, "點擊序列欄位移除該張");
            Assert.AreEqual(view.HandIds[2], view.Session.Staged[0].CardId);
        }

        [Test]
        public void SchoolAndTargetButtons()
        {
            Click(view.SchoolButtons[1]);
            Assert.AreEqual(SchoolKind.Process, view.Session.SelectedSchool);
            StringAssert.Contains("過程哲學", view.TopText);
            Click(view.EnemyButtons[1]);
            Assert.AreEqual(2, view.Session.SelectedTargetId);
            StringAssert.Contains("目標", view.EnemyButtons[1].GetComponentInChildren<Text>().text);
        }

        [Test]
        public void PreviewThenCommit_ShowsResultAndAdvancesRound()
        {
            StringAssert.Contains("若直接結束回合", view.PreviewText, "沒有編排時就預估空序列");
            string before = view.Session.Controller.CloneState().Fingerprint();
            Click(view.HandButtons[0]);
            StringAssert.Contains("若現在確認施放", view.PreviewText, "選牌後立即更新預估，不必按按鈕");
            Click(view.HandButtons[1]);
            StringAssert.Contains("敵方回合", view.PreviewText);
            Assert.AreEqual(before, view.Session.Controller.CloneState().Fingerprint(), "即時預覽不改動狀態");

            Click(view.CommitButton);
            StringAssert.Contains("第 2 回合", view.TopText);
            StringAssert.Contains("第 1 回合", view.LogText);
            StringAssert.Contains("行動：攻擊", view.LogText);
            StringAssert.Contains("本回合結果", view.LogText);
            StringAssert.Contains("第 1 回合：", view.MessageText, "訊息列顯示上一回合敵方攻擊的影響");
            StringAssert.Contains("若直接結束回合", view.PreviewText);
            Assert.AreEqual(0, view.Session.Staged.Count);
            Assert.AreEqual(7, view.HandButtons.Count(b => b.gameObject.activeSelf));
            CollectionAssert.AreEqual(view.Session.Snapshot().Hand.Select(c => c.Id).ToArray(), view.HandIds.ToArray(),
                "手牌按鈕與引擎手牌同步");
        }

        [Test]
        public void EmptyCommitButton_EndsRound()
        {
            StringAssert.Contains("空序列", view.CommitButton.GetComponentInChildren<Text>().text);
            Click(view.CommitButton);
            StringAssert.Contains("第 2 回合", view.TopText);
        }

        [Test]
        public void RedrawButton_ThenHandCard_RedrawsOnce()
        {
            Click(view.RedrawButton);
            Assert.IsTrue(view.RedrawMode);
            Click(view.HandButtons[3]);
            Assert.IsFalse(view.RedrawMode);
            StringAssert.Contains("調度", view.LogText);
            Assert.AreEqual(0, view.Session.Snapshot().RedrawsRemaining);
            Assert.IsFalse(view.RedrawButton.interactable, "每回合一次");
            Assert.AreEqual(0, view.Session.Staged.Count, "調度模式點牌不會加入編排");
        }

        [Test]
        public void ResetButton_RestartsWithSeed()
        {
            Click(view.CommitButton);
            view.SeedInput.text = "123";
            Click(view.ResetButton);
            Assert.AreEqual(123, view.Session.Seed);
            StringAssert.Contains("第 1 回合", view.TopText);
            var fresh = new CombatLabSession(RuleSet.CreateDefault(), CombatLabScenario.Default(), 123);
            CollectionAssert.AreEqual(fresh.Snapshot().Hand.Select(c => c.Id).ToArray(), view.HandIds.ToArray());
        }

        private static string Plain(string richText)
        {
            return System.Text.RegularExpressions.Regex.Replace(richText, "<.*?>", "");
        }

        [Test]
        public void NoPreviewButton_PreviewIsAlwaysLive()
        {
            Assert.IsFalse(view.GetComponentsInChildren<Button>(true).Any(b => b.name == "Preview"), "預覽按鈕已改為即時預覽");
            Assert.IsNotNull(view.Session.Forecast);
        }

        [Test]
        public void EnemyCards_ShowWhatTheirAttackDoesToPlayer()
        {
            var f = view.Session.Forecast;
            for (int i = 0; i < view.EnemyButtons.Count; i++)
            {
                var e = f.Enemies[i];
                string text = Plain(view.EnemyText(i));
                Assert.IsTrue(e.Acted && e.Attack > 0, "情境第 1 回合兩個敵人都會攻擊");
                StringAssert.Contains("攻擊 " + e.Attack + " → ", text);
                StringAssert.Contains(e.AnchorLoss > 0 ? "錨定值 −" + e.AnchorLoss : "錨定值無損", text);
            }
            string chip = Plain(view.ChipText("Anchor"));
            StringAssert.Contains(f.AnchorBefore + " → " + f.AnchorAfter, chip, "頂部顯示回合後的錨定值");
        }

        [Test]
        public void HandCards_ShowShortEffect()
        {
            var snap = view.Session.Snapshot();
            for (int i = 0; i < snap.Hand.Count; i++)
            {
                string label = Plain(view.HandButtons[i].GetComponentInChildren<Text>().text);
                StringAssert.Contains(CombatLabText.EffectShort(view.Session.Rules, view.Session.SelectedSchool, snap.Hand[i].Element), label);
            }
        }

        [Test]
        public void HoverTooltip_ShowsCardDetailsAndForecast()
        {
            Assert.IsFalse(view.TooltipVisible);
            var b = view.HandButtons[0];
            var trigger = b.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            Assert.IsNotNull(trigger, "手牌需要滑鼠移入／移出事件");
            var data = new UnityEngine.EventSystems.PointerEventData(null);
            UnityEngine.EventSystems.ExecuteEvents.Execute(b.gameObject, data, UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
            Assert.IsTrue(view.TooltipVisible, "滑鼠移入顯示說明");

            var card = view.Session.Snapshot().Hand[0];
            var school = view.Session.SelectedSchool;
            string text = Plain(view.TooltipText);
            StringAssert.Contains(CombatLabText.Element(card.Element) + "・" + CombatLabText.EffectName(school, card.Element), text);
            StringAssert.Contains(Plain(CombatLabText.EffectDetail(view.Session.Rules, school, card.Element)), text);
            StringAssert.Contains("加入後本回合預估", text);
            StringAssert.Contains("錨定值", text);
            StringAssert.Contains("其他學派", text);

            var group = view.Tooltip.GetComponent<CanvasGroup>();
            Assert.IsFalse(group.blocksRaycasts, "說明框不得攔截滑鼠，否則會閃爍");

            // 點擊後說明框仍在，並改為顯示已編排。
            Click(b);
            Assert.IsTrue(view.TooltipVisible);
            StringAssert.Contains("已在編排第 1 張", Plain(view.TooltipText));

            UnityEngine.EventSystems.ExecuteEvents.Execute(b.gameObject, data, UnityEngine.EventSystems.ExecuteEvents.pointerExitHandler);
            Assert.IsFalse(view.TooltipVisible, "滑鼠移出隱藏說明");
        }

        [Test]
        public void HoverTooltip_DoesNotChangeBattleOrPlan()
        {
            string before = view.Session.Controller.CloneState().Fingerprint();
            for (int i = 0; i < view.HandIds.Count; i++) view.ShowCardTooltip(i);
            Assert.AreEqual(before, view.Session.Controller.CloneState().Fingerprint());
            Assert.AreEqual(0, view.Session.Staged.Count);
        }
    }
}

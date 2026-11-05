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
            Click(view.HandButtons[0]);
            Click(view.HandButtons[1]);
            string before = view.Session.Controller.CloneState().Fingerprint();
            Click(view.PreviewButton);
            StringAssert.Contains("預覽結果", view.PreviewText);
            Assert.AreEqual(before, view.Session.Controller.CloneState().Fingerprint(), "預覽不改動狀態");

            Click(view.CommitButton);
            StringAssert.Contains("第 2 回合", view.TopText);
            StringAssert.Contains("第 1 回合", view.LogText);
            StringAssert.Contains("行動：攻擊", view.LogText);
            StringAssert.Contains("尚未預覽", view.PreviewText);
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
    }
}

using System.Collections;
using NUnit.Framework;
using Shepherd.Battle.Core;
using Shepherd.Exploration.Case01;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Shepherd.Exploration.Tests
{
    /// <summary>Play 模式：實際載入「戰鬥 Demo UI QA」場景，由 Update 驅動 HUD 與事件播放。</summary>
    public sealed class Case01CombatDemoPlayModeTests
    {
        private const string ScenePath = "Assets/Exploration/Scenes/Case01CombatDemoQA.unity";

        private static IEnumerator LoadScene()
        {
#if UNITY_EDITOR
            var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            while (!op.isDone) yield return null;
#else
            Assert.Ignore("只在 Editor 的 PlayMode 測試中執行");
            yield break;
#endif
        }

        private static IEnumerator WaitUntil(System.Func<bool> done, float timeout, string what)
        {
            float t = 0f;
            while (!done() && t < timeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(done(), "逾時：" + what);
        }

        [UnityTest]
        public IEnumerator DemoQaScene_PlaysOneRealRound_ThenRestartsCleanly()
        {
            yield return LoadScene();
            var qa = Object.FindObjectOfType<Case01CombatDemoQA>();
            Assert.IsNotNull(qa);
            yield return null;
            Assert.IsNotNull(Object.FindObjectOfType<EventSystem>());
            Assert.AreEqual(1, Object.FindObjectsOfType<AudioListener>().Length);
            var hud = qa.Hud;
            Assert.IsNotNull(hud);
            Assert.IsTrue(hud.Canvas.enabled);
            hud.OnCard(0);
            hud.OnCard(1);
            yield return null;
            StringAssert.Contains("確認施放（2）", hud.CommitText);
            hud.OnCommit();
            Assert.IsTrue(qa.Arena.IsPlaying);
            yield return WaitUntil(() => !qa.Arena.IsPlaying, 30f, "事件播放完畢（Update 驅動）");
            Assert.Greater(qa.Arena.Stats.Casts, 0);
            yield return null;
            Assert.IsTrue(hud.Fx.CastVisible && hud.Fx.CellLit(0) && hud.Fx.CellLit(1), "施放條逐張點亮（Update 驅動）");
            Assert.IsTrue(hud.Fx.RecapVisible, "回合結果");
            Assert.Greater(hud.Fx.ChipLog.Count, 0, "數值標籤");
            StringAssert.Contains("第 2 回合", hud.PlayerText);
            Assert.AreEqual(qa.Arena.Session.Snapshot().Anchor, qa.Arena.DisplayAnchor, "畫面與引擎狀態一致");

            qa.Restart();
            yield return null;
            yield return null;
            Assert.AreEqual(1, Object.FindObjectsOfType<Shepherd.Battle.UI.Arena.CombatDemoHud>().Length, "重來後只有一個 HUD");
            Assert.AreEqual(1, Object.FindObjectsOfType<Shepherd.Battle.UI.Arena.CombatArenaView>().Length);
            Assert.AreEqual(0, qa.Arena.Session.Snapshot().Round);
        }

        [UnityTest]
        public IEnumerator Tutorial_CompletesInPlayMode_ThenTwoRestartsLeaveNoOverlay()
        {
            yield return LoadScene();
            var qa = Object.FindObjectOfType<Case01CombatDemoQA>();
            Assert.IsNotNull(qa);
            yield return null;
            var hud = qa.Hud;
            var tut = hud.Tutorial;
            Assert.IsTrue(qa.TutorialEnabled);
            Assert.AreEqual(Shepherd.Battle.UI.Arena.CombatTutorialStep.Threat, tut.Step);
            Assert.IsTrue(tut.PanelVisible);
            tut.Continue();
            tut.Continue();
            hud.OnCard(0);
            yield return null;
            tut.Continue();
            Assert.AreEqual(Shepherd.Battle.UI.Arena.CombatTutorialStep.Commit, tut.Step);
            hud.OnCommit();
            yield return null;
            Assert.AreEqual(Shepherd.Battle.UI.Arena.CombatTutorialStep.Results, tut.Step);
            yield return WaitUntil(() => tut.Step == Shepherd.Battle.UI.Arena.CombatTutorialStep.Wrap, 30f, "結算播放完畢後收尾");
            tut.Continue();
            yield return null;
            Assert.IsFalse(tut.Active);
            Assert.IsFalse(tut.PanelVisible);
            Assert.IsTrue(hud.ReplayVisible);

            qa.Restart();
            yield return null;
            qa.Restart();
            yield return null;
            yield return null;
            int panels = 0;
            foreach (var rt in Object.FindObjectsOfType<RectTransform>()) if (rt.name == "TutorialPanel") panels++;
            Assert.AreEqual(1, panels, "重來兩次後只有一份教學面板");
            Assert.AreEqual(1, Object.FindObjectsOfType<AudioListener>().Length);
            Assert.AreEqual(Shepherd.Battle.UI.Arena.CombatTutorialStep.Threat, qa.Hud.Tutorial.Step);
        }
    }
}

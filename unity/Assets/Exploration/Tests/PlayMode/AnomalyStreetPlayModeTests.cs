using System.Collections;
using NUnit.Framework;
using Shepherd.Battle.Core;
using Shepherd.Battle.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// Play 模式：實際載入 AnomalyStreetDemo 場景，由 Update／Time.deltaTime 驅動（與手動遊玩相同的程式路徑），
    /// 以腳本輸入代替鍵盤滑鼠（舊 Input Manager 無法模擬實體按鍵）。
    /// </summary>
    public sealed class AnomalyStreetPlayModeTests
    {
        private const string ScenePath = "Assets/Exploration/Scenes/AnomalyStreetDemo.unity";

        /// <summary>腳本輸入：Hold 為持續按住的狀態；Once 只在下一個影格送出一次。</summary>
        private sealed class ScriptedInput : IExploreInputSource
        {
            public ExploreInput Hold;
            public ExploreInput? Once;

            public ExploreInput Read()
            {
                var r = Hold;
                if (Once.HasValue)
                {
                    var o = Once.Value;
                    r.Interact |= o.Interact;
                    r.ToggleView |= o.ToggleView;
                    r.ReleaseCursor |= o.ReleaseCursor;
                    r.Click |= o.Click;
                    r.Look += o.Look;
                    Once = null;
                }
                return r;
            }
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

        private static IEnumerator LoadScene()
        {
#if UNITY_EDITOR
            var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            while (!op.isDone) yield return null;
#else
            Assert.Ignore("只在 Editor 的 PlayMode 測試中執行");
            yield break;
#endif
        }

        /// <summary>
        /// 滑鼠鎖定：Demo 自己的狀態一定檢查；作業系統游標只在有視窗時檢查
        /// （batchmode 沒有視窗，Cursor.lockState 永遠是 None，需在 Unity 編輯器中親自確認）。
        /// </summary>
        private static void AssertCursor(AnomalyStreetDemo demo, bool captured, string what)
        {
            Assert.AreEqual(captured, demo.CursorCaptured, what);
            if (Application.isBatchMode) return;
            Assert.AreEqual(captured ? CursorLockMode.Locked : CursorLockMode.None, Cursor.lockState, what);
            Assert.AreEqual(!captured, Cursor.visible, what);
        }

        [UnityTest]
        public IEnumerator Scene_WalkIntoEncounter_EntersBattle_InRealPlayMode()
        {
            yield return LoadScene();
            var demo = Object.FindObjectOfType<AnomalyStreetDemo>();
            Assert.IsNotNull(demo, "場景中有 AnomalyStreetDemo");
            yield return null;
            Assert.AreEqual(DemoState.Exploring, demo.State);
            Assert.IsNotNull(Object.FindObjectOfType<EventSystem>());
            Assert.AreEqual(1, Object.FindObjectsOfType<AudioListener>().Length, "只有一個 AudioListener");
            AssertCursor(demo, true, "探索時鎖定滑鼠");

            var input = new ScriptedInput();
            demo.InputSource = input;

            // 滑鼠視角。
            float yaw = demo.Player.Yaw;
            input.Once = new ExploreInput { Look = new Vector2(5f, 0f) };
            yield return null;
            yield return null;
            Assert.AreNotEqual(yaw, demo.Player.Yaw);
            input.Once = new ExploreInput { Look = new Vector2(-5f, 0f) };
            yield return null;
            yield return null;

            // Esc 釋放滑鼠、點擊重新鎖定。
            input.Once = new ExploreInput { ReleaseCursor = true };
            yield return null;
            yield return null;
            AssertCursor(demo, false, "Esc 釋放滑鼠");
            input.Once = new ExploreInput { Click = true };
            yield return null;
            yield return null;
            AssertCursor(demo, true, "點擊重新鎖定");

            // 沿主街往北奔跑，直到遭遇。
            var start = demo.Player.transform.position;
            input.Hold = ExploreInput.Forward(true);
            yield return WaitUntil(() => demo.State != DemoState.Exploring, 15f, "走進遭遇區");
            Assert.Greater(demo.Player.transform.position.z - start.z, 15f, "實際在街上移動了");
            Assert.AreEqual(DemoState.EnteringBattle, demo.State);
            Assert.AreEqual(1, demo.Encounter.TriggerCount);

            // 過場中持續按 W：角色不動。
            var frozen = demo.Player.transform.position;
            yield return WaitUntil(() => demo.State == DemoState.Battle, 5f, "鏡頭過場結束");
            Assert.AreEqual(frozen, demo.Player.transform.position);
            input.Hold = ExploreInput.None;

            var arena = demo.Arena;
            Assert.IsTrue(arena.HudVisible);
            Assert.IsTrue(arena.ArenaCamera.enabled);
            Assert.IsFalse(demo.ExploreCamera.enabled);
            AssertCursor(demo, false, "戰鬥中滑鼠解除鎖定");
            Assert.AreEqual(1, Object.FindObjectsOfType<AudioListener>().Length);

            // 施放一回合：由戰鬥元件自己的 Update 播放事件。
            var s = arena.Session;
            var hand = s.Snapshot().Hand;
            s.AddCard(hand[0].Id);
            arena.CommitButton.onClick.Invoke();
            Assert.IsTrue(arena.LastCommit.Accepted);
            yield return WaitUntil(() => !arena.IsPlaying, 30f, "事件播放完畢");
            Assert.Greater(arena.Stats.Casts, 0);
            Assert.AreEqual(DemoState.Battle, demo.State);
        }

        [UnityTest]
        public IEnumerator Victory_ReturnToExplore_InRealPlayMode()
        {
            yield return LoadScene();
            var old = Object.FindObjectOfType<AnomalyStreetDemo>();
            Object.Destroy(old.gameObject);
            yield return null;

            // 用測試情境（敵人只剩 1 點並帶校正）建立 Demo，同樣由 Update 驅動。
            var scenario = new CombatLabScenario { Name = "quick-victory" };
            scenario.Enemies.Add(new EnemyState { Id = 1, Name = "E1 失序體・甲", Value = 1, Corrections = 1, AttackPattern = new[] { 12 } });
            scenario.Enemies.Add(new EnemyState { Id = 2, Name = "E2 失序體・乙", Value = 1, Corrections = 1, AttackPattern = new[] { 6 } });
            var go = new GameObject("AnomalyStreetDemo");
            go.SetActive(false);
            var demo = go.AddComponent<AnomalyStreetDemo>();
            var input = new ScriptedInput();
            demo.InputSource = input;
            demo.Build(true, scenario, 1);
            go.SetActive(true);
            yield return null;

            demo.Player.Teleport(new Vector3(0.2f, 0.05f, 2f), 0f);
            input.Hold = ExploreInput.Forward();
            yield return WaitUntil(() => demo.State == DemoState.Battle, 10f, "進入戰鬥");
            input.Hold = ExploreInput.None;
            var arena = demo.Arena;
            arena.CommitButton.onClick.Invoke();
            yield return WaitUntil(() => !arena.IsPlaying, 30f, "事件播放完畢");
            Assert.AreEqual(BattleOutcome.Victory, arena.Session.Snapshot().Outcome);
            Assert.IsTrue(arena.BannerVisible);
            arena.BannerButton.onClick.Invoke();
            yield return WaitUntil(() => demo.State == DemoState.Exploring, 5f, "返回探索");
            yield return null;
            Assert.IsTrue(arena == null, "戰鬥物件已銷毀");
            Assert.IsTrue(demo.ExploreCamera.enabled);
            AssertCursor(demo, true, "返回探索後重新鎖定");

            // 返回後在遭遇區內走動 2 秒：不再觸發。
            input.Hold = ExploreInput.Forward();
            float t = 0f;
            var before = demo.Player.transform.position;
            while (t < 2f)
            {
                t += Time.deltaTime;
                yield return null;
                Assert.AreEqual(DemoState.Exploring, demo.State);
            }
            Assert.Greater(Vector3.Distance(before, demo.Player.transform.position), 1f, "可以繼續移動");
            Assert.AreEqual(1, demo.Encounter.TriggerCount);
            Object.Destroy(go);
            yield return null;
        }
    }
}

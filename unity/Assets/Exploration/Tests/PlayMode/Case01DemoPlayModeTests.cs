using System.Collections;
using NUnit.Framework;
using Shepherd.Exploration.Case01;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// v0.7 Play 模式：實際載入 Case01DemoSliceQA 場景（Update／Time.deltaTime 驅動），事務所 → 街區 → 17 號 → 前門回街區；
    /// 每個區域只有一個 AudioListener／相機，屋內沒有 QA 字樣，委託與屋內流程跨區域保留。
    /// </summary>
    public sealed class Case01DemoPlayModeTests
    {
        private const string ScenePath = "Assets/Exploration/Scenes/Case01DemoSliceQA.unity";

        private sealed class ScriptedInput : ICase01InputSource
        {
            public Case01Input? Once;

            public Case01Input Read()
            {
                var r = Once ?? Case01Input.None;
                Once = null;
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

        [UnityTest]
        public IEnumerator DemoScene_Office_Street_House_BackToStreet_InRealPlayMode()
        {
#if UNITY_EDITOR
            var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            while (!op.isDone) yield return null;
#else
            Assert.Ignore("只在 Editor 的 PlayMode 測試中執行");
            yield break;
#endif
            var d = Object.FindObjectOfType<Case01DemoSlice>();
            Assert.IsNotNull(d);
            var input = new ScriptedInput();
            d.InputSource = input;
            yield return null;
            Assert.AreEqual(Case01DemoArea.Office, d.Area);
            Assert.AreEqual(1, Object.FindObjectsOfType<AudioListener>().Length, "事務所：一個 AudioListener");
            d.Interact(Case01DemoIds.OfficeCase);
            d.Hud.HidePanel();
            d.Interact(Case01DemoIds.OfficeDoor);
            yield return WaitUntil(() => d.Area == Case01DemoArea.Street && !d.Transitioning, 5f, "到街區");
            Assert.AreEqual(1, Object.FindObjectsOfType<AudioListener>().Length, "街區：一個 AudioListener");
            d.Interact(Case01DemoIds.StreetListing);
            d.Hud.HidePanel();
            d.Interact(Case01DemoIds.Door17);
            yield return WaitUntil(() => d.Area == Case01DemoArea.House && !d.Transitioning && d.House != null, 5f, "進 17 號");
            yield return null;
            var house = d.House;
            Assert.IsTrue(house.IsDemo);
            Assert.AreEqual(1, Object.FindObjectsOfType<AudioListener>().Length, "屋內：一個 AudioListener（街區的已移除）");
            Assert.AreEqual(Case01Room.Entrance, house.CurrentRoom);
            StringAssert.DoesNotContain("QA", house.Hud.Base.Objective.text);
            house.Flow.Interact(Case01Ids.Table);
            // 對著前門按 E（由 Update 讀腳本輸入）。
            house.Player.Teleport(house.Layout.Spawn.Position + new Vector3(0f, 0f, 0.3f), 180f, 8f);
            yield return null;
            yield return null;
            Assert.AreEqual(Case01Ids.FrontDoor, house.CurrentTarget != null ? house.CurrentTarget.Id : null);
            input.Once = Case01Input.From(new Shepherd.Exploration.ExploreInput { Interact = true });
            yield return WaitUntil(() => d.Area == Case01DemoArea.Street && !d.Transitioning, 5f, "回街區");
            yield return null;
            Assert.IsTrue(house == null, "屋內物件已銷毀");
            Assert.AreEqual(1, Object.FindObjectsOfType<AudioListener>().Length, "回街區：一個 AudioListener");
            Assert.IsTrue(Case01RunState.CaseAccepted);
            Assert.IsTrue(Case01RunState.HouseFlow.Session.Has(Case01Flags.TableTwo), "屋內進度保留");
        }
    }
}

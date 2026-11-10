using System.Collections;
using NUnit.Framework;
using Shepherd.Exploration.Case01;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// Phase 1.9 Play 模式：Update 驅動的 Phase 1.9 QA（與選單開的獨立場景同一個元件、phase19 = true）。
    /// 從前門直接走進走廊（沒有第二道內門）；M 進夾層、K 進中庭（不開戰）、T 圖文巡覽；F2 切 1.8 不留下多餘的玩家或聽者。
    /// </summary>
    public sealed class Case01Phase19PlayModeTests
    {
        private sealed class ScriptedInput : ICase01InputSource
        {
            public Case01Input? Once;
            public Vector2 Move;

            public Case01Input Read()
            {
                var r = Once ?? Case01Input.From(new ExploreInput { Move = Move });
                Once = null;
                return r;
            }
        }

        private static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator Phase19Qa_RealPlayMode_WalkIn_Mezzanine_Courtyard_Tour()
        {
            var go = new GameObject("Case01Phase19QA");
            go.SetActive(false);
            try
            {
                var s = go.AddComponent<Case01ImmersionQA>();
                // 選單開的場景裡 phase19 是序列化的；這裡在啟用前設定，效果相同。
                s.Setup(true, null, null, true, Case01QaMode.CoreV03, true);
                go.SetActive(true);
                var input = new ScriptedInput();
                s.UseInput(input);
                yield return Frames(3);
                Assert.IsTrue(s.Phase19);
                Assert.AreEqual(Case01Floorplan.R3, s.House.Layout.Floorplan);
                Assert.IsNull(s.World.Door("door_vestibule"), "沒有第二道內門");
                s.House.Player.Teleport(new Vector3(7.1f, 0.05f, 0.9f), 0f, 0f);
                input.Move = new Vector2(0f, 1f);
                float t = 0f;
                while (s.House.Player.transform.position.z < 3.0f && t < 4f)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
                input.Move = Vector2.zero;
                Assert.Greater(s.House.Player.transform.position.z, 3.0f, "從前門直接走進走廊");
                input.Once = new Case01Input { Function = 15 };
                yield return Frames(3);
                Assert.AreEqual(Case01Room.Mezzanine, s.House.CurrentRoom, "M：夾層");
                input.Once = new Case01Input { Function = 16 };
                yield return Frames(3);
                Assert.AreEqual(Case01Room.Courtyard, s.House.CurrentRoom, "K：中庭");
                Assert.AreEqual(Case01Mode.Exploring, s.House.Mode, "中庭可以自由走，不開戰");
                input.Once = new Case01Input { Function = 17 };
                yield return Frames(3);
                Assert.AreEqual(0, s.TourIndex, "T：圖文巡覽第一件");
                input.Once = new Case01Input { Function = 2 };
                yield return Frames(3);
                Assert.AreEqual(Case01Floorplan.R2, s.House.Layout.Floorplan, "F2：Phase 1.8 R2");
                Assert.AreEqual(1, go.GetComponentsInChildren<FirstPersonController>(true).Length);
                Assert.AreEqual(1, go.GetComponentsInChildren<AudioListener>(true).Length);
            }
            finally
            {
                Object.Destroy(go);
            }
            yield return null;
        }
    }
}

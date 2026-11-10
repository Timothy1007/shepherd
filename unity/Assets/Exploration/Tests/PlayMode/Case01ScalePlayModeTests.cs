using System.Collections;
using NUnit.Framework;
using Shepherd.Exploration.Case01;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// Phase 1.7 Play 模式：實際以 Update 驅動住宅尺度 QA；腳本輸入經過同一條功能鍵過濾（F9 換格局、F6 步速、F7 眼高），
    /// 在 R1 的走廊上以候選步速往前走，速度與距離相符；只有一個玩家、一個 AudioListener。
    /// </summary>
    public sealed class Case01ScalePlayModeTests
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
        public IEnumerator ScaleQa_RealPlayMode_PlansSpeedEyeAndWalk()
        {
            var go = new GameObject("Case01ScaleQA");
            try
            {
                var s = go.AddComponent<Case01ScaleQA>();
                Assert.IsNotNull(s.House, "Awake 建好 QA");
                var input = new ScriptedInput();
                s.UseInput(input);
                yield return Frames(3);
                Assert.AreEqual(Case01Floorplan.R1, s.Plan, "預設從 R1 開始");
                Assert.AreEqual(Case01Room.Entrance, s.House.CurrentRoom);
                foreach (var expect in new[] { Case01Floorplan.R2, Case01Floorplan.V05A, Case01Floorplan.R1 })
                {
                    input.Once = new Case01Input { Function = 9 };
                    yield return Frames(3);
                    Assert.AreEqual(expect, s.Plan, "F9 經過 QA 輸入過濾換格局");
                    Assert.AreEqual(1, go.GetComponentsInChildren<FirstPersonController>(true).Length, "只有一個玩家");
                    Assert.AreEqual(1, go.GetComponentsInChildren<AudioListener>(true).Length);
                }
                input.Once = new Case01Input { Function = 7 };
                yield return Frames(2);
                Assert.AreEqual(1.72f, s.House.Player.EyeHeightValue, 1e-4f, "F7 眼高");
                Assert.AreEqual(2.2f, s.House.Player.WalkSpeedValue, 1e-4f, "候選步速 2.2 m/s");
                // 沿 R1 的走廊往北走 1.5 秒：距離約等於 2.2 m/s × 時間（不是正式的 3.5 m/s）。
                s.House.Player.Teleport(new Vector3(7.7f, 0.05f, 2.0f), 0f);
                yield return Frames(2);
                var start = s.House.Player.transform.position;
                input.Move = new Vector2(0f, 1f);
                float t = 0f;
                while (t < 1.5f)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
                input.Move = Vector2.zero;
                float d = s.House.Player.transform.position.z - start.z;
                Assert.That(d / t, Is.InRange(1.6f, 2.6f), "實際步速（" + d.ToString("0.00") + " m / " + t.ToString("0.00") + " s）");
                StringAssert.Contains("Phase 1.7", s.House.Hud.Base.ViewMode.text);
                Assert.Greater(s.Walked, 1.5f, "QA 記錄走了多遠");
            }
            finally
            {
                Object.Destroy(go);
            }
            yield return null;
        }
    }
}

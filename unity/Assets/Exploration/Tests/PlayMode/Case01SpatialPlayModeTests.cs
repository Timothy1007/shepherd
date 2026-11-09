using System.Collections;
using NUnit.Framework;
using Shepherd.Exploration.Case01;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// Phase 1.6 Play 模式：實際以 Update 驅動空間結構灰盒 QA；腳本輸入走同一條 F 鍵過濾（F9 換方案、F2 視點、F7 灰盒），
    /// 換方案後下一影格才檢查位置合法性；只有一個玩家、一個 AudioListener、一份房屋。
    /// </summary>
    public sealed class Case01SpatialPlayModeTests
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
        public IEnumerator SpatialQa_RealPlayMode_SwitchVariants_ViewsAndWalk()
        {
            var go = new GameObject("Case01SpatialQA");
            try
            {
                var s = go.AddComponent<Case01SpatialQA>();
                Assert.IsNotNull(s.House, "Awake 建好 QA");
                var input = new ScriptedInput();
                s.UseInput(input);
                yield return Frames(3);
                Assert.AreEqual(Case01Room.Entrance, s.House.CurrentRoom);
                Assert.AreEqual(1, go.GetComponentsInChildren<AudioListener>(true).Length);
                foreach (var expect in new[] { Case01SpatialVariant.B, Case01SpatialVariant.C, Case01SpatialVariant.A })
                {
                    input.Once = new Case01Input { Function = 9 };
                    yield return Frames(3);
                    Assert.AreEqual(expect, s.Variant, "F9 經過 QA 輸入過濾換方案");
                    Assert.AreEqual(1, go.GetComponentsInChildren<FirstPersonController>(true).Length, "只有一個玩家");
                    Assert.AreEqual(1, go.GetComponentsInChildren<AudioListener>(true).Length);
                    Assert.IsTrue(s.Free(s.House.Player.transform.position), "站在合法位置");
                }
                input.Once = new Case01Input { Jump = 2 };
                yield return Frames(3);
                Assert.AreEqual(0, s.ViewIndex, "F2 第一個固定視點");
                Assert.AreEqual(Case01Mode.Exploring, s.House.Mode, "F2 沒有被屋內 QA 當成檢查點跳轉");
                input.Once = new Case01Input { Function = 7 };
                yield return Frames(2);
                Assert.IsFalse(s.Graybox);
                s.Press(9);
                s.Press(9);
                yield return Frames(2);
                Assert.AreEqual(Case01SpatialVariant.C, s.Variant);
                // 前廊裡往北走過內門到內玄關。
                var start = s.House.Player.transform.position;
                s.House.Player.Teleport(new Vector3(7.1f, 0.05f, 1.0f), 0f);
                input.Move = new Vector2(0f, 1f);
                float t = 0f;
                while (s.House.Player.transform.position.z < 2.3f && t < 4f)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
                input.Move = Vector2.zero;
                Assert.Greater(s.House.Player.transform.position.z, 2.3f, "走得過前廊內門（" + start + " → " + s.House.Player.transform.position + "）");
                Assert.AreEqual(Case01Room.Entrance, s.House.CurrentRoom);
                StringAssert.Contains("Phase 1.6", s.House.Hud.Base.ViewMode.text);
            }
            finally
            {
                Object.Destroy(go);
            }
            yield return null;
        }
    }
}

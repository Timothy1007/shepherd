using System.Collections;
using NUnit.Framework;
using Shepherd.Exploration.Case01;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// Phase 1.8 Play 模式：Update 驅動的沉浸式住宅 QA。從前門內按 E（腳本輸入）打開前廊內門、走進走廊；
    /// 環境聲在 Play 中真的在播放、腳步有計數；F2 切原版／沉浸版不留下多餘的玩家或聽者。
    /// </summary>
    public sealed class Case01ImmersionPlayModeTests
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
        public IEnumerator ImmersionQa_RealPlayMode_OpenTheInnerDoor_WalkIn_Sound()
        {
            var go = new GameObject("Case01ImmersionQA");
            try
            {
                var s = go.AddComponent<Case01ImmersionQA>();
                var input = new ScriptedInput();
                s.UseInput(input);
                yield return Frames(3);
                Assert.IsNotNull(s.World, "沉浸版");
                var door = s.World.Door("door_vestibule");
                Assert.AreEqual(Case01DoorState.Closed, door.State, "前廊內門一開始關著");
                // 面向內門、按 E。
                s.House.Player.Teleport(new Vector3(7.1f, 0.05f, 0.75f), 0f, 8f);
                yield return Frames(3);
                Assert.AreEqual("door_vestibule", s.House.CurrentTarget != null ? s.House.CurrentTarget.Id : null, "準星對到內門");
                input.Once = Case01Input.From(new ExploreInput { Interact = true });
                float t = 0f;
                while (!door.IsOpenEnough && t < 3f)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
                Assert.IsTrue(door.IsOpenEnough, "內門打開（" + door.State + "）");
                Assert.IsTrue(s.World.Source("street").A.isPlaying, "環境聲在 Play 中播放");
                input.Move = new Vector2(0f, 1f);
                t = 0f;
                while (s.House.Player.transform.position.z < 3.0f && t < 4f)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
                input.Move = Vector2.zero;
                Assert.Greater(s.House.Player.transform.position.z, 3.0f, "走進走廊");
                Assert.Greater(s.World.StepCount, 1, "有腳步聲");
                input.Once = new Case01Input { Function = 2 };
                yield return Frames(3);
                Assert.IsNull(s.World, "F2：原 R2 測試屋");
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

using System.Collections;
using NUnit.Framework;
using Shepherd.Exploration.Case01;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shepherd.Exploration.Tests
{
    /// <summary>全屋重製 QA 的 Play 模式：真正的 Update 迴圈裡建出新客廳、玩家與鏡頭；走動、開手電筒、關環境燈、拿起合照再放回；只有一個聽者。</summary>
    public sealed class Case01RebuildPlayModeTests
    {
        private static IEnumerator Wait(float sec)
        {
            float t = 0f;
            while (t < sec)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator RebuildQa_RealPlayMode_Walk_Flashlight_Lamps_Photo()
        {
            foreach (var stray in Object.FindObjectsOfType<Case01HouseQA>()) Object.Destroy(stray.transform.root.gameObject);
            yield return null;
            var go = new GameObject("Case01HouseRebuildQA");
            var s = go.AddComponent<Case01RebuildQA>();
            try
            {
                yield return null;
                yield return null;
                Assert.IsNotNull(s.Room);
                Assert.IsNotNull(s.Room.Root);
                Assert.IsTrue(s.Post.Available);
                var start = s.Player.transform.position;
                s.ScriptedInput = new ExploreInput { Move = new Vector2(0f, 1f) };
                float t = 0f;
                while (t < 0.8f)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
                s.ScriptedInput = ExploreInput.None;
                Assert.Greater(Vector3.Distance(start, s.Player.transform.position), 0.1f, "可以走動");
                s.ToggleFlashlight();
                yield return Wait(0.5f);
                Assert.IsTrue(s.Flashlight.Light.enabled, "手電筒在 Play 中亮起");
                s.SetLamps(false);
                Assert.IsFalse(s.Room.Lamps[0].enabled);
                s.SetLamps(true);
                s.BeginInspect();
                yield return Wait(0.6f);
                Assert.IsTrue(s.Inspecting);
                Assert.IsTrue(s.Inspector.StageCamera.enabled, "檢視鏡頭啟用");
                s.Inspector.End();
                yield return Wait(0.8f);
                Assert.IsFalse(s.Inspecting);
                Assert.AreEqual(1, Object.FindObjectsOfType<AudioListener>().Length);
            }
            finally
            {
                Object.Destroy(go);
                if (s != null && s.Room != null && s.Room.Root != null) Object.Destroy(s.Room.Root.gameObject);
            }
            yield return null;
        }
    }
}

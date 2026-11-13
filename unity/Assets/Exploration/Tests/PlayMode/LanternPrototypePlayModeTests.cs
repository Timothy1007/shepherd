using System.Collections;
using NUnit.Framework;
using Shepherd.Exploration.Lantern;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shepherd.Exploration.Tests
{
    /// <summary>實際執行（Update 迴圈）：提燈 QA 啟動、走動、開關提燈。</summary>
    public sealed class LanternPrototypePlayModeTests
    {
        [UnityTest]
        public IEnumerator LanternQA_RunsInPlayMode_WalkAndToggle()
        {
            var go = new GameObject("LanternPlayTest");
            var qa = go.AddComponent<LanternPrototypeQA>();
            yield return null;
            Assert.IsTrue(qa.Ready, "Start 建好場景");
            Assert.IsFalse(qa.Rig.On, "從新版房子門外開始，提燈先放下");
            qa.ToggleLantern();
            var start = qa.Player.transform.position;
            qa.ScriptedInput = new ExploreInput { Move = new Vector2(0f, 1f) };
            float t = 0f;
            while (t < 1.2f) { t += Time.deltaTime; yield return null; }
            qa.ScriptedInput = ExploreInput.None;
            Assert.Greater(Vector3.Distance(start, qa.Player.transform.position), 0.5f, "往前走");
            Assert.Greater(LanternReveal.Radius, 0f);
            qa.ToggleLantern();
            t = 0f;
            while (t < 0.6f) { t += Time.deltaTime; yield return null; }
            Assert.AreEqual(0f, LanternReveal.Radius, 1e-3f, "提燈關掉");
            Object.Destroy(go);
            yield return null;
        }
    }
}

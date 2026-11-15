using System.Linq;
using NUnit.Framework;
using Shepherd.Exploration.Lantern;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// 提燈原型的滑鼠視角與游標鎖定：連續轉圈不卡、俯仰有上限不翻轉、開關介面／Esc／失去焦點後能恢復。
    /// batchmode 沒有視窗，真正的 Cursor.lockState 不會變，所以換成假的游標來確認狀態。
    /// 手感（靈敏度、邊緣會不會停）要在 Play 模式實測。
    /// </summary>
    public sealed class LanternPrototypeMouseLookTests
    {
        private const float Dt = 1f / 60f;
        private GameObject root;
        private CursorLockMode fake;
        private int applies;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                var qa = root.GetComponent<LanternPrototypeQA>();
                if (qa != null) qa.Shutdown();
                Object.DestroyImmediate(root);
            }
            root = null;
            LanternReveal.Clear();
        }

        private LanternPrototypeQA NewQa()
        {
            root = new GameObject("LanternLookTest");
            var qa = root.AddComponent<LanternPrototypeQA>();
            qa.Setup(false);
            fake = CursorLockMode.None;
            applies = 0;
            qa.CursorLock.ReadLock = () => fake;
            qa.CursorLock.Apply = locked => { fake = locked ? CursorLockMode.Locked : CursorLockMode.None; applies++; };
            qa.Step(Dt, ExploreInput.None);
            return qa;
        }

        /// <summary>模擬 Update 的一個影格：先處理游標，沒鎖定時不吃滑鼠位移，再推進遊戲。</summary>
        private static bool Frame(LanternPrototypeQA qa, Vector2 look, bool esc = false, bool click = false, bool held = false)
        {
            bool consumed = qa.HandleCursor(esc, click, held || click);
            var inp = new ExploreInput { Look = look, ReleaseCursor = esc };
            if (!qa.CursorLock.LookActive) inp.Look = Vector2.zero;
            qa.Step(Dt, inp);
            return consumed;
        }

        private static float Gain(LanternPrototypeQA qa) => FirstPersonController.LookSensitivity * qa.Player.LookScale;

        [Test]
        public void ContinuousYaw_SpinsManyTurnsBothWays_WithoutSticking()
        {
            var qa = NewQa();
            Frame(qa, Vector2.zero);
            Frame(qa, Vector2.zero);
            Assert.IsTrue(qa.CursorLock.LookActive, "探索中可以轉視角");
            Assert.AreEqual(CursorLockMode.Locked, fake, "探索時游標鎖定");

            foreach (float perFrameDeg in new[] { 7.3f, -11.7f, 0.9f })
            {
                float expected = qa.Player.Yaw;
                int frames = Mathf.CeilToInt(360f * 4f / Mathf.Abs(perFrameDeg)); // 每個方向至少四圈
                for (int i = 0; i < frames; i++)
                {
                    float before = qa.Player.Yaw;
                    Frame(qa, new Vector2(perFrameDeg / Gain(qa), 0f));
                    expected += perFrameDeg;
                    Assert.AreEqual(perFrameDeg, Mathf.DeltaAngle(before, qa.Player.Yaw), 0.01f, "每一格都轉了同樣的角度（第 " + i + " 格，角度 " + before + "）");
                    Assert.AreEqual(0f, Mathf.DeltaAngle(expected, qa.Player.Yaw), 0.05f, "累計角度正確");
                    Assert.That(qa.Player.Yaw, Is.InRange(0f, 360f));
                }
                // 鏡頭跟著角色的視角走，沒有延遲或平滑。
                Assert.Less(Quaternion.Angle(qa.Cam.transform.rotation, qa.Player.ViewRotation), 0.01f, "鏡頭朝向＝視角");
            }
        }

        [Test]
        public void Pitch_RepeatedUpDown_ClampedAndNeverFlips()
        {
            var qa = NewQa();
            Frame(qa, Vector2.zero);
            Frame(qa, Vector2.zero);
            float yaw = qa.Player.Yaw;
            for (int cycle = 0; cycle < 6; cycle++)
            {
                float dir = cycle % 2 == 0 ? 1f : -1f;
                for (int i = 0; i < 60; i++)
                {
                    Frame(qa, new Vector2(0f, dir * 6f / Gain(qa)));
                    Assert.That(qa.Player.Pitch, Is.InRange(-FirstPersonController.PitchLimit, FirstPersonController.PitchLimit), "俯仰在上限內");
                    Assert.Greater(Vector3.Dot(qa.Cam.transform.up, Vector3.up), 0.1f, "畫面沒有翻過去");
                }
                Assert.AreEqual(dir > 0 ? -FirstPersonController.PitchLimit : FirstPersonController.PitchLimit, qa.Player.Pitch, 0.01f, "推到底就停在上限");
                // 一推到底後反方向馬上就有反應（不用先「退回」多推的量）。
                float atLimit = qa.Player.Pitch;
                Frame(qa, new Vector2(0f, -dir * 2f / Gain(qa)));
                Assert.AreEqual(2f, Mathf.Abs(qa.Player.Pitch - atLimit), 0.01f, "反方向立即生效");
                Frame(qa, new Vector2(0f, dir * 2f / Gain(qa)));
            }
            Assert.AreEqual(0f, Mathf.DeltaAngle(yaw, qa.Player.Yaw), 0.01f, "上下看不會改到水平角度");
        }

        [Test]
        public void CursorUi_ReleasesAndRelocks()
        {
            var qa = NewQa();
            Frame(qa, Vector2.zero);
            Assert.AreEqual(CursorLockMode.Locked, fake);

            // 需要游標的介面（之後的選單）：放開游標、不轉視角；關掉後恢復。
            qa.CursorUiOpen = true;
            float yaw = qa.Player.Yaw;
            Frame(qa, new Vector2(10f, 0f));
            Assert.AreEqual(CursorLockMode.None, fake, "介面開著：游標放開");
            Assert.IsFalse(qa.CursorLock.LookActive);
            Assert.AreEqual(yaw, qa.Player.Yaw, 0.001f, "介面開著時不轉視角");
            qa.CursorUiOpen = false;
            Frame(qa, new Vector2(10f, 0f));
            Assert.AreEqual(CursorLockMode.Locked, fake, "關掉介面：重新鎖定");
            Frame(qa, new Vector2(3f / Gain(qa), 0f));
            Assert.AreEqual(3f, Mathf.DeltaAngle(yaw, qa.Player.Yaw), 0.01f, "關掉介面後可以繼續轉（剛鎖定那一格不吃位移）");

            // Esc：放開滑鼠；再按 Esc 不會又鎖回去；點一下畫面恢復，那一下不算射擊。
            Frame(qa, Vector2.zero, esc: true);
            Assert.AreEqual(CursorLockMode.None, fake, "Esc 放開滑鼠");
            Assert.IsTrue(qa.CursorLock.Paused);
            Frame(qa, Vector2.zero, esc: true);
            Assert.AreEqual(CursorLockMode.None, fake, "再按 Esc 仍是放開");
            Assert.IsTrue(Frame(qa, Vector2.zero, click: true), "點畫面：用來恢復鎖定");
            Assert.AreEqual(CursorLockMode.Locked, fake);
            Assert.IsFalse(qa.CursorLock.PrimaryAllowed, "恢復用的那一下，放開前不算射擊");
            Frame(qa, Vector2.zero, held: true);
            Assert.IsFalse(qa.CursorLock.PrimaryAllowed);
            Frame(qa, Vector2.zero);
            Assert.IsTrue(qa.CursorLock.PrimaryAllowed, "放開左鍵後恢復射擊");

            // 筆記開著按 Esc：只關筆記，滑鼠仍鎖定。
            qa.ToggleNotebook();
            Frame(qa, Vector2.zero, esc: true);
            Assert.IsFalse(qa.NotebookOpen, "Esc 關筆記");
            Assert.AreEqual(CursorLockMode.Locked, fake, "關筆記不放開滑鼠");

            // 近看：滑鼠轉物件（游標維持鎖定），Esc 放下東西後回到探索。
            var item = root.GetComponentsInChildren<LanternInspectable>(true).FirstOrDefault();
            Assert.IsNotNull(item, "場景裡有可以近看的東西");
            qa.BeginInspect(item);
            yaw = qa.Player.Yaw;
            Frame(qa, new Vector2(5f, 0f));
            Assert.AreEqual(CursorLockMode.Locked, fake, "近看時游標仍鎖定（用相對位移轉物件）");
            Assert.AreEqual(yaw, qa.Player.Yaw, 0.001f, "近看時不轉視角");
            Frame(qa, Vector2.zero, esc: true);
            Assert.IsNull(qa.Inspecting, "Esc 放下東西");
            Assert.IsFalse(qa.CursorLock.Paused, "放下東西不是暫停");
            Frame(qa, new Vector2(4f / Gain(qa), 0f));
            Assert.AreEqual(4f, Mathf.DeltaAngle(yaw, qa.Player.Yaw), 0.01f, "放下後繼續轉");
        }

        [Test]
        public void FocusLostAndRegained_LookResumes()
        {
            var qa = NewQa();
            Frame(qa, Vector2.zero);
            Frame(qa, Vector2.zero);
            float yaw = qa.Player.Yaw;

            qa.CursorLock.OnFocus(false);
            Assert.AreEqual(CursorLockMode.None, fake, "失去焦點：放開游標");
            Frame(qa, new Vector2(10f, 0f), click: true);
            Assert.AreEqual(yaw, qa.Player.Yaw, 0.001f, "沒有焦點時不轉視角");
            Assert.AreEqual(CursorLockMode.None, fake);

            qa.CursorLock.OnFocus(true);
            Assert.AreEqual(CursorLockMode.Locked, fake, "回到視窗：自動重新鎖定");
            Frame(qa, new Vector2(50f, 0f), held: true);
            Assert.AreEqual(yaw, qa.Player.Yaw, 0.001f, "剛鎖定那一格不吃位移（避免游標跳回中央造成的大跳動）");
            Assert.IsFalse(qa.CursorLock.PrimaryAllowed, "點回視窗的那一下不算射擊");
            for (int i = 0; i < 200; i++) Frame(qa, new Vector2(9f / Gain(qa), 0f));
            Assert.AreEqual(0f, Mathf.DeltaAngle(yaw + 1800f, qa.Player.Yaw), 0.1f, "恢復後連續轉五圈");

            // 編輯器自己把游標解鎖（按 Esc 或切換視窗時）而程式沒收到：下一格就重新鎖定，不會「以為鎖著、其實游標在亂跑」。
            fake = CursorLockMode.None;
            int before = qa.CursorLock.Reapplied;
            Frame(qa, new Vector2(9f / Gain(qa), 0f));
            Assert.AreEqual(CursorLockMode.Locked, fake, "實際狀態對不上時重新鎖定");
            Assert.AreEqual(before + 1, qa.CursorLock.Reapplied);
            float y0 = qa.Player.Yaw;
            Frame(qa, new Vector2(9f / Gain(qa), 0f));
            Assert.AreEqual(9f, Mathf.DeltaAngle(y0, qa.Player.Yaw), 0.01f, "重新鎖定後繼續轉");
        }

        [Test]
        public void EditorSaysLockedButCursorMoves_ForcesRelockAndAsksForClick()
        {
            // 編輯器讀回來說「鎖著」，但游標其實在螢幕上移動（會跑到邊緣）：要偵測到並強制重鎖。
            var qa = NewQa();
            int lockedCalls = 0;
            qa.CursorLock.ReadLock = () => CursorLockMode.Locked;
            qa.CursorLock.Apply = locked => { if (locked) lockedCalls++; };
            var pos = new Vector2(400f, 300f);
            for (int i = 0; i < 3; i++) { qa.CursorLock.ReportPointer(pos); Frame(qa, Vector2.zero); }
            int start = qa.CursorLock.Reapplied;
            Assert.IsFalse(qa.CursorLock.LockFailing);

            // 游標靜止（真的鎖住時就是這樣）：不會誤判。
            for (int i = 0; i < 40; i++) { qa.CursorLock.ReportPointer(pos); Frame(qa, new Vector2(1f, 0f)); }
            Assert.AreEqual(start, qa.CursorLock.Reapplied, "游標不動＝鎖定正常，不重鎖");

            // 游標一直在動：每兩格強制重鎖一次，三次都沒用就提示點畫面。
            for (int i = 0; i < 6; i++) { pos.x += 25f; qa.CursorLock.ReportPointer(pos); Frame(qa, new Vector2(1f, 0f)); }
            Assert.AreEqual(start + 3, qa.CursorLock.Reapplied, "偵測到游標在動就強制重鎖");
            Assert.IsTrue(qa.CursorLock.LockFailing, "重鎖沒用：提示點一下畫面");

            // 點一下畫面：再鎖一次，這一下不算射擊。
            int before = qa.CursorLock.Reapplied;
            qa.CursorLock.ReportPointer(pos);
            Assert.IsTrue(Frame(qa, Vector2.zero, click: true), "點畫面用來鎖定");
            Assert.AreEqual(before + 1, qa.CursorLock.Reapplied);
            Assert.IsFalse(qa.CursorLock.PrimaryAllowed);

            // 鎖住後游標不再動：提示消失，視角照常轉。
            for (int i = 0; i < 31; i++) { qa.CursorLock.ReportPointer(pos); Frame(qa, Vector2.zero); }
            Assert.IsFalse(qa.CursorLock.LockFailing, "鎖住後提示消失");
            float yaw = qa.Player.Yaw;
            qa.CursorLock.ReportPointer(pos);
            Frame(qa, new Vector2(5f / Gain(qa), 0f));
            Assert.AreEqual(5f, Mathf.DeltaAngle(yaw, qa.Player.Yaw), 0.01f);
            Assert.Greater(lockedCalls, 0);
        }

        [Test]
        public void PlatformNeverReportsLocked_LookStillWorks()
        {
            // 有些情況（batchmode、某些平台）讀回來永遠不是 Locked：重試幾次後不要讓視角一直卡住。
            var qa = NewQa();
            qa.CursorLock.Apply = _ => { };
            qa.CursorLock.ReadLock = () => CursorLockMode.None;
            for (int i = 0; i < 5; i++) Frame(qa, Vector2.zero);
            float yaw = qa.Player.Yaw;
            for (int i = 0; i < 10; i++) Frame(qa, new Vector2(2f / Gain(qa), 0f));
            Assert.AreEqual(20f, Mathf.DeltaAngle(yaw, qa.Player.Yaw), 0.05f);
        }

        [Test]
        public void Sensitivity_ScalesAndClamps()
        {
            var qa = NewQa();
            Frame(qa, Vector2.zero);
            Frame(qa, Vector2.zero);
            Assert.AreEqual(LanternLookSettings.Default, qa.Player.LookScale, "測試用的預設靈敏度");
            qa.Player.LookScale = 2f;
            float yaw = qa.Player.Yaw;
            Frame(qa, new Vector2(1f, 0f));
            Assert.AreEqual(2f * FirstPersonController.LookSensitivity, Mathf.DeltaAngle(yaw, qa.Player.Yaw), 0.01f, "靈敏度倍率線性放大");
            Assert.AreEqual(LanternLookSettings.Min, LanternLookSettings.Clamp(0.01f));
            Assert.AreEqual(LanternLookSettings.Max, LanternLookSettings.Clamp(10f));
            Assert.AreEqual(1.3f, LanternLookSettings.Clamp(1.2999f), 0.0001f);
        }
    }
}

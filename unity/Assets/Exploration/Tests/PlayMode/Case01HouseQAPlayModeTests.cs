using System.Collections;
using System.Linq;
using NUnit.Framework;
using Shepherd.Exploration.Case01;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// Play 模式：實際載入 Case01HouseInteriorQA 場景，由 Update／Time.deltaTime 驅動（與手動遊玩相同的程式路徑），
    /// 以腳本輸入代替鍵盤滑鼠（舊 Input Manager 無法模擬實體按鍵）。
    /// </summary>
    public sealed class Case01HouseQAPlayModeTests
    {
        private const string ScenePath = "Assets/Exploration/Scenes/Case01HouseInteriorQA.unity";

        private sealed class ScriptedInput : ICase01InputSource
        {
            public Case01Input Hold;
            public Case01Input? Once;

            public Case01Input Read()
            {
                var r = Hold;
                if (Once.HasValue)
                {
                    var o = Once.Value;
                    r.Base.Interact |= o.Base.Interact;
                    r.Base.Look += o.Base.Look;
                    r.Base.ReleaseCursor |= o.Base.ReleaseCursor;
                    r.Base.Click |= o.Base.Click;
                    if (o.Choice != 0) r.Choice = o.Choice;
                    if (o.Jump != 0) r.Jump = o.Jump;
                    if (o.Function != 0) r.Function = o.Function;
                    r.Journal |= o.Journal;
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
            var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            while (!op.isDone) yield return null;
#else
            Assert.Ignore("只在 Editor 的 PlayMode 測試中執行");
            yield break;
#endif
        }

        private static Vector2 LookDelta(FirstPersonController p, Vector3 target, bool level)
        {
            var d = target - p.EyePosition;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = level ? 0f : -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            return new Vector2(Mathf.DeltaAngle(p.Yaw, yaw) / FirstPersonController.LookSensitivity,
                -(pitch - p.Pitch) / FirstPersonController.LookSensitivity);
        }

        /// <summary>每影格用滑鼠轉向目標並按住 W，直到到達或觸發其他狀態。</summary>
        private static IEnumerator WalkTo(Case01HouseQA qa, ScriptedInput input, float x, float z, float timeout = 12f)
        {
            float t = 0f;
            while (t < timeout && qa.Mode == Case01Mode.Exploring)
            {
                var p = qa.Player.transform.position;
                if (new Vector2(x - p.x, z - p.z).magnitude < 0.3f) break;
                input.Hold = Case01Input.From(new ExploreInput
                {
                    Move = new Vector2(0f, 1f), Look = LookDelta(qa.Player, new Vector3(x, qa.Player.EyePosition.y, z), true),
                });
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            input.Hold = Case01Input.None;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_WalkToDining_InvestigateTable_InRealPlayMode()
        {
            yield return LoadScene();
            var qa = Object.FindObjectOfType<Case01HouseQA>();
            Assert.IsNotNull(qa, "場景中有 Case01HouseQA");
            yield return null;
            Assert.AreEqual(Case01Mode.Exploring, qa.Mode);
            Assert.AreEqual(Case01Room.Entrance, qa.CurrentRoom);
            Assert.IsNotNull(Object.FindObjectOfType<EventSystem>());
            Assert.AreEqual(1, Object.FindObjectsOfType<AudioListener>().Length, "只有一個 AudioListener");
            Assert.IsTrue(qa.CursorCaptured);
            if (!Application.isBatchMode) Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState);

            var input = new ScriptedInput();
            qa.InputSource = input;
            Assert.IsTrue(qa.Hud.MonologueVisible, "v0.4：進門自語（字幕顯示時照常移動與調查）");
            yield return WalkTo(qa, input, 6.5f, 4.4f);
            yield return WalkTo(qa, input, 6.5f, 5.8f);
            yield return WalkTo(qa, input, 6.6f, 6.2f);
            Assert.AreEqual(Case01Room.Dining, qa.CurrentRoom, "實際走進餐廳");

            var hot = qa.Layout.Interactables[Case01Ids.Table];
            var target = hot.transform.TransformPoint(hot.GetComponent<BoxCollider>().center);
            input.Once = Case01Input.From(new ExploreInput { Look = LookDelta(qa.Player, target, false) });
            yield return null;
            yield return null;
            Assert.IsNotNull(qa.CurrentTarget);
            Assert.AreEqual(Case01Ids.Table, qa.CurrentTarget.Id);
            input.Once = Case01Input.From(new ExploreInput { Interact = true });
            yield return null;
            yield return null;
            Assert.IsTrue(qa.Session.Has(Case01Flags.TableTwo));
            Assert.IsTrue(qa.Hud.PanelVisible);
            Assert.IsFalse(qa.Hud.MonologueVisible, "調查面板開著時自語不疊在上面");
        }

        [UnityTest]
        public IEnumerator ArtQaLaunch_ManifestationSkippable_AudioStopsWhenSceneUnloads_InRealPlayMode()
        {
            PlayerPrefs.SetString(Case01HouseQA.LaunchModeKey, "art");
            yield return LoadScene();
            var qa = Object.FindObjectOfType<Case01HouseQA>();
            Assert.AreEqual(Case01QaMode.ArtPreview, qa.QaMode, "選單的一次性啟動參數");
            Assert.IsFalse(PlayerPrefs.HasKey(Case01HouseQA.LaunchModeKey), "讀取後刪除");
            var input = new ScriptedInput();
            qa.InputSource = input;
            yield return null;
            input.Once = new Case01Input { Function = 6 };
            yield return null;
            yield return null;
            Assert.AreEqual(1, qa.HouseStage, "F6 切換房屋階段");
            input.Once = new Case01Input { Function = 7 };
            yield return WaitUntil(() => qa.Mode == Case01Mode.Manifesting, 3f, "F7 開始形成演出");
            yield return new WaitForSeconds(0.5f);
            input.Once = Case01Input.From(new ExploreInput { Interact = true });
            yield return WaitUntil(() => qa.Mode == Case01Mode.Exploring, 2f, "E 跳過");
            Assert.IsTrue(qa.Manifestation.Skipped);
            var sources = qa.GetComponentsInChildren<AudioSource>();
            Assert.Greater(sources.Length, 0);
            Object.Destroy(qa.gameObject);
            yield return null;
            Assert.IsTrue(sources.All(s => s == null || !s.isPlaying), "Case01 的聲音隨場景物件停止");
        }

        private static IEnumerator AimAndPress(Case01HouseQA qa, ScriptedInput input, string id)
        {
            var hot = qa.Layout.Interactables[id];
            input.Once = Case01Input.From(new ExploreInput
            {
                Look = LookDelta(qa.Player, hot.transform.TransformPoint(hot.GetComponent<BoxCollider>().center), false),
            });
            yield return null;
            yield return null;
            input.Once = Case01Input.From(new ExploreInput { Interact = true });
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator CoreV03_FullCorrection_OneWalkThroughTriangle_NoSoftlock_InRealPlayMode()
        {
            PlayerPrefs.SetString(Case01HouseQA.LaunchModeKey, "v03");
            yield return LoadScene();
            var qa = Object.FindObjectOfType<Case01HouseQA>();
            var input = new ScriptedInput();
            qa.InputSource = input;
            yield return null;
            Assert.AreEqual(Case01CorrectionMode.RevisedV03, qa.Flow.Correction, "選單的 v0.3 啟動參數");
            Assert.AreEqual(Case01Room.Mezzanine, qa.CurrentRoom, "從戰後核前開始");
            yield return WalkTo(qa, input, 40f, -0.4f);
            yield return AimAndPress(qa, input, Case01Ids.Core);
            Assert.IsNotNull(qa.Hud.ActiveChoice, "校正裝置");
            input.Once = new Case01Input { Choice = 3 };
            yield return null;
            yield return null;
            Assert.AreEqual(Case01IdsV03.ChoiceUndo, qa.Hud.ActiveChoice.Id, "選錯看到反證");
            input.Once = new Case01Input { Choice = 1 };
            yield return null;
            yield return null;
            input.Once = new Case01Input { Choice = 1 };
            yield return null;
            yield return null;
            Assert.IsTrue(qa.Flow.V03Anchored);
            input.Once = Case01Input.From(new ExploreInput { Interact = true });
            yield return null;
            yield return AimAndPress(qa, input, Case01Ids.Core);
            input.Once = new Case01Input { Choice = 1 };
            yield return null;
            yield return null;
            Assert.IsTrue(qa.Flow.V03SecondLink);
            input.Once = Case01Input.From(new ExploreInput { Interact = true });
            yield return null;
            // 走過 △ 門（西側）一次即寫回。
            yield return WalkTo(qa, input, 36f, 0f);
            yield return WalkTo(qa, input, 33.4f, 0f);
            yield return WaitUntil(() => qa.Mode == Case01Mode.Exploring && qa.CurrentRoom == Case01Room.Study, 4f, "到達書房");
            Assert.IsTrue(qa.Flow.CoreStabilized, "一次穿門寫回");
            Assert.IsTrue(qa.DoorSnapPlaying || qa.DoorSnapPieces > 0, "門框歸位演出");
            // 從書房走回樓梯平台：一般門洞，不再錯接到餐廳。
            yield return WalkTo(qa, input, Case01HouseBuilder.StudyDoorX, 4.2f);
            Assert.AreEqual(Case01Room.Landing, qa.CurrentRoom);
            yield return WalkTo(qa, input, Case01HouseBuilder.StudyDoorX, 5.8f);
            Assert.AreEqual(Case01Room.Study, qa.CurrentRoom, "書房門已歸位");
            Assert.AreEqual(3, qa.HouseStage);
        }

        [UnityTest]
        public IEnumerator Scene_JumpToS40_CoreTriggersDefense_ThenExistingBattle_InRealPlayMode()
        {
            yield return LoadScene();
            var qa = Object.FindObjectOfType<Case01HouseQA>();
            var input = new ScriptedInput();
            qa.InputSource = input;
            yield return null;
            input.Once = new Case01Input { Jump = 3 };
            yield return null;
            yield return null;
            Assert.AreEqual(Case01Room.Mezzanine, qa.CurrentRoom, "F3 跳到夾層");
            yield return WalkTo(qa, input, 40f, -0.4f);
            var hot = qa.Layout.Interactables[Case01Ids.Core];
            input.Once = Case01Input.From(new ExploreInput
            {
                Look = LookDelta(qa.Player, hot.transform.TransformPoint(hot.GetComponent<BoxCollider>().center), false),
            });
            yield return null;
            yield return null;
            input.Once = Case01Input.From(new ExploreInput { Interact = true });
            yield return WaitUntil(() => qa.Mode == Case01Mode.Defense, 2f, "異域反制");
            yield return WaitUntil(() => qa.Mode == Case01Mode.Exploring && qa.CurrentRoom == Case01Room.Courtyard && qa.Manifested && qa.Manifestation.Done, 14f, "被拉進中庭並看完失序體形成");
            yield return WalkTo(qa, input, 40f, 46f, 15f);
            yield return WaitUntil(() => qa.Mode == Case01Mode.Battle, 6f, "進入戰鬥");
            var arena = qa.Arena;
            Assert.IsTrue(arena.HudVisible);
            Assert.IsFalse(qa.CursorCaptured, "戰鬥中滑鼠解除鎖定");
            Assert.AreEqual(1, Object.FindObjectsOfType<AudioListener>().Length);
            var s = arena.Session;
            s.AddCard(s.Snapshot().Hand[0].Id);
            arena.CommitButton.onClick.Invoke();
            Assert.IsTrue(arena.LastCommit.Accepted, "既有戰鬥接受出牌");
            yield return WaitUntil(() => !arena.IsPlaying, 30f, "事件播放完畢");
            Assert.Greater(arena.Stats.Casts, 0);
            Assert.IsTrue(qa.Session.Has(Case01Flags.BattleStarted));
        }
    }
}

using System.Diagnostics;
using System.IO;
using System.Text;
using Shepherd.Exploration.Lantern;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// 三種實機渲染即時切換 QA（docs/CH17_RENDER_STYLE_SWITCH_QA_TASK.md）的入口。
    /// 獨立場景 Assets/Exploration/Scenes/LanternRenderStyleQA.unity：提燈 QA 本體＋LanternRenderStyleSwitch。
    /// 正式的 LanternPrototypeQA.unity 不受影響。
    /// </summary>
    public static class LanternRenderStyleQAEditor
    {
        public const string ScenePath = "Assets/Exploration/Scenes/LanternRenderStyleQA.unity";

        [MenuItem("Shepherd/Prototype/Case01 三種渲染切換 QA（按 F6 切換 A／B／C）")]
        public static void Launch()
        {
            if (!File.Exists(ScenePath)) BuildScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Shepherd/Prototype/Case01 三種渲染切換 QA 重建場景")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            new GameObject("LanternPrototypeQA").AddComponent<LanternPrototypeQA>();
            new GameObject("RenderStyleSwitch").AddComponent<LanternRenderStyleSwitch>();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }

        public static void BuildFromCommandLine() => BuildScene();

        /// <summary>
        /// 驗證用截圖（不是主要交付；主要交付是自己進場景走動切換）：同一位置依序切 A→B→C，
        /// 每種拍褪色態與提燈照亮的畫面，量切換耗時、每種模式的渲染耗時，確認切換不會移動玩家。
        /// </summary>
        public static void CaptureFromCommandLine()
        {
            string dir = null;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-captureDir") dir = args[i + 1];
            Capture(dir ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/RenderStyleQA")));
        }

        public static void Capture(string dir)
        {
            Directory.CreateDirectory(dir);
            BuildScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var log = new StringBuilder();
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            LanternGraybox.DiningArtExperiment = 3;   // 編輯模式不會跑 Awake，手動設定（和遊玩時一樣）
            var qa = Object.FindObjectOfType<LanternPrototypeQA>();
            var sw = Object.FindObjectOfType<LanternRenderStyleSwitch>();
            try
            {
                qa.Setup(false);
                qa.Rig.HideHands = true;
                Frames(qa, sw, 10);
                sw.Bind(qa);
                sw.EnterDining();
                var st = qa.World.V2DiningStruggle;
                if (st != null && st.Door != null && !st.Door.IsOpen) st.Door.Toggle();
                var o = LanternGraybox.V2Origin;
                Vector3 R(float X, float y, float Y) => o + new Vector3(0.125f + X, y, 6.56f + Y);
                var views = new (string tag, Vector3 eye, Vector3 look)[]
                {
                    ("west_fireplace", R(5.0f, 0.05f, 3.5f), R(0f, 1.2f, 4.7f)),
                    ("north_from_sitting", R(3.7f, 0.05f, 1.0f), R(2.4f, 1.25f, 6.3f)),
                    ("close_table", R(4.2f, 0.05f, 3.6f), R(3.05f, 0.75f, 4.45f)),
                };
                log.AppendLine("## 切換");
                foreach (var v in views)
                {
                    qa.Player.Teleport(v.eye, 0f, 0f);
                    LanternPrototypeBuilder.Aim(qa, v.look);
                    Frames(qa, sw, 6);
                    foreach (var lantern in new[] { false, true })
                    {
                        qa.Rig.SetOn(lantern);
                        Frames(qa, sw, lantern ? 30 : 15);
                        var pos0 = qa.Player.transform.position; float yaw0 = qa.Player.Yaw;
                        for (int s = 0; s < 3; s++)
                        {
                            var sws = Stopwatch.StartNew();
                            sw.Apply((LanternRenderStyleSwitch.Style)s);
                            sws.Stop();
                            Frames(qa, sw, 2);
                            var rs = Stopwatch.StartNew();
                            for (int k = 0; k < 5; k++) Render(qa, rt);
                            rs.Stop();
                            Save(rt, Path.Combine(dir, v.tag + (lantern ? "_lantern_" : "_faded_") + (LanternRenderStyleSwitch.Style)s + ".png"));
                            log.AppendLine("- " + v.tag + (lantern ? "（提燈）" : "（褪色）") + " " + (LanternRenderStyleSwitch.Style)s
                                + "：切換 " + sws.Elapsed.TotalMilliseconds.ToString("0.00") + " ms，渲染 " + (rs.Elapsed.TotalMilliseconds / 5).ToString("0.0") + " ms/張");
                        }
                        float moved = Vector3.Distance(pos0, qa.Player.transform.position);
                        log.AppendLine("  位置移動 " + moved.ToString("0.000") + " m、轉向 " + Mathf.Abs(Mathf.DeltaAngle(yaw0, qa.Player.Yaw)).ToString("0.0") + "°");
                    }
                }
                log.AppendLine();
                log.AppendLine("## 各模式改了什麼");
                for (int s = 0; s < 3; s++) log.AppendLine("- " + LanternRenderStyleSwitch.Names[s] + "：" + LanternRenderStyleSwitch.Describe((LanternRenderStyleSwitch.Style)s));
                log.AppendLine("ok");
            }
            catch (System.Exception e)
            {
                log.AppendLine("error: " + e);
                throw;
            }
            finally
            {
                if (sw != null) sw.Restore();
                if (qa != null) qa.Shutdown();
                LanternGraybox.DiningArtExperiment = 0;
                Object.DestroyImmediate(rt);
                File.WriteAllText(Path.Combine(dir, "render_style_qa.md"), log.ToString(), new UTF8Encoding(false));
            }
        }

        private static void Frames(LanternPrototypeQA qa, LanternRenderStyleSwitch sw, int n)
        {
            for (int i = 0; i < n; i++)
            {
                qa.Step(1f / 30f, ExploreInput.None);
                // 編輯模式沒有 LateUpdate：手動套用一次（遊玩時由 LateUpdate 每格套用）。
                if (sw != null && sw.Qa != null) sw.ApplyFrame();
            }
            qa.Rig.Snap();
            qa.Post.Apply();
        }

        private static void Render(LanternPrototypeQA qa, RenderTexture rt)
        {
            var cam = qa.Cam;
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
        }

        private static void Save(RenderTexture rt, string path)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}

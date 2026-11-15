using System.IO;
using System.Text;
using Shepherd.Exploration.Lantern;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// 餐廳「空間層次與探索構圖」QA（docs/level/CASE01_DINING_SPATIAL_DEPTH_QA_PLAN.md）的入口與 A／B 對照截圖。
    /// 獨立場景 Assets/Exploration/Scenes/LanternDiningLayoutQA.unity；正式場景與渲染 A/B/C QA 都不受影響。
    /// </summary>
    public static class LanternDiningLayoutQAEditor
    {
        public const string ScenePath = "Assets/Exploration/Scenes/LanternDiningLayoutQA.unity";

        /// <summary>走動路線（HP 座標）：門口 → 起居區 → 開口西側 → 壁爐前 → 廚房門 → 壁龕櫃前 → 北側椅背後 → 碗櫃前 → 開口東側 → 書櫃旁 → 電視前 → 書櫃旁 → 門口。</summary>
        public static readonly (string name, float x, float z)[] Route =
        {
            ("起居區中間", 3.02f, 6.28f), ("橫樑開口西側", 1.33f, 7.51f), ("壁爐前走道", 1.10f, 8.59f), ("往廚房的門", 1.52f, 9.66f),
            ("壁龕櫃前", 0.87f, 9.47f), ("北側椅背後", 1.52f, 9.62f), ("北側椅背後（東）", 3.56f, 9.62f), ("碗櫃前", 3.71f, 8.12f),
            ("橫樑開口東側", 3.63f, 6.82f), ("書櫃旁", 2.56f, 6.47f), ("電視前", 2.29f, 5.78f), ("書櫃旁（回程）", 2.56f, 6.47f), ("門口", 4.1f, 6.45f),
        };

        [MenuItem("Shepherd/Prototype/Case01 餐廳空間構圖 QA（F6 切換 A／B）")]
        public static void Launch()
        {
            if (!File.Exists(ScenePath)) BuildScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Shepherd/Prototype/Case01 餐廳空間構圖 QA 重建場景")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            new GameObject("LanternPrototypeQA").AddComponent<LanternPrototypeQA>();
            new GameObject("DiningLayoutSwitch").AddComponent<LanternDiningLayoutSwitch>();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }

        public static void CaptureFromCommandLine()
        {
            string dir = null;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-captureDir") dir = args[i + 1];
            Capture(dir ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/DiningLayoutQA")));
        }

        /// <summary>四個同機位（入口第一眼、餐桌側面、壁爐側面、南側起居角）× A／B 只改擺設／B＋光照引導；褪色態與記憶態參考各一組；B 的走動與淨寬。</summary>
        public static void Capture(string dir)
        {
            Directory.CreateDirectory(dir);
            BuildScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var log = new StringBuilder();
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var qa = Object.FindObjectOfType<LanternPrototypeQA>();
            var sw = Object.FindObjectOfType<LanternDiningLayoutSwitch>();
            try
            {
                qa.Setup(false);
                qa.Rig.HideHands = true;
                sw.Bind(qa);
                sw.EnterAtDoor();
                Frames(qa, 20);
                var w = qa.World;
                var o = LanternGraybox.V2Origin;
                Vector3 R(float X, float y, float Y) => o + new Vector3(0.125f + X, y, 6.56f + Y);
                Vector3 HP(float x, float z) => o + new Vector3(x * LanternGraybox.V2Scale, 0.05f, z * LanternGraybox.V2Scale);
                var views = new (string tag, Vector3 eye, Vector3 look)[]
                {
                    ("1_entrance", R(5.3f, 0.05f, 1.83f), R(0.5f, 1.1f, 2.4f)),
                    ("2_table_side", R(3.6f, 0.05f, 2.2f), R(2.6f, 0.85f, 5.6f)),
                    ("3_fireplace_side", R(2.6f, 0.05f, 3.2f), R(0.3f, 1.2f, 4.6f)),
                    ("4_sitting_corner", R(3.2f, 0.05f, 3.3f), R(1.4f, 0.8f, 0.6f)),
                };
                void Pass(string prefix)
                {
                    foreach (var v in views)
                    {
                        qa.Player.Teleport(v.eye, 0f, 0f);
                        LanternPrototypeBuilder.Aim(qa, v.look);
                        Frames(qa, 8);
                        Shot(qa, rt, Path.Combine(dir, prefix + v.tag + ".png"));
                    }
                }
                var variants = new (string tag, bool b, bool focus)[] { ("A_", false, false), ("B_layout_", true, false), ("B_focus_", true, true) };
                // 褪色態（玩家預設看到的）。
                qa.Rig.SetOn(false);
                foreach (var v in variants) { sw.SetFocus(v.focus); sw.SetB(v.b); Frames(qa, 4); Pass("faded_" + v.tag); }
                // 記憶態參考（整間顯現，只用來看構圖本身，不是玩家實際畫面）。
                var inside = w.V2Interior; inside.Expand(new Vector3(0.5f, 1f, 4f));
                LanternReveal.AddRoom(inside, inside.center, true);
                Frames(qa, 4);
                foreach (var v in variants) { sw.SetFocus(v.focus); sw.SetB(v.b); Frames(qa, 4); Pass("memory_" + v.tag); }

                // B 的走動與淨寬；A 也走一次對照。
                foreach (var b in new[] { true, false })
                {
                    sw.SetB(b);
                    Frames(qa, 2);
                    qa.Player.Teleport(HP(4.1f, 6.45f), -90f, 0f);
                    Frames(qa, 2);
                    log.AppendLine("## 走動測試（" + (b ? "B" : "A") + "）");
                    foreach (var (name, x, z) in Route)
                    {
                        bool ok = Walk(qa, HP(x, z), 600, out float secs);
                        log.AppendLine("- " + name + "：" + (ok ? "到了（" + secs.ToString("0.0") + " 秒）" : "卡住（" + qa.Player.transform.position + "）"));
                    }
                    Physics.SyncTransforms();
                    var res = LanternClearance.Measure(w.V2Root, w.V2ClearanceRoute());
                    log.AppendLine();
                    log.AppendLine("## 動線淨寬（" + (b ? "B" : "A") + "）");
                    log.Append(LanternClearance.Report(res));
                    log.AppendLine();
                }
                log.AppendLine("ok");
            }
            catch (System.Exception e) { log.AppendLine("error: " + e); throw; }
            finally
            {
                if (qa != null) qa.Shutdown();
                Object.DestroyImmediate(rt);
                File.WriteAllText(Path.Combine(dir, "dining_layout_qa.md"), log.ToString(), new UTF8Encoding(false));
            }
        }

        private static void Frames(LanternPrototypeQA qa, int n)
        {
            for (int i = 0; i < n; i++) qa.Step(1f / 30f, ExploreInput.None);
            qa.Rig.Snap();
            qa.Post.Apply();
        }

        private static bool Walk(LanternPrototypeQA qa, Vector3 target, int maxFrames, out float secs)
        {
            var p = qa.Player;
            const float dt = 1f / 30f;
            for (int i = 0; i < maxFrames; i++)
            {
                var d = target - p.transform.position; d.y = 0f;
                if (d.magnitude < 0.35f) { secs = i * dt; return true; }
                float want = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                float look = Mathf.Clamp(Mathf.DeltaAngle(p.Yaw, want), -20f, 20f) / (FirstPersonController.LookSensitivity * p.LookScale);
                qa.Step(dt, new ExploreInput { Move = new Vector2(0f, 1f), Look = new Vector2(look, 0f) });
            }
            secs = maxFrames * dt;
            return false;
        }

        private static void Shot(LanternPrototypeQA qa, RenderTexture rt, string path)
        {
            var cam = qa.Cam;
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            var prevA = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevA;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}

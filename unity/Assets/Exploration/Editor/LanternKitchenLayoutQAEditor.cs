using System.IO;
using System.Text;
using Shepherd.Exploration.Lantern;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// 廚房「空間改造 B 方案」QA 的入口與 A／B 對照截圖（四個固定機位、走動、淨寬）。
    /// 獨立場景 Assets/Exploration/Scenes/LanternKitchenLayoutQA.unity；正式場景與渲染 A/B/C QA 都不受影響。
    /// </summary>
    public static class LanternKitchenLayoutQAEditor
    {
        public const string ScenePath = "Assets/Exploration/Scenes/LanternKitchenLayoutQA.unity";

        /// <summary>走動路線（HP 座標）：廚房門內 → 爐灶前 → 流理台北段 → 水槽前 → 高櫃（K2）前 → 後門前 → 窗邊 → 冰箱（K1）前 → 廚房門內。</summary>
        public static readonly (string name, float x, float z)[] Route =
        {
            ("爐灶前", 1.019f, 11.827f), ("流理台北段前", 1.096f, 13.327f), ("水槽前", 1.865f, 13.942f), ("高櫃前（K2）", 2.596f, 13.942f),
            ("後門前", 3.404f, 13.788f), ("廚房中央", 1.865f, 13.173f), ("廚房中段", 1.865f, 11.635f), ("冰箱前（K1）", 2.865f, 11.019f),
            ("廚房門內（回程）", 1.635f, 10.558f),
        };

        [MenuItem("Shepherd/Prototype/Case01 廚房空間改造 QA（F6 切換 A／B）")]
        public static void Launch()
        {
            if (!File.Exists(ScenePath)) BuildScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Shepherd/Prototype/Case01 廚房空間改造 QA 重建場景")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            new GameObject("LanternPrototypeQA").AddComponent<LanternPrototypeQA>();
            new GameObject("KitchenLayoutSwitch").AddComponent<LanternKitchenLayoutSwitch>();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }

        public static void CaptureFromCommandLine()
        {
            string dir = null;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-captureDir") dir = args[i + 1];
            Capture(dir ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/KitchenLayoutQA")));
        }

        /// <summary>四個同機位（入口第一眼、爐灶凹槽、窗邊小桌、儲物與後門）× A／B／B 生活細節關閉；褪色態與記憶態參考各一組；A、B 的走動與淨寬。</summary>
        public static void Capture(string dir)
        {
            Directory.CreateDirectory(dir);
            BuildScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var log = new StringBuilder();
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var qa = Object.FindObjectOfType<LanternPrototypeQA>();
            var sw = Object.FindObjectOfType<LanternKitchenLayoutSwitch>();
            try
            {
                qa.Setup(false);
                qa.Rig.HideHands = true;
                sw.Bind(qa);
                sw.EnterAtDoor();
                Frames(qa, 20);
                var w = qa.World;
                var o = LanternGraybox.V2Origin;
                Vector3 R(float X, float y, float Y) => o + new Vector3(0.125f + X, y, 13.125f + Y);
                Vector3 HP(float x, float z) => o + new Vector3(x * LanternGraybox.V2Scale, 0.05f, z * LanternGraybox.V2Scale);
                var views = new (string tag, Vector3 eye, Vector3 look)[]
                {
                    ("1_entrance", R(2.0f, 0.05f, 0.5f), R(2.2f, 1.2f, 6.0f)),
                    ("2_cooker_recess", R(2.6f, 0.05f, 1.0f), R(0.2f, 1.1f, 2.5f)),
                    ("3_window_table", R(1.4f, 0.05f, 4.6f), R(4.6f, 0.9f, 2.2f)),
                    ("4_storage_backdoor", R(1.6f, 0.05f, 2.6f), R(3.9f, 1.2f, 5.9f)),
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
                var variants = new (string tag, bool b, bool life)[] { ("A_", false, true), ("B_", true, true), ("B_sparse_", true, false) };
                // 褪色態（玩家預設看到的）。
                qa.Rig.SetOn(false);
                foreach (var v in variants) { sw.SetLife(v.life); sw.SetB(v.b); Frames(qa, 4); Pass("faded_" + v.tag); }
                // 記憶態參考（整間顯現，只用來看構圖本身，不是玩家實際畫面）。
                var inside = w.V2KitchenRoom; inside.Expand(1f);
                LanternReveal.AddRoom(inside, inside.center, true);
                Frames(qa, 4);
                foreach (var v in variants) { sw.SetLife(v.life); sw.SetB(v.b); Frames(qa, 4); Pass("memory_" + v.tag); }
                sw.SetLife(true);
                // B 的走動與淨寬；A 也走一次對照。
                foreach (var b in new[] { true, false })
                {
                    sw.SetB(b);
                    Frames(qa, 2);
                    qa.Player.Teleport(HP(1.635f, 10.558f), 0f, 0f);
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
                File.WriteAllText(Path.Combine(dir, "kitchen_layout_qa.md"), log.ToString(), new UTF8Encoding(false));
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

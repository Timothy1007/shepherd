using System.IO;
using System.Text;
using Shepherd.Exploration.Lantern;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shepherd.Exploration.Editor
{
    /// <summary>第一人稱移動手感 QA 的入口與自動量測（獨立場景，不影響正式場景的移動參數）。</summary>
    public static class LanternMovementQAEditor
    {
        public const string ScenePath = "Assets/Exploration/Scenes/LanternMovementQA.unity";
        private const float Dt = 1f / 60f;

        [MenuItem("Shepherd/Prototype/Case01 移動手感 QA（F6 切換 A／B／C）")]
        public static void Launch()
        {
            if (!File.Exists(ScenePath)) BuildScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Shepherd/Prototype/Case01 移動手感 QA 重建場景")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            new GameObject("LanternPrototypeQA").AddComponent<LanternPrototypeQA>();
            new GameObject("MovementFeelSwitch").AddComponent<LanternMovementFeelSwitch>();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 自動量測（60 fps 模擬）：每組預設的穩定步行／快走速度、起步到 95% 的時間、放開後滑行距離、
        /// 一邊走一邊轉 90° 時的側滑量，以及從走廊經餐廳門、廚房門再回來的固定路線是否走得通。
        /// </summary>
        public static void MeasureFromCommandLine()
        {
            string dir = null;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-captureDir") dir = args[i + 1];
            dir = dir ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/MovementQA"));
            Directory.CreateDirectory(dir);
            BuildScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var qa = Object.FindObjectOfType<LanternPrototypeQA>();
            var sw = Object.FindObjectOfType<LanternMovementFeelSwitch>();
            var log = new StringBuilder();
            try
            {
                qa.Setup(false);
                sw.Bind(qa);
                sw.EnterAtStart();
                log.AppendLine("| 預設 | 步行穩定 | 快走穩定 | 起步到 95% | 放開後滑行（步行） | 放開後滑行（快走） | 走路轉 90° 的側滑 | 固定路線 |");
                log.AppendLine("|---|---|---|---|---|---|---|---|");
                for (int i = 0; i < LanternMovementFeelSwitch.Presets.Length; i++)
                {
                    sw.Apply(i);
                    var p = sw.Current;
                    var r = MeasurePreset(qa, sw);
                    bool route = Route(qa, sw);
                    log.AppendLine("| " + p.Name + " | " + r.walk.ToString("0.00") + " m/s | " + r.brisk.ToString("0.00") + " m/s | " + r.start.ToString("0.00") + " 秒 | "
                        + r.stopWalk.ToString("0.00") + " m | " + r.stopBrisk.ToString("0.00") + " m | " + r.skid.ToString("0.00") + " m | " + (route ? "走得通" : "卡住") + " |");
                }
                log.AppendLine();
                log.AppendLine("ok");
            }
            catch (System.Exception e) { log.AppendLine("error: " + e); throw; }
            finally
            {
                if (qa != null) qa.Shutdown();
                File.WriteAllText(Path.Combine(dir, "movement_qa.md"), log.ToString(), new UTF8Encoding(false));
            }
        }

        private static void Step(LanternPrototypeQA qa, LanternMovementFeelSwitch sw, ExploreInput inp)
        {
            qa.Step(Dt, inp);
            sw.Apply(sw.Index);
        }

        /// <summary>在走廊（直線 3 m 以上）量。</summary>
        public static (float walk, float brisk, float start, float stopWalk, float stopBrisk, float skid) MeasurePreset(LanternPrototypeQA qa, LanternMovementFeelSwitch sw)
        {
            var o = LanternGraybox.V2Origin; float S = LanternGraybox.V2Scale;
            var start = o + new Vector3(5.07f * S, 0.05f, 5.4f * S);
            var p = qa.Player;
            // 步行：起步時間＋穩定速度。
            p.Teleport(start, 0f, 0f);
            for (int i = 0; i < 5; i++) Step(qa, sw, ExploreInput.None);
            float t = 0f, startTime = -1f;
            for (int i = 0; i < 60; i++)
            {
                Step(qa, sw, new ExploreInput { Move = new Vector2(0f, 1f) });
                t += Dt;
                if (startTime < 0f && p.LastHorizontalSpeed >= sw.Current.Walk * 0.95f) startTime = t;
            }
            float walk = p.LastHorizontalSpeed;
            float stopWalk = Coast(qa, sw);
            // 快走。
            p.Teleport(start, 0f, 0f);
            for (int i = 0; i < 5; i++) Step(qa, sw, ExploreInput.None);
            for (int i = 0; i < 60; i++) Step(qa, sw, new ExploreInput { Move = new Vector2(0f, 1f), Run = true });
            float brisk = p.LastHorizontalSpeed;
            float stopBrisk = Coast(qa, sw);
            // 走路時轉 90°（餐廳裡空間夠大）：量轉完之後實際行進方向和鏡頭方向的偏離（側滑）距離。
            var room = o + new Vector3((0.125f + 4.6f), 0.05f, 6.56f + 1.2f);
            p.Teleport(room, -90f, 0f);
            for (int i = 0; i < 5; i++) Step(qa, sw, ExploreInput.None);
            for (int i = 0; i < 40; i++) Step(qa, sw, new ExploreInput { Move = new Vector2(0f, 1f) });
            float lookPerFrame = 90f / 12f / (FirstPersonController.LookSensitivity * p.LookScale);
            for (int i = 0; i < 12; i++) Step(qa, sw, new ExploreInput { Move = new Vector2(0f, 1f), Look = new Vector2(lookPerFrame, 0f) });
            var before = p.transform.position;   // 轉完之後才開始量：照理應該完全沿著新的鏡頭方向走
            for (int i = 0; i < 20; i++) Step(qa, sw, new ExploreInput { Move = new Vector2(0f, 1f) });
            var moved = p.transform.position - before; moved.y = 0f;
            var forward = Quaternion.Euler(0f, p.Yaw, 0f) * Vector3.forward;
            float skid = Mathf.Abs(Vector3.Dot(moved, Quaternion.Euler(0f, 90f, 0f) * forward));
            return (walk, brisk, startTime, stopWalk, stopBrisk, skid);
        }

        private static float Coast(LanternPrototypeQA qa, LanternMovementFeelSwitch sw)
        {
            var p = qa.Player;
            var a = p.transform.position;
            for (int i = 0; i < 120 && (i == 0 || p.LastHorizontalSpeed > 0.02f); i++) Step(qa, sw, ExploreInput.None);
            var d = p.transform.position - a; d.y = 0f;
            return d.magnitude;
        }

        /// <summary>固定路線：走廊 → 餐廳門 → 餐桌東側 → 往廚房的門 → 廚房 → 回到走廊（穿過兩道門與轉角）。</summary>
        public static bool Route(LanternPrototypeQA qa, LanternMovementFeelSwitch sw)
        {
            var o = LanternGraybox.V2Origin; float S = LanternGraybox.V2Scale;
            Vector3 HP(float x, float z) => o + new Vector3(x * S, 0.05f, z * S);
            qa.Player.Teleport(HP(5.07f, 5.5f), 0f, 0f);
            for (int i = 0; i < 3; i++) Step(qa, sw, ExploreInput.None);
            var pts = new[] { (5.07f, 6.45f), (4.1f, 6.45f), (3.63f, 6.82f), (2.33f, 7.14f), (1.33f, 7.51f), (1.1f, 8.59f), (1.6f, 9.66f), (1.6f, 10.6f), (1.6f, 9.66f), (1.33f, 7.51f), (3.63f, 6.82f), (4.1f, 6.45f), (5.07f, 6.45f) };
            foreach (var (x, z) in pts)
                if (!WalkTo(qa, sw, HP(x, z))) return false;
            return true;
        }

        private static bool WalkTo(LanternPrototypeQA qa, LanternMovementFeelSwitch sw, Vector3 target)
        {
            var p = qa.Player;
            for (int i = 0; i < 900; i++)
            {
                var d = target - p.transform.position; d.y = 0f;
                if (d.magnitude < 0.3f) return true;
                float want = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                float look = Mathf.Clamp(Mathf.DeltaAngle(p.Yaw, want), -10f, 10f) / (FirstPersonController.LookSensitivity * p.LookScale);
                Step(qa, sw, new ExploreInput { Move = new Vector2(0f, 1f), Look = new Vector2(look, 0f) });
            }
            return false;
        }
    }
}

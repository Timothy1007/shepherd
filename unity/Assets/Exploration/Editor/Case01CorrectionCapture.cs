using System.IO;
using System.Text;
using Shepherd.Exploration.Case01;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// 現實校正 × 沉浸式操作 QA：選單入口（獨立場景 Assets/Exploration/Scenes/Case01RealityCorrectionQA.unity，沒有就建立，直接進 Play），
    /// 與同位置前後對照截圖（batchmode：-executeMethod Shepherd.Exploration.Editor.Case01CorrectionCapture.CaptureFromCommandLine -captureDir 路徑）。
    /// 截圖只是參考；臨場感要真人在 Play 裡玩過才算。
    /// </summary>
    public static class Case01CorrectionCapture
    {
        public const string ScenePath = "Assets/Exploration/Scenes/Case01RealityCorrectionQA.unity";

        [MenuItem("Shepherd/Case01/現實校正沉浸式玩法 QA")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) BuildScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Shepherd/Case01/現實校正 QA 重建場景")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Case01RealityCorrectionQA").AddComponent<Case01CorrectionQA>();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }

        public static void BuildSceneFromCommandLine()
        {
            BuildScene();
            Debug.Log("[Case01CorrectionCapture] 場景建立：" + ScenePath);
        }

        [MenuItem("Shepherd/Case01/現實校正 QA 前後對照截圖")]
        public static void CaptureFromMenu()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01CorrectionCapture"));
            Capture(dir);
            EditorUtility.RevealInFinder(dir);
        }

        public static void CaptureFromCommandLine()
        {
            Capture(Case01HouseCapture.Arg("-captureDir") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01CorrectionCapture")));
        }

        private static void Settle(Case01CorrectionQA s, float sec)
        {
            for (float t = 0f; t < sec; t += 1f / 30f)
            {
                s.House.Tick(1f / 30f, Case01Input.None);
                s.Host.Frame(1f / 30f);
                s.Frame(1f / 30f);
            }
            if (s.House.Echo != null) s.House.Echo.Stop();
            s.House.Hud.ClearMonologue();
            s.House.Hud.HideNote();
            s.House.Hud.Visible = false;
            s.House.ExploreCamera.fieldOfView = s.Host.FieldOfView;
        }

        private static void Look(Case01CorrectionQA s, Vector3 feet, Vector3 target)
        {
            var eye = feet + Vector3.up * s.Host.EyeHeight;
            var d = target - eye;
            s.House.Player.Teleport(feet, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
        }

        private static void Shot(Case01CorrectionQA s, RenderTexture rt, string path)
        {
            var cam = s.House.ExploreCamera;
            cam.transform.position = s.House.Player.EyePosition;
            cam.transform.rotation = s.House.Player.ViewRotation;
            Case01HouseCapture.Shot(s.House, rt, path);
        }

        /// <summary>真的走過去（第一人稱控制器＋碰撞），同時看著 look；牽引的椅子會跟著。</summary>
        private static void Walk(Case01CorrectionQA s, float x, float z, Vector3? look = null)
        {
            var p = s.House.Player;
            for (float t = 0f; t < 12f; t += 1f / 30f)
            {
                var pos = p.transform.position;
                var to = new Vector3(x - pos.x, 0f, z - pos.z);
                if (to.magnitude < 0.2f) break;
                var lookAt = look ?? new Vector3(x, p.EyePosition.y, z);
                var d = lookAt - p.EyePosition;
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                var local = Quaternion.Inverse(Quaternion.Euler(0f, p.Yaw, 0f)) * to.normalized;
                var lookIn = new Vector2(Mathf.DeltaAngle(p.Yaw, yaw) / (FirstPersonController.LookSensitivity * p.LookScale), p.Pitch / (FirstPersonController.LookSensitivity * p.LookScale));
                s.House.Tick(1f / 30f, Case01Input.From(new ExploreInput { Move = new Vector2(local.x, local.z) * Mathf.Clamp01(to.magnitude / 0.6f + 0.3f), Look = lookIn }));
                s.Host.Frame(1f / 30f);
                s.Frame(1f / 30f);
            }
        }

        public static void Capture(string dir)
        {
            Directory.CreateDirectory(dir);
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var log = new StringBuilder();
            var root = new GameObject("Case01CorrectionCaptureRoot");
            try
            {
                var s = root.AddComponent<Case01CorrectionQA>();
                s.Setup(false, 1);
                s.House.ApplyAtmosphere();
                var slice = s.Slice;
                var view = new Vector3(5.2f, 0.05f, 12.9f);
                var viewTarget = new Vector3(6.9f, 0.9f, 14.6f);
                // 1. 倒下：擋住拱口。
                Look(s, view, viewTarget);
                Settle(s, 0.3f);
                Shot(s, rt, Path.Combine(dir, "rc_01_fallen.png"));
                // 2. 校正：同位置，櫃子直立、拱口打開。
                Look(s, view, slice.CabinetCollider.bounds.center);
                Settle(s, 0.05f);
                log.AppendLine("correct: " + s.Correct());
                Look(s, view, viewTarget);
                Settle(s, 0.7f);
                Shot(s, rt, Path.Combine(dir, "rc_02_corrected.png"));
                // 3. 後廚的照片線索。
                Look(s, new Vector3(8.0f, 0.05f, 15.3f), Case01CorrectionSlice.PhotoPos);
                Settle(s, 0.1f);
                Shot(s, rt, Path.Combine(dir, "rc_03_photo_clue.png"));
                // 4. 超時倒回。
                Look(s, view, viewTarget);
                Settle(s, s.Duration + 1f);
                Shot(s, rt, Path.Combine(dir, "rc_04_fell_back.png"));
                // 5. 牽引椅子到凹痕（真的走：餐桌北側的通道）。
                Walk(s, 5.75f, 13.15f);
                Walk(s, 2.05f, 13.15f);
                Walk(s, 1.0f, 14.6f);
                Look(s, s.House.Player.transform.position, slice.ChairCollider.bounds.center);
                Settle(s, 0.05f);
                log.AppendLine("grab: " + s.Tether());
                Walk(s, 1.05f, 14.6f, new Vector3(9f, 1.2f, 14.6f));
                Settle(s, 1.2f);
                Walk(s, 1.4f, 16.3f, new Vector3(9f, 1.2f, 16.3f));
                Walk(s, 3.2f, 16.35f, new Vector3(9f, 0.9f, 16.35f));
                Shot(s, rt, Path.Combine(dir, "rc_05_tether.png"));
                Walk(s, 4.6f, 16.35f, new Vector3(9f, 1.2f, 16.35f));
                Walk(s, 6.6f - slice.HoldDistance, 16.85f, new Vector3(9f, 1.2f, 16.85f));
                Settle(s, 1.0f);
                Look(s, s.House.Player.transform.position, new Vector3(6.6f, 0.3f, 16.85f));
                Settle(s, 0.3f);
                Shot(s, rt, Path.Combine(dir, "rc_05b_ghost_on_mark.png"));
                log.AppendLine("place: " + s.Tether() + " onMark=" + slice.ChairOnMark);
                Walk(s, view.x, view.z);
                Look(s, view, slice.CabinetCollider.bounds.center);
                Settle(s, 0.05f);
                log.AppendLine("correct: " + s.Correct());
                Settle(s, 2.5f);
                Look(s, view, viewTarget);
                Settle(s, 0.1f);
                Shot(s, rt, Path.Combine(dir, "rc_06_stable.png"));
                log.AppendLine("state: " + slice.State);
                foreach (var l in slice.Log) log.AppendLine("- " + l);
                s.House.RestoreAtmosphere();
            }
            finally
            {
                Object.DestroyImmediate(root);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
            File.WriteAllText(Path.Combine(dir, "rc_capture.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log("[Case01CorrectionCapture] 輸出到 " + dir);
        }
    }
}

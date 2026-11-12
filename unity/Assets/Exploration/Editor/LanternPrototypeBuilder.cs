using System.IO;
using System.Text;
using Shepherd.Exploration.Lantern;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shepherd.Exploration.Editor
{
    /// <summary>提燈手感 QA：建立／開啟獨立場景，產生玩家眼睛高度的對照截圖。</summary>
    public static class LanternPrototypeBuilder
    {
        public const string ScenePath = "Assets/Exploration/Scenes/LanternPrototypeQA.unity";

        [MenuItem("Shepherd/Prototype/提燈手感 QA")]
        public static void Launch()
        {
            if (!File.Exists(ScenePath)) BuildScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Shepherd/Prototype/提燈手感 QA 重建場景")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            new GameObject("LanternPrototypeQA").AddComponent<LanternPrototypeQA>();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }

        public static void BuildFromCommandLine() => BuildScene();

        public static void CaptureFromCommandLine()
        {
            string dir = Arg("-captureDir") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/LanternPrototypeCapture"));
            Capture(dir);
        }

        [MenuItem("Shepherd/Prototype/提燈手感 QA 對照截圖")]
        public static void CaptureFromMenu()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/LanternPrototypeCapture"));
            Capture(dir);
            EditorUtility.RevealInFinder(dir);
        }

        public static void Capture(string dir)
        {
            Directory.CreateDirectory(dir);
            if (!File.Exists(ScenePath)) BuildScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var qa = Object.FindObjectOfType<LanternPrototypeQA>();
            var log = new StringBuilder();
            try
            {
                qa.Setup(false);
                Pair(qa, rt, dir, "Spawn", "01_hall", log);
                Pair(qa, rt, dir, "HallView", "02_hall_wall", log);
                Pair(qa, rt, dir, "LivingEntry", "03_living_entry", log);
                Pair(qa, rt, dir, "LivingTable", "04_living_table", log);
                Pair(qa, rt, dir, "LivingAlcove", "05_living_alcove", log);
                Pair(qa, rt, dir, "BrightEntry", "06_bright_entry", log);
                Pair(qa, rt, dir, "BrightCalendar", "07_bright_calendar", log);
                // 光圈大小（同機位）。
                qa.GoTo("LivingTable");
                qa.Rig.SetOn(true);
                for (int i = 0; i < 3; i++)
                {
                    qa.Rig.SetRadius(i);
                    Frames(qa, 4);
                    Shot(qa, rt, Path.Combine(dir, "08_radius_" + i + ".png"));
                }
                qa.Rig.SetRadius(1);
                // 低頭看身體與提燈。
                qa.GoTo("Spawn");
                qa.Player.Teleport(qa.Player.transform.position, qa.Player.Yaw, 62f);
                Frames(qa, 4);
                Shot(qa, rt, Path.Combine(dir, "09_look_down.png"));
                // 走路中（提燈擺動、鏡頭晃動）。
                qa.GoTo("Spawn");
                for (int i = 0; i < 24; i++) qa.Step(1f / 30f, new ExploreInput { Move = new Vector2(0f, 1f), Look = new Vector2(i < 12 ? 0.8f : -0.8f, 0f) });
                Shot(qa, rt, Path.Combine(dir, "10_walking_swing.png"));
                log.AppendLine("ok");
            }
            finally
            {
                if (rt != null) Object.DestroyImmediate(rt);
                File.WriteAllText(Path.Combine(dir, "capture.txt"), log.ToString());
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        private static void Pair(LanternPrototypeQA qa, RenderTexture rt, string dir, string mark, string tag, StringBuilder log)
        {
            qa.GoTo(mark);
            qa.Rig.SetOn(false);
            Frames(qa, 4);
            Shot(qa, rt, Path.Combine(dir, tag + "_off.png"));
            qa.Rig.SetOn(true);
            Frames(qa, 4);
            Shot(qa, rt, Path.Combine(dir, tag + "_on.png"));
            log.AppendLine(tag + " " + qa.Player.transform.position.ToString("F2"));
        }

        private static void Frames(LanternPrototypeQA qa, int n)
        {
            for (int i = 0; i < n; i++) qa.Step(1f / 30f, ExploreInput.None);
            qa.Rig.Snap();
            qa.Post.Apply();
        }

        private static void Shot(LanternPrototypeQA qa, RenderTexture rt, string path)
        {
            var cam = qa.Cam;
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }
    }
}

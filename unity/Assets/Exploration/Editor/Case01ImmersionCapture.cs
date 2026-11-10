using System.IO;
using System.Text;
using Shepherd.Exploration.Case01;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// Phase 1.8 沉浸式住宅與失序空間 QA：選單入口（暫時的空場景，只放 Case01ImmersionQA，直接進 Play），
    /// 以及「原 R2 ↔ 沉浸版」同位置、同 FOV 55°、同眼高 1.58 m 的截圖與 H0／H2 空間異變對照
    /// （batchmode：-executeMethod Shepherd.Exploration.Editor.Case01ImmersionCapture.CaptureFromCommandLine -captureDir 路徑）。
    /// 截圖只是參考；臨場感要真人在 Play 裡走過才算。
    /// </summary>
    public static class Case01ImmersionCapture
    {
        [MenuItem("Shepherd/Case01/Phase 1.8 沉浸式住宅與失序空間 QA")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Case01ImmersionQA").AddComponent<Case01ImmersionQA>();
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Shepherd/Case01/Phase 1.8 新舊同角度截圖")]
        public static void CaptureFromMenu()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01ImmersionCapture"));
            Capture(dir);
            EditorUtility.RevealInFinder(dir);
        }

        public static void CaptureFromCommandLine()
        {
            Capture(Case01HouseCapture.Arg("-captureDir") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01ImmersionCapture")));
        }

        private static void Shot(Case01ImmersionQA s, RenderTexture rt, Case01ScaleQA.View v, string path, int stage)
        {
            s.House.Player.Teleport(v.Position, v.Yaw, v.Pitch);
            Case01HouseCapture.Advance(s.House, 0.1f);
            for (int i = 0; i < 6; i++) s.Frame(1f / 30f);
            if (s.House.Echo != null) s.House.Echo.Stop();
            s.House.Hud.ClearMonologue();
            s.House.Hud.Visible = false;
            s.House.ExploreCamera.fieldOfView = s.FieldOfView;
            Case01HouseCapture.Shot(s.House, rt, path);
        }

        public static void Capture(string dir)
        {
            Directory.CreateDirectory(dir);
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var small = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var log = new StringBuilder();
            var views = Case01ScaleQA.ViewsFor(Case01Floorplan.R2);
            try
            {
                foreach (var immersive in new[] { false, true })
                {
                    var root = new GameObject("Case01ImmersionCaptureRoot");
                    Case01ImmersionQA s = null;
                    try
                    {
                        s = root.AddComponent<Case01ImmersionQA>();
                        s.Setup(false, null, 1, immersive);
                        s.House.ApplyAtmosphere();
                        s.SetPanelVisible(false);
                        string tag = immersive ? "new" : "old";
                        for (int i = 0; i < views.Length; i++)
                        {
                            string name = "immersion_v" + (i + 1).ToString("00") + "_" + tag;
                            Shot(s, rt, views[i], Path.Combine(dir, name + "_1920.png"), 0);
                            Shot(s, small, views[i], Path.Combine(dir, name + "_1280.png"), 0);
                        }
                        if (immersive)
                        {
                            // 空間異變：同一扇門（走廊 → 餐廳），H0 與 H2 從走廊看進去；H3 不回暖。
                            var door = s.World.Door("door_hall_dining");
                            var doorView = new Case01ScaleQA.View("走廊那扇門", 7.6f, 0.05f, Case01HouseBuilder.R17.DiningHallDoorZ, -90f, 0f);
                            door.SetAngle(-door.OpenAngle, null);
                            Shot(s, rt, doorView, Path.Combine(dir, "anomaly_door_H0.png"), 0);
                            s.Press(3);
                            s.Press(3);
                            s.World.SetTopology(true, false);
                            s.World.Door("door_hall_dining").SetAngle(-92f, null);
                            Shot(s, rt, doorView, Path.Combine(dir, "anomaly_door_H2.png"), 2);
                            Shot(s, rt, views[1], Path.Combine(dir, "immersion_v02_new_H2.png"), 2);
                            s.Press(3);
                            Shot(s, rt, views[1], Path.Combine(dir, "immersion_v02_new_H3.png"), 3);
                            log.AppendLine("doors=" + s.World.Doors.Count + " lights=" + s.World.AddedLights.Count + " sources=" + s.World.Sources.Count);
                        }
                    }
                    finally
                    {
                        if (s != null && s.House != null) s.House.RestoreAtmosphere();
                        Object.DestroyImmediate(root);
                    }
                }
            }
            finally
            {
                rt.Release();
                small.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(small);
            }
            File.WriteAllText(Path.Combine(dir, "immersion_capture.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log("[Case01ImmersionCapture] 輸出到 " + dir);
        }
    }
}

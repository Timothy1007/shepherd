using System.Collections.Generic;
using System.IO;
using System.Text;
using Shepherd.Exploration.Case01;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// Phase 1.6 空間結構灰盒 QA：選單入口（開一個暫時的空場景，只放 Case01SpatialQA，直接進 Play；不存場景、不改 Build Settings），
    /// 以及截圖／平面圖／視線量測報表（batchmode：-executeMethod Shepherd.Exploration.Editor.Case01SpatialCapture.CaptureFromCommandLine -captureDir 路徑）。
    /// </summary>
    public static class Case01SpatialCapture
    {
        [MenuItem("Shepherd/Case01/Phase 1.6 空間結構灰盒 QA")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Case01SpatialQA").AddComponent<Case01SpatialQA>();
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Shepherd/Case01/Phase 1.6 灰盒截圖與視線量測")]
        public static void CaptureFromMenu()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01SpatialCapture"));
            Capture(dir);
            EditorUtility.RevealInFinder(dir);
        }

        public static void CaptureFromCommandLine()
        {
            Capture(Case01HouseCapture.Arg("-captureDir") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01SpatialCapture")));
        }

        private struct Row
        {
            public string Rooms, Foci;
            public bool Substituted, Remapped;
        }

        public static void Capture(string dir)
        {
            Directory.CreateDirectory(dir);
            var big = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var small = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var rows = new Dictionary<string, Row>();
            try
            {
                foreach (Case01SpatialVariant v in new[] { Case01SpatialVariant.A, Case01SpatialVariant.B, Case01SpatialVariant.C })
                {
                    var root = new GameObject("Case01SpatialCaptureRoot");
                    Case01SpatialQA s = null;
                    try
                    {
                        s = root.AddComponent<Case01SpatialQA>();
                        s.Setup(false);
                        s.House.ApplyAtmosphere();
                        s.QaCanvas.enabled = false;
                        if (v != Case01SpatialVariant.A) s.SetVariant(v);
                        s.House.Hud.Visible = false;
                        File.WriteAllBytes(Path.Combine(dir, "plan_" + v + "_1F.png"), s.PlanTexture(0).EncodeToPNG());
                        File.WriteAllBytes(Path.Combine(dir, "plan_" + v + "_2F.png"), s.PlanTexture(1).EncodeToPNG());
                        for (int i = 0; i < Case01SpatialQA.Views.Length; i++)
                        {
                            string name = "v" + (i + 1).ToString("00");
                            foreach (var fov in new[] { 70f, 60f })
                            {
                                if (s.FieldOfView != fov) s.Press(5);
                                s.GoView(i);
                                Case01HouseCapture.Advance(s.House, 0.1f);
                                if (s.House.Echo != null) s.House.Echo.Stop();
                                s.House.Hud.ClearMonologue();
                                s.House.Hud.Visible = false;
                                s.House.ExploreCamera.fieldOfView = fov;
                                s.UpdateMetrics(16f / 9f);
                                rows[v + "|" + i + "|" + fov] = new Row { Rooms = s.Metrics, Foci = s.FocusText, Substituted = s.ViewSubstituted, Remapped = s.PositionRemapped };
                                Case01HouseCapture.Shot(s.House, big, Path.Combine(dir, "spatial_" + v + "_" + name + "_fov" + fov.ToString("0") + "_1920.png"));
                                if (fov > 65f)
                                {
                                    s.House.ExploreCamera.fieldOfView = fov;
                                    Case01HouseCapture.Shot(s.House, small, Path.Combine(dir, "spatial_" + v + "_" + name + "_fov70_1280.png"));
                                }
                            }
                        }
                        if (s.FieldOfView < 65f) s.Press(5);
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
                big.Release();
                small.Release();
                Object.DestroyImmediate(big);
                Object.DestroyImmediate(small);
            }

            var sb = new StringBuilder();
            sb.AppendLine("# Phase 1.6 視線量測（自動產生）");
            sb.AppendLine();
            sb.AppendLine("同眼高 1.65 m、同座標與朝向、16:9；「地面」＝該房間地板上方 1.0 m 的 0.25 m 格點中，落在畫面內且視線沒有被碰撞體擋住的比例（沒有碰撞體的小裝飾不算遮擋）。焦點＝調查熱區中心在畫面內且沒有被擋住。");
            sb.AppendLine();
            for (int i = 0; i < Case01SpatialQA.Views.Length; i++)
            {
                var view = Case01SpatialQA.Views[i];
                sb.AppendLine("## " + (i + 1) + ". " + view.Name + "（" + view.Position.x + ", " + view.Position.z + "｜朝向 " + view.Yaw + "°）");
                sb.AppendLine();
                sb.AppendLine("| 方案 | FOV | 畫面中看得到的地面 | 看得到的焦點 | 備註 |");
                sb.AppendLine("|---|---|---|---|---|");
                foreach (var v in new[] { "A", "B", "C" })
                foreach (var fov in new[] { 70f, 60f })
                {
                    Row r;
                    if (!rows.TryGetValue(v + "|" + i + "|" + fov, out r)) continue;
                    string note = r.Substituted ? "替代視點（此方案沒有這條走廊）" : r.Remapped ? "原位置不合法，已移到安全點" : "";
                    sb.AppendLine("| " + v + " | " + fov.ToString("0") + "° | " + r.Rooms + " | " + r.Foci + " | " + note + " |");
                }
                sb.AppendLine();
            }
            File.WriteAllText(Path.Combine(dir, "spatial_metrics.md"), sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[Case01SpatialCapture] 輸出到 " + dir);
        }
    }
}

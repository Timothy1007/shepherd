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
    /// Phase 1.7 住宅尺度與探索重構 QA：選單入口（暫時的空場景，只放 Case01ScaleQA，直接進 Play；不存場景、不改 Build Settings），
    /// 以及 O／R1／R2 的平面圖、固定視點截圖與尺度量測表
    /// （batchmode：-executeMethod Shepherd.Exploration.Editor.Case01ScaleCapture.CaptureFromCommandLine -captureDir 路徑）。
    /// </summary>
    public static class Case01ScaleCapture
    {
        [MenuItem("Shepherd/Case01/Phase 1.7 住宅尺度與探索重構 QA")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Case01ScaleQA").AddComponent<Case01ScaleQA>();
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Shepherd/Case01/Phase 1.7 截圖與尺度量測")]
        public static void CaptureFromMenu()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01ScaleCapture"));
            Capture(dir);
            EditorUtility.RevealInFinder(dir);
        }

        public static void CaptureFromCommandLine()
        {
            Capture(Case01HouseCapture.Arg("-captureDir") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01ScaleCapture")));
        }

        private static string Tag(Case01Floorplan p) { return p == Case01Floorplan.V05A ? "O" : p.ToString(); }

        public static void Capture(string dir)
        {
            Directory.CreateDirectory(dir);
            var big = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var small = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var sb = new StringBuilder();
            sb.AppendLine("# Phase 1.7 尺度量測（自動產生；batchmode 腳本，不是真人試玩）");
            sb.AppendLine();
            try
            {
                foreach (var plan in Case01ScaleQA.Plans)
                {
                    var root = new GameObject("Case01ScaleCaptureRoot");
                    Case01ScaleQA s = null;
                    try
                    {
                        s = root.AddComponent<Case01ScaleQA>();
                        s.Setup(false, null, 1, plan);
                        s.House.ApplyAtmosphere();
                        s.QaCanvas.enabled = false;
                        s.House.Hud.Visible = false;
                        string tag = Tag(plan);
                        File.WriteAllBytes(Path.Combine(dir, "plan_" + tag + "_1F.png"), s.PlanTexture(0).EncodeToPNG());
                        File.WriteAllBytes(Path.Combine(dir, "plan_" + tag + "_2F.png"), s.PlanTexture(1).EncodeToPNG());
                        Measure(s, plan, sb);
                        var views = Case01ScaleQA.ViewsFor(plan);
                        sb.AppendLine("**固定視點（FOV 70°、眼高 1.65 m、16:9；地面＝畫面中看得到的房間地面比例，只算碰撞體遮擋）**");
                        sb.AppendLine();
                        sb.AppendLine("| # | 視點 | 看得到的地面 | 看得到的焦點 |");
                        sb.AppendLine("|---|---|---|---|");
                        for (int i = 0; i < views.Length; i++)
                        {
                            s.GoView(i);
                            Case01HouseCapture.Advance(s.House, 0.1f);
                            if (s.House.Echo != null) s.House.Echo.Stop();
                            s.House.Hud.ClearMonologue();
                            s.House.Hud.Visible = false;
                            s.House.ExploreCamera.fieldOfView = 70f;
                            var eye = s.House.Player.EyePosition;
                            var rot = s.House.Player.ViewRotation;
                            var stats = Case01SpatialMap.Visible(s.House.Layout, eye, rot, 70f, 16f / 9f, null);
                            var foci = Case01SpatialMap.VisibleFoci(s.House.Layout, eye, rot, 70f, 16f / 9f);
                            sb.AppendLine("| " + (i + 1) + " | " + views[i].Name + " | " + Case01SpatialMap.Summary(stats, s.House.Layout.RoomAt(s.House.Player.transform.position)) + " | " +
                                          (foci.Count == 0 ? "（無）" : string.Join("、", foci.ToArray())) + " |");
                            string name = "scale_" + tag + "_v" + (i + 1).ToString("00");
                            Case01HouseCapture.Shot(s.House, big, Path.Combine(dir, name + "_1920.png"));
                            s.House.ExploreCamera.fieldOfView = 70f;
                            Case01HouseCapture.Shot(s.House, small, Path.Combine(dir, name + "_1280.png"));
                        }
                        sb.AppendLine();
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
            File.WriteAllText(Path.Combine(dir, "scale_metrics.md"), sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[Case01ScaleCapture] 輸出到 " + dir);
        }

        /// <summary>每個房間「玩家中心可以站」的淨面積（0.2 m 格點、膠囊檢查）、主路線各段路長與各步速秒數、樓梯。</summary>
        private static void Measure(Case01ScaleQA s, Case01Floorplan plan, StringBuilder sb)
        {
            var L = s.House.Layout;
            sb.AppendLine("## " + Case01ScaleQA.PlanName(plan));
            sb.AppendLine();
            var fp = L.Footprint;
            sb.AppendLine("外框 " + fp.width.ToString("0.0") + " × " + fp.height.ToString("0.0") + " m；樓梯坡度 " + L.StairAngle.ToString("0.0") + "°。");
            sb.AppendLine();
            var area = new Dictionary<Case01Room, int>();
            s.House.Player.Body.enabled = false;
            foreach (var y in new[] { 0.05f, Case01HouseBuilder.FloorHeight + 0.05f })
            {
                for (float x = fp.xMin + 0.1f; x < fp.xMax; x += 0.2f)
                for (float z = fp.yMin + 0.1f; z < fp.yMax; z += 0.2f)
                {
                    var p = new Vector3(x, y, z);
                    var room = L.RoomAt(p);
                    if (!Case01SpatialMap.HouseRoom(room)) continue;
                    if (!Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, 0.6f, Case01SpatialMap.SightMask, QueryTriggerInteraction.Ignore)) continue;
                    if (!s.Free(p)) continue;
                    int n;
                    area.TryGetValue(room, out n);
                    area[room] = n + 1;
                }
            }
            s.House.Player.Body.enabled = true;
            sb.AppendLine("| 房間 | 玩家可站的淨面積（m²，0.2 m 格點） |");
            sb.AppendLine("|---|---|");
            foreach (var kv in area) sb.AppendLine("| " + Case01Texts.RoomName(kv.Key) + " | " + (kv.Value * 0.04f).ToString("0.0") + " |");
            sb.AppendLine();
            sb.AppendLine("| 主路線分段 | 水平路長 | 1.8 m/s | 2.2 m/s | 2.6 m/s | 3.5 m/s（正式） |");
            sb.AppendLine("|---|---|---|---|---|---|");
            var from = Case01ScaleRoutes.Start(plan);
            float total = 0f;
            foreach (var leg in Case01ScaleRoutes.For(plan))
            {
                Vector3 end;
                float d = Case01ScaleRoutes.Length(from, leg, out end);
                total += d;
                from = end;
                sb.AppendLine("| " + leg.Name + " | " + d.ToString("0.0") + " m | " + (d / 1.8f).ToString("0.0") + " s | " + (d / 2.2f).ToString("0.0") + " s | " + (d / 2.6f).ToString("0.0") + " s | " + (d / 3.5f).ToString("0.0") + " s |");
            }
            sb.AppendLine("| **合計** | **" + total.ToString("0.0") + " m** | " + (total / 1.8f).ToString("0.0") + " s | " + (total / 2.2f).ToString("0.0") + " s | " + (total / 2.6f).ToString("0.0") + " s | " + (total / 3.5f).ToString("0.0") + " s |");
            sb.AppendLine();
        }
    }
}

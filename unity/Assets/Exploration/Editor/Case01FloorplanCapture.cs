using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Shepherd.Exploration.Case01;
using UnityEditor;
using UnityEngine;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// v0.4 一樓格局 A/B：俯視圖（一樓／二樓）、第一人稱視角，以及**實測**路線長度
    /// （用同一個第一人稱控制器、真正的碰撞，沿著路點走完後累計移動距離與時間；不是估算）。報告寫入 floorplan_routes.md。
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod Shepherd.Exploration.Editor.Case01FloorplanCapture.CaptureFromCommandLine
    ///   -captureDir &lt;資料夾&gt; -captureSizes 1920x1080,1280x720
    /// </summary>
    public static class Case01FloorplanCapture
    {
        private const float Step = 1f / 30f;

        [MenuItem("Shepherd/Case01/一樓格局 A／B 截圖與路線實測")]
        public static void CaptureFromMenu()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01FloorplanCapture"));
            CaptureAll(dir, new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) });
            EditorUtility.RevealInFinder(dir);
        }

        public static void CaptureFromCommandLine()
        {
            string dir = Case01HouseCapture.Arg("-captureDir") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01FloorplanCapture"));
            string sizes = Case01HouseCapture.Arg("-captureSizes") ?? "1920x1080,1280x720";
            var list = new List<Vector2Int>();
            foreach (var s in sizes.Split(','))
            {
                var p = s.Trim().Split('x');
                list.Add(new Vector2Int(int.Parse(p[0]), int.Parse(p[1])));
            }
            CaptureAll(dir, list.ToArray());
        }

        public static void CaptureAll(string dir, Vector2Int[] sizes)
        {
            Directory.CreateDirectory(dir);
            foreach (var size in sizes) Shots(dir, size.x, size.y);
            File.WriteAllText(Path.Combine(dir, "floorplan_routes.md"), RouteReport(), new UTF8Encoding(false));
            Debug.Log("[Case01FloorplanCapture] 完成：" + dir);
        }

        // ---------------- 路線 ----------------

        public sealed class Route
        {
            public string Name;
            public Vector3? Start;
            public float StartYaw;
            public Vector2[] A;
            public Vector2[] B;
        }

        public static readonly Route[] Routes =
        {
            new Route
            {
                Name = "玄關起點 → 餐桌旁",
                A = new[] { new Vector2(6.5f, 4.4f), new Vector2(6.5f, 5.8f), new Vector2(6.6f, 6.2f) },
                B = new[] { new Vector2(5.8f, 2.5f), new Vector2(3.5f, 2.5f), new Vector2(2.5f, 4.2f), new Vector2(2.5f, 6.4f), new Vector2(4.2f, 7.0f), new Vector2(4.2f, 7.5f), new Vector2(6.0f, 7.5f), new Vector2(6.4f, 6.6f) },
            },
            new Route
            {
                Name = "玄關起點 → 二樓平台（書房門前）",
                A = new[] { new Vector2(8.2f, 1.35f), new Vector2(9.4f, 1.35f), new Vector2(9.4f, 4.3f), new Vector2(8.3f, 4.5f), new Vector2(6.95f, 3.6f), new Vector2(6.95f, 4.3f) },
                B = new[] { new Vector2(8.2f, 3.4f), new Vector2(8.2f, 4.5f), new Vector2(9.4f, 4.55f), new Vector2(9.4f, 1.3f), new Vector2(9.4f, 0.5f), new Vector2(8.2f, 0.5f), new Vector2(6.95f, 1.6f), new Vector2(6.95f, 4.3f) },
            },
            new Route
            {
                Name = "錯接到餐廳後 → 回到二樓平台（書房門前）",
                Start = new Vector3(6.5f, 0.05f, 5.8f),
                A = new[] { new Vector2(6.5f, 5.6f), new Vector2(6.5f, 4.2f), new Vector2(8.2f, 1.35f), new Vector2(9.4f, 1.35f), new Vector2(9.4f, 4.3f), new Vector2(8.3f, 4.5f), new Vector2(6.95f, 3.6f), new Vector2(6.95f, 4.3f) },
                B = new[] { new Vector2(6.0f, 7.5f), new Vector2(4.2f, 7.5f), new Vector2(4.2f, 7.0f), new Vector2(2.5f, 6.4f), new Vector2(2.5f, 4.2f), new Vector2(3.5f, 2.5f), new Vector2(5.8f, 2.5f), new Vector2(8.2f, 3.4f), new Vector2(8.2f, 4.5f), new Vector2(9.4f, 4.55f), new Vector2(9.4f, 1.3f), new Vector2(9.4f, 0.5f), new Vector2(8.2f, 0.5f), new Vector2(6.95f, 1.6f), new Vector2(6.95f, 4.3f) },
            },
            new Route
            {
                Name = "二樓平台 → 第三扇門前",
                Start = new Vector3(6.95f, 3.05f, 4.3f), StartYaw = 180f,
                A = new[] { new Vector2(6.95f, 1.0f) },
                B = new[] { new Vector2(6.95f, 1.0f) },
            },
        };

        public struct Measure
        {
            public bool Reached;
            public float Distance;
            public float Seconds;
        }

        /// <summary>用控制器實際走一條路線（不跑步），回傳移動距離與時間；到不了則 Reached＝false。</summary>
        public static Measure Walk(Case01Floorplan plan, Route r)
        {
            var go = new GameObject("FloorplanRouteProbe");
            try
            {
                var qa = go.AddComponent<Case01HouseQA>();
                qa.Build(false, null, 1, null, null, plan);
                qa.Hud.ClearMonologue();
                if (r.Start.HasValue) qa.Player.Teleport(r.Start.Value, r.StartYaw);
                for (int i = 0; i < 3; i++) qa.Tick(Step, Case01Input.None);
                var m = new Measure { Reached = true };
                var last = qa.Player.transform.position;
                foreach (var w in plan == Case01Floorplan.V04 ? r.B : r.A)
                {
                    float t = 0f;
                    while (true)
                    {
                        var p = qa.Player.transform.position;
                        if (new Vector2(w.x - p.x, w.y - p.z).magnitude < 0.25f) break;
                        if (t > 15f || qa.Mode != Case01Mode.Exploring)
                        {
                            m.Reached = false;
                            return m;
                        }
                        var aim = new Vector3(w.x, qa.Player.EyePosition.y, w.y);
                        var d = aim - qa.Player.EyePosition;
                        float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                        var look = new Vector2(Mathf.DeltaAngle(qa.Player.Yaw, yaw) / FirstPersonController.LookSensitivity, 0f);
                        qa.Tick(Step, Case01Input.From(new ExploreInput { Move = new Vector2(0f, 1f), Look = look }));
                        var now = qa.Player.transform.position;
                        m.Distance += (now - last).magnitude;
                        last = now;
                        m.Seconds += Step;
                        t += Step;
                    }
                }
                return m;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        public static string RouteReport()
        {
            var sb = new StringBuilder();
            sb.Append("# Case01 一樓格局 A／B 路線實測\n\n");
            sb.Append("以 `FirstPersonController`（步行、不跑步）沿固定路點實際移動（含碰撞與樓梯斜坡），累計位移與時間。路點見 `Case01FloorplanCapture.Routes`。\n");
            sb.Append("A＝原版格局；B＝玄關↔餐廳封住、樓梯反向上行。數字只代表這組路點下的步行量，不代表玩家實際會走的最短路徑。\n\n");
            sb.Append("| 路線 | A 距離 (m) | A 時間 (s) | B 距離 (m) | B 時間 (s) |\n|---|---|---|---|---|\n");
            foreach (var r in Routes)
            {
                var a = Walk(Case01Floorplan.Original, r);
                var b = Walk(Case01Floorplan.V04, r);
                sb.Append("| ").Append(r.Name).Append(" | ")
                  .Append(a.Reached ? a.Distance.ToString("0.0") : "到不了").Append(" | ").Append(a.Reached ? a.Seconds.ToString("0.0") : "-").Append(" | ")
                  .Append(b.Reached ? b.Distance.ToString("0.0") : "到不了").Append(" | ").Append(b.Reached ? b.Seconds.ToString("0.0") : "-").Append(" |\n");
            }
            return sb.ToString();
        }

        // ---------------- 截圖 ----------------

        private static void Shots(string dir, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            try
            {
                foreach (var plan in new[] { Case01Floorplan.Original, Case01Floorplan.V04 })
                {
                    string prefix = Path.Combine(dir, "floor_" + (plan == Case01Floorplan.V04 ? "B" : "A") + "_" + width + "x" + height + "_");
                    var go = new GameObject("FloorplanCaptureRoot");
                    try
                    {
                        var qa = go.AddComponent<Case01HouseQA>();
                        qa.Build(false, null, 1, null, null, plan);
                        qa.ApplyAtmosphere();
                        qa.Hud.ClearMonologue();
                        if (qa.Echo != null) qa.Echo.Stop();
                        Case01HouseCapture.Advance(qa, 0.3f);
                        Case01HouseCapture.Pose(qa, new Vector3(6.2f, 0.05f, 1.4f), 40f, 6f);
                        Case01HouseCapture.Shot(qa, rt, prefix + "fp1_entrance_to_stair.png");
                        Case01HouseCapture.Pose(qa, new Vector3(6.5f, 0.05f, 3.4f), 0f, 4f);
                        Case01HouseCapture.Shot(qa, rt, prefix + "fp2_entrance_north_wall.png");
                        if (plan == Case01Floorplan.V04)
                        {
                            Case01HouseCapture.Pose(qa, new Vector3(9.4f, 0.05f, 4.6f), 180f, 18f);
                            Case01HouseCapture.Shot(qa, rt, prefix + "fp3_stair_bottom_facing_south.png");
                            Case01HouseCapture.Pose(qa, new Vector3(9.4f, 3.05f, 0.5f), -90f, 6f);
                            Case01HouseCapture.Shot(qa, rt, prefix + "fp4_stair_top_toward_third_door.png");
                        }
                        else
                        {
                            Case01HouseCapture.Pose(qa, new Vector3(9.4f, 0.05f, 0.6f), 0f, 18f);
                            Case01HouseCapture.Shot(qa, rt, prefix + "fp3_stair_bottom_facing_north.png");
                            Case01HouseCapture.Pose(qa, new Vector3(9.4f, 3.05f, 4.75f), -90f, 6f);
                            Case01HouseCapture.Shot(qa, rt, prefix + "fp4_stair_top_toward_study.png");
                        }
                        // 俯視圖最後拍（不影響上面的 HUD 截圖）。
                        TopDown(qa, rt, prefix + "map_ground.png", 2.55f);
                        TopDown(qa, rt, prefix + "map_upper.png", 5.75f);
                        qa.RestoreAtmosphere();
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(go);
                    }
                }
            }
            finally
            {
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        /// <summary>正交俯視（相機放在樓板／屋頂下方往下看；不顯示 HUD）。</summary>
        internal static void TopDown(Case01HouseQA qa, RenderTexture rt, string path, float camY)
        {
            var go = new GameObject("TopDownCamera");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5.6f;
                cam.aspect = rt.width / (float)rt.height;
                cam.nearClipPlane = 0.02f;
                cam.farClipPlane = 3.2f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.05f, 0.05f, 0.06f);
                go.transform.position = new Vector3(5f, camY, 5f);
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                cam.targetTexture = rt;
                bool hud = qa.Hud.Visible;
                qa.Hud.Visible = false;
                cam.Render();
                qa.Hud.Visible = hud;
                cam.targetTexture = null;
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}

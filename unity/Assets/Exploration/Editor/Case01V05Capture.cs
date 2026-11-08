using System;
using System.Collections.Generic;
using System.IO;
using Shepherd.Battle.UI;
using Shepherd.Exploration.Case01;
using UnityEditor;
using UnityEngine;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// v0.5 截圖（1920×1080／1280×720）。-captureSet 選擇內容（逗號分隔，預設全部）：
    /// enemy＝失序體形成的連續影格＋進戰鬥後的同一份外殼。
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod Shepherd.Exploration.Editor.Case01V05Capture.CaptureFromCommandLine
    ///   -captureDir &lt;資料夾&gt; -captureSizes 1920x1080,1280x720 -captureSet enemy
    /// </summary>
    public static class Case01V05Capture
    {
        public const float Step = 1f / 30f;

        [MenuItem("Shepherd/Case01/v0.5 截圖（失序體／Portal／住宅）")]
        public static void CaptureFromMenu()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01V05Capture"));
            CaptureAll(dir, new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) }, null);
            EditorUtility.RevealInFinder(dir);
        }

        public static void CaptureFromCommandLine()
        {
            string dir = Case01HouseCapture.Arg("-captureDir") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01V05Capture"));
            string sizes = Case01HouseCapture.Arg("-captureSizes") ?? "1920x1080,1280x720";
            string set = Case01HouseCapture.Arg("-captureSet");
            var list = new List<Vector2Int>();
            foreach (var s in sizes.Split(','))
            {
                var p = s.Trim().Split('x');
                list.Add(new Vector2Int(int.Parse(p[0]), int.Parse(p[1])));
            }
            CaptureAll(dir, list.ToArray(), set);
        }

        public static void CaptureAll(string dir, Vector2Int[] sizes, string set)
        {
            Directory.CreateDirectory(dir);
            var sets = new HashSet<string>(string.IsNullOrEmpty(set) ? new[] { "enemy", "portal", "rooms" } : set.Split(','));
            foreach (var size in sizes)
            {
                var rt = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                try
                {
                    string prefix = Path.Combine(dir, "v05_" + size.x + "x" + size.y + "_");
                    if (sets.Contains("enemy")) Enemy(rt, prefix);
                    if (sets.Contains("portal")) Portal(rt, prefix);
                    if (sets.Contains("rooms")) Rooms(rt, prefix);
                }
                finally
                {
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                }
            }
        }

        private static void WithQa(Action<Case01HouseQA> body, Case01Checkpoint? cp = null, Case01QaMode? mode = null, Case01Floorplan? plan = null)
        {
            var root = new GameObject("Case01V05CaptureRoot");
            Case01HouseQA qa = null;
            try
            {
                qa = root.AddComponent<Case01HouseQA>();
                qa.Build(false, null, 1, cp, mode, plan);
                qa.ApplyAtmosphere();
                qa.Hud.ClearMonologue();
                body(qa);
            }
            finally
            {
                if (qa != null) qa.RestoreAtmosphere();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Portal：同一扇錯接的書房門，近距離連續 12 格（每格 0.1 秒）拼成一張接觸印樣，
        /// 依序為 v0.5 彩色、v0.5 降低閃爍、v0.4 舊版、夾層 △ 門校正後；另存單張大圖。
        /// </summary>
        private static void Portal(RenderTexture rt, string prefix)
        {
            WithQa(qa =>
            {
                qa.Hud.Visible = false;
                var cases = new[] { "v05_wrong", "v05_reduced", "v04_wrong", "v05_corrected" };
                foreach (var c in cases)
                {
                    qa.ReduceFlicker = c == "v05_reduced";
                    qa.PortalStyle = c.StartsWith("v04") ? Case01PortalStyle.V04 : Case01PortalStyle.V05;
                    qa.PreviewGlitch(c == "v05_corrected" ? 4 : 0);
                    qa.Hud.Visible = false;
                    if (c == "v05_corrected")
                        qa.Player.Teleport(Case01HouseBuilder.MezzCenter + new Vector3(-4.2f, 0.05f, 0f), -90f, 4f);
                    else
                        qa.Player.Teleport(new Vector3(Case01HouseBuilder.StudyDoorX, Case01HouseBuilder.FloorHeight + 0.05f, 3.4f), 0f, 4f);
                    Case01HouseCapture.Advance(qa, 0.6f);
                    Sheet(qa, rt, prefix + "portal_" + c + "_sheet.png", 12, 0.1f);
                    Case01HouseCapture.Shot(qa, rt, prefix + "portal_" + c + ".png");
                }
            }, null, Case01QaMode.PortalV05);
        }

        /// <summary>住宅 v0.5：方案 A／B 的俯視圖與各房間第一人稱視角、書房書櫃、艾琳臥室、窗外（H0／H3）。</summary>
        private static void Rooms(RenderTexture rt, string prefix)
        {
            var shotsA = new[]
            {
                Shot("a01_entrance_front_wall", 7.1f, 0f, 0.9f, 0f, 4f),
                Shot("a02_entrance_to_living", 7.0f, 0f, 1.2f, -75f, 6f),
                Shot("a03_living_fireplace_photo", 4.75f, 0f, 0.95f, -62f, 10f),
                Shot("a04_dining_table", 3.7f, 0f, 5.95f, -20f, 18f),
                Shot("a05_kitchen", 5.9f, 0f, 5.6f, 32f, 10f),
                Shot("a06_kitchen_to_stair", 7.6f, 0f, 6.8f, 135f, 12f),
                Shot("a07_study_bookcase", 7.7f, 3f, 7.5f, 90f, 6f),
                Shot("a08_bedroom", 4.3f, 3f, 5.9f, -35f, 12f),
                Shot("a09_window_street_H0", 2.7f, 0f, 1.6f, 180f, 2f),
            };
            var shotsB = new[]
            {
                Shot("b01_entrance_stair_storage", 6.6f, 0f, 1.4f, 45f, 8f),
                Shot("b02_under_stair_cupboard", 7.4f, 0f, 3.4f, 80f, 10f),
                Shot("b03_living", 4.4f, 0f, 5.9f, -62f, 10f),
                Shot("b04_dining", 6.5f, 0f, 5.75f, 25f, 18f),
                Shot("b05_kitchen", 3.9f, 0f, 3.6f, -150f, 10f),
            };
            foreach (var plan in new[] { Case01Floorplan.V05A, Case01Floorplan.V05B })
            {
                WithQa(qa =>
                {
                    string p = prefix + "rooms_";
                    foreach (var s in plan == Case01Floorplan.V05A ? shotsA : shotsB)
                    {
                        if (qa.Echo != null) qa.Echo.Stop();
                        qa.Hud.ClearMonologue();
                        Case01HouseCapture.Pose(qa, s.Pos, s.Yaw, s.Pitch);
                        if (qa.Echo != null) qa.Echo.Stop();
                        qa.Hud.ClearMonologue();
                        Case01HouseCapture.Shot(qa, rt, p + s.Name + ".png");
                    }
                    if (plan == Case01Floorplan.V05A)
                    {
                        qa.SetArtStage(3);
                        if (qa.Echo != null) qa.Echo.Stop();
                        qa.Hud.ClearMonologue();
                        Case01HouseCapture.Pose(qa, new Vector3(2.7f, 0.05f, 1.6f), 180f, 2f);
                        Case01HouseCapture.Advance(qa, 2f);
                        Case01HouseCapture.Shot(qa, rt, p + "a10_window_street_H3.png");
                    }
                    string tag = plan == Case01Floorplan.V05A ? "A" : "B";
                    Case01FloorplanCapture.TopDown(qa, rt, p + "map_" + tag + "_ground.png", 2.55f);
                    Case01FloorplanCapture.TopDown(qa, rt, p + "map_" + tag + "_upper.png", 5.75f);
                }, null, null, plan);
            }
        }

        private struct ShotSpec
        {
            public string Name;
            public Vector3 Pos;
            public float Yaw, Pitch;
        }

        private static ShotSpec Shot(string name, float x, float floorY, float z, float yaw, float pitch)
        {
            return new ShotSpec { Name = name, Pos = new Vector3(x, floorY + 0.05f, z), Yaw = yaw, Pitch = pitch };
        }

        /// <summary>連續 n 格（每格間隔 step 秒）縮小拼成 4 欄的接觸印樣。</summary>
        private static void Sheet(Case01HouseQA qa, RenderTexture rt, string path, int n, float step)
        {
            const int cols = 4;
            int rows = (n + cols - 1) / cols;
            int cw = rt.width / 2, ch = rt.height / 2;
            var sheet = new Texture2D(cw * cols, ch * rows, TextureFormat.RGB24, false);
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            var cell = new Color[cw * ch];
            try
            {
                for (int i = 0; i < n; i++)
                {
                    // 先以完整尺寸渲染，再 2×2 取平均縮小（避免攝影機 viewport 沿用舊尺寸而只拍到一角）。
                    var cam = qa.ActiveCamera;
                    bool was = cam.enabled;
                    cam.enabled = true;
                    cam.targetTexture = rt;
                    cam.Render();
                    cam.targetTexture = null;
                    cam.enabled = was;
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    tex.Apply();
                    RenderTexture.active = prev;
                    var full = tex.GetPixels();
                    for (int y = 0; y < ch; y++)
                        for (int x = 0; x < cw; x++)
                        {
                            int a = (y * 2) * rt.width + x * 2;
                            cell[y * cw + x] = (full[a] + full[a + 1] + full[a + rt.width] + full[a + rt.width + 1]) * 0.25f;
                        }
                    int cx = i % cols, cy = rows - 1 - i / cols;
                    sheet.SetPixels(cx * cw, cy * ch, cw, ch, cell);
                    Case01HouseCapture.Advance(qa, step);
                }
                sheet.Apply();
                File.WriteAllBytes(path, sheet.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sheet);
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        /// <summary>失序體：碰核 → 中庭只有碎片 → 凝聚各拍 → 完成 → 戰鬥鏡頭（同一份外殼）。</summary>
        private static void Enemy(RenderTexture rt, string prefix)
        {
            WithQa(qa =>
            {
                qa.Player.Teleport(Case01HouseBuilder.MezzCenter + new Vector3(0f, 0.05f, -0.4f), 0f, 22f);
                qa.Tick(Step, Case01Input.None);
                qa.Tick(Step, Case01Input.From(new ExploreInput { Interact = true }));
                Case01HouseCapture.AdvanceUntil(qa, q => q.Mode == Case01Mode.Manifesting, 0f);
                qa.Hud.ClearMonologue();
                float[] times = { 0f, 1.2f, 2.6f, 3.3f, 3.8f };
                float t = 0f;
                foreach (var target in times)
                {
                    for (; t < target; t += Step) qa.Tick(Step, Case01Input.None);
                    Case01HouseCapture.Shot(qa, rt, prefix + "enemy_t" + target.ToString("0.0") + "_" + qa.EnemyVisual + ".png");
                }
                Case01HouseCapture.AdvanceUntil(qa, q => q.Mode == Case01Mode.Exploring, 0.2f);
                Case01HouseCapture.Shot(qa, rt, prefix + "enemy_done_" + qa.EnemyVisual + ".png");
                qa.StartEncounter();
                Case01HouseCapture.AdvanceUntil(qa, q => q.Mode == Case01Mode.Battle, 0.5f);
                Case01HouseCapture.Shot(qa, rt, prefix + "enemy_battle_" + qa.EnemyVisual + ".png");
            }, Case01Checkpoint.S40);
        }
    }
}

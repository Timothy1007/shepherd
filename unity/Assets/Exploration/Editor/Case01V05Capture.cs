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
            var sets = new HashSet<string>(string.IsNullOrEmpty(set) ? new[] { "enemy" } : set.Split(','));
            foreach (var size in sizes)
            {
                var rt = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                try
                {
                    string prefix = Path.Combine(dir, "v05_" + size.x + "x" + size.y + "_");
                    if (sets.Contains("enemy")) Enemy(rt, prefix);
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

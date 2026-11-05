using System;
using System.Collections.Generic;
using System.IO;
using Shepherd.Battle.Core;
using Shepherd.Battle.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// 異域探索 Demo 離屏截圖：實際渲染街區、探索 HUD、過場與戰鬥畫面，依序擷取探索 → 遭遇 → 戰鬥 → 勝利 → 返回探索。
    /// 以固定時間步進推進；不修改場景或任何資產。
    /// batchmode（需圖形裝置，不可加 -nographics）：
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod Shepherd.Exploration.Editor.AnomalyStreetCapture.CaptureFromCommandLine
    ///   -captureDir &lt;資料夾&gt; -captureSizes 1920x1080
    /// </summary>
    public static class AnomalyStreetCapture
    {
        private const float Step = 1f / 30f;

        [MenuItem("Shepherd/Exploration/截圖檢查（1920×1080）")]
        public static void CaptureFromMenu()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/AnomalyStreetCapture"));
            CaptureAll(dir, new[] { new Vector2Int(1920, 1080) });
            EditorUtility.RevealInFinder(dir);
        }

        public static void CaptureFromCommandLine()
        {
            string dir = Arg("-captureDir") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/AnomalyStreetCapture"));
            string sizes = Arg("-captureSizes") ?? "1920x1080";
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
            foreach (var size in sizes) CaptureSequence(dir, size.x, size.y);
        }

        private static void CaptureSequence(string dir, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            GameObject root = null;
            AnomalyStreetDemo demo = null;
            string prefix = Path.Combine(dir, "street_" + width + "x" + height + "_");
            try
            {
                // ---- 標準情境（現有 Combat Lab 數值） ----
                root = new GameObject("AnomalyStreetCaptureRoot");
                demo = root.AddComponent<AnomalyStreetDemo>();
                demo.Build(false);
                demo.ApplyAtmosphere();
                var p = demo.Player;

                Advance(demo, 0.3f);
                Shot(demo, rt, prefix + "0_spawn.png");

                p.Teleport(new Vector3(-3f, 0.12f, -7f), -90f, 4f);
                Advance(demo, 0.1f);
                Shot(demo, rt, prefix + "1_alley.png");

                p.Teleport(new Vector3(-13.4f, 0.05f, -7f), -90f, 12f);
                demo.Tick(Step, new ExploreInput { Interact = true });
                Advance(demo, 0.2f);
                Shot(demo, rt, prefix + "2_inspect.png");

                p.Teleport(new Vector3(0.2f, 0.05f, -1f), 0f, 3f);
                Advance(demo, 0.1f);
                Shot(demo, rt, prefix + "3_approach.png");

                // 走進遭遇區（真的用移動輸入，不直接呼叫 StartEncounter）。
                for (int i = 0; i < 300 && demo.State == DemoState.Exploring; i++) demo.Tick(Step, ExploreInput.Forward(true));
                Advance(demo, AnomalyStreetDemo.EnterBlendSeconds * 0.45f);
                Shot(demo, rt, prefix + "4_entering.png");
                AdvanceUntil(demo, d => d.State == DemoState.Battle, 0.4f);
                var s = demo.Arena.Session;
                var hand = new List<CardDef>(s.Snapshot().Hand);
                s.AddCard(hand[0].Id);
                s.AddCard(hand[1].Id);
                Advance(demo, 0.3f);
                Shot(demo, rt, prefix + "5_battle.png");
                demo.Arena.CommitButton.onClick.Invoke();
                Advance(demo, 1.6f);
                Shot(demo, rt, prefix + "6_cast.png");
                Kill(ref root, demo);

                // ---- 第一人稱戰鬥（Demo 比較選項） ----
                root = new GameObject("AnomalyStreetCaptureRoot");
                demo = root.AddComponent<AnomalyStreetDemo>();
                demo.Build(false);
                demo.ApplyAtmosphere();
                demo.BattleView = BattleViewMode.FirstPerson;
                demo.Player.Teleport(new Vector3(0.2f, 0.05f, 4f), 0f);
                for (int i = 0; i < 300 && demo.State == DemoState.Exploring; i++) demo.Tick(Step, ExploreInput.Forward());
                AdvanceUntil(demo, d => d.State == DemoState.Battle, 0.4f);
                Shot(demo, rt, prefix + "7_battle_firstperson.png");
                Kill(ref root, demo);

                // ---- 勝利 → 返回探索（敵人只剩 1 點失序值＋1 層校正，空序列由核心的持續校正解除；與 CombatLab3D 截圖相同手法） ----
                var scenario = new CombatLabScenario { Name = "capture-victory" };
                scenario.Enemies.Add(new EnemyState { Id = 1, Name = "E1 失序體・甲", Value = 1, Corrections = 1, AttackPattern = new[] { 12 } });
                scenario.Enemies.Add(new EnemyState { Id = 2, Name = "E2 失序體・乙", Value = 1, Corrections = 1, AttackPattern = new[] { 6 } });
                root = new GameObject("AnomalyStreetCaptureRoot");
                demo = root.AddComponent<AnomalyStreetDemo>();
                demo.Build(false, scenario, 1);
                demo.ApplyAtmosphere();
                demo.Player.Teleport(new Vector3(0.2f, 0.05f, 4f), 0f);
                for (int i = 0; i < 300 && demo.State == DemoState.Exploring; i++) demo.Tick(Step, ExploreInput.Forward());
                AdvanceUntil(demo, d => d.State == DemoState.Battle, 0.2f);
                demo.Arena.CommitButton.onClick.Invoke();
                AdvanceUntil(demo, d => !d.Arena.IsPlaying, 0.4f);
                Shot(demo, rt, prefix + "8_victory.png");
                demo.Arena.BannerButton.onClick.Invoke();
                AdvanceUntil(demo, d => d.State == DemoState.Exploring, 0.3f);
                Shot(demo, rt, prefix + "9_returned.png");
                Debug.Log("[AnomalyStreetDemo] 截圖完成 " + prefix + "*.png");
            }
            finally
            {
                if (demo != null) demo.RestoreAtmosphere();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        private static void Kill(ref GameObject root, AnomalyStreetDemo demo)
        {
            demo.RestoreAtmosphere();
            UnityEngine.Object.DestroyImmediate(root);
            root = null;
        }

        private static void Advance(AnomalyStreetDemo demo, float seconds)
        {
            for (float t = 0; t < seconds; t += Step) demo.Tick(Step, ExploreInput.None);
        }

        private static void AdvanceUntil(AnomalyStreetDemo demo, Func<AnomalyStreetDemo, bool> done, float extra)
        {
            for (int i = 0; i < 30 * 40 && !done(demo); i++) demo.Tick(Step, ExploreInput.None);
            Advance(demo, extra);
        }

        private static void Shot(AnomalyStreetDemo demo, RenderTexture rt, string path)
        {
            var cam = demo.ActiveCamera;
            bool wasEnabled = cam.enabled;
            cam.enabled = true;
            // 先指定輸出貼圖：頭頂資訊依攝影機畫面尺寸換算位置。
            cam.targetTexture = rt;
            var canvases = new List<Canvas>();
            if (demo.Hud.Visible) canvases.Add(demo.Hud.Canvas);
            if (demo.Arena != null && demo.Arena.HudVisible) canvases.Add(demo.Arena.HudCanvas);
            var saved = new List<KeyValuePair<Canvas, RenderMode>>();
            foreach (var c in canvases)
            {
                saved.Add(new KeyValuePair<Canvas, RenderMode>(c, c.renderMode));
                var scaler = c.GetComponent<CanvasScaler>();
                float scale = CombatLabView.ScaleFactorFor(scaler, rt.width, rt.height);
                scaler.enabled = false;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = c == demo.Hud.Canvas ? 0.5f : 1f;
                c.scaleFactor = scale;
            }
            // 先讓畫布套用新的渲染模式與縮放，再依新尺寸擺放頭頂資訊。
            Canvas.ForceUpdateCanvases();
            if (demo.Arena != null && demo.Arena.HudVisible)
            {
                demo.Arena.Refresh();
                if (demo.Arena.IsPlaying) demo.Arena.Tick(0f);
            }
            Canvas.ForceUpdateCanvases();
            cam.Render();
            cam.targetTexture = null;
            cam.enabled = wasEnabled;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            foreach (var kv in saved)
            {
                kv.Key.renderMode = kv.Value;
                kv.Key.worldCamera = null;
                kv.Key.GetComponent<CanvasScaler>().enabled = true;
            }
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }
            return null;
        }
    }
}

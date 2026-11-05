using System;
using System.IO;
using Shepherd.Battle.Core;
using Shepherd.Battle.UI.Arena;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Battle.Editor
{
    /// <summary>
    /// 3D Combat Lab 離屏截圖：實際渲染 3D 場景與 HUD，依序擷取「編排／懸停／施放命中／敵方反擊／回合後」幾個畫面。
    /// 動畫以固定時間步進推進，事件與數值全部來自戰鬥核心。不修改場景或任何資產。
    /// batchmode（需圖形裝置，不可加 -nographics）：
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod Shepherd.Battle.Editor.CombatArenaCapture.CaptureFromCommandLine
    ///   -captureDir &lt;資料夾&gt; -captureSizes 1920x1080
    /// </summary>
    public static class CombatArenaCapture
    {
        private const float Step = 1f / 30f;

        [MenuItem("Shepherd/Combat Lab 3D/截圖檢查（1920×1080）")]
        public static void CaptureFromMenu()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/CombatLab3DCapture"));
            CaptureAll(dir, new[] { new Vector2Int(1920, 1080) });
            EditorUtility.RevealInFinder(dir);
        }

        public static void CaptureFromCommandLine()
        {
            string dir = Arg("-captureDir") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/CombatLab3DCapture"));
            string sizes = Arg("-captureSizes") ?? "1920x1080";
            var list = new System.Collections.Generic.List<Vector2Int>();
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
            var root = new GameObject("CombatLab3DCaptureRoot");
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            CombatArenaView view = null;
            try
            {
                view = root.AddComponent<CombatArenaView>();
                view.Build(false);
                view.ApplyAtmosphere();
                var cam = view.ArenaCamera;
                cam.targetTexture = rt;
                var canvas = view.HudCanvas;
                var scaler = canvas.GetComponent<CanvasScaler>();
                float scale = Shepherd.Battle.UI.CombatLabView.ScaleFactorFor(scaler, width, height);
                scaler.enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f;
                canvas.scaleFactor = scale;
                string prefix = Path.Combine(dir, "combatlab3d_" + width + "x" + height + "_");

                var s = view.Session;
                Advance(view, 0.5f);
                Shot(view, rt, prefix + "0_start.png");

                // 編排：優先放燃素（施壓）讓截圖看得到敵人受擊，其次基體；目標為目前目標。
                var hand = new System.Collections.Generic.List<CardDef>(s.Snapshot().Hand);
                hand.Sort((a, b) => Rank(a.Element).CompareTo(Rank(b.Element)));
                s.AddCard(hand[0].Id);
                s.AddCard(hand[1].Id);
                Advance(view, 0.3f);
                Shot(view, rt, prefix + "1_plan.png");

                view.ShowCardTooltip(3);
                Advance(view, 0.1f);
                Shot(view, rt, prefix + "2_hover.png");
                view.HideCardTooltip(-1);

                view.CommitButton.onClick.Invoke();
                // 施放命中：第一次壓制或擬態增加的瞬間之後。
                AdvanceUntil(view, v => Count(v, EventKind.Pressure) + Count(v, EventKind.ShieldChanged) >= 1, 0.18f);
                Shot(view, rt, prefix + "3_cast.png");
                // 敵方反擊：第一次玩家錨定值受損或擬態吸收之後。
                AdvanceUntil(view, v => HasEnemyDamage(v), 0.12f);
                Shot(view, rt, prefix + "4_enemy_attack.png");
                AdvanceUntil(view, v => !v.IsPlaying, 0.6f);
                Shot(view, rt, prefix + "5_after.png");

                // 勝利畫面：重置後讓兩個敵人只剩 1 點失序值並帶 1 層校正，以空序列結束回合，由核心的持續校正解除。
                view.RestoreAtmosphere();
                view.ArenaCamera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(root);
                root = new GameObject("CombatLab3DCaptureRoot");
                var scenario = new Shepherd.Battle.UI.CombatLabScenario { Name = "capture-victory" };
                scenario.Enemies.Add(new EnemyState { Id = 1, Name = "E1 失序體・甲", Value = 1, Corrections = 1, AttackPattern = new[] { 12 } });
                scenario.Enemies.Add(new EnemyState { Id = 2, Name = "E2 失序體・乙", Value = 1, Corrections = 1, AttackPattern = new[] { 6 } });
                view = root.AddComponent<CombatArenaView>();
                view.Build(false, scenario, 1);
                view.ApplyAtmosphere();
                view.ArenaCamera.targetTexture = rt;
                canvas = view.HudCanvas;
                canvas.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = view.ArenaCamera;
                canvas.planeDistance = 1f;
                canvas.scaleFactor = scale;
                view.CommitButton.onClick.Invoke();
                AdvanceUntil(view, v => Count(v, EventKind.EnemyResolved) >= 1, 0.3f);
                Shot(view, rt, prefix + "6_resolve.png");
                AdvanceUntil(view, v => !v.IsPlaying, 0.4f);
                Shot(view, rt, prefix + "7_victory.png");
                Debug.Log("[CombatLab3D] 截圖完成 " + prefix + "*.png");
            }
            finally
            {
                if (view != null) view.RestoreAtmosphere();
                if (view != null && view.ArenaCamera != null) view.ArenaCamera.targetTexture = null;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static int Rank(ElementKind e)
        {
            return e == ElementKind.Ember ? 0 : e == ElementKind.Base ? 1 : 2;
        }

        private static int Count(CombatArenaView v, EventKind kind)
        {
            int n = 0;
            foreach (var e in v.PlayedEvents) if (e.Kind == kind) n++;
            return n;
        }

        private static bool HasEnemyDamage(CombatArenaView v)
        {
            foreach (var e in v.PlayedEvents)
            {
                if ((e.Kind == EventKind.AnchorChanged || e.Kind == EventKind.ShieldChanged) && e.Amount < 0
                    && e.Source != null && e.Source.StartsWith("enemy:")) return true;
            }
            return false;
        }

        private static void Advance(CombatArenaView view, float seconds)
        {
            for (float t = 0; t < seconds; t += Step) view.Tick(Step);
        }

        private static void AdvanceUntil(CombatArenaView view, Func<CombatArenaView, bool> done, float extra)
        {
            for (int i = 0; i < 30 * 30 && !done(view); i++) view.Tick(Step);
            Advance(view, extra);
        }

        private static void Shot(CombatArenaView view, RenderTexture rt, string path)
        {
            Canvas.ForceUpdateCanvases();
            view.Refresh();
            if (view.IsPlaying) view.Tick(0f);
            Canvas.ForceUpdateCanvases();
            view.ArenaCamera.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
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

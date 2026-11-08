using System;
using System.Collections.Generic;
using System.IO;
using Shepherd.Battle.Core;
using Shepherd.Battle.UI;
using Shepherd.Battle.UI.Arena;
using Shepherd.Exploration.Case01;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// 戰鬥 Demo UI 離屏截圖（1920×1080／1280×720）：完整一回合──回合開始、牌面詳情、編排兩張（承接）、更換目標、三連共鳴、
    /// 切換學派、播放中、下一回合，以及舊版 HUD 對照。不修改場景或資產。
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod Shepherd.Exploration.Editor.Case01CombatDemoCapture.CaptureFromCommandLine
    ///   -captureDir &lt;資料夾&gt; -captureSizes 1920x1080,1280x720
    /// </summary>
    public static class Case01CombatDemoCapture
    {
        private const float Step = 1f / 30f;

        [MenuItem("Shepherd/Case01/戰鬥 Demo UI 截圖（1920×1080、1280×720）")]
        public static void CaptureFromMenu()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01CombatDemoCapture"));
            CaptureAll(dir, new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) });
            EditorUtility.RevealInFinder(dir);
        }

        public static void CaptureFromCommandLine()
        {
            string dir = Arg("-captureDir") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01CombatDemoCapture"));
            string sizes = Arg("-captureSizes") ?? "1920x1080,1280x720";
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
            foreach (var size in sizes)
            {
                Sequence(dir, size.x, size.y);
                TutorialSequence(dir, size.x, size.y);
            }
        }

        /// <summary>首戰教學：四個節點、播放中的事件說明、收尾。</summary>
        private static void TutorialSequence(string dir, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            GameObject root = null;
            Case01CombatDemoQA qa = null;
            string prefix = Path.Combine(dir, "tutorial_" + width + "x" + height + "_");
            try
            {
                root = new GameObject("CombatDemoTutorialCaptureRoot");
                qa = root.AddComponent<Case01CombatDemoQA>();
                qa.Build(false, null, null, true);
                qa.Arena.ApplyAtmosphere();
                var hud = qa.Hud;
                var tut = hud.Tutorial;
                Advance(qa, 0.3f);
                Shot(qa, rt, prefix + "T1_threat.png");
                tut.Continue();
                hud.OnSchool(SchoolKind.Process);
                Advance(qa, 0.1f);
                Shot(qa, rt, prefix + "T2_resources_school.png");
                tut.Continue();
                Advance(qa, 0.1f);
                Shot(qa, rt, prefix + "T3_compose.png");
                hud.OnCard(0);
                Advance(qa, 0.1f);
                Shot(qa, rt, prefix + "T3b_compose_residue_tip.png");
                hud.OnCard(1);
                Advance(qa, 0.6f);
                Shot(qa, rt, prefix + "T4_commit.png");
                hud.OnCommit();
                bool noted = false;
                for (int i = 0; i < 1500 && qa.Arena.IsPlaying; i++)
                {
                    Advance(qa, Step);
                    if (!noted && tut.NoteVisible)
                    {
                        noted = true;
                        Advance(qa, 0.3f);
                        Shot(qa, rt, prefix + "T5_result_note.png");
                    }
                }
                Advance(qa, 0.2f);
                Shot(qa, rt, prefix + "T6_wrap.png");
                Debug.Log("[Case01CombatDemoQA] 教學截圖完成 " + prefix + "*.png（說明：" + string.Join("／", tut.Notes.ToArray()) + "）");
            }
            finally
            {
                if (qa != null && qa.Arena != null) qa.Arena.RestoreAtmosphere();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        private static void Sequence(string dir, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            GameObject root = null;
            Case01CombatDemoQA qa = null;
            string prefix = Path.Combine(dir, "combat_" + width + "x" + height + "_");
            try
            {
                root = new GameObject("CombatDemoCaptureRoot");
                qa = root.AddComponent<Case01CombatDemoQA>();
                qa.Build(false, null, null, false);
                qa.Arena.ApplyAtmosphere();
                var hud = qa.Hud;
                var s = qa.Arena.Session;
                Advance(qa, 0.3f);
                Shot(qa, rt, prefix + "01_turn_start.png");

                hud.ShowDetail(0);
                Shot(qa, rt, prefix + "02_card_detail.png");
                hud.HideDetail(-1);

                hud.OnCard(0);
                hud.OnCard(1);
                Advance(qa, 0.1f);
                Shot(qa, rt, prefix + "03_sequence_two.png");
                hud.OnRetarget(0);
                Advance(qa, 0.1f);
                Shot(qa, rt, prefix + "04_retarget.png");

                // 三連：手牌中若有三張同元素就排入。
                hud.OnClear();
                var hand = s.Snapshot().Hand;
                foreach (ElementKind el in Enum.GetValues(typeof(ElementKind)))
                {
                    var same = new List<int>();
                    for (int i = 0; i < hand.Count; i++) if (hand[i].Element == el) same.Add(i);
                    if (same.Count < 3) continue;
                    for (int k = 0; k < 3; k++) hud.OnCard(same[k]);
                    break;
                }
                Advance(qa, 0.1f);
                Shot(qa, rt, prefix + "05_resonance.png");

                hud.OnClear();
                hud.OnSchool(SchoolKind.Process);
                hud.OnCard(0);
                hud.OnCard(2);
                Advance(qa, 0.1f);
                Shot(qa, rt, prefix + "06_school_process.png");

                hud.OnCommit();
                Advance(qa, 1.1f);
                Shot(qa, rt, prefix + "07_playback.png");
                qa.Arena.FinishPlayback();
                Advance(qa, 0.2f);
                Shot(qa, rt, prefix + "08_next_round.png");

                qa.Arena.RestoreAtmosphere();
                qa.ToggleLegacy();
                qa.Arena.ApplyAtmosphere();
                Advance(qa, 0.2f);
                Shot(qa, rt, prefix + "09_legacy_compare.png");
                Debug.Log("[Case01CombatDemoQA] 截圖完成 " + prefix + "*.png");
            }
            finally
            {
                if (qa != null && qa.Arena != null) qa.Arena.RestoreAtmosphere();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        private static void Advance(Case01CombatDemoQA qa, float seconds)
        {
            for (float t = 0; t < seconds; t += Step)
            {
                qa.Arena.Tick(Step);
                if (qa.Hud != null) qa.Hud.Tick(Step);
            }
        }

        private static void Shot(Case01CombatDemoQA qa, RenderTexture rt, string path)
        {
            var cam = qa.Arena.ArenaCamera;
            cam.targetTexture = rt;
            var canvases = new List<Canvas> { qa.Arena.HudCanvas };
            if (qa.Hud != null) canvases.Add(qa.Hud.Canvas);
            var saved = new List<KeyValuePair<Canvas, RenderMode>>();
            for (int i = 0; i < canvases.Count; i++)
            {
                var c = canvases[i];
                saved.Add(new KeyValuePair<Canvas, RenderMode>(c, c.renderMode));
                var scaler = c.GetComponent<CanvasScaler>();
                float scale = CombatLabView.ScaleFactorFor(scaler, rt.width, rt.height);
                scaler.enabled = false;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f - i * 0.2f;
                c.scaleFactor = scale;
            }
            Canvas.ForceUpdateCanvases();
            qa.Arena.Refresh();
            if (qa.Hud != null)
            {
                qa.Hud.Refresh();
                qa.Hud.Tick(0f);
            }
            Canvas.ForceUpdateCanvases();
            cam.Render();
            cam.targetTexture = null;
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
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}

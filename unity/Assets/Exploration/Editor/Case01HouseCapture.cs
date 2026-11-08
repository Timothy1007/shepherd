using System;
using System.Collections.Generic;
using System.IO;
using Shepherd.Battle.UI;
using Shepherd.Exploration.Case01;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// 室內 QA 離屏截圖（檢查 E 提示、字幕、日誌、選項、QA 面板在 1920×1080／1280×720 的可讀性）。
    /// 以固定時間步進推進；不修改場景或任何資產。
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod Shepherd.Exploration.Editor.Case01HouseCapture.CaptureFromCommandLine
    ///   -captureDir &lt;資料夾&gt; -captureSizes 1920x1080,1280x720
    /// </summary>
    public static class Case01HouseCapture
    {
        private const float Step = 1f / 30f;

        [MenuItem("Shepherd/Case01/截圖檢查（1920×1080、1280×720）")]
        public static void CaptureFromMenu()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01HouseCapture"));
            CaptureAll(dir, new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) });
            EditorUtility.RevealInFinder(dir);
        }

        public static void CaptureFromCommandLine()
        {
            string dir = Arg("-captureDir") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01HouseCapture"));
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
            foreach (var size in sizes) CaptureSequence(dir, size.x, size.y);
        }

        private static void CaptureSequence(string dir, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            GameObject root = null;
            Case01HouseQA qa = null;
            string prefix = Path.Combine(dir, "case01_" + width + "x" + height + "_");
            try
            {
                root = new GameObject("Case01CaptureRoot");
                qa = root.AddComponent<Case01HouseQA>();
                qa.Build(false, QuickVictory(), 1);
                qa.ApplyAtmosphere();
                var p = qa.Player;
                Advance(qa, 0.3f);
                Shot(qa, rt, prefix + "00_entrance.png");
                // v0.2 生活細節（H0）。
                Pose(qa, new Vector3(3.9f, 0.05f, 6.3f), -55f, 8f);
                Shot(qa, rt, prefix + "00b_living_h0.png");
                Pose(qa, new Vector3(2.6f, 0.05f, 3.4f), -120f, 15f);
                Shot(qa, rt, prefix + "00c_kitchen.png");
                Pose(qa, new Vector3(4.2f, 3.05f, 6.6f), -35f, 18f);
                Shot(qa, rt, prefix + "00d_bedroom.png");
                Pose(qa, new Vector3(6.3f, 3.05f, 6.3f), 40f, 12f);
                Shot(qa, rt, prefix + "00e_study_h0.png");
                Pose(qa, new Vector3(7.8f, 3.05f, 4.3f), -122f, 14f);
                Shot(qa, rt, prefix + "00f_landing_h0.png");

                p.Teleport(new Vector3(6.6f, 0.05f, 6.2f), 0f);
                Advance(qa, 0.1f);
                Interact(qa, Case01Ids.Table);
                Shot(qa, rt, prefix + "01_table_two.png");

                p.Teleport(new Vector3(6.5f, 0.05f, 3.6f), 180f);
                Advance(qa, 0.2f);
                p.Teleport(new Vector3(6.6f, 0.05f, 6.2f), 0f);
                Advance(qa, 0.2f);
                Interact(qa, Case01Ids.Table);
                Shot(qa, rt, prefix + "02_table_one_radio.png");

                p.Teleport(new Vector3(6.7f, 3.05f, 8.2f), 0f);
                Advance(qa, 0.2f);
                Interact(qa, Case01Ids.EllieNotes);
                Shot(qa, rt, prefix + "03_study_notes.png");
                qa.Tick(Step, new Case01Input { Journal = true });
                Shot(qa, rt, prefix + "04_journal.png");
                qa.Tick(Step, new Case01Input { Journal = true });

                qa.JumpTo(Case01Checkpoint.S30);
                Advance(qa, 0.3f);
                Shot(qa, rt, prefix + "05_landing_wrong_connection.png");
                // H1／H2 的安靜異變（檢查點直接套用）。
                Pose(qa, new Vector3(3.9f, 0.05f, 6.3f), -55f, 8f);
                Shot(qa, rt, prefix + "05b_living_h1.png");
                Pose(qa, new Vector3(6.3f, 3.05f, 6.3f), 40f, 12f);
                Shot(qa, rt, prefix + "05c_study_h2.png");
                Pose(qa, new Vector3(7.8f, 3.05f, 4.3f), -122f, 14f);
                Shot(qa, rt, prefix + "05d_landing_h2.png");
                qa.Player.Teleport(new Vector3(7.6f, 3.05f, 2.4f), -12f);
                Advance(qa, 0.1f);
                qa.Flow.Traverse(Case01Door.StudyDoor);
                qa.Player.Teleport(new Vector3(6.3f, 3.05f, 3.9f), 0f);
                Advance(qa, 0.1f);
                Interact(qa, Case01Ids.StudyMark);
                Shot(qa, rt, prefix + "06_override_choice.png");
                qa.Tick(Step, new Case01Input { Choice = 1 });
                qa.Player.Teleport(new Vector3(6.95f, 3.05f, 2.6f), 180f);
                Advance(qa, 0.2f);
                Shot(qa, rt, prefix + "07_third_door_open.png");

                qa.JumpTo(Case01Checkpoint.S40);
                Advance(qa, 0.3f);
                Shot(qa, rt, prefix + "08_mezzanine.png");
                qa.Player.Teleport(new Vector3(40f, 0.05f, -0.4f), 0f);
                Advance(qa, 0.1f);
                Interact(qa, Case01Ids.Core);
                Advance(qa, 1.2f);
                Shot(qa, rt, prefix + "09_defense.png");
                // v0.3：失序體由碎片凝聚成形（前／中／後）。
                AdvanceUntil(qa, q => q.Mode == Case01Mode.Manifesting, 0f);
                Advance(qa, 0.6f);
                Shot(qa, rt, prefix + "10a_manifest_tremble.png");
                Advance(qa, 1.3f);
                Shot(qa, rt, prefix + "10b_manifest_drift.png");
                Advance(qa, 1.4f);
                Shot(qa, rt, prefix + "10c_manifest_assemble.png");
                AdvanceUntil(qa, q => q.Mode == Case01Mode.Exploring, 0.3f);
                Shot(qa, rt, prefix + "10_courtyard.png");
                for (int i = 0; i < 600 && qa.Mode == Case01Mode.Exploring; i++) qa.Tick(Step, ExploreInput.Forward());
                AdvanceUntil(qa, q => q.Mode == Case01Mode.Battle, 0.4f);
                Shot(qa, rt, prefix + "11_battle.png");

                qa.JumpTo(Case01Checkpoint.S50);
                var f = qa.Flow;
                f.Interact(Case01Ids.Core);
                f.Traverse(Case01Door.MezzCircle);
                f.Interact(Case01Ids.Core);
                f.Choose(Case01Ids.ChoiceAnchor, 0);
                f.Traverse(Case01Door.MezzTriangle);
                f.Traverse(Case01Door.Mezz17);
                qa.Player.Teleport(new Vector3(40f, 0.05f, -0.4f), 0f);
                Advance(qa, 0.1f);
                Interact(qa, Case01Ids.Core);
                Advance(qa, 2.5f);
                qa.Player.Teleport(new Vector3(40f, 0.05f, -3.5f), 0f, 8f);
                Advance(qa, 0.1f);
                Shot(qa, rt, prefix + "12_core_stabilized.png");
                // H3：夾層的 FP-001 與 3D 檢視（正面、背面）、客廳殘留。
                Pose(qa, new Vector3(38.7f, 0.05f, -0.2f), -60f, 14f);
                Shot(qa, rt, prefix + "12b_fp001_in_mezzanine.png");
                Interact(qa, Case01Ids.EvidencePhoto);
                Shot(qa, rt, prefix + "12c_inspect_front.png");
                qa.RotateInspect(180f);
                Shot(qa, rt, prefix + "12d_inspect_back.png");
                qa.Tick(Step, new ExploreInput { Interact = true });
                Pose(qa, new Vector3(3.9f, 0.05f, 6.3f), -55f, 8f);
                Shot(qa, rt, prefix + "12e_living_h3.png");

                qa.Player.Teleport(new Vector3(7.1f, 0.05f, 1.6f), 180f);
                Advance(qa, 0.1f);
                Interact(qa, Case01Ids.FrontDoor);
                Shot(qa, rt, prefix + "13_exit_choice.png");
                qa.Tick(Step, new Case01Input { Choice = 1 });
                Advance(qa, 0.2f);
                Shot(qa, rt, prefix + "14_end.png");
                // ---- v0.3 核校正（關係校正）：核前、選錯、連結校正中、完成後 ----
                qa.RestoreAtmosphere();
                UnityEngine.Object.DestroyImmediate(root);
                root = new GameObject("Case01CaptureRoot");
                qa = root.AddComponent<Case01HouseQA>();
                qa.Build(false, QuickVictory(), 1, Case01Checkpoint.S50, Case01QaMode.CoreV03);
                qa.ApplyAtmosphere();
                var mc = Case01HouseBuilder.MezzCenter;
                qa.Player.Teleport(mc + new Vector3(0f, 0.05f, -0.4f), 0f);
                Advance(qa, 0.2f);
                Interact(qa, Case01Ids.Core);
                Shot(qa, rt, prefix + "v03_1_device.png");
                qa.Tick(Step, new Case01Input { Choice = 2 });
                Advance(qa, 0.3f);
                Shot(qa, rt, prefix + "v03_2_wrong_preview.png");
                qa.Tick(Step, new Case01Input { Choice = 1 });
                qa.Tick(Step, new Case01Input { Choice = 1 });
                qa.Tick(Step, new ExploreInput { Interact = true });
                Interact(qa, Case01Ids.Core);
                Shot(qa, rt, prefix + "v03_3_second_link.png");
                qa.Tick(Step, new Case01Input { Choice = 1 });
                qa.Tick(Step, new ExploreInput { Interact = true });
                Pose(qa, mc + new Vector3(-1.5f, 0.05f, -2.5f), 20f, 8f);
                Shot(qa, rt, prefix + "v03_4_linked.png");
                qa.Player.Teleport(mc + new Vector3(0f, 0.05f, 4.6f), 0f);
                for (int i = 0; i < 90 && qa.CurrentRoom != Case01Room.Entrance; i++) qa.Tick(Step, ExploreInput.Forward());
                Advance(qa, 0.55f);
                Shot(qa, rt, prefix + "v03_5_commit_snap.png");
                Advance(qa, 2.5f);
                Pose(qa, new Vector3(3.9f, 0.05f, 6.3f), -55f, 8f);
                Shot(qa, rt, prefix + "v03_6_after_living.png");
                Debug.Log("[Case01HouseQA] 截圖完成 " + prefix + "*.png");
            }
            finally
            {
                if (qa != null) qa.RestoreAtmosphere();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        private static Shepherd.Battle.UI.CombatLabScenario QuickVictory()
        {
            var s = new Shepherd.Battle.UI.CombatLabScenario { Name = "capture" };
            s.Enemies.Add(new Shepherd.Battle.Core.EnemyState { Id = 1, Name = "E1 失序體・甲", Value = 40, AttackPattern = new[] { 12, 8, 14, 6 } });
            s.Enemies.Add(new Shepherd.Battle.Core.EnemyState { Id = 2, Name = "E2 失序體・乙", Value = 24, AttackPattern = new[] { 6, 0, 9, 5 } });
            return s;
        }

        private static void Interact(Case01HouseQA qa, string id)
        {
            var it = qa.Layout.Interactables[id];
            var target = it.transform.TransformPoint(it.GetComponent<BoxCollider>().center);
            for (int i = 0; i < 2; i++)
            {
                var d = target - qa.Player.EyePosition;
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
                var look = new Vector2(Mathf.DeltaAngle(qa.Player.Yaw, yaw), -(pitch - qa.Player.Pitch)) / FirstPersonController.LookSensitivity;
                qa.Tick(Step, new ExploreInput { Look = look });
            }
            qa.Tick(Step, new ExploreInput { Interact = true });
        }

        private static void Pose(Case01HouseQA qa, Vector3 pos, float yaw, float pitch)
        {
            qa.Player.Teleport(pos, yaw, pitch);
            Advance(qa, 0.1f);
        }

        private static void Advance(Case01HouseQA qa, float seconds)
        {
            for (float t = 0; t < seconds; t += Step) qa.Tick(Step, Case01Input.None);
        }

        private static void AdvanceUntil(Case01HouseQA qa, Func<Case01HouseQA, bool> done, float extra)
        {
            for (int i = 0; i < 30 * 20 && !done(qa); i++) qa.Tick(Step, Case01Input.None);
            Advance(qa, extra);
        }

        private static void Shot(Case01HouseQA qa, RenderTexture rt, string path)
        {
            var cam = qa.ActiveCamera;
            bool wasEnabled = cam.enabled;
            cam.enabled = true;
            cam.targetTexture = rt;
            var canvases = new List<Canvas>();
            if (qa.Hud.Visible) canvases.Add(qa.Hud.Base.Canvas);
            if (qa.Arena != null && qa.Arena.HudVisible) canvases.Add(qa.Arena.HudCanvas);
            if (qa.DemoHud != null && qa.Arena != null && qa.Arena.HudVisible) canvases.Add(qa.DemoHud.Canvas);
            var saved = new List<KeyValuePair<Canvas, RenderMode>>();
            foreach (var c in canvases)
            {
                saved.Add(new KeyValuePair<Canvas, RenderMode>(c, c.renderMode));
                var scaler = c.GetComponent<CanvasScaler>();
                float scale = CombatLabView.ScaleFactorFor(scaler, rt.width, rt.height);
                scaler.enabled = false;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = c == qa.Hud.Base.Canvas ? 0.5f : qa.DemoHud != null && c == qa.DemoHud.Canvas ? 0.8f : 1f;
                c.scaleFactor = scale;
            }
            Canvas.ForceUpdateCanvases();
            if (qa.Arena != null && qa.Arena.HudVisible)
            {
                qa.Arena.Refresh();
                if (qa.Arena.IsPlaying) qa.Arena.Tick(0f);
                if (qa.DemoHud != null)
                {
                    qa.DemoHud.Refresh();
                    qa.DemoHud.Tick(0f);
                }
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

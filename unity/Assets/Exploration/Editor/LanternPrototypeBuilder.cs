using System.IO;
using System.Text;
using Shepherd.Exploration.Lantern;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shepherd.Exploration.Editor
{
    /// <summary>提燈手感 QA：建立／開啟獨立場景，產生玩家眼睛高度的對照截圖。</summary>
    public static class LanternPrototypeBuilder
    {
        public const string ScenePath = "Assets/Exploration/Scenes/LanternPrototypeQA.unity";

        [MenuItem("Shepherd/Prototype/提燈手感 QA")]
        public static void Launch()
        {
            if (!File.Exists(ScenePath)) BuildScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Shepherd/Prototype/提燈手感 QA 重建場景")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            new GameObject("LanternPrototypeQA").AddComponent<LanternPrototypeQA>();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }

        public static void BuildFromCommandLine() => BuildScene();

        public static void CaptureFromCommandLine()
        {
            string dir = Arg("-captureDir") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/LanternPrototypeCapture"));
            Capture(dir);
        }

        [MenuItem("Shepherd/Prototype/提燈手感 QA 對照截圖")]
        public static void CaptureFromMenu()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/LanternPrototypeCapture"));
            Capture(dir);
            EditorUtility.RevealInFinder(dir);
        }

        public static void Capture(string dir)
        {
            Directory.CreateDirectory(dir);
            if (!File.Exists(ScenePath)) BuildScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var qa = Object.FindObjectOfType<LanternPrototypeQA>();
            var log = new StringBuilder();
            try
            {
                qa.Setup(false);
                Pair(qa, rt, dir, "Spawn", "01_hall", log);
                Pair(qa, rt, dir, "HallView", "02_hall_wall", log);
                Pair(qa, rt, dir, "LivingEntry", "03_living_entry", log);
                Pair(qa, rt, dir, "LivingTable", "04_living_table", log);
                Pair(qa, rt, dir, "LivingAlcove", "05_living_alcove", log);
                Pair(qa, rt, dir, "BrightEntry", "06_bright_entry", log);
                Pair(qa, rt, dir, "BrightCalendar", "07_bright_calendar", log);
                Pair(qa, rt, dir, "KitchenEntry", "07b_kitchen", log);
                ActionShots(qa, rt, dir, log);
                CombatShots(qa, rt, dir, log);
                // 光圈大小（同機位）。
                qa.GoTo("LivingTable");
                qa.Rig.SetOn(true);
                for (int i = 0; i < 3; i++)
                {
                    qa.Rig.SetRadius(i);
                    Frames(qa, 4);
                    Shot(qa, rt, Path.Combine(dir, "08_radius_" + i + ".png"));
                }
                qa.Rig.SetRadius(1);
                // 低頭看身體與提燈。
                qa.GoTo("Spawn");
                qa.Player.Teleport(qa.Player.transform.position, qa.Player.Yaw, 62f);
                Frames(qa, 4);
                Shot(qa, rt, Path.Combine(dir, "09_look_down.png"));
                // 走路中（提燈擺動、鏡頭晃動）。
                qa.GoTo("Spawn");
                for (int i = 0; i < 24; i++) qa.Step(1f / 30f, new ExploreInput { Move = new Vector2(0f, 1f), Look = new Vector2(i < 12 ? 0.8f : -0.8f, 0f) });
                Shot(qa, rt, Path.Combine(dir, "10_walking_swing.png"));
                log.AppendLine("ok");
            }
            finally
            {
                if (qa != null) qa.Shutdown();
                if (rt != null) Object.DestroyImmediate(rt);
                File.WriteAllText(Path.Combine(dir, "capture.txt"), log.ToString());
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        /// <summary>讓玩家看向某一點（保持目前位置）。</summary>
        public static void Aim(LanternPrototypeQA qa, Vector3 point)
        {
            var d = point - qa.Player.EyePosition;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            qa.Player.Teleport(qa.Player.transform.position, yaw, pitch);
        }

        private static void Act(LanternPrototypeQA qa, int frames, LanternActionInput a)
        {
            for (int i = 0; i < frames; i++) qa.Step(1f / 30f, ExploreInput.None, a);
        }

        private static void ActionShots(LanternPrototypeQA qa, RenderTexture rt, string dir, StringBuilder log)
        {
            var w = qa.World;
            qa.Rig.SetOn(true);
            // 牽引：抓起藍坐墊椅子，浮在面前。
            qa.GoTo("KitchenEntry");
            var chair = w.Tetherables.Find(t => t.name == "BlueChair");
            Aim(qa, chair.GetComponent<Collider>().bounds.center);
            Act(qa, 1, new LanternActionInput { SelectMode = 2 });
            Act(qa, 1, LanternActionInput.None);
            Act(qa, 1, new LanternActionInput { Primary = true });
            Act(qa, 30, new LanternActionInput { Primary = true });
            Aim(qa, new Vector3(5.2f, 1.1f, 2.4f));
            Act(qa, 25, new LanternActionInput { Primary = true });
            Shot(qa, rt, Path.Combine(dir, "11_tether_hold.png"));
            log.AppendLine("tether held=" + (qa.Actions.Held != null) + " chairY=" + chair.transform.position.y.ToString("F2"));
            // 放回凹痕上。
            Act(qa, 1, LanternActionInput.None);
            Act(qa, 1, new LanternActionInput { Primary = true });
            Act(qa, 1, LanternActionInput.None);
            chair.Body.position = new Vector3(6.05f, 0.02f, 2.55f);
            chair.Body.rotation = Quaternion.identity;
            chair.transform.SetPositionAndRotation(chair.Body.position, chair.Body.rotation);
            chair.Body.velocity = Vector3.zero;
            Act(qa, 30, LanternActionInput.None);
            qa.GoTo("KitchenDents");
            Frames(qa, 3);
            Shot(qa, rt, Path.Combine(dir, "12_chair_placed.png"));
            log.AppendLine("placed=" + w.Spots[0].Solved);
            // 復原：倒下的高櫃（中途與完成）。
            qa.GoTo("LivingAlcove");
            var cab = w.Restorables.Find(r => r.name == "Cabinet");
            Aim(qa, cab.GetComponent<Collider>().bounds.center);
            Act(qa, 1, new LanternActionInput { SelectMode = 3 });
            Act(qa, 24, new LanternActionInput { Primary = true });
            Shot(qa, rt, Path.Combine(dir, "13_restore_mid.png"));
            Act(qa, 40, new LanternActionInput { Primary = true });
            Act(qa, 2, LanternActionInput.None);
            Aim(qa, new Vector3(6.3f, 1.0f, 3.9f));
            Frames(qa, 2);
            Shot(qa, rt, Path.Combine(dir, "14_restore_done.png"));
            log.AppendLine("restored=" + cab.Done);
            // 射擊：提燈關掉，對異域牆蓄力射擊，撞擊點短暫打開現實，看到後面的儲藏室。
            qa.GoTo("KitchenWall");
            qa.Rig.SetOn(false);
            Act(qa, 1, new LanternActionInput { SelectMode = 1 });
            Frames(qa, 6);
            Shot(qa, rt, Path.Combine(dir, "15a_wall_before.png"));
            Act(qa, 33, new LanternActionInput { Primary = true });
            Act(qa, 12, LanternActionInput.None);
            Shot(qa, rt, Path.Combine(dir, "15b_wall_shot.png"));
            log.AppendLine("impacts=" + qa.Actions.Impacts + " open=" + w.Blockers[0].Open);
            qa.Rig.SetOn(true);
            // 練習靶：蓄力射擊，打中後暈眩、出現干擾粒子。
            qa.GoTo("BrightTarget");
            Aim(qa, w.Target.transform.position + Vector3.up * 0.9f);
            Act(qa, 20, new LanternActionInput { Primary = true });
            Act(qa, 6, LanternActionInput.None);
            Aim(qa, w.Target.transform.position + Vector3.up * 0.9f);
            Act(qa, 2, LanternActionInput.None);
            Shot(qa, rt, Path.Combine(dir, "16_target_hit.png"));
            log.AppendLine("targetHits=" + qa.Actions.TargetHits + " stunned=" + w.Target.Stunned);
        }

        private static void CombatShots(LanternPrototypeQA qa, RenderTexture rt, string dir, StringBuilder log)
        {
            // 聚光對照（走廊，同機位）。
            qa.GoTo("Spawn");
            qa.Rig.SetOn(true);
            qa.FocusHeld = false;
            Frames(qa, 20);
            Shot(qa, rt, Path.Combine(dir, "17a_light_normal.png"));
            qa.FocusHeld = true;
            Frames(qa, 25);
            Shot(qa, rt, Path.Combine(dir, "17b_light_focus.png"));
            qa.FocusHeld = false;
            Frames(qa, 25);
            // 戰鬥場：失序體在暗處是黑影，進到光裡才看清楚。
            qa.GoTo("ArenaStart");
            var e = qa.Combat.Enemies[0];
            var p = qa.Player.transform.position;
            e.transform.position = new Vector3(p.x + 2.9f, 0f, p.z + 0.4f);
            e.transform.rotation = Quaternion.LookRotation(Vector3.left);
            Physics.SyncTransforms();
            Aim(qa, e.transform.position + Vector3.up * 1.2f);
            qa.Rig.SetOn(false);
            Frames(qa, 2);
            e.transform.position = new Vector3(p.x + 2.9f, 0f, p.z + 0.4f);
            Shot(qa, rt, Path.Combine(dir, "18a_enemy_dark.png"));
            qa.Rig.SetOn(true);
            Frames(qa, 2);
            e.transform.position = new Vector3(p.x + 2.9f, 0f, p.z + 0.4f);
            Shot(qa, rt, Path.Combine(dir, "18b_enemy_in_light.png"));
            // 攻擊前兆（身體膨脹、核心發亮）。
            for (int i = 0; i < 120 && e.Current != LanternEnemy.State.WindUp; i++) qa.Step(1f / 30f, ExploreInput.None);
            Act(qa, 12, LanternActionInput.None);
            Aim(qa, e.transform.position + Vector3.up * 1.2f);
            Frames(qa, 1);
            Shot(qa, rt, Path.Combine(dir, "19_enemy_windup.png"));
            log.AppendLine("windup state=" + e.Current + " health=" + qa.Combat.Health);
            // 被蓄力射擊打中：暈眩＋干擾粒子＋撞擊處的現實泡泡。
            qa.Combat.Respawn();
            e = qa.Combat.Enemies[0];
            p = qa.Player.transform.position;
            e.transform.position = new Vector3(p.x + 4.5f, 0f, p.z);
            Physics.SyncTransforms();
            Aim(qa, e.transform.position + Vector3.up * 1.2f);
            Act(qa, 1, new LanternActionInput { SelectMode = 1 });
            Act(qa, 33, new LanternActionInput { Primary = true });
            Act(qa, 8, LanternActionInput.None);
            Aim(qa, e.transform.position + Vector3.up * 1.2f);
            Frames(qa, 1);
            Shot(qa, rt, Path.Combine(dir, "20_enemy_stunned.png"));
            log.AppendLine("enemy after shot state=" + e.Current + " hp=" + e.Hp + " hits=" + qa.Actions.EnemyHits);
            qa.GoTo("ArenaView");
            Frames(qa, 2);
            Shot(qa, rt, Path.Combine(dir, "21_arena_overview.png"));
        }

        private static void Pair(LanternPrototypeQA qa, RenderTexture rt, string dir, string mark, string tag, StringBuilder log)
        {
            qa.GoTo(mark);
            qa.Rig.SetOn(false);
            Frames(qa, 4);
            Shot(qa, rt, Path.Combine(dir, tag + "_off.png"));
            qa.Rig.SetOn(true);
            Frames(qa, 4);
            Shot(qa, rt, Path.Combine(dir, tag + "_on.png"));
            log.AppendLine(tag + " " + qa.Player.transform.position.ToString("F2"));
        }

        private static void Frames(LanternPrototypeQA qa, int n)
        {
            for (int i = 0; i < n; i++) qa.Step(1f / 30f, ExploreInput.None);
            qa.Rig.Snap();
            qa.Post.Apply();
        }

        private static void Shot(LanternPrototypeQA qa, RenderTexture rt, string path)
        {
            var cam = qa.Cam;
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }
    }
}

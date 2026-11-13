using System.IO;
using System.Text;
using Shepherd.Exploration.Lantern;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
                // 住宅（體感放大 ×1.4）：提燈關／開。
                Pair(qa, rt, dir, "HouseEntry", "h01_entry", log);
                Pair(qa, rt, dir, "HouseLiving", "h02_living", log);
                Pair(qa, rt, dir, "HouseDining", "h03_dining", log);
                Pair(qa, rt, dir, "HouseKitchen", "h04_kitchen", log);
                Pair(qa, rt, dir, "HouseUpper", "h05_upper", log);
                Pair(qa, rt, dir, "HouseEleanor", "h06_eleanor", log);
                Pair(qa, rt, dir, "HouseStudy", "h07_study", log);
                // 樓梯（從玄關往上看）。
                qa.GoTo("HouseEntry");
                var w = qa.World;
                Aim(qa, w.HouseRoot.position + new Vector3(4.25f * w.HouseScale, w.Floor2Y + 1.2f, 7.5f * w.HouseScale));
                Frames(qa, 4);
                Shot(qa, rt, Path.Combine(dir, "h08_stairs.png"));
                // 房子大小對照（同一個位置：玄關看客廳門）。
                foreach (var s in new[] { 1.25f, 1.6f })
                {
                    qa.SetHouseScale(s);
                    qa.GoTo("HouseLiving");
                    Frames(qa, 4);
                    Shot(qa, rt, Path.Combine(dir, "h09_scale_" + s.ToString("0.00") + ".png"));
                }
                qa.SetHouseScale(LanternGraybox.HouseDefaultScale);
                // 沒電／有電（提燈開著）。
                qa.Rig.SetOn(true);
                qa.GoTo("HouseLiving");
                Frames(qa, 6);
                Shot(qa, rt, Path.Combine(dir, "p01_unpowered.png"));
                qa.World.FuseBox.ForceDone();
                Frames(qa, 45);
                Shot(qa, rt, Path.Combine(dir, "p02_powered.png"));
                qa.GoTo("HouseDining");
                Frames(qa, 4);
                Shot(qa, rt, Path.Combine(dir, "p03_powered_dining.png"));
                qa.GoTo("HouseKitchen");
                Frames(qa, 4);
                Shot(qa, rt, Path.Combine(dir, "p04_powered_kitchen.png"));
                qa.GoTo("HouseEleanor");
                Frames(qa, 4);
                Shot(qa, rt, Path.Combine(dir, "p05_powered_eleanor.png"));
                log.AppendLine("powered=" + qa.World.HousePowered + " lamp0=" + qa.World.LampIntensity(0).ToString("F2") + " ambient=" + qa.AmbientLevel.ToString("F2"));
                qa.SetHouseScale(LanternGraybox.HouseDefaultScale);
                // 準心校正：對牆射一發，記錄撞擊點與準心的距離。
                qa.GoTo("Spawn");
                RaycastHit hit;
                Physics.SyncTransforms();
                if (Physics.Raycast(qa.Cam.transform.position, qa.Cam.transform.forward, out hit, 30f))
                {
                    qa.Actions.Fire(0f);
                    for (int i = 0; i < 30 && qa.Actions.Impacts == 0; i++) qa.Step(1f / 30f, ExploreInput.None);
                    log.AppendLine("crosshair error m=" + Vector3.Distance(hit.point, qa.Actions.LastImpact).ToString("F3"));
                }
                StyleShots(qa, rt, dir, log);
                V2Shots(qa, rt, dir, log);
                LivingShots(qa, rt, dir, log);
                ClueShots(qa, rt, dir, log);
                CombatShots(qa, rt, dir, log);
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

        /// <summary>風格測試：同一個畫面，A＝原樣、B＝風格化（手繪感半寫實）。</summary>
        private static void StyleShots(LanternPrototypeQA qa, RenderTexture rt, string dir, StringBuilder log)
        {
            var w = qa.World;
            var o = LanternGraybox.V2Origin;
            float S = LanternGraybox.V2Scale;
            void Pair(string tag)
            {
                qa.Style.Set(false);
                Frames(qa, 3);
                Shot(qa, rt, Path.Combine(dir, "s_" + tag + "_A.png"));
                qa.Style.Set(true);
                Frames(qa, 3);
                Shot(qa, rt, Path.Combine(dir, "s_" + tag + "_B.png"));
            }
            w.BuildHouseV2();
            qa.V2.Rebind();
            qa.Rig.SetOn(true);
            // 門外：舉起提燈、門出現。
            qa.GoTo("V2Alley");
            qa.Player.Teleport(qa.Player.transform.position + Vector3.forward * 1.6f, 0f, 0f);
            Frames(qa, 8);
            Pair("01_street");
            // 店面：從門口往裡看。
            qa.Player.Teleport(o + new Vector3(5.6f * S, 0.05f, 0.9f * S), 0f, 0f);
            Aim(qa, o + new Vector3(1.5f * S, 1.1f, 3.8f * S));
            Frames(qa, 6);
            Pair("02_shop");
            // 走廊：推門那扇門的光。
            qa.GoTo("V2Hall");
            Frames(qa, 6);
            Pair("03_hall");
            // 舊房子修好的客廳（真實的家、暖燈）。
            var photo = w.Clues.Find(c => c.Id == "photo");
            photo.ForceComplete();
            Frames(qa, 90);
            qa.Combat.ClearEnemies();
            qa.GoTo("HouseEnding");
            Frames(qa, 20);
            Pair("04_living_home");
            // 舊房子沒修好的餐廳（廢墟，提燈照著）。
            qa.GoTo("HouseDining");
            Frames(qa, 6);
            Pair("05_dining_ruin");
            qa.Style.Set(true);
            qa.SetHouseScale(LanternGraybox.HouseDefaultScale);
            log.AppendLine("style test done");
        }

        /// <summary>新版房子（v2）灰盒：門外開場、店面、走廊樓梯、餐廳、廚房、側院。</summary>
        private static void V2Shots(LanternPrototypeQA qa, RenderTexture rt, string dir, StringBuilder log)
        {
            var w = qa.World;
            w.BuildHouseV2();
            qa.V2.Rebind();
            qa.GoTo("V2Street");
            qa.Rig.SetOn(false);
            Frames(qa, 8);
            Shot(qa, rt, Path.Combine(dir, "v00_street.png"));
            qa.Player.Teleport(qa.Player.transform.position, -35f, -2f);
            Frames(qa, 4);
            Shot(qa, rt, Path.Combine(dir, "v00_street_left.png"));
            qa.Player.Teleport(qa.Player.transform.position, 150f, -2f);
            Frames(qa, 4);
            Shot(qa, rt, Path.Combine(dir, "v00_street_behind.png"));
            qa.GoTo("V2Alley");
            qa.Rig.SetOn(false);
            Frames(qa, 8);
            Shot(qa, rt, Path.Combine(dir, "v01_street_lantern_down.png"));
            qa.Player.Teleport(qa.Player.transform.position + Vector3.forward * 1.6f, 0f, 0f);
            qa.Rig.SetOn(true);
            Frames(qa, 8);
            Shot(qa, rt, Path.Combine(dir, "v02_street_lantern_up.png"));
            for (int i = 0; i < 150 && !w.V2Sealed; i++)
            {
                qa.Step(1f / 30f, new ExploreInput { Move = new Vector2(0f, 1f) });
                if (i % 15 == 0) log.AppendLine("  walk " + i + " z=" + (qa.Player.transform.position.z).ToString("F2") + " open=" + w.V2FrontDoor.Open + " real=" + LanternReveal.IsReal(w.V2FrontDoor.Probe) + " radius=" + LanternReveal.Radius.ToString("F2") + " center=" + LanternReveal.Center.ToString("F2"));
            }
            Frames(qa, 4);
            log.AppendLine("v2 after walk-in local=" + (qa.Player.transform.position - LanternGraybox.V2Origin).ToString("F2") + " sealedAt=" + (w.V2SealedAt - LanternGraybox.V2Origin).ToString("F2"));
            qa.Player.Teleport(LanternGraybox.V2Origin + new Vector3(4.6f * LanternGraybox.V2Scale, 0.05f, 2.2f * LanternGraybox.V2Scale), 180f, 0f);
            Aim(qa, w.V2SealWall.position);
            Frames(qa, 4);
            Shot(qa, rt, Path.Combine(dir, "v03_sealed_behind.png"));
            foreach (var m in new[] { "V2Shop", "V2Hall", "V2Dining", "V2Kitchen", "V2Yard" })
            {
                qa.GoTo(m);
                Frames(qa, 6);
                Shot(qa, rt, Path.Combine(dir, "v_" + m + ".png"));
            }
            qa.GoTo("V2Shop");
            qa.Rig.SetOn(false);
            Frames(qa, 6);
            Shot(qa, rt, Path.Combine(dir, "v_V2Shop_dark.png"));
            qa.Rig.SetOn(true);
            // 第一批謎題：店面全景、電箱、縫紉機、修好後牆上的影子。
            var o = LanternGraybox.V2Origin;
            float S = LanternGraybox.V2Scale;
            var shopEye = o + new Vector3(4.6f * S, 0.05f, 1.4f * S);
            qa.Player.Teleport(shopEye, 0f, 0f);
            Aim(qa, o + new Vector3(1.6f * S, 1.0f, 3.6f * S));
            Frames(qa, 6);
            Shot(qa, rt, Path.Combine(dir, "b1_shop_overview.png"));
            qa.Player.Teleport(o + new Vector3(3.2f * S, 0.05f, 2.2f * S), 0f, 0f);
            Aim(qa, w.V2CoatTag.position);
            Frames(qa, 4);
            Shot(qa, rt, Path.Combine(dir, "b1_dress_form_coat.png"));
            var lever = w.V2PowerLever.GetComponent<Collider>().bounds.center;
            qa.Player.Teleport(new Vector3(lever.x + 0.3f, o.y + 0.05f, lever.z - 0.75f), 0f, 0f);
            Aim(qa, lever);
            Frames(qa, 4);
            Shot(qa, rt, Path.Combine(dir, "b1_power_box.png"));
            var sew = w.V2SewingMachine.GetComponent<Collider>().bounds.center;
            qa.Player.Teleport(new Vector3(sew.x + 1.3f, o.y + 0.05f, sew.z - 0.4f), 0f, 0f);
            Aim(qa, sew);
            Frames(qa, 4);
            Shot(qa, rt, Path.Combine(dir, "b1_sewing_machine.png"));
            qa.V2.DebugForce(true, true, true, true);
            Frames(qa, 30);
            qa.Player.Teleport(o + new Vector3(2.2f * S, 0.05f, 2.9f * S), 0f, 0f);
            Aim(qa, w.V2ShopEchoAt);
            Frames(qa, 225);
            Shot(qa, rt, Path.Combine(dir, "b1_repaired_echo.png"));
            qa.Player.Teleport(shopEye, 0f, 0f);
            Aim(qa, o + new Vector3(1.6f * S, 1.0f, 3.6f * S));
            Frames(qa, 4);
            Shot(qa, rt, Path.Combine(dir, "b1_repaired_overview.png"));
            log.AppendLine("b1 repaired=" + qa.V2.ShopRepaired + " echoLines=" + (qa.V2.ShopEcho != null ? qa.V2.ShopEcho.LinesShown : -1));
            log.AppendLine("v2 sealed=" + w.V2Sealed + " doors=" + w.V2Doors.Count);
        }

        /// <summary>客廳樣板：從玄關看地標、月光、修好後的真實客廳與細節。</summary>
        private static void LivingShots(LanternPrototypeQA qa, RenderTexture rt, string dir, StringBuilder log)
        {
            var w = qa.World;
            qa.SetHouseScale(LanternGraybox.HouseDefaultScale);
            var photo = w.Clues.Find(c => c.Id == "photo");
            qa.Rig.SetOn(true);
            qa.GoTo("HouseEntry");
            qa.Player.Teleport(w.HouseRoot.position + LanternGraybox.HouseOrigin + new Vector3(4.25f * w.HouseScale, 0.05f, 2.8f * w.HouseScale), 0f, 0f);
            Aim(qa, photo.transform.position);
            Frames(qa, 6);
            Shot(qa, rt, Path.Combine(dir, "l01_landmark_from_hall.png"));
            qa.Rig.SetOn(false);
            qa.GoTo("HouseLiving");
            Frames(qa, 6);
            Shot(qa, rt, Path.Combine(dir, "l02_living_moonlight_dark.png"));
            qa.Rig.SetOn(true);
            Frames(qa, 4);
            Shot(qa, rt, Path.Combine(dir, "l03_living_lantern.png"));
            photo.ForceComplete();
            Frames(qa, 90);
            qa.Combat.ClearEnemies();
            qa.GoTo("HouseEnding");
            Frames(qa, 30);
            Shot(qa, rt, Path.Combine(dir, "l04_living_repaired_unpowered.png"));
            var mug = w.HouseRoot.Find("MugB");
            Aim(qa, mug.position);
            Frames(qa, 2);
            Shot(qa, rt, Path.Combine(dir, "l05_coffee_table.png"));
            var scarf = w.HouseRoot.Find("UnfinishedScarf");
            Aim(qa, scarf.position);
            Frames(qa, 2);
            Shot(qa, rt, Path.Combine(dir, "l06_scarf.png"));
            var screen = w.Toggles.Find(t => t.Label == "壁爐的防火屏");
            screen.Toggle();
            Frames(qa, 30);
            var secret = w.Inspectables.Find(i => i.Secret);
            qa.Player.Teleport(LanternGraybox.HouseOrigin + new Vector3(1.6f * w.HouseScale, 0.05f, 1.2f * w.HouseScale), 0f, 0f);
            Aim(qa, secret.transform.position);
            Frames(qa, 3);
            Shot(qa, rt, Path.Combine(dir, "l07_fire_screen_secret.png"));
            log.AppendLine("living interactables=" + w.LivingInteractables + " safeRooms=" + w.SafeRooms.Count + " windows=" + w.WindowPanes.Count);
            qa.SetHouseScale(LanternGraybox.HouseDefaultScale);
        }

        private static void ClueShots(LanternPrototypeQA qa, RenderTexture rt, string dir, StringBuilder log)
        {
            var w = qa.World;
            qa.Rig.SetOn(true);
            // 聚光照全家福。
            var photo = w.Clues.Find(c => c.Id == "photo");
            qa.GoTo("HouseLiving");
            Aim(qa, photo.transform.position);
            qa.FocusHeld = true;
            Act(qa, 25, LanternActionInput.None);
            Shot(qa, rt, Path.Combine(dir, "c01_focus_photo.png"));
            Act(qa, 40, LanternActionInput.None);
            qa.FocusHeld = false;
            Act(qa, 20, LanternActionInput.None);
            log.AppendLine("photo done=" + photo.Done + " repaired=" + qa.Flow.Repaired);
            // 復原艾琳的椅子（第二個線索 → 反撲）。
            var chair = w.Clues.Find(c => c.Id == "chair");
            qa.GoTo("HouseDining");
            Aim(qa, chair.GetComponent<Collider>().bounds.center);
            Act(qa, 1, LanternActionInput.None);
            Act(qa, 20, new LanternActionInput { Interact = true });
            Shot(qa, rt, Path.Combine(dir, "c02_restore_chair.png"));
            Act(qa, 40, new LanternActionInput { Interact = true });
            Act(qa, 45, LanternActionInput.None);
            Shot(qa, rt, Path.Combine(dir, "c03_backlash.png"));
            log.AppendLine("chair done=" + chair.Done + " backlashes=" + qa.Flow.Backlashes + " houseLight=" + w.HouseLightScale.ToString("F2") + " enemies=" + qa.Combat.Enemies.Count + " relocations=" + qa.Combat.Relocations);
            qa.Combat.ClearEnemies();
            Act(qa, 30, LanternActionInput.None);
            // 修好的房間：提燈關著也是真實的家。
            qa.GoTo("HouseDining");
            qa.Rig.SetOn(false);
            Frames(qa, 30);
            Shot(qa, rt, Path.Combine(dir, "c05_room_real_lantern_off.png"));
            qa.GoTo("HouseLiving");
            Frames(qa, 4);
            Shot(qa, rt, Path.Combine(dir, "c06_living_real_lantern_off.png"));
            qa.Rig.SetOn(true);
            log.AppendLine("rooms real=" + LanternReveal.ActiveRooms + " waveCleared=" + qa.Flow.WavesCleared);
            // 抓起藍坐墊椅子（後廚）。
            qa.GoTo("HouseScullery");
            var blue = w.Tetherables.Find(t => t.name == "HouseBlueChair");
            Aim(qa, blue.GetComponent<Collider>().bounds.center);
            Act(qa, 1, LanternActionInput.None);
            Act(qa, 1, new LanternActionInput { Interact = true });
            Act(qa, 30, LanternActionInput.None);
            Shot(qa, rt, Path.Combine(dir, "c04_carry_chair.png"));
            log.AppendLine("held=" + (qa.Actions.Held != null));
            Act(qa, 1, new LanternActionInput { Interact = true });
        }

        private static void CombatShots(LanternPrototypeQA qa, RenderTexture rt, string dir, StringBuilder log)
        {
            // 擬態錨定（戰鬥場）。
            qa.GoTo("ArenaStart");
            Act(qa, 2, LanternActionInput.None);
            qa.Player.Teleport(qa.Player.transform.position, qa.Player.Yaw, 20f);
            Act(qa, 1, new LanternActionInput { Deploy = true });
            Act(qa, 1, LanternActionInput.None);
            qa.Player.Teleport(qa.Player.transform.position, qa.Player.Yaw, 8f);
            Act(qa, 8, LanternActionInput.None);
            Shot(qa, rt, Path.Combine(dir, "k01_shield.png"));
            log.AppendLine("shields=" + qa.Actions.Shields.Count);
            // 最後的反撲：直接完成全部線索 → 崩壞 → 最終戰。
            foreach (var c in qa.World.Clues) c.ForceComplete();
            qa.GoTo("HouseEntry");
            Act(qa, 2, LanternActionInput.None);
            qa.Flow.BeginCollapse();
            Act(qa, 30, LanternActionInput.None);
            Shot(qa, rt, Path.Combine(dir, "k02_collapse.png"));
            Act(qa, 60, LanternActionInput.None);
            log.AppendLine("phase=" + qa.Flow.Current);
            var boss = qa.Flow.Boss;
            if (boss != null)
            {
                Act(qa, 10, LanternActionInput.None);
                Aim(qa, boss.transform.position + Vector3.up * 2.2f);
                Frames(qa, 1);
                Shot(qa, rt, Path.Combine(dir, "k05_boss_dark.png"));
                qa.World.Switches[3].Hit();
                qa.World.Switches[0].Hit();
                boss.transform.position = qa.World.Switches[3].Zone + Vector3.forward * 2f;
                qa.Player.Teleport(qa.World.Switches[3].Zone + new Vector3(2.5f, 0f, -3.2f), 0f, 0f);
                Act(qa, 3, LanternActionInput.None);
                Aim(qa, boss.transform.position + Vector3.up * 2.2f);
                Frames(qa, 1);
                Shot(qa, rt, Path.Combine(dir, "k06_boss_lit.png"));
                log.AppendLine("boss lit=" + boss.Lit + " switchesOn=" + qa.World.SwitchesOn);
                for (int i = 0; i < 150 && qa.Flow.Phrases.Count == 0; i++) qa.Step(1f / 30f, ExploreInput.None);
                Act(qa, 12, LanternActionInput.None);
                Aim(qa, boss.transform.position + Vector3.up * 2.5f);
                Frames(qa, 1);
                Shot(qa, rt, Path.Combine(dir, "k03_boss_phrases.png"));
                if (qa.Flow.Phrases.Count > 0)
                {
                    var p = qa.Flow.Phrases[0];
                    p.Shatter();
                    Act(qa, 10, LanternActionInput.None);
                    Aim(qa, p != null ? p.transform.position : boss.transform.position + Vector3.up * 2.5f);
                    Frames(qa, 1);
                    Shot(qa, rt, Path.Combine(dir, "k04_warm_line.png"));
                }
                log.AppendLine("boss phrases=" + qa.Flow.Phrases.Count + " warm=" + qa.Flow.WarmLinesShown);
                // 結局：擊倒 → 回到恢復真實的家。
                for (int i = 0; i < 80 && boss.Alive; i++) { boss.Lit = true; boss.Hit(1f, true); }
                for (int i = 0; i < 200 && qa.Flow.EndingTime < 7.5f; i++) qa.Step(1f / 30f, ExploreInput.None);
                Frames(qa, 1);
                Shot(qa, rt, Path.Combine(dir, "k07_ending_home.png"));
                qa.Rig.SetOn(false);
                qa.GoTo("HouseDining");
                Frames(qa, 3);
                Shot(qa, rt, Path.Combine(dir, "k08_ending_dining.png"));
                qa.GoTo("HouseUpper");
                Frames(qa, 3);
                Shot(qa, rt, Path.Combine(dir, "k09_ending_upper.png"));
                log.AppendLine("ending phase=" + qa.Flow.Current + " t=" + qa.Flow.EndingTime.ToString("F1") + " line=" + qa.Flow.EndingLine);
            }
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
            RenderTexture painted = null;
            if (qa.Style != null && qa.Style.On && qa.Style.PaintMaterial != null)
            {
                painted = RenderTexture.GetTemporary(rt.width, rt.height, 0, rt.format);
                qa.Style.Paint(rt, painted);
            }
            var active = RenderTexture.active;
            RenderTexture.active = painted != null ? painted : rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            if (painted != null) RenderTexture.ReleaseTemporary(painted);
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

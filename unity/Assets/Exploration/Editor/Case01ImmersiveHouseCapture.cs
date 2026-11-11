using System.IO;
using System.Text;
using Shepherd.Exploration.Case01;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// 沉浸式住宅探索 QA：選單入口（獨立場景 Assets/Exploration/Scenes/Case01ImmersiveHouseQA.unity，沒有就建立，直接進 Play），
    /// 以及沿著「前門 → 玄關 → 客廳（手電筒）→ 拿起合照（正面、放大、背面、異常候選）→ 樓梯 → 二樓走廊 → 回一樓」實際行走的截圖，
    /// 1920×1080 與 1280×720 各一組（batchmode：-executeMethod Shepherd.Exploration.Editor.Case01ImmersiveHouseCapture.CaptureFromCommandLine -captureDir 路徑）。
    /// 截圖只是參考；沉浸感要真人在 Play 裡走過才算。
    /// </summary>
    public static class Case01ImmersiveHouseCapture
    {
        public const string ScenePath = "Assets/Exploration/Scenes/Case01ImmersiveHouseQA.unity";

        [MenuItem("Shepherd/Case01/沉浸式住宅探索 QA")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) BuildScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Shepherd/Case01/沉浸式住宅 QA 重建場景")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Case01ImmersiveHouseQA").AddComponent<Case01ImmersiveHouseQA>();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }

        public static void BuildSceneFromCommandLine()
        {
            BuildScene();
            Debug.Log("[Case01ImmersiveHouseCapture] 場景建立：" + ScenePath);
        }

        [MenuItem("Shepherd/Case01/沉浸式住宅 QA 實機截圖")]
        public static void CaptureFromMenu()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01ImmersiveHouseCapture"));
            Capture(dir);
            EditorUtility.RevealInFinder(dir);
        }

        public static void CaptureFromCommandLine()
        {
            Capture(Case01HouseCapture.Arg("-captureDir") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01ImmersiveHouseCapture")));
        }

        private static void Step(Case01ImmersiveHouseQA s, Case01Input input, float dt = 1f / 30f)
        {
            s.House.Tick(dt, s.Filter(input));
            s.Host.Frame(dt);
            s.Frame(dt);
        }

        private static void Settle(Case01ImmersiveHouseQA s, float sec)
        {
            for (float t = 0f; t < sec; t += 1f / 30f) Step(s, Case01Input.None);
        }

        private static void Walk(Case01ImmersiveHouseQA s, float x, float z, Vector3? look = null)
        {
            var p = s.House.Player;
            for (float t = 0f; t < 25f; t += 1f / 30f)
            {
                var pos = p.transform.position;
                var to = new Vector3(x - pos.x, 0f, z - pos.z);
                if (to.magnitude < 0.2f) break;
                var lookAt = look ?? new Vector3(x, p.EyePosition.y, z);
                var d = lookAt - p.EyePosition;
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                var local = Quaternion.Inverse(Quaternion.Euler(0f, p.Yaw, 0f)) * to.normalized;
                var lk = new Vector2(Mathf.DeltaAngle(p.Yaw, yaw) / (FirstPersonController.LookSensitivity * p.LookScale) * 0.3f, p.Pitch / (FirstPersonController.LookSensitivity * p.LookScale) * 0.3f);
                Step(s, Case01Input.From(new ExploreInput { Move = new Vector2(local.x, local.z), Look = lk }));
            }
        }

        private static void Look(Case01ImmersiveHouseQA s, Vector3 target)
        {
            var p = s.House.Player;
            var d = target - p.EyePosition;
            p.Teleport(p.transform.position, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
        }

        private static void Shot(Case01ImmersiveHouseQA s, RenderTexture[] rts, string dir, string name)
        {
            s.House.Hud.Visible = false;
            if (s.House.Echo != null) s.House.Echo.Stop();
            s.House.Hud.ClearMonologue();
            var cam = s.House.ExploreCamera;
            cam.transform.position = s.House.Player.EyePosition;
            cam.transform.rotation = s.House.Player.ViewRotation;
            cam.fieldOfView = s.Host.FieldOfView;
            foreach (var rt in rts)
            {
                var path = Path.Combine(dir, name + "_" + rt.width + ".png");
                if (s.Inspecting) ShotWithStage(s, rt, path);
                else Case01HouseCapture.Shot(s.House, rt, path);
            }
        }

        /// <summary>檢視中：主鏡頭＋檢視鏡頭疊在同一張圖。</summary>
        private static void ShotWithStage(Case01ImmersiveHouseQA s, RenderTexture rt, string path)
        {
            var main = s.House.ExploreCamera;
            var stage = s.Inspector.StageCamera;
            var prevA = main.targetTexture;
            var prevB = stage.targetTexture;
            main.targetTexture = rt;
            stage.targetTexture = rt;
            main.Render();
            stage.Render();
            main.targetTexture = prevA;
            stage.targetTexture = prevB;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        public static void Capture(string dir)
        {
            Directory.CreateDirectory(dir);
            var rts = new[] { new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }, new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 } };
            var log = new StringBuilder();
            var root = new GameObject("Case01ImmersiveHouseCaptureRoot");
            try
            {
                var s = root.AddComponent<Case01ImmersiveHouseQA>();
                s.Setup(false, 1);
                s.House.ApplyAtmosphere();
                Settle(s, 0.3f);
                var d = s.Dressing;
                foreach (var pr in d.Props)
                {
                    if (pr == null) continue;
                    var rs = pr.GetComponentsInChildren<Renderer>();
                    if (rs.Length == 0) continue;
                    var bb = rs[0].bounds;
                    foreach (var r in rs) bb.Encapsulate(r.bounds);
                    if (bb.size.x > 0.8f || bb.size.y > 1.3f || bb.size.z > 0.8f) log.AppendLine("big prop " + pr.name + " " + bb.center.ToString("F2") + " size " + bb.size.ToString("F2") + " scale " + pr.localScale.ToString("F3"));
                }
                foreach (var r in s.House.Layout.Root.GetComponentsInChildren<Renderer>())
                {
                    var bb = r.bounds;
                    if (bb.center.x > 1.5f && bb.center.x < 3.5f && bb.center.z > 2f && bb.center.z < 4f && bb.size.y > 0.6f && bb.center.y < 2f) log.AppendLine("living renderer " + r.name + " " + bb.center.ToString("F2") + " size " + bb.size.ToString("F2") + " mat " + (r.sharedMaterial != null ? r.sharedMaterial.name : "-"));
                }
                foreach (var pr in d.Props)
                    if (pr != null && (pr.name == "WallSconce" || pr.name == "Flashlight" || pr.name == "ShoesPair" || pr.name == "CoatHanging"))
                    {
                        var rs = pr.GetComponentsInChildren<Renderer>();
                        var bb = rs[0].bounds;
                        foreach (var r in rs) bb.Encapsulate(r.bounds);
                        log.AppendLine("orient " + pr.name + " origin " + pr.position.ToString("F2") + " fwd " + pr.forward.ToString("F2") + " center " + bb.center.ToString("F2") + " size " + bb.size.ToString("F2"));
                    }
                log.AppendLine("props=" + d.Props.Count + " sconces=" + d.Sconces.Count + " balusters=" + d.Balusters.Count + " missing=" + d.Missing);
                // 1. 前門內看玄關。
                Shot(s, rts, dir, "ih_01_entrance");
                // 2. 走到客廳門口、看客廳。
                Walk(s, 7.6f, 3.6f);
                Look(s, new Vector3(3.0f, 1.2f, 2.9f));
                Settle(s, 0.2f);
                Shot(s, rts, dir, "ih_02_living_from_hall");
                var door = s.Host.World.Door("door_living");
                if (door != null && !door.IsOpenEnough) door.SetAngle(-door.OpenAngle, null);
                Walk(s, 5.6f, 3.55f);
                Walk(s, 4.95f, 3.55f);
                Walk(s, 4.95f, 1.4f);
                Look(s, new Vector3(0.6f, 1.2f, 2.9f));
                Settle(s, 0.3f);
                Shot(s, rts, dir, "ih_03_living_lamps");
                // 3. 開手電筒照茶几與沙發（木頭、布料、陶瓷）。
                s.ToggleFlashlight();
                Look(s, new Vector3(2.4f, 0.45f, 2.9f));
                Settle(s, 0.4f);
                Shot(s, rts, dir, "ih_04_flashlight_materials");
                s.ToggleFlashlight();
                // 4. 走到壁爐前拿起合照。
                Walk(s, 5.0f, 1.2f);
                Walk(s, 1.6f, 1.2f);
                Walk(s, 1.45f, 1.9f);
                var it = s.House.Layout.Interactables[Case01Ids.Photo];
                Look(s, it.transform.TransformPoint(it.GetComponent<BoxCollider>().center));
                Settle(s, 0.2f);
                Shot(s, rts, dir, "ih_05_mantel_photo_world");
                Step(s, Case01Input.None);
                log.AppendLine("target=" + (s.House.CurrentTarget != null ? s.House.CurrentTarget.Id : "null") + " eye=" + s.House.Player.EyePosition.ToString("F2") + " hot=" + it.transform.position.ToString("F2"));
                Step(s, Case01Input.From(new ExploreInput { Interact = true }));
                Settle(s, 0.5f);
                log.AppendLine("inspecting=" + s.Inspecting + " screenH=" + s.Inspector.ScreenHeightFraction.ToString("0.00"));
                Shot(s, rts, dir, "ih_06_photo_front");
                s.Inspector.Feed(Vector2.zero, 6f, false);
                s.Inspector.Feed(new Vector2(-20f, 10f), 0f, true);
                Settle(s, 0.3f);
                Shot(s, rts, dir, "ih_07_photo_zoom");
                s.Inspector.Feed(Vector2.zero, -20f, false);
                s.Inspector.Flip();
                Settle(s, 0.6f);
                Shot(s, rts, dir, "ih_08_photo_back");
                s.Inspector.Flip();
                s.TogglePhotoVariant();
                Settle(s, 0.6f);
                Shot(s, rts, dir, "ih_09_photo_anomaly");
                s.TogglePhotoVariant();
                Step(s, Case01Input.From(new ExploreInput { Interact = true }));
                Settle(s, 0.5f);
                log.AppendLine("after put back inspecting=" + s.Inspecting);
                // 5. 往樓梯、上二樓。
                Walk(s, 4.95f, 1.2f);
                Walk(s, 4.95f, 3.55f);
                Walk(s, 5.6f, 3.55f);
                Walk(s, 7.6f, 3.6f);
                Walk(s, 7.6f, 11.2f);
                Walk(s, 9.45f, 11.2f);
                Look(s, new Vector3(9.45f, 2.6f, 6.5f));
                Settle(s, 0.2f);
                Shot(s, rts, dir, "ih_10_stairs_foot");
                Walk(s, 9.45f, 9.0f, new Vector3(9.45f, 2.6f, 6.0f));
                Shot(s, rts, dir, "ih_11_stairs_mid");
                Walk(s, 9.45f, 6.2f);
                Walk(s, 8.4f, 6.2f);
                Walk(s, 7.6f, 7.0f);
                Look(s, new Vector3(7.6f, 4.3f, 1.0f));
                Settle(s, 0.2f);
                Shot(s, rts, dir, "ih_12_upper_hall");
                s.ToggleFlashlight();
                Look(s, new Vector3(6.5f, 4.2f, 4.0f));
                Settle(s, 0.4f);
                Shot(s, rts, dir, "ih_13_upper_hall_flashlight");
                Look(s, new Vector3(8.9f, 3.5f, 9.0f));
                Settle(s, 0.3f);
                Shot(s, rts, dir, "ih_14_landing_balusters");
                s.ToggleFlashlight();
                // 6. 回一樓。
                Walk(s, 7.6f, 7.0f);
                Walk(s, 8.4f, 6.2f);
                Walk(s, 9.45f, 6.2f);
                Walk(s, 9.45f, 10.9f);
                Walk(s, 9.45f, 11.2f);
                log.AppendLine("back downstairs y=" + s.House.Player.transform.position.y.ToString("0.00") + " room=" + s.House.CurrentRoom);
                Walk(s, 7.6f, 11.2f);
                Look(s, new Vector3(7.6f, 1.4f, 1.0f));
                Settle(s, 0.2f);
                Shot(s, rts, dir, "ih_15_back_downstairs");
                // 亮度比較（同位置）。
                for (int b = 0; b < 3; b++)
                {
                    while (s.BrightnessPreset != b) s.CycleBrightness();
                    Settle(s, 0.1f);
                    Shot(s, rts, dir, "ih_16_brightness_" + b);
                }
                s.House.RestoreAtmosphere();
            }
            finally
            {
                Object.DestroyImmediate(root);
                foreach (var rt in rts)
                {
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }
            }
            File.WriteAllText(Path.Combine(dir, "ih_capture.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log("[Case01ImmersiveHouseCapture] 輸出到 " + dir);
        }
    }
}

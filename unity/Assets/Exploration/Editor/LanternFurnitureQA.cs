using System.Collections.Generic;
using System.IO;
using System.Text;
using Shepherd.Exploration.Lantern;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// Case01 家具配置 QA（docs/level/CASE01_LIVED_IN_HOME_PROP_AND_SPATIAL_PLAN_V1.md）：獨立的截圖流程，不影響正式遊戲。
    /// - 正常態（整棟房子都是「記憶中的樣子」）＋褪色態的對照；
    /// - 俯視平面圖：拿掉天花板，動線檢查點用顏色標（綠＝合格、紅＝不足、黃＝門洞）；
    /// - 各房間的視線高度截圖；
    /// - 動線淨寬表（clearance.md）。
    /// </summary>
    public static class LanternFurnitureQA
    {
        [MenuItem("Shepherd/Prototype/Case01 家具配置 QA 截圖")]
        public static void CaptureFromMenu()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01FurnitureQA"));
            Capture(dir);
            EditorUtility.RevealInFinder(dir);
        }

        public static void CaptureFromCommandLine()
        {
            string dir = null;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-captureDir") dir = args[i + 1];
            Capture(dir ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01FurnitureQA")));
        }

        public static void Capture(string dir)
        {
            Directory.CreateDirectory(dir);
            if (!File.Exists(LanternPrototypeBuilder.ScenePath)) LanternPrototypeBuilder.BuildScene();
            EditorSceneManager.OpenScene(LanternPrototypeBuilder.ScenePath, OpenSceneMode.Single);
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var qa = Object.FindObjectOfType<LanternPrototypeQA>();
            var log = new StringBuilder();
            var markers = new List<GameObject>();
            try
            {
                qa.Setup(false);
                var w = qa.World;
                var o = LanternGraybox.V2Origin;
                float S = LanternGraybox.V2Scale;
                Vector3 HP(float x, float y, float z) => o + new Vector3(x * S, y, z * S);
                qa.Rig.HideHands = true;
                qa.GoTo("V2Shop");
                Frames(qa, 10);

                // 動線淨寬。
                Physics.SyncTransforms();
                var res = LanternClearance.Measure(w.V2Root, w.V2ClearanceRoute());
                File.WriteAllText(Path.Combine(dir, "clearance.md"), LanternClearance.Report(res), new UTF8Encoding(false));

                // 褪色態的幾張（平常玩家看到的）。
                void View(string tag, Vector3 eye, Vector3 look)
                {
                    qa.Player.Teleport(eye, 0f, 0f);
                    LanternPrototypeBuilder.Aim(qa, look);
                    Frames(qa, 6);
                    Shot(qa, rt, Path.Combine(dir, tag + ".png"));
                }
                var views = new (string tag, Vector3 eye, Vector3 look)[]
                {
                    ("01_shop_from_door", HP(5.0f, 0.05f, 0.7f), HP(2.6f, 1.0f, 4.2f)),
                    ("02_shop_to_hall_door", HP(4.2f, 0.05f, 2.2f), HP(5.6f, 1.1f, 5.0f)),
                    ("03_hall_entrance", HP(5.07f, 0.05f, 5.35f), HP(5.07f, 1.2f, 9.8f)),
                    ("04_hall_from_end", HP(5.07f, 0.05f, 9.5f), HP(5.07f, 1.0f, 5.2f)),
                    ("05_dining", HP(4.0f, 0.05f, 9.3f), HP(1.5f, 0.8f, 6.0f)),
                    ("06_dining_other_side", HP(0.9f, 0.05f, 5.6f), HP(3.4f, 0.9f, 9.2f)),
                    ("07_kitchen", HP(3.3f, 0.05f, 11.0f), HP(0.6f, 1.0f, 13.8f)),
                    ("08_kitchen_back", HP(1.0f, 0.05f, 11.2f), HP(2.4f, 1.0f, 14.9f)),
                    ("09_yard", HP(4.6f, 0.05f, 10.6f), HP(6.0f, 0.6f, 13.6f)),
                };
                qa.Rig.SetOn(false);
                foreach (var v in new[] { views[0], views[2], views[4], views[6] }) View("faded_" + v.tag, v.eye, v.look);

                // 正常態：整棟一樓都顯示「記憶中的樣子」（提燈不用開）。
                var inside = w.V2Interior;
                inside.Expand(new Vector3(0.5f, 1f, 4f));
                LanternReveal.AddRoom(inside, inside.center, true);
                Frames(qa, 4);
                foreach (var v in views) View("normal_" + v.tag, v.eye, v.look);

                // 俯視平面圖（拿掉天花板、二樓、屋頂），動線檢查點上色。
                var hidden = new List<Renderer>();
                foreach (var r in w.V2Root.GetComponentsInChildren<Renderer>())
                {
                    var n = r.name;
                    if (n.Contains("Ceiling") || n.Contains("Roof") || n.Contains("UpperFloor") || n.Contains("Landing") || n.Contains("UpperClosed") || n.Contains("Lamp") || n.Contains("Shade") || n.Contains("Bulb") || r.bounds.min.y > 2.6f)
                    { if (r.enabled) { r.enabled = false; hidden.Add(r); } }
                }
                var font = LanternFonts.UI;
                int idx = 1;
                foreach (var r in res)
                {
                    var m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Object.DestroyImmediate(m.GetComponent<Collider>());
                    m.transform.position = r.P.At + Vector3.up * 0.4f;
                    m.transform.localScale = Vector3.one * 0.32f;
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
                    var col = r.P.Doorway ? new Color(1f, 0.8f, 0.2f) : r.Ok ? new Color(0.2f, 0.85f, 0.35f) : new Color(0.95f, 0.2f, 0.15f);
                    mat.SetColor("_BaseColor", col); mat.color = col;
                    m.GetComponent<Renderer>().sharedMaterial = mat;
                    markers.Add(m);
                    var label = new GameObject("Label" + idx);
                    label.transform.position = r.P.At + Vector3.up * 0.9f + new Vector3(0f, 0f, 0.25f);
                    label.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.left);
                    var tm = label.AddComponent<TextMesh>();
                    tm.font = font; tm.fontSize = 64; tm.characterSize = 0.05f; tm.anchor = TextAnchor.MiddleCenter; tm.text = idx.ToString();
                    label.GetComponent<MeshRenderer>().sharedMaterial = LanternFonts.WorldMaterial(font, Color.black);
                    markers.Add(label);
                    idx++;
                }
                var camGo = new GameObject("PlanCamera");
                markers.Add(camGo);
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 6.2f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.92f, 0.92f, 0.9f);
                cam.transform.position = HP(3.4f, 30f, 7.6f);
                cam.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.left);
                cam.nearClipPlane = 0.1f; cam.farClipPlane = 60f;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
                Save(rt, Path.Combine(dir, "00_plan_ground_floor.png"));
                foreach (var r in hidden) if (r != null) r.enabled = true;

                int bad = 0; foreach (var r in res) if (!r.Ok) bad++;
                log.AppendLine("route points=" + res.Count + " bad=" + bad);
                log.AppendLine("ok");
            }
            finally
            {
                foreach (var m in markers) if (m != null) Object.DestroyImmediate(m);
                if (qa != null) qa.Shutdown();
                Object.DestroyImmediate(rt);
                File.WriteAllText(Path.Combine(dir, "furniture_qa.txt"), log.ToString());
            }
        }

        // ---------------- 一樓餐廳美術品質樣板 QA ----------------

        [MenuItem("Shepherd/Prototype/Case01 餐廳美術樣板 QA 截圖")]
        public static void CaptureDiningFromMenu()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01DiningQA"));
            CaptureDining(dir);
            EditorUtility.RevealInFinder(dir);
        }

        public static void CaptureDiningFromCommandLine()
        {
            string dir = null;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-captureDir") dir = args[i + 1];
            CaptureDining(dir ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01DiningQA")));
        }

        /// <summary>
        /// 同一組視角各拍兩次：改之前（DiningSampleOn = false）與改之後；每組都有正常態（記憶中的樣子）與褪色態。
        /// 特寫用自由相機位置（不是玩家視線），其他都是玩家視線高度。
        /// </summary>
        public static void CaptureDining(string dir)
        {
            Directory.CreateDirectory(dir);
            var log = new StringBuilder();
            try
            {
                foreach (var after in new[] { false, true })
                {
                    LanternGraybox.DiningSampleOn = after;
                    CaptureDiningPass(dir, after ? "after_" : "before_", log, after);
                }
            }
            finally
            {
                LanternGraybox.DiningSampleOn = true;
                File.WriteAllText(Path.Combine(dir, "dining_qa.txt"), log.ToString(), new UTF8Encoding(false));
            }
        }

        private static void CaptureDiningPass(string dir, string prefix, StringBuilder log, bool after)
        {
            if (!File.Exists(LanternPrototypeBuilder.ScenePath)) LanternPrototypeBuilder.BuildScene();
            EditorSceneManager.OpenScene(LanternPrototypeBuilder.ScenePath, OpenSceneMode.Single);
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var qa = Object.FindObjectOfType<LanternPrototypeQA>();
            try
            {
                qa.Setup(false);
                var w = qa.World;
                var o = LanternGraybox.V2Origin;
                float S = LanternGraybox.V2Scale;
                Vector3 HP(float x, float y, float z) => o + new Vector3(x * S, y, z * S);
                qa.Rig.HideHands = true;
                qa.GoTo("V2Shop");
                Frames(qa, 10);
                // 餐廳的門先打開（拉鋸門；正常遊玩要推開才進得去）。
                var st = w.V2DiningStruggle;
                if (st != null && st.Door != null && !st.Door.IsOpen) st.Door.Toggle();
                Frames(qa, 30);

                void View(string tag, Vector3 eye, Vector3 look)
                {
                    qa.Player.Teleport(eye, 0f, 0f);
                    LanternPrototypeBuilder.Aim(qa, look);
                    Frames(qa, 6);
                    Shot(qa, rt, Path.Combine(dir, prefix + tag + ".png"));
                }
                void Close(string tag, Vector3 cam, Vector3 look)
                {
                    qa.Player.Teleport(HP(2.2f, 0.05f, 7.5f) + new Vector3(0f, 0f, 0f), 0f, 0f);
                    Frames(qa, 4);
                    var c = qa.Cam.transform;
                    var p0 = c.position; var r0 = c.rotation; float fov = qa.Cam.fieldOfView;
                    c.position = cam; c.rotation = Quaternion.LookRotation(look - cam);
                    qa.Cam.fieldOfView = 45f;
                    // 特寫用自由相機：玩家身體（膠囊）先藏起來。
                    var body = new List<Renderer>();
                    foreach (var r in qa.Player.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; body.Add(r); }
                    Shot(qa, rt, Path.Combine(dir, prefix + tag + ".png"));
                    foreach (var r in body) r.enabled = true;
                    c.position = p0; c.rotation = r0; qa.Cam.fieldOfView = fov;
                }
                var views = new (string tag, Vector3 eye, Vector3 look)[]
                {
                    ("01_entrance", HP(4.25f, 0.05f, 6.45f), HP(1.0f, 1.0f, 8.3f)),
                    ("02_other_side", HP(0.9f, 0.05f, 5.6f), HP(3.4f, 0.9f, 9.2f)),
                    ("03_from_kitchen_door", HP(1.65f, 0.05f, 9.55f), HP(2.6f, 0.9f, 5.5f)),
                    ("04_corner_northeast", HP(4.0f, 0.05f, 9.3f), HP(1.5f, 0.8f, 6.0f)),
                    // 第二版：站在餐廳裡（門口進來一步）往四個方向看。
                    ("20_look_north", HP(3.5f, 0.05f, 6.5f), HP(2.9f, 1.15f, 10f)),
                    ("21_look_east", HP(2.6f, 0.05f, 8.9f), HP(4.6f, 1.15f, 8.0f)),
                    ("22_look_south", HP(3.0f, 0.05f, 8.9f), HP(2.0f, 1.0f, 5.0f)),
                    ("23_look_west", HP(3.6f, 0.05f, 7.3f), HP(0f, 1.05f, 7.6f)),
                };
                var closes = new (string tag, Vector3 cam, Vector3 look)[]
                {
                    ("10_close_table", HP(3.25f, 1.3f, 6.5f), HP(2.2f, 0.78f, 7.6f)),
                    ("11_close_sideboard", HP(1.75f, 1.35f, 6.9f), HP(0.15f, 1.15f, 7.5f)),
                    ("12_close_chair", HP(0.95f, 0.95f, 6.35f), HP(1.7f, 0.5f, 7.2f)),
                    ("13_close_door_trim", HP(3.2f, 1.4f, 8.7f), HP(4.6f, 1.0f, 7.0f)),
                    ("14_close_pendant", HP(3.0f, 1.65f, 8.5f), HP(2.2f, 2.4f, 7.5f)),
                    ("15_close_console_calendar", HP(2.5f, 1.4f, 8.5f), HP(2.75f, 1.15f, 10f)),
                    ("16_close_desk", HP(3.1f, 1.5f, 8.4f), HP(4.5f, 0.95f, 9.2f)),
                    ("17_close_reading_corner", HP(1.7f, 1.35f, 7.1f), HP(0.45f, 0.55f, 5.6f)),
                    ("18_close_bookcase", HP(2.4f, 1.3f, 6.6f), HP(2.85f, 0.7f, 5.0f)),
                };
                // 褪色態（平常玩家看到的）。
                qa.Rig.SetOn(false);
                View("faded_01_entrance", views[0].eye, views[0].look);
                View("faded_04_corner_northeast", views[3].eye, views[3].look);
                // 正常態（記憶中的樣子）。
                var inside = w.V2Interior;
                inside.Expand(new Vector3(0.5f, 1f, 4f));
                LanternReveal.AddRoom(inside, inside.center, true);
                Frames(qa, 4);
                foreach (var v in views) View("normal_" + v.tag, v.eye, v.look);
                foreach (var v in closes) Close(v.tag, v.cam, v.look);

                // 走動測試：門口 → 桌子東側 → 往廚房的門 → 碗櫃前 → 桌子南邊 → 門口（玩家用走的，不是瞬移）。
                if (after)
                {
                    var route = DiningRoute;
                    qa.Player.Teleport(HP(4.1f, 0.05f, 6.45f), 0f, 0f);
                    Frames(qa, 2);
                    log.AppendLine("## 走動測試");
                    foreach (var (name, x, z) in route)
                    {
                        bool ok = Walk(qa, HP(x, 0.05f, z), 600, out float secs);
                        log.AppendLine("- " + name + "：" + (ok ? "到了（" + secs.ToString("0.0") + " 秒）" : "卡住（" + qa.Player.transform.position + "）"));
                    }
                    Physics.SyncTransforms();
                    var res = LanternClearance.Measure(w.V2Root, w.V2ClearanceRoute());
                    log.AppendLine();
                    log.AppendLine("## 動線淨寬");
                    log.Append(LanternClearance.Report(res));
                }
                log.AppendLine(prefix + "ok");
            }
            catch (System.Exception e)
            {
                log.AppendLine(prefix + "error: " + e);
                throw;
            }
            finally
            {
                if (qa != null) qa.Shutdown();
                Object.DestroyImmediate(rt);
            }
        }

        /// <summary>餐廳走動路線（HP 座標）：門口 → 桌子東側 → 書桌與矮櫃之間 → 廚房門 → 碗櫃前 → 扶手椅旁 → 電視前 → 門口。</summary>
        public static readonly (string name, float x, float z)[] DiningRoute =
        {
            ("桌子東側", 3.6f, 7.2f), ("書桌前", 3.35f, 9.15f), ("矮櫃前", 2.3f, 9.15f), ("往廚房的門", 1.65f, 9.6f),
            ("碗櫃前", 0.95f, 7.8f), ("扶手椅旁", 1.15f, 6.5f), ("電視前", 2.0f, 5.9f), ("門口", 4.1f, 6.45f),
        };

        private static bool Walk(LanternPrototypeQA qa, Vector3 target, int maxFrames, out float secs)
        {
            var p = qa.Player;
            const float dt = 1f / 30f;
            for (int i = 0; i < maxFrames; i++)
            {
                var d = target - p.transform.position;
                d.y = 0f;
                if (d.magnitude < 0.35f) { secs = i * dt; return true; }
                float want = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                float look = Mathf.Clamp(Mathf.DeltaAngle(p.Yaw, want), -20f, 20f) / (FirstPersonController.LookSensitivity * p.LookScale);
                qa.Step(dt, new ExploreInput { Move = new Vector2(0f, 1f), Look = new Vector2(look, 0f) });
            }
            secs = maxFrames * dt;
            return false;
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
            Save(rt, path);
        }

        private static void Save(RenderTexture rt, string path)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}

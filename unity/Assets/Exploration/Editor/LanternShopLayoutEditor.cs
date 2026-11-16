using System.IO;
using System.Linq;
using System.Text;
using Shepherd.Exploration.Lantern;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// 服飾店布局 QA 的命令列工具（沒有選單入口：玩家在正式提燈原型裡按 Shift+F2 進入服飾店測試模式）。
    /// Survey：盤點現況（家具平面範圍、高度、碰撞）＋俯視圖。
    /// </summary>
    public static class LanternShopLayoutEditor
    {
        static string Arg(string name, string fallback)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return fallback;
        }

        /// <summary>世界座標 → 房子平面座標（x：左牆 0～右牆 6.5；z：前牆 0～店後牆 5）。</summary>
        public static Vector2 Plan(Vector3 w)
        {
            var d = w - LanternGraybox.V2Origin;
            return new Vector2(d.x / LanternGraybox.V2Scale, d.z / LanternGraybox.V2Scale);
        }

        public static void SurveyFromCommandLine()
        {
            string dir = Arg("-captureDir", Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/ShopSurvey")));
            Directory.CreateDirectory(dir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var log = new StringBuilder();
            var qa = new GameObject("LanternPrototypeQA").AddComponent<LanternPrototypeQA>();
            try
            {
                qa.Setup(false);
                var room = qa.World.V2ShopRoom;
                log.AppendLine("店面世界範圍 " + room.min.ToString("F2") + "～" + room.max.ToString("F2") + "，V2Scale " + LanternGraybox.V2Scale);
                log.AppendLine("| 物件 | x0 | x1 | z0 | z1 | 高 | 碰撞 |");
                log.AppendLine("|---|---|---|---|---|---|---|");
                foreach (Transform t in qa.World.V2Root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.parent == null || !t.name.StartsWith("V2") || t.parent.name.StartsWith("V2")) continue;
                    var rs = t.GetComponentsInChildren<Renderer>(true);
                    if (rs.Length == 0) continue;
                    var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    if (!room.Intersects(b) || b.size.x > 7f || b.size.z > 7f) continue;
                    var p0 = Plan(b.min); var p1 = Plan(b.max);
                    bool col = t.GetComponentsInChildren<Collider>(true).Any(c => !c.isTrigger);
                    log.AppendLine("| " + t.name + " | " + p0.x.ToString("0.00") + " | " + p1.x.ToString("0.00") + " | " + p0.y.ToString("0.00") + " | " + p1.y.ToString("0.00") + " | " + b.max.y.ToString("0.00") + " | " + (col ? "有" : "") + " |");
                }
                TopDown(qa, Path.Combine(dir, "shop_topdown_current.png"));
            }
            finally
            {
                qa.Shutdown();
                File.WriteAllText(Path.Combine(dir, "shop_survey.md"), log.ToString(), new UTF8Encoding(false));
            }
        }

        // 要追蹤的線索與家具（名稱、標籤）
        static readonly (string obj, string label)[] Targets =
        {
            ("V2PowerLever", "S1 電箱"), ("V2CoatTag", "S2 外套吊牌"), ("V2SewingMachine", "S4 縫紉機"), ("V2SecretPhoto", "衣架後的照片"),
            ("V2Counter", "櫃台"), ("V2HangingCoat", "那件外套"), ("V2Mirror", "鏡子"),
        };

        public static void CaptureFromCommandLine()
        {
            string dir = Arg("-captureDir", Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/ShopLayout")));
            Directory.CreateDirectory(dir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var log = new StringBuilder();
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            var qa = new GameObject("LanternPrototypeQA").AddComponent<LanternPrototypeQA>();
            try
            {
                qa.Setup(false);
                qa.Rig.SetOn(false);
                qa.Rig.HideHands = true;
                var labels = new StringBuilder();
                for (int layout = 0; layout < LanternGraybox.ShopLayoutNames.Length; layout++)
                {
                    qa.World.SetShopLayout(layout);
                    log.AppendLine("## " + LanternGraybox.ShopLayoutNames[layout]);
                    TopDown(qa, Path.Combine(dir, "top_" + layout + ".png"), labels, layout);
                    log.AppendLine("| 觀察點 | 視角 | " + string.Join(" | ", Targets.Select(t => t.label)) + " |");
                    log.AppendLine("|---|---|" + string.Concat(Targets.Select(_ => "---|")));
                    for (int v = 0; v < LanternPrototypeQA.ShopViews.Length; v++)
                    {
                        foreach (bool child in new[] { false, true })
                        {
                            qa.ViewOverride = child ? qa.ChildEyeHeight : LanternPrototypeQA.AdultEyeHeight;
                            qa.GoToShopView(v);
                            for (int k = 0; k < 4; k++) qa.Step(1f / 30f, ExploreInput.None);
                            qa.Rig.Snap();
                            qa.Post.Apply();
                            Shot(qa, rt, Path.Combine(dir, "v" + layout + "_" + v + (child ? "_child" : "_adult") + ".png"));
                            var cells = Targets.Select(t => Visible(qa, t.obj) ? "看得到" : "—");
                            log.AppendLine("| " + LanternPrototypeQA.ShopViews[v].name + " | " + (child ? "孩子" : "成人") + " | " + string.Join(" | ", cells) + " |");
                        }
                    }
                    qa.ViewOverride = null;
                    log.AppendLine();
                }
                qa.World.SetShopLayout(1);
                File.WriteAllText(Path.Combine(dir, "labels.txt"), labels.ToString(), new UTF8Encoding(false));
                log.AppendLine("ok");
            }
            catch (System.Exception e) { log.AppendLine("error: " + e); throw; }
            finally
            {
                qa.Shutdown();
                Object.DestroyImmediate(rt);
                File.WriteAllText(Path.Combine(dir, "shop_layout_qa.md"), log.ToString(), new UTF8Encoding(false));
            }
        }

        /// <summary>廚房↔側院通路的截圖：封住／打開 × 廚房側／側院側（封住時開著提燈，確認照不出門洞）。</summary>
        public static void CapturePassageFromCommandLine()
        {
            string dir = Arg("-captureDir", Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/KitchenYard")));
            Directory.CreateDirectory(dir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            var qa = new GameObject("LanternPrototypeQA").AddComponent<LanternPrototypeQA>();
            try
            {
                qa.Setup(false);
                qa.Rig.HideHands = true;
                qa.ViewOverride = LanternPrototypeQA.AdultEyeHeight;   // 截圖用成人高度，看得到整面牆
                var o = LanternGraybox.V2Origin; float S = LanternGraybox.V2Scale;
                foreach (bool open in new[] { false, true })
                    foreach (var (side, x, yaw) in new[] { ("kitchen", 2.2f, 90f), ("yard", 5.9f, -90f) })
                    {
                        qa.Rig.SetOn(!open);
                        qa.Player.Teleport(o + new Vector3(x * S, 0.05f, 13.8f * S), yaw, 6f);
                        for (int k = 0; k < 4; k++) qa.Step(1f / 30f, ExploreInput.None);
                        qa.World.SetKitchenYardPassage(open);   // 站進廚房會自動打開；截圖前再設定一次
                        qa.Rig.Snap();
                        qa.Post.Apply();
                        Shot(qa, rt, Path.Combine(dir, "passage_" + (open ? "open" : "sealed") + "_" + side + ".png"));
                    }
                // 顯現中（提燈關著，只看通路自己慢慢出現）：0.6、1.2、1.8 秒各一張。
                qa.Rig.SetOn(false);
                qa.Player.Teleport(o + new Vector3(2.2f * S, 0.05f, 13.8f * S), 90f, 6f);
                for (int k = 0; k < 4; k++) qa.Step(1f / 30f, ExploreInput.None);
                qa.World.SetKitchenYardPassage(false);
                qa.World.RevealKitchenYardPassage();
                for (int shot = 1; shot <= 3; shot++)
                {
                    for (int k = 0; k < 18; k++) qa.Step(1f / 30f, ExploreInput.None);
                    qa.Rig.Snap();
                    qa.Post.Apply();
                    Shot(qa, rt, Path.Combine(dir, "passage_revealing_" + shot + ".png"));
                }
            }
            finally
            {
                qa.Shutdown();
                Object.DestroyImmediate(rt);
            }
        }
        /// <summary>從目前的眼睛位置看不看得到某個物件：在畫面內，且至少一個取樣點沒有被碰撞體擋住。</summary>
        public static bool Visible(LanternPrototypeQA qa, string objName)
        {
            var t = qa.World.V2Root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == objName && x.gameObject.activeInHierarchy);
            if (t == null) return false;
            var cam = qa.Cam;
            var rs = t.GetComponentsInChildren<Renderer>();
            Bounds b;
            if (rs.Length > 0) { b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); }
            else { var c = t.GetComponent<Collider>(); if (c == null) return false; b = c.bounds; }
            var own = t.GetComponentsInChildren<Collider>(true);
            var eye = cam.transform.position;
            var samples = new[] { b.center, b.center + Vector3.up * b.extents.y * 0.6f, b.center - Vector3.up * b.extents.y * 0.6f,
                b.center + Vector3.right * b.extents.x * 0.6f, b.center - Vector3.right * b.extents.x * 0.6f, b.center + Vector3.forward * b.extents.z * 0.6f, b.center - Vector3.forward * b.extents.z * 0.6f };
            foreach (var s in samples)
            {
                var vp = cam.WorldToViewportPoint(s);
                if (vp.z <= 0.05f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) continue;
                var dir = s - eye;
                var hits = Physics.RaycastAll(eye, dir.normalized, dir.magnitude - 0.02f, ~0, QueryTriggerInteraction.Ignore);
                // 包住目標的碰撞體（例如外套包住袖口的吊牌）不算遮擋
                bool blocked = hits.Any(h => !own.Contains(h.collider) && h.collider.gameObject != qa.Player.gameObject && !h.collider.transform.IsChildOf(qa.Player.transform) && !h.collider.bounds.Contains(s));
                if (!blocked) return true;
            }
            return false;
        }

        static void Shot(LanternPrototypeQA qa, RenderTexture rt, string path)
        {
            var cam = qa.Cam;
            var prev = cam.targetTexture;
            cam.targetTexture = rt; cam.Render(); cam.Render(); cam.targetTexture = prev;
            Save(rt, path);
        }

        /// <summary>俯視圖：正交相機從店面正上方往下看（天花板暫時藏起來）。labels 收集標籤在圖上的像素位置。</summary>
        public static void TopDown(LanternPrototypeQA qa, string path, StringBuilder labels = null, int layout = 0)
        {
            var room = qa.World.V2ShopRoom;
            var hidden = qa.World.V2Root.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.enabled && r.bounds.min.y > room.min.y + 2.2f).ToList();
            foreach (var r in hidden) r.enabled = false;
            var go = new GameObject("TopDownCam");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = room.size.z * 0.5f + 0.4f;
            cam.aspect = room.size.x / room.size.z;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 20f;
            go.transform.position = new Vector3(room.center.x, room.min.y + 8f, room.center.z);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            int h = 1100, w = Mathf.RoundToInt(h * cam.aspect);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var sun = new GameObject("TopDownLight").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 0.9f; sun.transform.rotation = Quaternion.Euler(70f, 30f, 0f);
            cam.targetTexture = rt; cam.Render(); cam.Render(); cam.targetTexture = null;
            Save(rt, path);
            if (labels != null)
            {
                void L(string text, Vector3 world, string kind)
                {
                    var vp = cam.WorldToViewportPoint(world);   // 用比例換算成圖片像素（批次模式的螢幕尺寸不是圖片尺寸）
                    labels.AppendLine(layout + "|" + kind + "|" + text + "|" + Mathf.RoundToInt(vp.x * w) + "|" + Mathf.RoundToInt((1f - vp.y) * h) + "|" + w + "|" + h);
                }
                Vector3 P(float x, float z) => LanternGraybox.V2Origin + new Vector3(x * LanternGraybox.V2Scale, 0f, z * LanternGraybox.V2Scale);
                foreach (var tg in Targets)
                {
                    var t = qa.World.V2Root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == tg.obj && x.gameObject.activeInHierarchy);
                    if (t != null) L(tg.label, t.position, tg.label.StartsWith("S") || tg.label.Contains("照片") ? "puzzle" : "furniture");
                }
                foreach (var n in new[] { "ShopA_CutTable", "ShopA_Fitting", "V2FabricShelf", "V2ClothesRack", "V2ButtonCabinet" })
                {
                    var t = qa.World.V2Root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == n && x.gameObject.activeInHierarchy);
                    if (t == null) continue;
                    var rs = t.GetComponentsInChildren<Renderer>();
                    var c = t.position;
                    if (rs.Length > 0) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); c = b.center; }
                    string text = n.Contains("CutTable") ? "裁剪桌" : n.Contains("Fitting") ? "試衣間"
                        : n == "V2FabricShelf" ? "布料架" : n == "V2ClothesRack" ? "左牆衣架" : "鈕扣櫃";
                    L(text, c, "furniture");
                }
                L("店門", P(5.1f, 0.15f), "door"); L("往住宅（走廊）", P(5.07f, 4.85f), "door"); L("前窗", P(1.35f, 0.15f), "door");
                foreach (var v in LanternPrototypeQA.ShopViews) L(v.name, P(v.x, v.z), "view");
            }
            Object.DestroyImmediate(rt); Object.DestroyImmediate(go); Object.DestroyImmediate(sun.gameObject);
            foreach (var r in hidden) r.enabled = true;
        }

        public static void Save(RenderTexture rt, string path)
        {
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}

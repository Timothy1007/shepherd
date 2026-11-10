using System.IO;
using System.Text;
using Shepherd.Exploration.Case01;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// Phase 1.9 整體關卡與異域美術重構 QA：
    /// 選單開啟獨立場景 Assets/Exploration/Scenes/Case01Phase19QA.unity（沒有就建立）並進 Play；
    /// 截圖與量測（batchmode：-executeMethod Shepherd.Exploration.Editor.Case01Phase19Capture.CaptureFromCommandLine -captureDir 路徑）：
    /// 1.8 R2 ↔ 1.9 R3 同角度、門的貼合量測表、夾層核校正前後、中庭、每件圖文的近距離巡覽（0.9 m）。截圖只是參考，驗收以第一人稱實際走動為準。
    /// </summary>
    public static class Case01Phase19Capture
    {
        public const string ScenePath = "Assets/Exploration/Scenes/Case01Phase19QA.unity";

        [MenuItem("Shepherd/Case01/Phase 1.9 整體關卡與異域美術重構 QA")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) BuildScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        /// <summary>建立獨立 QA 場景：只有一個 Case01ImmersionQA（phase19 = true），其他全部在 Play 時程序生成。</summary>
        [MenuItem("Shepherd/Case01/Phase 1.9 重建 QA 場景")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Case01Phase19QA");
            var qa = go.AddComponent<Case01ImmersionQA>();
            var so = new SerializedObject(qa);
            so.FindProperty("phase19").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }

        public static void BuildSceneFromCommandLine()
        {
            BuildScene();
            Debug.Log("[Case01Phase19Capture] 場景建立：" + ScenePath);
        }

        [MenuItem("Shepherd/Case01/Phase 1.9 截圖與量測")]
        public static void CaptureFromMenu()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01Phase19Capture"));
            Capture(dir);
            EditorUtility.RevealInFinder(dir);
        }

        public static void CaptureFromCommandLine()
        {
            Capture(Case01HouseCapture.Arg("-captureDir") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01Phase19Capture")));
        }

        private static void Settle(Case01ImmersionQA s)
        {
            Case01HouseCapture.Advance(s.House, 0.1f);
            for (int i = 0; i < 6; i++) s.Frame(1f / 30f);
            if (s.House.Echo != null) s.House.Echo.Stop();
            s.House.Hud.ClearMonologue();
            s.House.Hud.Visible = false;
            s.House.ExploreCamera.fieldOfView = s.FieldOfView;
        }

        private static void Shot(Case01ImmersionQA s, RenderTexture rt, Vector3 pos, float yaw, float pitch, string path)
        {
            s.House.Player.Teleport(pos, yaw, pitch);
            Settle(s);
            Case01HouseCapture.Shot(s.House, rt, path);
        }

        private static void ShotHere(Case01ImmersionQA s, RenderTexture rt, string path)
        {
            Settle(s);
            Case01HouseCapture.Shot(s.House, rt, path);
        }

        /// <summary>門的量測：門洞寬、門板寬高、兩側縫、門軸位置、開向、碰撞（關著時與門洞兩側牆的縫、全開時是否撞到東西）。</summary>
        public static string DoorTable(Case01ImmersionWorld w)
        {
            var sb = new StringBuilder();
            sb.AppendLine("| 門 id | 位置（門洞中心） | 牆法線 | 門軸 | 門洞寬 | 門板 寬×高×厚 | 兩側縫 | 開向 | 碰撞 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (var d in w.Doors)
            {
                var leaf = d.Leaf.transform.localScale;
                float gap = (d.Width - leaf.x) * 0.5f;
                string side = d.OnlySide == 0 ? "兩側（遠離玩家）" : d.OnlySide > 0 ? "只往法線正向" : "只往法線反向";
                sb.Append("| ").Append(d.Id).Append(" | (").Append(d.Center.x.ToString("0.00")).Append(", ").Append(d.Center.y.ToString("0.00")).Append(", ").Append(d.Center.z.ToString("0.00"))
                    .Append(") | ").Append(V(d.Normal)).Append(" | ").Append(V(d.Hinge.position)).Append(" | ").Append(d.Width.ToString("0.00")).Append(" m | ")
                    .Append(leaf.x.ToString("0.00")).Append("×").Append(leaf.y.ToString("0.00")).Append("×").Append(leaf.z.ToString("0.000")).Append(" m | ")
                    .Append((gap * 100f).ToString("0")).Append(" cm | ").Append(side).Append(" | BoxCollider 門板，").Append(MaxOpen(d)).AppendLine(" |");
            }
            return sb.ToString();
        }

        /// <summary>每個允許的開向，門板不碰任何東西能開到的最大角度（門在那裡停住，不穿模）。</summary>
        public static string MaxOpen(Case01SwingDoor d)
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (var sgn in new[] { 1, -1 })
            {
                if (d.OnlySide != 0 && d.OnlySide != sgn) continue;
                float best = 0f;
                for (float a = 4f; a <= d.OpenAngle + 0.01f; a += 4f)
                {
                    // Free 的角度是「門板轉角」；換算成往法線哪一側：與 SetAngle 相同（angle * dirSign）。
                    float signedLeaf = sgn * a * Mathf.Sign(d.Leaf.transform.localPosition.x);
                    if (!d.Free(signedLeaf, null)) break;
                    best = a;
                }
                parts.Add((sgn > 0 ? "法線正向" : "法線反向") + "最大 " + best.ToString("0") + "°");
            }
            return string.Join("／", parts);
        }

        private static string V(Vector3 v)
        {
            return "(" + v.x.ToString("0.00") + ", " + v.y.ToString("0.00") + ", " + v.z.ToString("0.00") + ")";
        }

        public static string GraphicsTable(Case01Phase19Art art)
        {
            var sb = new StringBuilder();
            sb.AppendLine("| # | 圖文 | 載體與實際尺寸 | 字高（每行） | 位置 |");
            sb.AppendLine("|---|---|---|---|---|");
            for (int i = 0; i < art.Graphics.Count; i++)
            {
                var g = art.Graphics[i];
                sb.Append("| ").Append(i + 1).Append(" | ").Append(g.Name).Append(" | ").Append(g.Carrier).Append(" | ").Append(g.Glyph > 0f ? (g.Glyph * 100f).ToString("0.0") + " cm" : "（圖像）")
                    .Append(" | ").Append(V(g.T.position)).AppendLine(" |");
            }
            return sb.ToString();
        }

        public static void Capture(string dir)
        {
            Directory.CreateDirectory(dir);
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var log = new StringBuilder();
            var views = Case01ScaleQA.ViewsFor(Case01Floorplan.R2);
            try
            {
                foreach (var p19 in new[] { false, true })
                {
                    var root = new GameObject("Case01Phase19CaptureRoot");
                    Case01ImmersionQA s = null;
                    try
                    {
                        s = root.AddComponent<Case01ImmersionQA>();
                        s.Setup(false, null, 1, p19, Case01QaMode.CoreV03, true);
                        s.House.ApplyAtmosphere();
                        s.SetPanelVisible(false);
                        string tag = p19 ? "p19" : "p18";
                        for (int i = 0; i < views.Length; i++)
                            Shot(s, rt, views[i].Position, views[i].Yaw, views[i].Pitch, Path.Combine(dir, "house_v" + (i + 1).ToString("00") + "_" + tag + ".png"));
                        // 前門進來：1.8 有第二道內門，1.9 直接是玄關。
                        Shot(s, rt, s.House.Layout.Spawn.Position, 0f, 0f, Path.Combine(dir, "entry_" + tag + ".png"));
                        log.AppendLine("## " + (p19 ? "Phase 1.9 R3" : "Phase 1.8 R2") + " 門").AppendLine(DoorTable(s.World));
                        if (!p19) continue;
                        // 門：每扇門兩側各一張（關著）。
                        foreach (var d in s.World.Doors)
                        {
                            foreach (var sgn in new[] { 1f, -1f })
                            {
                                var eye = d.Center + d.Normal * (1.4f * sgn);
                                Shot(s, rt, eye, Mathf.Atan2(-d.Normal.x * sgn, -d.Normal.z * sgn) * Mathf.Rad2Deg, 4f, Path.Combine(dir, "door_" + d.Id + (sgn > 0 ? "_a" : "_b") + ".png"));
                            }
                        }
                        // 夾層：校正前、校正後；中庭。
                        s.Press(15);
                        ShotHere(s, rt, Path.Combine(dir, "mezz_s40_entry.png"));
                        Shot(s, rt, Case01HouseBuilder.MezzCenter + new Vector3(3.5f, 0.05f, -4.5f), -35f, 2f, Path.Combine(dir, "mezz_s40_diag.png"));
                        Shot(s, rt, Case01HouseBuilder.MezzCenter + new Vector3(-2.5f, 0.05f, 1.5f), 160f, -8f, Path.Combine(dir, "mezz_s40_stair.png"));
                        s.Press(18);
                        Shot(s, rt, Case01HouseBuilder.MezzCenter + new Vector3(0f, 0.05f, -4.7f), 0f, 0f, Path.Combine(dir, "mezz_stable_entry.png"));
                        Shot(s, rt, Case01HouseBuilder.MezzCenter + new Vector3(3.5f, 0.05f, -4.5f), -35f, 2f, Path.Combine(dir, "mezz_stable_diag.png"));
                        s.Press(16);
                        ShotHere(s, rt, Path.Combine(dir, "court_entry.png"));
                        Shot(s, rt, new Vector3(40f, 0.05f, 33f), 0f, -6f, Path.Combine(dir, "court_plaza.png"));
                        Shot(s, rt, new Vector3(31f, 0.05f, 50f), 120f, 0f, Path.Combine(dir, "court_west.png"));
                        Shot(s, rt, new Vector3(49f, 0.05f, 52f), -140f, 0f, Path.Combine(dir, "court_back.png"));
                        // 圖文巡覽：回到 H0 的屋子，逐件 0.9 m 近看。
                        s.House.QaView(Case01Flow.ForHouseStage(0), s.House.Layout.Spawn, 0f);
                        s.Frame(1f / 30f);
                        var art = s.World.P19;
                        if (art != null)
                        {
                            log.AppendLine("## 夾層／中庭").AppendLine("撤除的佔位：" + art.RemovedPlaceholders + "｜夾層新造型：" + art.MezzFeatures.Count + "｜中庭掩體：" + art.CourtCover.Count);
                            var court = s.House.Layout.Root.Find("Courtyard");
                            foreach (Transform t in court)
                            {
                                var rs = t.GetComponentsInChildren<Renderer>();
                                if (rs.Length == 0 || !t.gameObject.activeInHierarchy) continue;
                                var b = rs[0].bounds;
                                foreach (var r in rs) if (r.enabled) b.Encapsulate(r.bounds);
                                if (b.size.x > 3f && b.center.y > 3f || b.size.z > 3f && b.center.y > 3f) log.AppendLine("- 高處大型：" + t.name + " center " + V(b.center) + " size " + V(b.size));
                            }
                            foreach (Transform t in art.CourtRoot)
                            {
                                var rs = t.GetComponentsInChildren<Renderer>();
                                if (rs.Length == 0) continue;
                                var b = rs[0].bounds;
                                foreach (var r in rs) b.Encapsulate(r.bounds);
                                if (b.center.y > 3f && (b.size.x > 3f || b.size.z > 3f)) log.AppendLine("- CourtRoot 高處大型：" + t.name + " center " + V(b.center) + " size " + V(b.size));
                            }
                            for (int i = 0; i < art.Graphics.Count; i++)
                            {
                                s.Press(17);
                                ShotHere(s, rt, Path.Combine(dir, "gfx_" + (i + 1).ToString("00") + ".png"));
                            }
                            log.AppendLine("## 圖文").AppendLine(GraphicsTable(art));
                        }
                    }
                    finally
                    {
                        if (s != null && s.House != null) s.House.RestoreAtmosphere();
                        Object.DestroyImmediate(root);
                    }
                }
            }
            finally
            {
                rt.Release();
                Object.DestroyImmediate(rt);
            }
            File.WriteAllText(Path.Combine(dir, "phase19_capture.md"), log.ToString(), new UTF8Encoding(false));
            Debug.Log("[Case01Phase19Capture] 輸出到 " + dir);
        }
    }
}

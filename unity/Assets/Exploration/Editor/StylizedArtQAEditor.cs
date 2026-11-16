using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Shepherd.Exploration.StyleQA;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// 正式美術風格可行性 QA：把 Blender 匯出的 FBX（Assets/Exploration/StyleQA/Models）擺進獨立場景
    /// Assets/Exploration/Scenes/StylizedArtQA.unity，建立 Shepherd/PainterlyLit 材質、燈光、碰撞與觀察點。
    /// 不碰正式的服飾店／廚房／餐廳，也不改專案的全域設定（光照只存在這個場景裡）。
    /// </summary>
    public static class StylizedArtQAEditor
    {
        public const string Root = "Assets/Exploration/StyleQA";
        public const string ScenePath = "Assets/Exploration/Scenes/StylizedArtQA.unity";

        // 各材質槽的 shader 參數：(高光強度, 高光大小, 暗面偏色, 雙面)
        private static readonly Dictionary<string, (float spec, float size, Color shadow, bool twoSided)> SlotLook =
            new Dictionary<string, (float, float, Color, bool)>
            {
                { "Brass", (1.1f, 0.35f, new Color(0.82f, 0.70f, 0.82f), false) },
                { "Gold", (0.9f, 0.3f, new Color(0.82f, 0.70f, 0.82f), false) },
                { "Steel", (1.0f, 0.3f, new Color(0.74f, 0.74f, 0.92f), false) },
                { "Lacquer", (0.8f, 0.18f, new Color(0.80f, 0.74f, 0.98f), false) },
                { "ClothCream", (0f, 0.2f, new Color(0.80f, 0.74f, 0.92f), true) },
                { "ClothRose", (0f, 0.2f, new Color(0.80f, 0.70f, 0.90f), true) },
                { "ClothCharcoal", (0f, 0.2f, new Color(0.80f, 0.74f, 0.96f), true) },
            };

        /// <summary>Blender（Z 朝上、右手）→ Unity：FBX 匯出時已套用軸向，Unity 匯入再鏡射 X。</summary>
        public static Vector3 B(float x, float y, float z) => new Vector3(-x, z, -y);

        [MenuItem("Shepherd/Prototype/正式美術風格 QA（縫紉工作區）")]
        public static void Launch()
        {
            if (!File.Exists(ScenePath)) BuildScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Shepherd/Prototype/正式美術風格 QA 重建場景")]
        public static void BuildScene()
        {
            ConfigureImports();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;

            var root = new GameObject("StylizedArtQA");
            var qa = root.AddComponent<StylizedArtQA>();
            var assets = new GameObject("Assets").transform;
            assets.SetParent(root.transform, false);

            GameObject Place(string model, Vector3 posB, float rotZ = 0f)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/" + model + ".fbx");
                if (src == null) { Debug.LogError("找不到模型 " + model); return null; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(src, assets);
                go.name = model;
                go.transform.localPosition = B(posB.x, posB.y, posB.z);
                go.transform.localRotation = Quaternion.Euler(0f, -rotZ, 0f);
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = MaterialFor(model, mats[i] != null ? mats[i].name : "Wood");
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
                return go;
            }

            var stage = Place("SQA_Stage", new Vector3(0, 0, 0));
            var table = Place("SQA_SewingTable", new Vector3(0.1f, 0.95f, 0f));
            Place("SQA_SewingMachine", new Vector3(-0.15f, 0.98f, 0.76f));
            var chair = Place("SQA_Chair", new Vector3(-0.15f, 0.40f, 0f), 180f);
            Place("SQA_ClothDrape", new Vector3(0.38f, 0.775f, 0.761f));
            Place("SQA_ClothRose", new Vector3(0.47f, 0.772f, 0.7655f), -8f);
            Place("SQA_ClothChair", new Vector3(-0.11f, 0.165f, 0.91f), 180f);
            Place("SQA_FabricStack", new Vector3(0.5f, 1.1f, 0.76f), 6f);
            var spools = Place("SQA_Spools", new Vector3(0.28f, 1.13f, 0.76f));
            if (spools != null) spools.transform.localScale = Vector3.one * 1.3f;   // 線軸放大一點，第一人稱才看得清楚

            // 窗外：一片不受光的亮面，讓窗戶讀起來是「外面很亮」（不投影，不擋太陽）。
            var sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sky.name = "WindowBackdrop";
            Object.DestroyImmediate(sky.GetComponent<Collider>());
            sky.transform.SetParent(root.transform, false);
            sky.transform.position = B(0.1f, 2.4f, 1.6f);
            sky.transform.rotation = Quaternion.LookRotation(B(0f, 1f, 0f) - B(0f, 0f, 0f), Vector3.up);
            sky.transform.localScale = new Vector3(3.2f, 2.6f, 1f);
            var skyR = sky.GetComponent<MeshRenderer>();
            skyR.sharedMaterial = SkyMaterial();
            skyR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            skyR.receiveShadows = false;
            qa.WindowBackdrop = skyR;
            qa.OrbitFloor = FloorCopy(stage, root.transform);

            // 碰撞：房間用 MeshCollider，家具用外框盒，開放的兩邊加隱形牆。
            if (stage != null) foreach (var mf in stage.GetComponentsInChildren<MeshFilter>()) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            foreach (var go in new[] { table, chair })
            {
                if (go == null) continue;
                var mf = go.GetComponentInChildren<MeshFilter>();
                var bc = mf.gameObject.AddComponent<BoxCollider>();
                bc.center = mf.sharedMesh.bounds.center;
                bc.size = mf.sharedMesh.bounds.size;
            }
            foreach (var (c, s) in new[] { (B(1.9f, 0f, 1.5f), new Vector3(0.2f, 3f, 3.8f)), (B(0f, -1.9f, 1.5f), new Vector3(3.8f, 3f, 0.2f)) })
            {
                var w = new GameObject("InvisibleBound");
                w.transform.SetParent(root.transform, false);
                w.transform.position = c;
                w.AddComponent<BoxCollider>().size = s;
            }

            // 燈光：太陽（從窗戶照進來）＋桌上方的暖色檯燈（點光）。
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(root.transform, false);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 1f;
            RenderSettings.sun = sun;
            var lampGo = new GameObject("DeskLamp");
            lampGo.transform.SetParent(root.transform, false);
            lampGo.transform.position = B(-0.05f, 0.85f, 1.45f);
            var lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 2.6f;
            lamp.intensity = 1.3f;
            lamp.shadows = LightShadows.Soft;

            // 觀察點：0 正面、1 側面、2 背面、3 45 度、4 第一人稱正常距離、5 第一人稱近距離。
            var center = B(0.05f, 0.95f, 0.62f);
            var machine = B(-0.15f, 0.98f, 0.86f);
            var views = new List<Transform>();
            void View(string n, Vector3 eye, Vector3 look)
            {
                var t = new GameObject("View_" + n).transform;
                t.SetParent(root.transform, false);
                t.position = eye;
                t.rotation = Quaternion.LookRotation(look - eye, Vector3.up);
                views.Add(t);
            }
            View("Front", B(0.05f, -1.15f, 1.45f), center);
            View("Side", B(1.75f, 0.95f, 1.35f), center);
            View("Back", B(0.05f, 2.95f, 1.5f), center);
            View("Diag45", B(1.45f, -0.5f, 1.55f), center);
            View("FirstPerson", B(0.65f, -0.25f, 1.62f), machine);
            View("FirstPersonClose", B(0.22f, 0.5f, 1.62f), machine);

            var spawn = new GameObject("Spawn").transform;
            spawn.SetParent(root.transform, false);
            spawn.position = B(0.9f, -1.25f, 0.05f);
            var toTable = B(0.05f, 0.95f, 0f) - spawn.position; toTable.y = 0f;
            spawn.rotation = Quaternion.LookRotation(toTable, Vector3.up);

            qa.Assets = assets;
            qa.Sun = sun;
            qa.Lamp = lamp;
            qa.Views = views.ToArray();
            qa.Spawn = spawn;
            qa.WallRenderers = stage != null ? stage.GetComponentsInChildren<MeshRenderer>() : new MeshRenderer[0];
            qa.WireMaterial = WireMaterial();
            qa.ApplyLighting(StylizedArtQA.Lighting.Warm);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }

        private static void ConfigureImports()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root + "/Models" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var mi = (ModelImporter)AssetImporter.GetAtPath(p);
                bool dirty = !mi.isReadable || mi.importNormals != ModelImporterNormals.Import || mi.importAnimation || mi.importCameras || mi.importLights;
                mi.isReadable = true;                       // 線框疊圖要讀頂點
                mi.importNormals = ModelImporterNormals.Import;  // 保留 Blender 的平面著色（切面）
                mi.importAnimation = false; mi.importCameras = false; mi.importLights = false;
                mi.globalScale = 1f;
                if (dirty) mi.SaveAndReimport();
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                if (ti.anisoLevel != 4 || !ti.sRGBTexture)
                {
                    ti.sRGBTexture = true; ti.anisoLevel = 4; ti.mipmapEnabled = true;
                    ti.textureCompression = TextureImporterCompression.CompressedHQ;
                    ti.SaveAndReimport();
                }
            }
        }

        private static Material MaterialFor(string model, string slotName)
        {
            string slot = slotName.Replace("SQA_", "").Replace(" (Instance)", "");
            Directory.CreateDirectory(Root + "/Materials");
            string path = Root + "/Materials/" + model + "_" + slot + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Shepherd/PainterlyLit");
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + model + "_Paint.png"));
            if (SlotLook.TryGetValue(slot, out var look))
            {
                m.SetFloat("_SpecStrength", look.spec);
                m.SetFloat("_ShadowStrength", 1.8f);
                m.SetFloat("_SpecSize", look.size);
                m.SetColor("_ShadowTint", look.shadow);
                m.SetFloat("_Cull", look.twoSided ? 0f : 2f);
            }
            else
            {
                m.SetFloat("_SpecStrength", 0f);
                m.SetFloat("_ShadowStrength", 1.8f);
                m.SetColor("_ShadowTint", new Color(0.78f, 0.70f, 0.92f));
                m.SetFloat("_Cull", 2f);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>從房間模型取出「地板」那個子網格，另存成網格資產，做成環繞視角用的地板。</summary>
        private static Renderer FloorCopy(GameObject stage, Transform parent)
        {
            if (stage == null) return null;
            var r = stage.GetComponentInChildren<MeshRenderer>();
            var src = r.GetComponent<MeshFilter>().sharedMesh;
            int sub = System.Array.FindIndex(r.sharedMaterials, m => m != null && m.name.Contains("WoodFloor"));
            if (sub < 0) return null;
            var tris = src.GetTriangles(sub);
            var mesh = new Mesh { name = "SQA_StageFloorOnly", indexFormat = src.indexFormat };
            mesh.vertices = src.vertices; mesh.normals = src.normals; mesh.uv = src.uv;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            string path = Root + "/Models/SQA_StageFloorOnly.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            var go = new GameObject("OrbitFloor");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
            go.transform.localScale = r.transform.lossyScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var fr = go.AddComponent<MeshRenderer>();
            fr.sharedMaterial = r.sharedMaterials[sub];
            fr.enabled = false;
            return fr;
        }

        private static Material SkyMaterial()
        {
            string path = Root + "/Materials/StyleQA_WindowBackdrop.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Directory.CreateDirectory(Root + "/Materials");
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                m.SetColor("_BaseColor", new Color(1f, 0.88f, 0.7f));
                AssetDatabase.CreateAsset(m, path);
            }
            return m;
        }

        private static Material WireMaterial()
        {
            string path = Root + "/Materials/StyleQA_Wire.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Directory.CreateDirectory(Root + "/Materials");
                m = new Material(Shader.Find("Shepherd/StyleQAWire"));
                AssetDatabase.CreateAsset(m, path);
            }
            return m;
        }

        // ------------------------------------------------------------ 截圖驗證

        public static void CaptureFromCommandLine()
        {
            string dir = null;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-captureDir") dir = args[i + 1];
            Capture(dir ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/StylizedArtQA")));
        }

        public static void Capture(string dir)
        {
            Directory.CreateDirectory(dir);
            BuildScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var log = new StringBuilder();
            var qa = Object.FindObjectOfType<StylizedArtQA>();
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            try
            {
                qa.Setup();
                log.AppendLine("## 資產（Unity 匯入後）");
                foreach (var mf in qa.Assets.GetComponentsInChildren<MeshFilter>())
                {
                    var b = mf.GetComponent<Renderer>().bounds;
                    log.AppendLine("- " + mf.transform.root.name + "/" + mf.name + "：" + mf.sharedMesh.vertexCount + " 頂點、" + (mf.sharedMesh.triangles.Length / 3) + " 三角形、子網格 " + mf.sharedMesh.subMeshCount
                        + "，世界範圍 " + b.min.ToString("F2") + "～" + b.max.ToString("F2"));
                }
                var timing = new List<double>();
                foreach (StylizedArtQA.Lighting l in new[] { StylizedArtQA.Lighting.Warm, StylizedArtQA.Lighting.Cool })
                {
                    qa.ApplyLighting(l);
                    for (int v = 0; v < 6; v++)
                    {
                        qa.GoToView(v);
                        var sw = Stopwatch.StartNew();
                        Shot(qa, rt, Path.Combine(dir, l.ToString().ToLower() + "_" + v + "_" + qa.Views[v].name.Replace("View_", "") + ".png"));
                        timing.Add(sw.Elapsed.TotalMilliseconds);
                    }
                }
                qa.ApplyLighting(StylizedArtQA.Lighting.Warm);
                foreach (var v in new[] { 3, 5 })
                {
                    qa.GoToView(v);
                    qa.SetWireframe(true);
                    Shot(qa, rt, Path.Combine(dir, "wire_" + v + "_" + qa.Views[v].name.Replace("View_", "") + ".png"));
                    qa.SetWireframe(false);
                    foreach (StylizedArtQA.MaterialMode mm in new[] { StylizedArtQA.MaterialMode.UrpLit, StylizedArtQA.MaterialMode.Graybox })
                    {
                        qa.ApplyMaterials(mm);
                        Shot(qa, rt, Path.Combine(dir, "compare_" + mm + "_" + v + ".png"));
                    }
                    qa.ApplyMaterials(StylizedArtQA.MaterialMode.Painterly);
                }
                log.AppendLine();
                log.AppendLine("截圖 " + timing.Count + " 張（暖／冷 × 6 個觀察點），平均每張含存檔 " + (timing.Count > 0 ? (System.Linq.Enumerable.Average(timing)).ToString("0") : "-") + " ms");
                log.AppendLine("ok");
            }
            catch (System.Exception e)
            {
                log.AppendLine("error: " + e);
                throw;
            }
            finally
            {
                Object.DestroyImmediate(rt);
                File.WriteAllText(Path.Combine(dir, "stylized_art_qa.md"), log.ToString(), new UTF8Encoding(false));
            }
        }

        private static void Shot(StylizedArtQA qa, RenderTexture rt, string path)
        {
            var cam = qa.Cam;
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
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
    }
}

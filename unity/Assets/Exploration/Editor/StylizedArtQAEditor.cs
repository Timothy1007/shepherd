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
                { "Bronze", (0.7f, 0.28f, new Color(0.82f, 0.72f, 0.86f), false) },
                { "BronzeDark", (0.4f, 0.22f, new Color(0.80f, 0.72f, 0.90f), false) },
                { "Skin", (0f, 0.2f, new Color(0.98f, 0.74f, 0.78f), false) },
                { "Glove", (0.15f, 0.12f, new Color(0.80f, 0.74f, 0.96f), false) },
                { "Sleeve", (0f, 0.2f, new Color(0.84f, 0.76f, 0.90f), false) },
                { "Strap", (0.25f, 0.15f, new Color(0.82f, 0.72f, 0.86f), false) },
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

            // 第一人稱：左手（Blender 模型）握著提燈的提把；執行時整組掛到相機下，由 StylizedArtQA 控制擺動。
            var vm = new GameObject("Viewmodel").transform;
            vm.SetParent(root.transform, false);
            var armRoot = new GameObject("ArmLeft").transform;
            armRoot.SetParent(vm, false);
            var leftModel = ViewPart("SQA_ArmLeft", armRoot);
            // 手和提燈必須同一個比例：握拳的空間是照提把粗細算的，比例不同就會穿模或握不住。
            var rightRoot = new GameObject("ArmRight").transform;
            rightRoot.SetParent(vm, false);
            var rightModel = ViewPart("SQA_ArmRight", rightRoot);

            qa.RightArm = rightRoot;
            foreach (var (n, isLeft) in new[] { ("SQA_ForearmLeft", true), ("SQA_ForearmRight", false) })
            {
                var fr = new GameObject(isLeft ? "ForearmLeft" : "ForearmRight").transform;
                fr.SetParent(vm, false);
                ViewPart(n, fr);
                if (isLeft) qa.ForearmLeft = fr; else qa.ForearmRight = fr;
            }
            var upper = new GameObject("UpperArmLeft").transform;
            upper.SetParent(vm, false);
            ViewPart("SQA_UpperArmLeft", upper);
            qa.UpperArmLeft = upper;
            var pivot = new GameObject("LanternPivot").transform;
            pivot.SetParent(vm, false);
            var lantern = ViewPart("SQA_Lantern", pivot);

            var lightGo = new GameObject("LanternLight");
            lightGo.transform.SetParent(lantern != null ? lantern.transform : pivot, false);
            lightGo.transform.localPosition = new Vector3(0f, -0.258f, 0f);   // 火焰的位置
            var ll = lightGo.AddComponent<Light>();
            ll.type = LightType.Point;
            ll.color = new Color(1f, 0.8f, 0.55f);
            ll.range = 3.2f;
            ll.intensity = 1.5f;
            ll.shadows = LightShadows.Soft;
            ll.shadowNearPlane = 0.1f;
            ll.shadowStrength = 0.7f;   // 提燈投出的影子不要全黑（插畫的暗面也有顏色）
            // 只照手和提燈的補光（和正式 LanternRig 一樣的做法），讓夜裡手與燈的輪廓讀得出來。
            foreach (var t in vm.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Shepherd.Exploration.Lantern.LanternRig.ViewLayer;
            var fillGo = new GameObject("ViewmodelFill");
            fillGo.transform.SetParent(vm, false);
            fillGo.transform.localPosition = new Vector3(0.05f, 0.25f, 0.2f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 1.6f;
            fill.intensity = 0.6f;
            fill.color = new Color(1f, 0.82f, 0.62f);
            fill.cullingMask = 1 << Shepherd.Exploration.Lantern.LanternRig.ViewLayer;
            fill.shadows = LightShadows.None;
            qa.Viewmodel = vm;
            qa.Arm = armRoot;
            qa.LanternPivot = pivot;
            qa.LanternRenderer = lantern != null ? lantern.GetComponentInChildren<MeshRenderer>() : null;
            qa.LanternLight = ll;
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

        /// <summary>第一人稱的零件：不投影（提燈的光從裡面照出來，外殼投影會把光擋住）。</summary>
        private static GameObject ViewPart(string model, Transform parent)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/" + model + ".fbx");
            if (src == null) { Debug.LogError("找不到模型 " + model); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
            go.name = model;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = MaterialFor(model, mats[i] != null ? mats[i].name : "Bronze");
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            return go;
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
            if (slot == "LanternGlass" || slot == "Flame")
            {
                // 玻璃：半透明發光；火焰：不透明、更亮，畫在玻璃之前。
                var glow = Shader.Find("Shepherd/StyleQAGlow");
                if (m == null) { m = new Material(glow); AssetDatabase.CreateAsset(m, path); }
                m.shader = glow;
                m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + model + "_Paint.png"));
                bool flameSlot = slot == "Flame";
                m.SetColor("_Color", flameSlot ? new Color(1f, 0.95f, 0.82f) : new Color(1f, 0.8f, 0.52f));
                m.SetFloat("_Intensity", flameSlot ? 2.2f : 1.4f);
                m.SetFloat("_Alpha", flameSlot ? 1f : 0.7f);
                m.SetFloat("_SrcBlend", flameSlot ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", flameSlot ? (float)UnityEngine.Rendering.BlendMode.Zero : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWriteOn", flameSlot ? 1f : 0f);
                m.renderQueue = flameSlot ? 2450 : 3000;
                EditorUtility.SetDirty(m);
                return m;
            }
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
                // 第一人稱：左手提燈（暖光／夜晚、放下／舉高、近距離）
                var vmShots = new (string tag, StylizedArtQA.Lighting l, bool raised, int view)[]
                {
                    ("vm_warm_lowered", StylizedArtQA.Lighting.Warm, false, 4),
                    ("vm_night_lowered", StylizedArtQA.Lighting.Night, false, 4),
                    ("vm_night_raised", StylizedArtQA.Lighting.Night, true, 4),
                    ("vm_night_close", StylizedArtQA.Lighting.Night, true, 5),
                    ("vm_warm_raised", StylizedArtQA.Lighting.Warm, true, 5),
                };
                foreach (var s in vmShots)
                {
                    qa.ApplyLighting(s.l);
                    qa.SetLantern(true, s.raised);
                    qa.GoToView(s.view);
                    Shot(qa, rt, Path.Combine(dir, s.tag + ".png"));
                }
                // 抬頭／平視／低頭：手和提燈在畫面裡應該幾乎一樣（固定在視角上）
                qa.ApplyLighting(StylizedArtQA.Lighting.Warm);
                qa.SetLantern(true, false);
                qa.GoToView(4);
                var feet = qa.Player.transform.position;
                float yawV = qa.Player.Yaw;
                foreach (var (n, pitch) in new[] { ("up", -25f), ("level", 0f), ("down", 30f) })
                {
                    qa.Player.Teleport(feet, yawV, pitch);
                    qa.SyncCamera();
                    qa.SettleViewmodel();
                    Shot(qa, rt, Path.Combine(dir, "vm_pitch_" + n + ".png"));
                    var sp = qa.Cam.WorldToViewportPoint(qa.LanternPivot.position);
                    log.AppendLine("- 視角 " + pitch.ToString("0") + "°：握把在畫面的位置 (" + sp.x.ToString("0.000") + ", " + sp.y.ToString("0.000") + ")");
                }
                // 按 F 開燈：手從下面抬起來展示、停一下、再放回去（平視、暖光與夜晚各一次）
                foreach (var l in new[] { StylizedArtQA.Lighting.Warm, StylizedArtQA.Lighting.Night })
                {
                    qa.ApplyLighting(l);
                    qa.Player.Teleport(feet, yawV, 8f);
                    qa.SyncCamera();
                    qa.SetLantern(false, false);
                    qa.SettleViewmodel();
                    qa.ToggleLantern();
                    float tt = 0f;
                    string tagL = l.ToString().ToLower();
                    for (int k = 0; k <= 240; k++)
                    {
                        qa.TickViewmodel(1f / 60f, tt);
                        tt += 1f / 60f;
                        if (k == 12) Shot(qa, rt, Path.Combine(dir, "f_" + tagL + "_1_raising.png"));
                        if (k == 60) { Shot(qa, rt, Path.Combine(dir, "f_" + tagL + "_2_show.png")); LogElbow(log, qa, "按 F 舉起來（" + tagL + "）"); }
                        if (k == 240) { Shot(qa, rt, Path.Combine(dir, "f_" + tagL + "_3_back_down.png")); LogElbow(log, qa, "放回去／平常走路（" + tagL + "）"); }
                    }
                }
                // 側面看整隻手臂（另一台相機）：平常提著（手肘約 90°）與舉起來
                qa.ApplyLighting(StylizedArtQA.Lighting.Warm);
                foreach (var (n, raised) in new[] { ("carry", false), ("show", true) })
                {
                    qa.Player.Teleport(feet, yawV, 0f);
                    qa.SyncCamera();
                    qa.SetLantern(true, raised);
                    qa.SettleViewmodel();
                    var c = qa.Cam.transform;
                    var mid = (qa.ElbowPosition + qa.LanternPivot.position) * 0.5f;
                    var go = new GameObject("ArmSideCam");
                    var sc = go.AddComponent<Camera>();
                    sc.fieldOfView = 50f; sc.nearClipPlane = 0.02f; sc.clearFlags = CameraClearFlags.SolidColor; sc.backgroundColor = new Color(0.12f, 0.1f, 0.1f);
                    go.transform.position = mid - c.right * 1.1f + c.forward * 0.1f + Vector3.up * 0.05f;
                    go.transform.rotation = Quaternion.LookRotation(mid - go.transform.position, Vector3.up);
                    var prev = sc.targetTexture; sc.targetTexture = rt; sc.Render(); sc.Render(); sc.targetTexture = prev;
                    SaveRT(rt, Path.Combine(dir, "arm_side_" + n + ".png"));
                    Object.DestroyImmediate(go);
                }
                qa.SetLantern(true, false);
                // 晃動：0.25 秒內往右轉 30°，記錄提燈的擺動幅度與回穩時間，並拍擺到一半的畫面
                qa.Player.Teleport(feet, yawV, 0f);
                qa.SyncCamera();
                qa.SettleViewmodel();
                float maxSwing = 0f, settle = -1f, t = 0f;
                const float dt = 1f / 60f;
                for (int k = 0; k < 180; k++)
                {
                    var look = k < 15 ? new ExploreInput { Look = new Vector2(2f / (FirstPersonController.LookSensitivity * qa.Player.LookScale), 0f) } : ExploreInput.None;
                    qa.Player.Step(dt, look, true);
                    qa.SyncCamera();
                    qa.TickViewmodel(dt, t);
                    t += dt;
                    float m = qa.SwingDegrees.magnitude;
                    maxSwing = Mathf.Max(maxSwing, m);
                    if (k == 20) Shot(qa, rt, Path.Combine(dir, "vm_swing_mid.png"));
                    if (k > 15 && m > 1.5f) settle = -1f; else if (k > 15 && settle < 0f) settle = t;
                }
                log.AppendLine("- 快速右轉 30°：提燈最大擺動 " + maxSwing.ToString("0.0") + "°，擺動降到 1.5° 以下約 " + (settle < 0f ? "超過 3" : settle.ToString("0.00")) + " 秒");
                // 外觀：另一台相機從前方、側面看手和提燈（證明是立體模型，不是貼在鏡頭上的圖）
                foreach (var l in new[] { StylizedArtQA.Lighting.Warm, StylizedArtQA.Lighting.Night })
                {
                    qa.ApplyLighting(l);
                    qa.SetLantern(true, false);
                    qa.GoToView(4);
                    var c = qa.Cam.transform;
                    var target = qa.LanternPivot.position - Vector3.up * 0.16f;
                    foreach (var (n, off) in new[] { ("front", c.forward * 0.55f + c.right * 0.05f), ("side", c.right * -0.5f + c.forward * 0.2f) })
                    {
                        var go = new GameObject("ShowcaseCam");
                        var sc = go.AddComponent<Camera>();
                        sc.fieldOfView = 40f; sc.nearClipPlane = 0.02f; sc.clearFlags = CameraClearFlags.SolidColor; sc.backgroundColor = qa.Cam.backgroundColor;
                        go.transform.position = target + off + Vector3.up * 0.05f;
                        go.transform.rotation = Quaternion.LookRotation(target - go.transform.position, Vector3.up);
                        var prev = sc.targetTexture; sc.targetTexture = rt; sc.Render(); sc.Render(); sc.targetTexture = prev;
                        SaveRT(rt, Path.Combine(dir, "vm_outside_" + n + "_" + l.ToString().ToLower() + ".png"));
                        Object.DestroyImmediate(go);
                    }
                }
                qa.SetLantern(true, false);
                foreach (var mf in qa.Viewmodel.GetComponentsInChildren<MeshFilter>())
                    log.AppendLine("- 第一人稱 " + mf.name + "：" + mf.sharedMesh.vertexCount + " 頂點、" + (mf.sharedMesh.triangles.Length / 3) + " 三角形、子網格 " + mf.sharedMesh.subMeshCount);

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
            SaveRT(rt, path);
        }

        private static void LogElbow(StringBuilder log, StylizedArtQA qa, string what)
        {
            var cam = qa.Cam.transform;
            var S = cam.TransformPoint(StylizedArtQA.Shoulder);
            var W = qa.Arm.TransformPoint(StylizedArtQA.WristLeft);
            var E = qa.ElbowPosition;
            float angle = Vector3.Angle(S - E, W - E);
            var vp = qa.Cam.WorldToViewportPoint(qa.LanternPivot.position);
            log.AppendLine("- " + what + "：手肘夾角 " + angle.ToString("0") + "°，握把在畫面 (" + vp.x.ToString("0.00") + ", " + vp.y.ToString("0.00") + ")"
                + (vp.y < 0f ? "（在畫面下方外面）" : ""));
        }

        private static void SaveRT(RenderTexture rt, string path)
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

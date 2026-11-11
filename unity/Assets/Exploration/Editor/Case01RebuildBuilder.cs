using System.IO;
using System.Text;
using Shepherd.Exploration.Case01;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// 全屋重製 v2 匯入規則：Case01Rebuild 資料夾內 *_N.png＝法線貼圖、*_MS.png＝線性資料、albedo 開各向異性過濾；LivingRoom.fbx 產生光照貼圖 UV、不匯入材質與動畫。
    /// 只作用在 Assets/Exploration/Case01Rebuild/ 底下，不影響其他資產。
    /// </summary>
    public sealed class Case01RebuildImport : AssetPostprocessor
    {
        private bool Mine { get { return assetPath.Replace('\\', '/').Contains("/Exploration/Case01Rebuild/"); } }

        private void OnPreprocessTexture()
        {
            if (!Mine) return;
            var ti = (TextureImporter)assetImporter;
            ti.anisoLevel = 8;
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 2048;
            if (assetPath.EndsWith("_N.png")) ti.textureType = TextureImporterType.NormalMap;
            else if (assetPath.EndsWith("_MS.png"))
            {
                ti.sRGBTexture = false;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
            }
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
        }

        private void OnPreprocessModel()
        {
            if (!Mine) return;
            var mi = (ModelImporter)assetImporter;
            mi.generateSecondaryUV = true;
            mi.secondaryUVHardAngle = 70f;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
    }

    /// <summary>
    /// 全屋重製 QA：選單、場景建置與光照烘焙、截圖對照。
    /// 建置：新場景 → 組裝新客廳 → 材質存成資產 → 建築與家具設為靜態 → 只用環境光烘焙（含環境光遮蔽）→ 存檔；燈都是即時燈。
    /// </summary>
    public static class Case01RebuildBuilder
    {
        public const string ScenePath = "Assets/Exploration/Scenes/Case01HouseRebuildQA.unity";
        public const string MatFolder = "Assets/Exploration/Case01Rebuild/Materials";

        [MenuItem("Shepherd/Case01/全屋重製 QA")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) BuildScene(false);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Shepherd/Case01/全屋重製 QA 重建場景")]
        public static void Rebuild() { BuildScene(false); }

        /// <summary>實驗：只烘焙環境光＋環境光遮蔽（目前預設不用：CPU 烘焙在這個專案有 UV 重疊的黑斑，燈也必須保持即時才能開關）。</summary>
        [MenuItem("Shepherd/Case01/全屋重製 QA 重建場景（實驗：烘焙環境光）")]
        public static void BuildAndBake() { BuildScene(true); }

        public static void BuildFromCommandLine()
        {
            BuildScene(System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-bake") >= 0);
            Debug.Log("[Case01RebuildBuilder] 場景：" + ScenePath);
        }

        public static void BuildScene(bool bake)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var room = new Case01RebuildRoom();
            room.Build(null);
            Directory.CreateDirectory(MatFolder);
            foreach (var kv in room.Materials)
            {
                var path = MatFolder + "/" + kv.Key + ".mat";
                if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(kv.Value, path);
            }
            foreach (var r in room.Root.GetComponentsInChildren<Renderer>(true))
            {
                var n = r.name;
                if (n.StartsWith("A_") || n.StartsWith("F_") || n.StartsWith("D_"))
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, (bake ? StaticEditorFlags.ContributeGI : 0) | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }
            // 門扇與玻璃不參與光照貼圖（門以後要能開；玻璃半透明）。
            foreach (var n in new[] { "F_Door", "D_WindowGlass", "D_MirrorGlass" })
            {
                var t = room.Root.Find(n);
                if (t != null) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            }
            Case01RebuildRoom.ApplyAmbient(1f);
            if (!bake) Lightmapping.Clear();
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
            new GameObject("Case01HouseRebuildQA").AddComponent<Case01RebuildQA>();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (bake)
            {
                var ls = new LightingSettings
                {
                    name = "Case01HouseRebuildLighting",
                    bakedGI = true,
                    realtimeGI = false,
                    lightmapper = LightingSettings.Lightmapper.ProgressiveCPU,
                    lightmapResolution = 28f,
                    lightmapPadding = 3,
                    lightmapMaxSize = 1024,
                    ao = true,
                    aoMaxDistance = 0.55f,
                    aoExponentIndirect = 1.2f,
                    aoExponentDirect = 0f,
                    directSampleCount = 32,
                    indirectSampleCount = 384,
                    environmentSampleCount = 384,
                    maxBounces = 2,
                    mixedBakeMode = MixedLightingMode.IndirectOnly,
                    directionalityMode = LightmapsMode.NonDirectional,
                    filteringMode = LightingSettings.FilterMode.Auto,
                };
                var lsPath = "Assets/Exploration/Case01Rebuild/Case01HouseRebuildLighting.lighting";
                if (File.Exists(lsPath)) AssetDatabase.DeleteAsset(lsPath);
                AssetDatabase.CreateAsset(ls, lsPath);
                Lightmapping.lightingSettings = ls;
                Lightmapping.Bake();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ---------------- 截圖 ----------------

        private static void Frames(Case01RebuildQA s, int n, ExploreInput input = default(ExploreInput))
        {
            for (int i = 0; i < n; i++) s.Step(1f / 30f, input);
        }

        private static void Place(Case01RebuildQA s, string mark, Vector3 target)
        {
            Transform t;
            if (!s.Room.Marks.TryGetValue(mark, out t)) return;
            var feet = new Vector3(t.position.x, 0.05f, t.position.z);
            var eye = feet + Vector3.up * Case01RebuildQA.EyeHeights[s.EyePreset];
            var d = target - eye;
            s.Player.Teleport(feet, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
            Frames(s, 2);
        }

        private static Vector3 Mark(Case01RebuildQA s, string n, Vector3 fallback)
        {
            Transform t;
            return s.Room.Marks.TryGetValue(n, out t) ? t.position : fallback;
        }

        private static void Shot(Camera main, Camera overlay, RenderTexture rt, string path)
        {
            var pa = main.targetTexture;
            main.targetTexture = rt;
            main.Render();
            main.targetTexture = pa;
            if (overlay != null && overlay.enabled)
            {
                var pb = overlay.targetTexture;
                overlay.targetTexture = rt;
                overlay.Render();
                overlay.targetTexture = pb;
            }
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        public static void CaptureFromCommandLine()
        {
            Capture(Case01HouseCapture.Arg("-captureDir") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01HouseRebuildCapture")));
        }

        [MenuItem("Shepherd/Case01/全屋重製 QA 對照截圖")]
        public static void CaptureFromMenu()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01HouseRebuildCapture"));
            Capture(dir);
            EditorUtility.RevealInFinder(dir);
        }

        public static void Capture(string dir)
        {
            Directory.CreateDirectory(dir);
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, hideFlags = HideFlags.DontUnloadUnusedAsset };
            var log = new StringBuilder();
            try
            {
                // ---- 舊版（上一版沉浸式住宅 QA 的客廳，從客廳門口看進去）。
                var oldRoot = new GameObject("OldCapture");
                try
                {
                    var o = oldRoot.AddComponent<Case01ImmersiveHouseQA>();
                    o.Setup(false, 1);
                    o.House.ApplyAtmosphere();
                    var eye = new Vector3(6.1f, 1.63f, 3.55f);
                    var d = new Vector3(0.6f, 1.15f, 2.9f) - eye;
                    o.House.Player.Teleport(new Vector3(6.1f, 0.05f, 3.55f), Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
                    for (int i = 0; i < 6; i++)
                    {
                        o.House.Tick(1f / 30f, o.Filter(Case01Input.None));
                        o.Host.Frame(1f / 30f);
                        o.Frame(1f / 30f);
                    }
                    o.House.Hud.Visible = false;
                    var cam = o.House.ExploreCamera;
                    cam.transform.position = o.House.Player.EyePosition;
                    cam.transform.rotation = o.House.Player.ViewRotation;
                    cam.fieldOfView = 55f;
                    Case01HouseCapture.Shot(o.House, rt, Path.Combine(dir, "cmp_old_living_door.png"));
                    o.House.RestoreAtmosphere();
                }
                finally
                {
                    Object.DestroyImmediate(oldRoot);
                }

                // ---- 新版（已烘焙的場景）。
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                // 開場景會卸載先前建立的物件（含 RenderTexture）：重新建立一張。
                rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                var qaGo = Object.FindObjectOfType<Case01RebuildQA>();
                var s = qaGo != null ? qaGo : new GameObject("Case01HouseRebuildQA").AddComponent<Case01RebuildQA>();
                s.Setup(false);
                s.Room.RefreshProbe();
                Transform fire;
                s.Room.Marks.TryGetValue("E_FireLight", out fire);
                var fireAt = fire != null ? fire.position + Vector3.up * 0.8f : new Vector3(0.5f, 1.1f, 2f);
                log.AppendLine("lightmaps=" + LightmapSettings.lightmaps.Length + " archColliders=" + s.Room.ArchitectureColliders + " furniture=" + s.Room.FurnitureColliders + " mats=" + s.Room.Materials.Count);
                Place(s, "E_CamDoor", fireAt);
                Frames(s, 4);
                Shot(s.Cam, null, rt, Path.Combine(dir, "cmp_new_living_door.png"));
                // 手電筒關／開（同機位、同曝光）：看向房間最暗的角落（後牆與書架）。
                Transform win;
                s.Room.Marks.TryGetValue("E_CamWindow", out win);
                Place(s, "E_CamFire", Mark(s, "E_TgtDark", fireAt));
                s.SetLamps(false);
                Frames(s, 3);
                Shot(s.Cam, null, rt, Path.Combine(dir, "cmp_flash_off.png"));
                s.ToggleFlashlight();
                Frames(s, 12);
                Shot(s.Cam, null, rt, Path.Combine(dir, "cmp_flash_on.png"));
                s.ToggleFlashlight();
                Frames(s, 12);
                // 環境燈關／開。
                Place(s, "E_CamDoor", fireAt);
                s.SetLamps(false);
                Frames(s, 3);
                Shot(s.Cam, null, rt, Path.Combine(dir, "cmp_lamps_off.png"));
                s.SetLamps(true);
                Frames(s, 3);
                Shot(s.Cam, null, rt, Path.Combine(dir, "cmp_lamps_on.png"));
                s.TogglePendant();
                Frames(s, 3);
                Shot(s.Cam, null, rt, Path.Combine(dir, "cmp_lamps_on_pendant.png"));
                s.TogglePendant();
                // FOV／眼高。
                foreach (var (fov, eye, tag) in new[] { (0, 0, "fov55_eye158"), (2, 0, "fov70_eye158"), (0, 2, "fov55_eye172") })
                {
                    s.SetFov(fov);
                    s.SetEye(eye);
                    Place(s, "E_CamDoor", fireAt);
                    Frames(s, 2);
                    Shot(s.Cam, null, rt, Path.Combine(dir, "cmp_" + tag + ".png"));
                }
                s.SetFov(0);
                s.SetEye(0);
                // 與 Blender 參考算圖同機位（FOV 55）。
                Place(s, "E_CamDoor", Mark(s, "E_TgtDoor", fireAt));
                Shot(s.Cam, null, rt, Path.Combine(dir, "unity_ref_door.png"));
                Transform camFire;
                if (s.Room.Marks.TryGetValue("E_CamFire", out camFire))
                {
                    Place(s, "E_CamFire", Mark(s, "E_TgtFire", fireAt));
                    Shot(s.Cam, null, rt, Path.Combine(dir, "unity_ref_fire.png"));
                }
                // 其他角度。
                Place(s, "E_CamFire", s.Room.PhotoPrint != null ? s.Room.PhotoPrint.bounds.center : fireAt);
                Shot(s.Cam, null, rt, Path.Combine(dir, "new_fireplace_close.png"));
                // 合照近看。
                if (s.Photo != null)
                {
                    var p = s.Room.PhotoPrint.bounds.center;
                    var feet = p + (s.Player.transform.position - p).normalized * 1.0f;
                    var e2 = new Vector3(feet.x, 0.05f, feet.z) + Vector3.up * 1.58f;
                    var d2 = p - e2;
                    s.Player.Teleport(new Vector3(feet.x, 0.05f, feet.z), Mathf.Atan2(d2.x, d2.z) * Mathf.Rad2Deg, -Mathf.Atan2(d2.y, new Vector2(d2.x, d2.z).magnitude) * Mathf.Rad2Deg);
                    Frames(s, 2);
                    log.AppendLine("aimPhoto=" + s.AimAtPhoto());
                    Frames(s, 1, new ExploreInput { Interact = true });
                    Frames(s, 12);
                    log.AppendLine("inspecting=" + s.Inspecting);
                    Shot(s.Cam, s.Inspector.StageCamera, rt, Path.Combine(dir, "new_photo_inspect.png"));
                    s.Inspector.Flip();
                    Frames(s, 20);
                    Shot(s.Cam, s.Inspector.StageCamera, rt, Path.Combine(dir, "new_photo_back.png"));
                    s.Inspector.End();
                    Frames(s, 20);
                }
            }
            finally
            {
                if (rt != null)
                {
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }
            }
            File.WriteAllText(Path.Combine(dir, "rebuild_capture.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log("[Case01RebuildBuilder] 截圖：" + dir);
        }
    }
}

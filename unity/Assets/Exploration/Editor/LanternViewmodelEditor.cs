using System.IO;
using System.Text;
using Shepherd.Exploration.Lantern;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// 把美術風格 QA 定稿的左手提燈（ffd5f7c）做成正式遊戲用的 prefab：
    /// Assets/Exploration/Lantern/Resources/Models/Custom/LanternViewmodel.prefab（LanternRig 在執行時載入）。
    /// 模型、材質都引用 Assets/Exploration/StyleQA 裡同一份檔案，之後美術再優化時改一處就好。
    /// </summary>
    public static class LanternViewmodelEditor
    {
        public const string PrefabAsset = "Assets/Exploration/Lantern/Resources/Models/Custom/LanternViewmodel.prefab";

        [MenuItem("Shepherd/Prototype/重建正式提燈第一人稱模型（LanternViewmodel）")]
        public static void BuildPrefab()
        {
            var root = new GameObject("LanternViewmodel");
            Part(root.transform, "ArmLeft", "SQA_ArmLeft");
            Part(root.transform, "ForearmLeft", "SQA_ForearmLeft");
            Part(root.transform, "UpperArmLeft", "SQA_UpperArmLeft");
            var pivot = new GameObject("LanternPivot").transform;
            pivot.SetParent(root.transform, false);
            var lantern = Part(pivot, null, "SQA_Lantern");
            var glass = new GameObject("Glass").transform;
            glass.SetParent(lantern != null ? lantern : pivot, false);
            glass.localPosition = new Vector3(0f, -0.263f, 0f);   // 玻璃中心（提把頂端往下）
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabAsset));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabAsset);
            Object.DestroyImmediate(root);
            AssetDatabase.Refresh();
        }

        private static Transform Part(Transform parent, string holder, string model)
        {
            var at = parent;
            if (holder != null)
            {
                at = new GameObject(holder).transform;
                at.SetParent(parent, false);
            }
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(StylizedArtQAEditor.Root + "/Models/" + model + ".fbx");
            if (src == null) { Debug.LogError("找不到模型 " + model); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, at);
            go.name = model;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string slot = (mats[i] != null ? mats[i].name : "Bronze").Replace("SQA_", "").Replace(" (Instance)", "");
                    var m = AssetDatabase.LoadAssetAtPath<Material>(StylizedArtQAEditor.Root + "/Materials/" + model + "_" + slot + ".mat");
                    if (m != null) mats[i] = m; else Debug.LogError("找不到材質 " + model + "_" + slot);
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            return go.transform;
        }

        // ------------------------------------------------------------ 正式遊戲裡的截圖驗證

        public static void CaptureFromCommandLine()
        {
            string dir = null;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-captureDir") dir = args[i + 1];
            Capture(dir ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/LanternViewmodel")));
        }

        public static void Capture(string dir)
        {
            Directory.CreateDirectory(dir);
            BuildPrefab();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var log = new StringBuilder();
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            var qa = new GameObject("LanternPrototypeQA").AddComponent<LanternPrototypeQA>();
            try
            {
                qa.Setup(false);
                log.AppendLine("新版模型：" + (qa.Rig.Model != null ? "已載入" : "沒有載入（用灰盒）"));
                qa.GoTo("V2Hall");
                Frames(qa, 40);
                Shot(qa, rt, Path.Combine(dir, "formal_1_walking.png"));
                Log(log, qa, "平常走路");
                qa.Rig.SetOn(false);
                Frames(qa, 30);
                qa.Rig.Toggle();   // 開燈：手抬起來展示
                Frames(qa, 14);
                Shot(qa, rt, Path.Combine(dir, "formal_2_toggle_show.png"));
                Log(log, qa, "開燈展示");
                Frames(qa, 60);
                Shot(qa, rt, Path.Combine(dir, "formal_3_back_down.png"));
                Log(log, qa, "展示結束放回去");
                qa.FocusHeld = true;
                Frames(qa, 30);
                Shot(qa, rt, Path.Combine(dir, "formal_4_focus.png"));
                Log(log, qa, "聚光");
                qa.FocusHeld = false;
                log.AppendLine("ok");
            }
            catch (System.Exception e)
            {
                log.AppendLine("error: " + e);
                throw;
            }
            finally
            {
                qa.Shutdown();
                Object.DestroyImmediate(rt);
                File.WriteAllText(Path.Combine(dir, "lantern_viewmodel.md"), log.ToString(), new UTF8Encoding(false));
            }
        }

        private static void Log(StringBuilder log, LanternPrototypeQA qa, string what)
        {
            var vc = qa.Rig.ViewCamera;
            var g = vc.WorldToViewportPoint(qa.Rig.Glass.position);
            log.AppendLine("- " + what + "：玻璃在畫面 (" + g.x.ToString("0.00") + ", " + g.y.ToString("0.00") + ")，LanternShown " + qa.Rig.LanternShown.ToString("0.00") + "，聚光 " + qa.Rig.FocusBlend.ToString("0.00"));
        }

        private static void Frames(LanternPrototypeQA qa, int n)
        {
            for (int i = 0; i < n; i++) qa.Step(1f / 30f, ExploreInput.None);
            qa.Post.Apply();
        }

        private static void Shot(LanternPrototypeQA qa, RenderTexture rt, string path)
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

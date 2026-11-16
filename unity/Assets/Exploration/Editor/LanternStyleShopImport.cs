using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// 改衣店正式美術（tools/blender/case01_shop_stylized/build_shop.py 匯出，2026-11-17）匯入 Unity：
    ///   每個 FBX（Assets/Exploration/StyleShop/Models）→ 每個材質槽一個 PainterlyLit 材質（手繪貼圖＝{物件名}_Paint.png）
    ///   → 一個 prefab（Lantern/Resources/Models/Custom/{名稱}.prefab），遊戲載入時再轉成提燈版的卡通 shader。
    /// 每一件東西都是自己的 prefab（不和其他東西綁在一起）。只有命令列入口。
    /// </summary>
    public static class LanternStyleShopImport
    {
        private const string Root = "Assets/Exploration/StyleShop";
        private const string PrefabDir = "Assets/Exploration/Lantern/Resources/Models/Custom";
        private const string SurfaceDir = "Assets/Exploration/Lantern/Resources/StyleShop";

        // 材質槽的外觀：(高光強度, 高光大小, 暗面偏色, 雙面)
        private static readonly Dictionary<string, (float spec, float size, Color shadow, bool twoSided)> SlotLook =
            new Dictionary<string, (float, float, Color, bool)>
            {
                { "Brass", (1.1f, 0.35f, new Color(0.82f, 0.70f, 0.82f), false) },
                { "Gold", (0.9f, 0.3f, new Color(0.82f, 0.70f, 0.82f), false) },
                { "Steel", (1.0f, 0.3f, new Color(0.74f, 0.74f, 0.92f), false) },
                { "Iron", (0.6f, 0.25f, new Color(0.74f, 0.74f, 0.92f), false) },
                { "Lacquer", (0.8f, 0.18f, new Color(0.80f, 0.74f, 0.98f), false) },
                { "Mirror", (1.2f, 0.5f, new Color(0.78f, 0.80f, 0.92f), false) },
                { "Glass", (0.9f, 0.4f, new Color(0.80f, 0.82f, 0.92f), false) },
                { "Curtain", (0f, 0.2f, new Color(0.80f, 0.70f, 0.90f), true) },
            };

        public static void BuildFromCommandLine()
        {
            ConfigureImports();
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(PrefabDir);
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root + "/Models" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (src == null) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                inst.name = src.name;
                foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                        mats[i] = MaterialFor(r.gameObject.name, mats[i] != null ? mats[i].name : "Wood");
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
                PrefabUtility.SaveAsPrefabAsset(inst, PrefabDir + "/" + src.name + ".prefab");
                Object.DestroyImmediate(inst);
                n++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("StyleShop prefabs: " + n);
        }

        private static void ConfigureImports()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root + "/Models" }))
            {
                var mi = (ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                mi.importNormals = ModelImporterNormals.Import;   // 保留平面著色（切面）
                mi.importAnimation = false; mi.importCameras = false; mi.importLights = false;
                mi.globalScale = 1f;
                mi.SaveAndReimport();
            }
            foreach (var dir in new[] { Root + "/Textures", SurfaceDir })
            {
                if (!AssetDatabase.IsValidFolder(dir)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
                {
                    var ti = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                    ti.sRGBTexture = true;
                    ti.mipmapEnabled = true;
                    ti.wrapMode = dir == SurfaceDir ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                    ti.filterMode = FilterMode.Bilinear;
                    ti.maxTextureSize = 2048;
                    ti.SaveAndReimport();
                }
            }
            AssetDatabase.Refresh();
        }

        private static Material MaterialFor(string objectName, string slotName)
        {
            string slot = slotName.Replace("SQA_", "").Replace(" (Instance)", "");
            int dot = slot.LastIndexOf('.');
            if (dot > 0 && int.TryParse(slot.Substring(dot + 1), out _)) slot = slot.Substring(0, dot);
            string path = Root + "/Materials/" + objectName + "_" + slot + ".mat";
            var shader = Shader.Find("Shepherd/PainterlyLit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + objectName + "_Paint.png"));
            bool cloth = slot.StartsWith("Cloth") || slot.StartsWith("Thread");
            if (SlotLook.TryGetValue(slot, out var look))
            {
                m.SetFloat("_SpecStrength", look.spec);
                m.SetFloat("_SpecSize", look.size);
                m.SetColor("_ShadowTint", look.shadow);
                m.SetFloat("_Cull", look.twoSided ? 0f : 2f);
            }
            else
            {
                m.SetFloat("_SpecStrength", 0f);
                m.SetColor("_ShadowTint", new Color(0.78f, 0.70f, 0.92f));
                m.SetFloat("_Cull", cloth ? 0f : 2f);
            }
            m.SetFloat("_ShadowStrength", 1.8f);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}

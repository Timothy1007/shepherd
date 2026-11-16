using System.IO;
using UnityEditor;
using UnityEngine;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// 把美術風格 QA 的縫紉機桌（Blender：SQA_SewingTable＋SQA_SewingMachine）做成正式遊戲可以載入的 prefab
    /// （Resources/Models/Custom/v2_sewing_workspace_sqa）。擺法和美術風格 QA 場景相同：縫紉機在桌子的一側，另一側空著放提燈。
    /// 材質沿用風格 QA 的手繪貼圖；進遊戲時由 LanternGraybox 轉成「提燈照到是記憶中的顏色、其他地方褪色」的材質。
    /// 只有命令列入口（沒有選單）。
    /// </summary>
    public static class LanternSewingArtEditor
    {
        public const string PrefabPath = "Assets/Exploration/Lantern/Resources/Models/Custom/v2_sewing_workspace_sqa.prefab";
        private const string Root = "Assets/Exploration/StyleQA";

        public static void BuildFromCommandLine()
        {
            var root = new GameObject("v2_sewing_workspace_sqa");
            try
            {
                Add(root.transform, "SQA_SewingTable", Vector3.zero);
                // 美術風格 QA：桌子在 Blender (0.1, 0.95, 0)、縫紉機在 (-0.15, 0.98, 0.76) → 縫紉機在桌面上、往一側偏 0.25 m。
                Add(root.transform, "SQA_SewingMachine", new Vector3(0.25f, 0.76f, -0.03f));
                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("saved " + PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void Add(Transform parent, string model, Vector3 pos)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/" + model + ".fbx");
            if (src == null) throw new FileNotFoundException(model);
            var go = (GameObject)Object.Instantiate(src, parent, false);
            go.name = model;
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.identity;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string slot = mats[i] != null ? mats[i].name.Replace("SQA_", "").Replace(" (Instance)", "") : "Wood";
                    var m = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/" + model + "_" + slot + ".mat");
                    if (m != null) mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
        }
    }
}

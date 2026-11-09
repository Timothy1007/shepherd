using System.IO;
using Shepherd.Exploration.Case01;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// v0.5 Demo 切片場景：Case01DemoSliceQA（事務所開始的完整閉環）、Case01OfficeQA、Case01StreetQA（各自從該區域開始）。
    /// 每個場景只有一個 Case01DemoSlice 物件，內容於 Play 時以程式建立。不修改 Build Settings 或既有場景。
    /// </summary>
    public static class Case01DemoSceneBuilder
    {
        public const string DemoScenePath = "Assets/Exploration/Scenes/Case01DemoSliceQA.unity";
        public const string OfficeScenePath = "Assets/Exploration/Scenes/Case01OfficeQA.unity";
        public const string StreetScenePath = "Assets/Exploration/Scenes/Case01StreetQA.unity";

        [MenuItem("Shepherd/Case01/Demo 切片 QA（事務所→街區→十七號，原版格局）")]
        public static void LaunchDemo() { Launch(DemoScenePath, "office;Original"); }

        [MenuItem("Shepherd/Case01/Demo 切片 QA（事務所→街區→十七號，住宅 v0.5 A）")]
        public static void LaunchDemoV05A() { Launch(DemoScenePath, "office;V05A"); }

        /// <summary>Demo Phase 1 品質回歸 QA：從街上看 17 號開始；F1 事務所、F2 街區看 17 號、F3 進 17 號、F4 下一個視點、F6 屋內 H0→H3。</summary>
        [MenuItem("Shepherd/Case01/Demo Phase 1 品質回歸 QA")]
        public static void LaunchPhase1Qa() { Launch(DemoScenePath, "phase1;V05A"); }

        [MenuItem("Shepherd/Case01/事務所 QA")]
        public static void LaunchOffice() { Launch(OfficeScenePath, "office;Original"); }

        [MenuItem("Shepherd/Case01/街區 QA（十七號門外）")]
        public static void LaunchStreet() { Launch(StreetScenePath, "street;Original"); }

        public static void Launch(string scene, string args)
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(scene)) BuildAll();
            EditorSceneManager.OpenScene(scene);
            PlayerPrefs.SetString(Case01DemoSlice.LaunchKey, args);
            PlayerPrefs.Save();
            EditorApplication.isPlaying = true;
        }

        /// <summary>batchmode：-executeMethod Shepherd.Exploration.Editor.Case01DemoSceneBuilder.BuildAll</summary>
        public static void BuildAll()
        {
            Build(DemoScenePath, "Case01DemoSliceQA", Case01DemoArea.Office);
            Build(OfficeScenePath, "Case01OfficeQA", Case01DemoArea.Office);
            Build(StreetScenePath, "Case01StreetQA", Case01DemoArea.Street);
            AssetDatabase.SaveAssets();
        }

        private static void Build(string path, string name, Case01DemoArea area)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject(name);
            go.SetActive(false);
            var demo = go.AddComponent<Case01DemoSlice>();
            var so = new SerializedObject(demo);
            so.FindProperty("startArea").enumValueIndex = (int)area;
            so.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(true);
            if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("無法儲存場景 " + path);
            Debug.Log("[Case01Demo] 已產生場景 " + path);
        }
    }
}

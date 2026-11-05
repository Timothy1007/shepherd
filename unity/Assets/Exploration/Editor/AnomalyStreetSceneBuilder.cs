using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// 產生異域探索 Demo 場景：只有一個掛著 AnomalyStreetDemo 的物件，街區、玩家、攝影機、燈光與 HUD 於 Play 時以程式建立。
    /// 不修改 CombatLab3D、平面 CombatLab、SampleScene 或 Build Settings。
    /// </summary>
    public static class AnomalyStreetSceneBuilder
    {
        public const string ScenePath = "Assets/Exploration/Scenes/AnomalyStreetDemo.unity";

        [MenuItem("Shepherd/Exploration/開啟 AnomalyStreetDemo 場景")]
        public static void Open()
        {
            if (!File.Exists(ScenePath)) Build();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Shepherd/Exploration/重新產生 AnomalyStreetDemo 場景")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
            EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>batchmode 入口：Unity.exe -batchmode -executeMethod Shepherd.Exploration.Editor.AnomalyStreetSceneBuilder.Build -quit</summary>
        public static void Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("AnomalyStreetDemo").AddComponent<AnomalyStreetDemo>();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("無法儲存場景 " + ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[AnomalyStreetDemo] 已產生場景 " + ScenePath);
        }
    }
}

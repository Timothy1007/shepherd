using System.IO;
using Shepherd.Battle.UI.Arena;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shepherd.Battle.Editor
{
    /// <summary>
    /// 產生 3D Combat Lab 場景：只有一個掛著 CombatArenaView 的物件，3D 場景、攝影機、燈光與 HUD 於 Play 時以程式建立。
    /// 不修改原本的 CombatLab 場景、SampleScene 或 Build Settings。
    /// </summary>
    public static class CombatArenaSceneBuilder
    {
        public const string ScenePath = "Assets/Battle/Scenes/CombatLab3D.unity";

        [MenuItem("Shepherd/Combat Lab 3D/開啟 CombatLab3D 場景")]
        public static void Open()
        {
            if (!File.Exists(ScenePath)) Build();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Shepherd/Combat Lab 3D/重新產生 CombatLab3D 場景")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
            EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>batchmode 入口：Unity.exe -batchmode -executeMethod Shepherd.Battle.Editor.CombatArenaSceneBuilder.Build -quit</summary>
        public static void Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("CombatLab3D").AddComponent<CombatArenaView>();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("無法儲存場景 " + ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[CombatLab3D] 已產生場景 " + ScenePath);
        }
    }
}

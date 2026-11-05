using System.IO;
using Shepherd.Battle.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shepherd.Battle.Editor
{
    /// <summary>
    /// 產生 Combat Lab 場景：一台攝影機與一個掛著 CombatLabView 的物件，介面於 Play 時以程式建立。
    /// 不修改 SampleScene，也不修改 Build Settings。
    /// </summary>
    public static class CombatLabSceneBuilder
    {
        public const string ScenePath = "Assets/Battle/Scenes/CombatLab.unity";

        [MenuItem("Shepherd/Combat Lab/重新產生 CombatLab 場景")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Shepherd/Combat Lab/開啟 CombatLab 場景")]
        public static void Open()
        {
            if (!File.Exists(ScenePath)) Build();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>batchmode 入口：Unity.exe -batchmode -executeMethod Shepherd.Battle.Editor.CombatLabSceneBuilder.Build -quit</summary>
        public static void Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.10f, 0.11f, 0.13f, 1f);
            camera.orthographic = true;
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            new GameObject("CombatLab").AddComponent<CombatLabView>();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("無法儲存場景 " + ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[CombatLab] 已產生場景 " + ScenePath);
        }
    }
}

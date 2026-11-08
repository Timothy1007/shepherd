using System.IO;
using Shepherd.Exploration.Case01;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// 產生〈第十七號門〉室內 QA 場景：只有一個掛著 Case01HouseQA 的物件，房屋、玩家、攝影機、燈光與 HUD 於 Play 時以程式建立。
    /// 不修改 AnomalyStreetDemo、CombatLab3D、平面 CombatLab、SampleScene 或 Build Settings。
    /// </summary>
    public static class Case01HouseSceneBuilder
    {
        public const string ScenePath = "Assets/Exploration/Scenes/Case01HouseInteriorQA.unity";

        [MenuItem("Shepherd/Case01/開啟室內 QA 場景")]
        public static void Open()
        {
            if (!File.Exists(ScenePath)) Build();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Shepherd/Case01/重新產生室內 QA 場景")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Shepherd/Case01/美術 QA（H0～H3／失序體形成）")]
        public static void LaunchArtQa() { Launch("art"); }

        /// <summary>開啟室內 QA 場景並以指定模式進入 Play（一次性參數，Awake 讀取後刪除）。</summary>
        public static void Launch(string mode)
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) Build();
            EditorSceneManager.OpenScene(ScenePath);
            PlayerPrefs.SetString(Case01HouseQA.LaunchModeKey, mode);
            PlayerPrefs.Save();
            EditorApplication.isPlaying = true;
        }

        /// <summary>batchmode 入口：Unity.exe -batchmode -executeMethod Shepherd.Exploration.Editor.Case01HouseSceneBuilder.Build -quit</summary>
        public static void Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Case01HouseQA").AddComponent<Case01HouseQA>();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("無法儲存場景 " + ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Case01HouseQA] 已產生場景 " + ScenePath);
        }
    }
}

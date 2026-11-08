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

        public const string CombatScenePath = "Assets/Exploration/Scenes/Case01CombatDemoQA.unity";

        /// <summary>戰鬥 Demo UI QA：只有 Case01 首戰的兩敵戰鬥＋新版戰鬥 HUD（不從房屋開頭玩）。</summary>
        [MenuItem("Shepherd/Case01/戰鬥 Demo UI QA")]
        public static void LaunchCombatDemo()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(CombatScenePath)) BuildCombat();
            EditorSceneManager.OpenScene(CombatScenePath);
            EditorApplication.isPlaying = true;
        }

        /// <summary>batchmode 入口：-executeMethod Shepherd.Exploration.Editor.Case01HouseSceneBuilder.BuildCombat</summary>
        public static void BuildCombat()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CombatScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Case01CombatDemoQA").AddComponent<Case01CombatDemoQA>();
            if (!EditorSceneManager.SaveScene(scene, CombatScenePath))
                throw new IOException("無法儲存場景 " + CombatScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Case01CombatDemoQA] 已產生場景 " + CombatScenePath);
        }

        [MenuItem("Shepherd/Case01/美術 QA（H0～H3／失序體形成）")]
        public static void LaunchArtQa() { Launch("art"); }

        /// <summary>核校正 v0.3（關係校正）實驗 QA：直接從戰後核前開始；遊戲中 F5 可切回 v0.2 比較。</summary>
        [MenuItem("Shepherd/Case01/核校正 v0.3 QA")]
        public static void LaunchCoreV03() { Launch("v03"); }

        /// <summary>空間敘事 v0.4 QA：逐句預覽自語、H0～H3、失序門、記憶殘響（不需重新破關）。</summary>
        [MenuItem("Shepherd/Case01/空間敘事 v0.4 QA")]
        public static void LaunchNarrativeV04() { Launch("v04"); }

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

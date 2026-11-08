using Shepherd.Battle.Core;
using Shepherd.Battle.UI;
using Shepherd.Battle.UI.Arena;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// 「Case01／戰鬥 Demo UI QA」：只有 Case01 首戰的兩敵戰鬥（既有 Combat Lab 標準情境，規則與數值不變），
    /// 掛上 Demo 戰鬥 HUD，不必從房屋開頭重玩、不影響正式場景。
    /// F1 重新開始｜F9 重置戰鬥｜F10 切換舊版 HUD（A/B 比較）｜T 教學開／關（第二階段起）。
    /// </summary>
    public sealed class Case01CombatDemoQA : MonoBehaviour
    {
        [Tooltip("固定亂數種子（與 Combat Lab 相同的預設值）。")]
        [SerializeField] private int seed = CombatLabScenario.DefaultSeed;

        [Tooltip("首戰情境教學（QA 可切換 A/B）。")]
        [SerializeField] private bool tutorial = true;

        [Tooltip("顯示舊版 Combat Lab HUD（比較用）。")]
        [SerializeField] private bool legacyHud;

        private bool built;
        private bool playMode;
        private CombatLabScenario scenario;
        private GameObject battleRoot;
        private Canvas qaCanvas;
        private Text qaText;

        public CombatArenaView Arena { get; private set; }
        public CombatDemoHud Hud { get; private set; }
        public bool TutorialEnabled { get { return tutorial; } }
        public bool LegacyHud { get { return legacyHud; } }
        public int Rebuilds { get; private set; }

        private void Awake()
        {
            Build(true);
        }

        public void Build(bool playMode, CombatLabScenario scenarioOverride = null, int? seedOverride = null, bool? tutorialOn = null)
        {
            if (built) return;
            built = true;
            this.playMode = playMode;
            scenario = scenarioOverride;
            if (seedOverride.HasValue) seed = seedOverride.Value;
            if (tutorialOn.HasValue) tutorial = tutorialOn.Value;
            BuildQaOverlay();
            BuildBattle();
        }

        private CombatLabScenario Scenario()
        {
            if (scenario == null) return CombatLabScenario.Default();
            var copy = new CombatLabScenario { Name = scenario.Name };
            foreach (var e in scenario.Enemies)
                copy.Enemies.Add(new EnemyState
                {
                    Id = e.Id, Name = e.Name, Value = e.Value, Links = e.Links, Corrections = e.Corrections, Bind = e.Bind, AttackPattern = e.AttackPattern,
                });
            return copy;
        }

        private void BuildBattle()
        {
            if (battleRoot != null)
            {
                if (Arena != null) Arena.RestoreAtmosphere();
                battleRoot.SetActive(false);
                CombatLabUiKill(battleRoot);
            }
            battleRoot = new GameObject("Case01DemoBattle");
            battleRoot.SetActive(false);
            battleRoot.transform.SetParent(transform, false);
            Arena = battleRoot.AddComponent<CombatArenaView>();
            Arena.Build(playMode, Scenario(), seed, ArenaHostOptions.Standalone());
            Hud = legacyHud ? null : CombatDemoHud.Attach(Arena, new CombatDemoOptions { ShowQaTools = true, Tutorial = tutorial });
            battleRoot.SetActive(true);
            RefreshQaText();
        }

        private static void CombatLabUiKill(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        private void OnDestroy()
        {
            if (Arena != null) Arena.RestoreAtmosphere();
        }

        // ---------------- QA 操作 ----------------

        private void Update()
        {
            if (!built) return;
            if (Input.GetKeyDown(KeyCode.F1)) Restart();
            else if (Input.GetKeyDown(KeyCode.F9)) ResetBattle();
            else if (Input.GetKeyDown(KeyCode.F10)) ToggleLegacy();
            else if (Input.GetKeyDown(KeyCode.T)) ToggleTutorial();
        }

        /// <summary>F1：重新建立整場戰鬥（新的 HUD 與教學狀態）。</summary>
        public void Restart()
        {
            Rebuilds++;
            BuildBattle();
        }

        /// <summary>F9：重置戰鬥（既有 Combat Lab 的重置，不在正式 Case01 路徑顯示）。</summary>
        public void ResetBattle()
        {
            Arena.ResetButton.onClick.Invoke();
        }

        public void ToggleLegacy()
        {
            legacyHud = !legacyHud;
            Restart();
        }

        public void ToggleTutorial()
        {
            tutorial = !tutorial;
            Restart();
        }

        private void BuildQaOverlay()
        {
            var go = new GameObject("QaOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.layer = 5;
            go.transform.SetParent(transform, false);
            qaCanvas = go.GetComponent<Canvas>();
            qaCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            qaCanvas.sortingOrder = 30;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = CombatLabStyle.ReferenceResolution;
            scaler.matchWidthOrHeight = CombatLabStyle.MatchWidthOrHeight;
            var t = new GameObject("Text", typeof(RectTransform));
            t.layer = 5;
            var rt = (RectTransform)t.transform;
            rt.SetParent(go.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(-60f, -132f);
            rt.sizeDelta = new Vector2(620f, 24f);
            qaText = t.AddComponent<Text>();
            qaText.font = Case01Hud.UiFont;
            qaText.fontSize = 15;
            qaText.alignment = TextAnchor.MiddleCenter;
            qaText.color = new Color(0.62f, 0.62f, 0.6f, 0.9f);
            qaText.raycastTarget = false;
        }

        private void RefreshQaText()
        {
            if (qaText == null) return;
            qaText.text = "戰鬥 Demo UI QA｜F1 重來｜F9 重置戰鬥｜F10 " + (legacyHud ? "改用新版 HUD" : "比較舊版 HUD") + "｜T 教學：" + (tutorial ? "開" : "關");
        }
    }
}

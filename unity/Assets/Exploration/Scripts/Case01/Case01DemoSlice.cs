using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// v0.5 公開 Demo 的最小閉環：事務所（接委託）→ 街區（查門牌／名冊）→ 十七號（現有 Case01 House：探索、失序體、戰鬥、核）
    /// → 前門回街區 → 搭車回事務所寫結案記錄。
    /// 三個區域在同一個場景中「門到門」切換（黑幕轉場，不假裝是無縫大世界；也不需要改 Build Settings）。
    /// 跨區域進度存在 <see cref="Case01RunState"/>（屋內進度是同一個 Case01Flow 物件）。正式遊玩沒有 QA 快捷鍵。
    /// </summary>
    public sealed class Case01DemoSlice : MonoBehaviour
    {
        public const float FadeSeconds = 0.35f;
        public const float InteractDistance = 2.6f;
        /// <summary>選單啟動參數：「區域;格局」，例如 "office;Original"、"street;V05A"。</summary>
        public const string LaunchKey = "Shepherd.Case01.DemoLaunch";

        [SerializeField] private Case01DemoArea startArea = Case01DemoArea.Office;
        [SerializeField] private Case01Floorplan floorplan = Case01Floorplan.Original;

        private bool built;
        private bool playMode;
        private Transform areaRoot;
        private Case01Palette palette;
        private Case01DemoAreaLayout areaLayout;
        private Camera cam;
        private Light moon;
        private bool cursorCaptured;
        private float clock;
        private float fadeT = -1f;
        private Case01DemoArea pendingArea;
        private string pendingSpawn;
        private bool atmosphereSaved;
        private Color savedAmbient;
        private UnityEngine.Rendering.AmbientMode savedMode;
        private bool savedFog;

        public ICase01InputSource InputSource { get; set; }
        /// <summary>測試用：屋內戰鬥情境（null＝Combat Lab 標準兩敵情境；不改規則）。</summary>
        public Shepherd.Battle.UI.CombatLabScenario TestScenario { get; set; }
        public Case01DemoArea Area { get; private set; }
        public Case01Hud Hud { get; private set; }
        public FirstPersonController Player { get; private set; }
        public Camera Camera { get { return cam; } }
        public Case01HouseQA House { get; private set; }
        public Case01DemoAreaLayout AreaLayout { get { return areaLayout; } }
        public Case01Interactable CurrentTarget { get; private set; }
        public bool Transitioning { get { return fadeT >= 0f; } }
        public bool Ended { get; private set; }
        public int Transitions { get; private set; }
        public Case01Floorplan Floorplan { get { return floorplan; } }

        private void Awake()
        {
            var launch = PlayerPrefs.GetString(LaunchKey, "");
            if (launch.Length > 0)
            {
                PlayerPrefs.DeleteKey(LaunchKey);
                var parts = launch.Split(';');
                if (parts[0] == "street") startArea = Case01DemoArea.Street;
                else if (parts[0] == "house") startArea = Case01DemoArea.House;
                else startArea = Case01DemoArea.Office;
                if (parts.Length > 1 && parts[1] == "V05A") floorplan = Case01Floorplan.V05A;
            }
            Build(true);
        }

        private void OnDestroy()
        {
            if (palette != null) palette.Dispose();
            if (playMode)
            {
                RestoreAtmosphere();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        /// <summary>建立 Demo（playMode＝false 供 EditMode 測試；start／plan 可覆寫序列化設定）。會重設 Case01RunState。</summary>
        public void Build(bool playMode, Case01DemoArea? start = null, Case01Floorplan? plan = null)
        {
            if (built) return;
            built = true;
            this.playMode = playMode;
            if (start.HasValue) startArea = start.Value;
            if (plan.HasValue) floorplan = plan.Value;
            if (InputSource == null) InputSource = new LegacyCase01Input();
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            Case01RunState.Reset(floorplan);
            if (startArea != Case01DemoArea.Office) Case01RunState.CaseAccepted = true;
            if (startArea == Case01DemoArea.House) Case01RunState.StreetSurveyed = true;
            if (playMode && FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(transform, false);
            }
            Hud = new Case01Hud(transform);
            Hud.Base.ViewMode.text = "";
            var moonGo = new GameObject("Moonlight");
            moonGo.transform.SetParent(transform, false);
            moonGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.shadows = LightShadows.None;
            Enter(startArea, null);
        }

        // ---------------- 區域切換 ----------------

        /// <summary>黑幕轉場到另一個區域（門到門）。</summary>
        public void GoTo(Case01DemoArea area, string spawn)
        {
            if (fadeT >= 0f) return;
            pendingArea = area;
            pendingSpawn = spawn;
            fadeT = 0f;
            Hud.Visible = true;
            Hud.HidePanel();
        }

        private void Enter(Case01DemoArea area, string spawn)
        {
            if (House != null)
            {
                House.DemoExitRequested -= OnHouseExit;
                House.gameObject.SetActive(false);
                ArenaArt.Kill(House.gameObject);
                House = null;
            }
            if (areaRoot != null)
            {
                areaRoot.gameObject.SetActive(false);
                ArenaArt.Kill(areaRoot.gameObject);
            }
            if (palette != null) palette.Dispose();
            palette = new Case01Palette();
            areaRoot = ArenaArt.Group("Area_" + area, transform, Vector3.zero);
            areaLayout = null;
            Player = null;
            cam = null;
            CurrentTarget = null;
            Area = area;
            Transitions++;
            Hud.HidePanel();
            Hud.ClearMonologue();
            Hud.JournalVisible = false;
            if (area == Case01DemoArea.House)
            {
                EnterHouse();
                return;
            }
            Hud.Visible = true;
            areaLayout = area == Case01DemoArea.Office ? Case01OfficeBuilder.Build(areaRoot, palette) : Case01StreetBuilder.Build(areaRoot, palette, floorplan);
            Case01Pose pose;
            if (spawn == null || !areaLayout.Spawns.TryGetValue(spawn, out pose))
                pose = areaLayout.Spawns[area == Case01DemoArea.Office ? Case01DemoIds.SpawnOfficeStart : Case01DemoIds.SpawnStreetStart];
            Player = FirstPersonController.Create(areaRoot, pose.Position, pose.Yaw);
            var camGo = new GameObject("DemoCamera");
            camGo.transform.SetParent(areaRoot, false);
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = areaLayout.SkyColor;
            cam.fieldOfView = Case01HouseQA.ExploreFieldOfView;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 150f;
            if (playMode)
            {
                camGo.tag = "MainCamera";
                camGo.AddComponent<AudioListener>();
            }
            moon.gameObject.SetActive(true);
            moon.color = area == Case01DemoArea.Street ? new Color(0.55f, 0.62f, 0.85f) : new Color(0.6f, 0.65f, 0.8f);
            moon.intensity = area == Case01DemoArea.Street ? 0.35f : 0.12f;
            ApplyAtmosphere(area == Case01DemoArea.Street ? new Color(0.17f, 0.18f, 0.24f) : new Color(0.26f, 0.24f, 0.22f));
            SnapCamera();
            SetCursorCaptured(true);
            Physics.SyncTransforms();
            if (area == Case01DemoArea.Street)
            {
                if (spawn == Case01DemoIds.SpawnStreetFromHouse && Case01RunState.HouseResolved) Hud.EnqueueMonologue(Case01DemoTexts.ReturnStreetMonologue);
                else if (spawn != Case01DemoIds.SpawnStreetFromHouse) Hud.EnqueueMonologue(Case01DemoTexts.FirstStreetMonologue);
            }
            UpdateTexts();
        }

        private void EnterHouse()
        {
            Hud.Visible = false;
            moon.gameObject.SetActive(false);
            RestoreAtmosphere();
            Case01RunState.EnteredHouse = true;
            var go = new GameObject("Case01House_Demo");
            go.SetActive(false);
            go.transform.SetParent(areaRoot, false);
            var qa = go.AddComponent<Case01HouseQA>();
            qa.InputSource = InputSource;
            qa.ConfigureDemo(Case01RunState.EnsureHouseFlow(), floorplan);
            qa.Build(playMode, TestScenario);
            qa.DemoExitRequested += OnHouseExit;
            go.SetActive(true);
            House = qa;
        }

        private void OnHouseExit()
        {
            GoTo(Case01DemoArea.Street, Case01DemoIds.SpawnStreetFromHouse);
        }

        // ---------------- 每影格 ----------------

        private void Update()
        {
            if (!built) return;
            // 屋內時由 Case01HouseQA 自己讀輸入（同一個來源不讀兩次）。
            bool houseOwnsInput = Area == Case01DemoArea.House && House != null && fadeT < 0f;
            Tick(Time.deltaTime, houseOwnsInput || InputSource == null ? Case01Input.None : InputSource.Read());
        }

        /// <summary>推進一個影格（測試可直接呼叫）。屋內時把輸入轉交給 Case01HouseQA。</summary>
        public void Tick(float dt, Case01Input input)
        {
            clock += dt;
            if (fadeT >= 0f)
            {
                TickFade(dt);
                if (House != null && !playMode) House.Tick(dt, Case01Input.None);
                return;
            }
            if (Area == Case01DemoArea.House)
            {
                // Case01HouseQA 在 Play 時自己讀輸入；測試時由這裡推進。
                if (House != null && !playMode) House.Tick(dt, input);
                return;
            }
            Hud.MonologueHold = Hud.PanelVisible || Hud.JournalVisible;
            Hud.Tick(dt);
            if (input.SkipLine) Hud.SkipMonologue();
            AnimateStreet();
            if (Ended)
            {
                if (input.Base.ReleaseCursor) SetCursorCaptured(false);
                return;
            }
            var b = input.Base;
            if (b.ReleaseCursor) SetCursorCaptured(false);
            else if (b.Click && !cursorCaptured && !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())) SetCursorCaptured(true);
            if (input.Journal)
            {
                Hud.JournalVisible = !Hud.JournalVisible;
                if (Hud.JournalVisible) Hud.RefreshJournal(JournalEntries());
            }
            Player.Step(dt, b, cursorCaptured);
            SnapCamera();
            CurrentTarget = FindTarget();
            if (Hud.PanelVisible && b.Interact && (CurrentTarget == null || CurrentTarget.Id == panelSource))
            {
                Hud.HidePanel();
            }
            else if (b.Interact && CurrentTarget != null)
            {
                panelSource = CurrentTarget.Id;
                Interact(CurrentTarget.Id);
            }
            Hud.Base.SetHint(Hud.JournalVisible || Hud.PanelVisible || CurrentTarget == null ? null : "［E］" + CurrentTarget.Prompt);
        }

        private string panelSource;

        private void TickFade(float dt)
        {
            float prev = fadeT;
            fadeT += dt;
            if (prev < FadeSeconds && fadeT >= FadeSeconds) Enter(pendingArea, pendingSpawn);
            float a = fadeT < FadeSeconds ? fadeT / FadeSeconds : 1f - (fadeT - FadeSeconds) / FadeSeconds;
            Hud.Fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(a));
            if (Area == Case01DemoArea.House && fadeT >= FadeSeconds)
            {
                // 屋內有自己的 HUD；這裡的黑幕直接收起。
                Hud.Fade.color = new Color(0f, 0f, 0f, 0f);
                fadeT = -1f;
                return;
            }
            if (fadeT >= FadeSeconds * 2f)
            {
                fadeT = -1f;
                Hud.Fade.color = new Color(0f, 0f, 0f, 0f);
            }
        }

        private void AnimateStreet()
        {
            if (Area != Case01DemoArea.Street || areaLayout == null || areaLayout.Plate17Label == null) return;
            // 「17」偶爾閃一下（低頻、幅度小）。
            float n = Mathf.PerlinNoise(clock * 1.7f, 3.3f);
            areaLayout.Plate17Label.color = n > 0.78f ? new Color(0.45f, 0.25f, 0.6f) : new Color(0.08f, 0.07f, 0.05f);
        }

        // ---------------- 互動 ----------------

        private Case01Interactable FindTarget()
        {
            var hits = Physics.RaycastAll(cam.transform.position, cam.transform.forward, InteractDistance, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            foreach (var h in hits)
            {
                if (h.collider == Player.Body) continue;
                var it = h.collider.GetComponent<Case01Interactable>();
                if (it != null) return it;
                if (!h.collider.isTrigger) return null;
            }
            return null;
        }

        /// <summary>處理一個事務所／街區的互動（測試可直接呼叫）。</summary>
        public void Interact(string id)
        {
            switch (id)
            {
                case Case01DemoIds.OfficeCase:
                    if (Case01RunState.CaseClosed) Hud.ShowText(Case01DemoTexts.ClosedAgain);
                    else if (Case01RunState.HouseResolved)
                    {
                        Case01RunState.CaseClosed = true;
                        Ended = true;
                        Hud.HidePanel();
                        Hud.EnqueueMonologue(Case01DemoTexts.ClosureMonologue);
                        Hud.ShowEnd(Case01DemoTexts.ClosureTitle, Case01DemoTexts.ClosureBody(Case01RunState.FamilyPhotoCollected));
                        SetCursorCaptured(false);
                    }
                    else if (!Case01RunState.CaseAccepted)
                    {
                        Case01RunState.CaseAccepted = true;
                        Hud.ShowText(Case01DemoTexts.CaseBrief);
                        Hud.EnqueueMonologue(Case01DemoTexts.CaseAcceptedMonologue);
                        Hud.Base.ShowToast("已接下委託：〈第十七號門〉");
                    }
                    else Hud.ShowText(Case01DemoTexts.CaseAgain);
                    break;
                case Case01DemoIds.OfficeBoard: Hud.ShowText(Case01DemoTexts.Board); break;
                case Case01DemoIds.OfficeCabinet: Hud.ShowText(Case01DemoTexts.Cabinet); break;
                case Case01DemoIds.OfficeDoor:
                    if (!Case01RunState.CaseAccepted) Hud.ShowNote(Case01DemoTexts.DoorLocked);
                    else GoTo(Case01DemoArea.Street, Case01DemoIds.SpawnStreetStart);
                    break;
                case Case01DemoIds.StreetSign: Hud.ShowText(Case01DemoTexts.Sign); break;
                case Case01DemoIds.StreetListing:
                    Case01RunState.SawListing = true;
                    Case01RunState.StreetSurveyed = true;
                    Hud.ShowText(Case01DemoTexts.Listing);
                    break;
                case Case01DemoIds.Plate16: Hud.ShowText(Case01DemoTexts.Plate16); break;
                case Case01DemoIds.Plate18: Hud.ShowText(Case01DemoTexts.Plate18); break;
                case Case01DemoIds.Plate17:
                    Case01RunState.SawPlate17 = true;
                    Case01RunState.StreetSurveyed = true;
                    Hud.ShowText(Case01DemoTexts.Plate17);
                    break;
                case Case01DemoIds.Door17:
                    if (Case01RunState.HouseResolved) Hud.ShowNote(Case01DemoTexts.HouseDone);
                    else if (!Case01RunState.StreetSurveyed) Hud.EnqueueMonologue(Case01DemoTexts.NeedSurvey);
                    else
                    {
                        Hud.Base.ShowToast(Case01DemoTexts.EnterHouse);
                        GoTo(Case01DemoArea.House, null);
                    }
                    break;
                case Case01DemoIds.StreetBack:
                    if (!Case01RunState.HouseResolved) Hud.ShowNote(Case01DemoTexts.BackLocked);
                    else GoTo(Case01DemoArea.Office, Case01DemoIds.SpawnOfficeReturn);
                    break;
            }
            UpdateTexts();
        }

        private List<JournalEntry> JournalEntries()
        {
            var list = new List<JournalEntry>();
            if (Case01RunState.CaseAccepted)
                list.Add(new JournalEntry { Id = "demo_case", Title = "委託：〈第十七號門〉", Text = "地址：" + Case01DemoTexts.StreetName + "，16 號與 18 號之間。先確認門牌與房屋。" });
            if (Case01RunState.SawListing) list.Add(new JournalEntry { Id = "demo_listing", Title = "郵遞名冊", Text = "名冊上是 15、16、18、19，沒有 17 號。" });
            if (Case01RunState.SawPlate17) list.Add(new JournalEntry { Id = "demo_plate", Title = "門牌「17」", Text = "名冊上沒有這一號，門卻在這裡。" });
            if (Case01RunState.HouseFlow != null) list.AddRange(Case01RunState.HouseFlow.Session.Journal);
            return list;
        }

        public string ObjectiveText()
        {
            if (Area == Case01DemoArea.Office)
            {
                if (Case01RunState.CaseClosed) return "Demo 結束。";
                if (Case01RunState.HouseResolved) return "回到桌前，寫下結案記錄（E）。";
                if (!Case01RunState.CaseAccepted) return "看看桌上的新委託。";
                return "出門，到" + Case01DemoTexts.StreetName + "確認門牌（南牆的門）。";
            }
            if (Area == Case01DemoArea.Street)
            {
                if (Case01RunState.HouseResolved) return "回到巷口的計程車招呼站，回事務所。";
                if (!Case01RunState.StreetSurveyed) return "確認 16、17、18 號的門牌，或看看巷口的郵遞名冊。";
                return "從前院走到十七號的門口（E）。";
            }
            return "";
        }

        private void UpdateTexts()
        {
            if (Area == Case01DemoArea.House) return;
            Hud.Base.Objective.text = "<b>〈第十七號門〉" + (Area == Case01DemoArea.Office ? "牧羊人調查事務所" : Case01DemoTexts.StreetName) + "</b>\n<color=#E6BD52>目標：</color>" + ObjectiveText();
            Hud.Base.Controls.text = "WASD 移動｜滑鼠 視角｜Shift 奔跑｜E 調查／互動｜J 日誌｜Tab 略過自語｜Esc 釋放滑鼠";
            Hud.Base.ViewMode.text = "";
            Hud.QaText.transform.parent.gameObject.SetActive(false);
        }

        private void SnapCamera()
        {
            if (cam == null || Player == null) return;
            cam.transform.position = Player.EyePosition;
            cam.transform.rotation = Player.ViewRotation;
        }

        private void SetCursorCaptured(bool captured)
        {
            cursorCaptured = captured;
            if (!playMode) return;
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }

        private void ApplyAtmosphere(Color ambient)
        {
            if (!playMode) return;
            if (!atmosphereSaved)
            {
                atmosphereSaved = true;
                savedAmbient = RenderSettings.ambientLight;
                savedMode = RenderSettings.ambientMode;
                savedFog = RenderSettings.fog;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            RenderSettings.fog = false;
        }

        private void RestoreAtmosphere()
        {
            if (!atmosphereSaved) return;
            atmosphereSaved = false;
            RenderSettings.ambientLight = savedAmbient;
            RenderSettings.ambientMode = savedMode;
            RenderSettings.fog = savedFog;
        }
    }
}

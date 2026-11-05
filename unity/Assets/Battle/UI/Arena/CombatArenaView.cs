using System.Collections.Generic;
using Shepherd.Battle.Core;
using Shepherd.Battle.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shepherd.Battle.UI.Arena
{
    /// <summary>
    /// 3D Combat Lab v0.2：Graybox 戰鬥場景＋精簡 HUD。
    /// 所有戰鬥結果都來自 <see cref="CombatLabSession"/>（BattleController／RoundResolver）；
    /// 本元件只負責擺放模型、轉發滑鼠操作，以及依 BattleEvent 播放視覺回饋。
    /// 場景與介面在執行時以程式建立，不使用外部素材。
    /// </summary>
    public sealed partial class CombatArenaView : MonoBehaviour
    {
        [Tooltip("可選：指定 BattleConfig 資產調整數值；留空則使用規格預設值。")]
        [SerializeField] private BattleConfig config;

        [Tooltip("固定亂數種子，方便重現問題。")]
        [SerializeField] private int seed = CombatLabScenario.DefaultSeed;

        public static readonly Vector3 PlayerPosition = new Vector3(-0.6f, 0f, -3.1f);
        public static readonly Vector3 CameraPosition = new Vector3(2.2f, 5.0f, -11.5f);
        public static readonly Vector3 CameraLookAt = new Vector3(0.4f, 0.9f, 1.2f);

        /// <summary>單一敵人的 3D 模型與狀態。</summary>
        private sealed class EnemyView
        {
            public int Id;
            public string Name;
            public Transform Root;
            public Transform Body;
            public Renderer[] Renderers;
            public Material Core;
            public List<Transform> Orbiters;
            public float Height;
            public bool Dissolved;
        }

        private bool built;
        private bool playMode;
        private CombatLabScenario scenario;
        private Transform world;
        private Camera cam;
        private Light moon;
        private Transform player;
        private Renderer[] playerRenderers;
        private Light lantern;
        private Transform shieldBubble;
        private Material shieldMat;
        private Transform targetRing;
        private Transform hoverRing;
        private List<Transform> floatingShards = new List<Transform>();
        private readonly List<Vector3> floatingHome = new List<Vector3>();
        private readonly List<EnemyView> enemies = new List<EnemyView>();
        private readonly Dictionary<int, int> maxValues = new Dictionary<int, int>();
        private ArenaFx fx;
        private float clock;
        private Vector3 cameraHome;
        private int? hoveredEnemy;

        public CombatLabSession Session { get; private set; }
        public Camera ArenaCamera { get { return cam; } }
        public Transform PlayerRoot { get { return player; } }
        public ArenaFx Fx { get { return fx; } }
        public int EnemyCount { get { return enemies.Count; } }

        private void Awake()
        {
            Build(true);
        }

        private void OnDestroy()
        {
            if (Session != null) Session.Changed -= OnSessionChanged;
            if (playMode) RestoreAtmosphere();
        }

        /// <summary>
        /// 建立場景、角色、HUD 與戰鬥。playMode＝false 供 EditMode 測試與截圖使用（不建立 EventSystem、不改 RenderSettings）。
        /// scenarioOverride／seedOverride 供測試指定情境。
        /// </summary>
        public void Build(bool playMode, CombatLabScenario scenarioOverride = null, int? seedOverride = null)
        {
            if (built) return;
            built = true;
            this.playMode = playMode;
            scenario = scenarioOverride ?? CombatLabScenario.Default();
            var rules = config != null ? RuleSetCompiler.Compile(config) : RuleSet.CreateDefault();
            Session = new CombatLabSession(rules, scenario, seedOverride ?? seed);
            Session.Changed += OnSessionChanged;

            if (playMode && FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(transform, false);
            }

            BuildWorld();
            BuildHud();
            fx = new ArenaFx(world, floaterLayer, WorldToHud);
            if (playMode) ApplyAtmosphere();
            RebuildEnemies();
            Refresh();
        }

        // ---------------- 3D 場景 ----------------

        private void BuildWorld()
        {
            world = ArenaArt.Group("ArenaWorld", transform, Vector3.zero);

            var camGo = new GameObject("ArenaCamera");
            camGo.transform.SetParent(world, false);
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ArenaArt.FogColor;
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 90f;
            camGo.transform.position = CameraPosition;
            camGo.transform.LookAt(CameraLookAt);
            cameraHome = camGo.transform.localPosition;
            if (playMode)
            {
                camGo.tag = "MainCamera";
                camGo.AddComponent<AudioListener>();
            }

            var moonGo = new GameObject("Moonlight");
            moonGo.transform.SetParent(world, false);
            moonGo.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
            moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.58f, 0.64f, 0.88f);
            moon.intensity = 0.55f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.8f;

            floatingShards = ArenaArt.BuildEnvironment(world);
            floatingHome.Clear();
            foreach (var s in floatingShards) floatingHome.Add(s.localPosition);

            player = ArenaArt.BuildPlayer(world, PlayerPosition, out playerRenderers, out lantern);
            player.rotation = Quaternion.LookRotation(new Vector3(0.6f, 0f, 5.6f));

            shieldMat = ArenaArt.FadeMat(new Color(0.35f, 0.65f, 1f, 0f), new Color(0.2f, 0.45f, 1f) * 0.35f);
            shieldBubble = ArenaArt.Prim(PrimitiveType.Sphere, player, new Vector3(0, 1.2f, 0), new Vector3(1.9f, 2.6f, 1.9f), shieldMat,
                null, "ShieldBubble", false, false);

            var ringMat = ArenaArt.FadeMat(new Color(1f, 0.8f, 0.3f, 0.75f), CombatLabUi.Highlight * 1.6f);
            targetRing = ArenaArt.Prim(PrimitiveType.Cylinder, world, Vector3.zero, new Vector3(1.7f, 0.012f, 1.7f), ringMat, null,
                "TargetRing", false, false);
            var hoverMat = ArenaArt.FadeMat(new Color(1f, 1f, 1f, 0.3f), Color.white * 0.4f);
            hoverRing = ArenaArt.Prim(PrimitiveType.Cylinder, world, Vector3.zero, new Vector3(1.9f, 0.008f, 1.9f), hoverMat, null,
                "HoverRing", false, false);
            hoverRing.gameObject.SetActive(false);
        }

        private Vector3 EnemyHome(int index, int count)
        {
            float x = (index - (count - 1) * 0.5f) * 3.6f + 0.6f;
            float z = 2.4f + (index % 2) * 0.7f;
            return new Vector3(x, 0f, z);
        }

        /// <summary>依目前 BattleState 的敵人重新建立 3D 敵人與頭頂資訊（數量與數值都來自快照）。</summary>
        private void RebuildEnemies()
        {
            foreach (var e in enemies) if (e.Root != null) ArenaArt.Kill(e.Root.gameObject);
            enemies.Clear();
            maxValues.Clear();
            ClearPlates();
            var snap = Session.Snapshot();
            for (int i = 0; i < snap.Enemies.Count; i++)
            {
                var s = snap.Enemies[i];
                maxValues[s.Id] = Mathf.Max(1, s.Value);
                float height = Mathf.Clamp(0.85f + s.Value / 120f, 0.85f, 1.35f);
                Renderer[] rs;
                Material core;
                List<Transform> orbiters;
                var root = ArenaArt.BuildEnemy(world, EnemyHome(i, snap.Enemies.Count), height, i % 2, out rs, out core, out orbiters);
                root.name = "Enemy_" + s.Id;
                root.rotation = Quaternion.LookRotation(PlayerPosition - root.position, Vector3.up);
                root.rotation = Quaternion.Euler(0f, root.rotation.eulerAngles.y, 0f);
                root.gameObject.AddComponent<ArenaEnemyMarker>().EnemyId = s.Id;
                enemies.Add(new EnemyView
                {
                    Id = s.Id, Name = s.Name, Root = root, Body = root.Find("Body"), Renderers = rs, Core = core, Orbiters = orbiters,
                    Height = height,
                });
                BuildPlate(s.Id);
            }
            Physics.SyncTransforms();
        }

        private EnemyView Enemy(int id)
        {
            foreach (var e in enemies) if (e.Id == id) return e;
            return null;
        }

        private Vector3 EnemyChest(EnemyView e)
        {
            return e.Root.position + Vector3.up * 1.5f * e.Height;
        }

        private Vector3 EnemyHead(EnemyView e)
        {
            return e.Root.position + Vector3.up * (2.65f * e.Height + 0.25f);
        }

        private Vector3 LanternPoint { get { return lantern != null ? lantern.transform.position : player.position + Vector3.up * 2f; } }

        private Vector3 PlayerChest { get { return player.position + Vector3.up * 1.4f; } }

        // ---------------- 滑鼠選取 ----------------

        public Transform EnemyRoot(int id)
        {
            var e = Enemy(id);
            return e != null ? e.Root : null;
        }

        /// <summary>敵人胸口在攝影機畫面上的座標（測試用）。</summary>
        public Vector2 EnemyScreenPoint(int id)
        {
            var e = Enemy(id);
            Vector3 p = cam.WorldToScreenPoint(e.Root.position + Vector3.up * 1.25f * e.Height);
            return new Vector2(p.x, p.y);
        }

        /// <summary>畫面座標下的敵人編號（以 Physics.Raycast 判定 3D 碰撞體）。</summary>
        public int? EnemyAt(Vector2 screenPoint)
        {
            if (cam == null) return null;
            Physics.SyncTransforms();
            var ray = cam.ScreenPointToRay(new Vector3(screenPoint.x, screenPoint.y, 0f));
            var hits = Physics.RaycastAll(ray, 200f);
            float best = float.MaxValue;
            int? id = null;
            foreach (var h in hits)
            {
                var m = h.collider.GetComponentInParent<ArenaEnemyMarker>();
                if (m == null || !m.transform.IsChildOf(world) || h.distance >= best) continue;
                var e = Enemy(m.EnemyId);
                if (e == null || e.Dissolved) continue;
                best = h.distance;
                id = m.EnemyId;
            }
            return id;
        }

        /// <summary>點擊 3D 敵人選為目標。回傳是否選取成功。</summary>
        public bool TrySelectEnemyAt(Vector2 screenPoint)
        {
            if (IsPlaying) return false;
            var id = EnemyAt(screenPoint);
            return id.HasValue && Session.SelectTarget(id.Value);
        }

        private void Update()
        {
            if (!built) return;
            Tick(Time.deltaTime);
            if (IsPlaying)
            {
                if (Input.GetKeyDown(KeyCode.Space)) FinishPlayback();
                return;
            }
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Vector2 mouse = Input.mousePosition;
            hoveredEnemy = overUi ? null : EnemyAt(mouse);
            if (!overUi && Input.GetMouseButtonDown(0)) TrySelectEnemyAt(mouse);
        }

        private void LateUpdate()
        {
            if (built) PositionPlates();
        }

        /// <summary>推進動畫與事件播放（Update 呼叫；測試可直接呼叫）。</summary>
        public void Tick(float dt)
        {
            clock += dt;
            fx.Tick(dt);
            TickPlayback(dt);
            AnimateIdle(dt);
        }

        private void AnimateIdle(float dt)
        {
            for (int i = 0; i < floatingShards.Count; i++)
            {
                var s = floatingShards[i];
                if (s == null) continue;
                s.localPosition = floatingHome[i] + Vector3.up * Mathf.Sin(clock * 0.7f + i) * 0.25f;
                s.Rotate(new Vector3(9f, 14f, 5f) * dt, Space.Self);
            }
            foreach (var e in enemies)
            {
                if (e.Root == null || e.Dissolved) continue;
                for (int k = 0; k < e.Orbiters.Count; k++)
                {
                    float a = clock * (0.9f + k * 0.15f) + k * 1.3f;
                    e.Orbiters[k].localPosition = new Vector3(Mathf.Cos(a) * 0.75f, 1.2f + Mathf.Sin(a * 1.7f) * 0.6f, Mathf.Sin(a) * 0.55f);
                }
                if (e.Core != null)
                    ArenaArt.SetEmission(e.Core, ArenaArt.Rift * (1.6f + Mathf.Sin(clock * 3f + e.Id) * 0.6f));
            }

            var target = Session.SelectedTargetId.HasValue ? Enemy(Session.SelectedTargetId.Value) : null;
            bool showTarget = target != null && !target.Dissolved && !Session.IsOver;
            targetRing.gameObject.SetActive(showTarget);
            if (showTarget)
            {
                targetRing.position = target.Root.position + Vector3.up * 0.04f;
                targetRing.rotation = Quaternion.Euler(0f, clock * 40f, 0f);
                float pulse = 1.6f + Mathf.Sin(clock * 4f) * 0.08f;
                targetRing.localScale = new Vector3(pulse * target.Height, 0.012f, pulse * target.Height);
            }
            var hover = hoveredEnemy.HasValue ? Enemy(hoveredEnemy.Value) : null;
            bool showHover = hover != null && hover != target && !IsPlaying;
            hoverRing.gameObject.SetActive(showHover);
            if (showHover) hoverRing.position = hover.Root.position + Vector3.up * 0.03f;

            if (shieldMat != null)
            {
                var c = shieldMat.color;
                float baseAlpha = DisplayShield > 0 ? 0.10f + Mathf.Sin(clock * 2f) * 0.03f : 0f;
                c.a = Mathf.Max(baseAlpha, c.a - dt * 0.9f);
                shieldMat.color = c;
            }
        }

        public bool TargetRingVisible { get { return targetRing != null && targetRing.gameObject.activeSelf; } }

        public Vector3 TargetRingPosition { get { return targetRing.position; } }

        // ---------------- 氛圍（RenderSettings） ----------------

        private bool atmosphereSaved;
        private bool savedFog;
        private Color savedFogColor;
        private FogMode savedFogMode;
        private float savedFogDensity;
        private UnityEngine.Rendering.AmbientMode savedAmbientMode;
        private Color savedAmbient;
        private Material savedSkybox;

        /// <summary>套用霧與環境光；只改執行中的設定，離開時還原（不寫入場景或專案設定）。</summary>
        public void ApplyAtmosphere()
        {
            if (!atmosphereSaved)
            {
                atmosphereSaved = true;
                savedFog = RenderSettings.fog;
                savedFogColor = RenderSettings.fogColor;
                savedFogMode = RenderSettings.fogMode;
                savedFogDensity = RenderSettings.fogDensity;
                savedAmbientMode = RenderSettings.ambientMode;
                savedAmbient = RenderSettings.ambientLight;
                savedSkybox = RenderSettings.skybox;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.03f;
            RenderSettings.fogColor = ArenaArt.FogColor;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.17f, 0.17f, 0.24f);
            RenderSettings.skybox = null;
        }

        public void RestoreAtmosphere()
        {
            if (!atmosphereSaved) return;
            atmosphereSaved = false;
            RenderSettings.fog = savedFog;
            RenderSettings.fogColor = savedFogColor;
            RenderSettings.fogMode = savedFogMode;
            RenderSettings.fogDensity = savedFogDensity;
            RenderSettings.ambientMode = savedAmbientMode;
            RenderSettings.ambientLight = savedAmbient;
            RenderSettings.skybox = savedSkybox;
        }

        // ---------------- 操作 ----------------

        private void OnReset()
        {
            if (IsPlaying) FinishPlayback();
            redrawMode = false;
            Session.Reset(Session.Seed);
            RebuildEnemies();
            Refresh();
        }

        private void OnSessionChanged()
        {
            if (committing) return;
            Refresh();
        }
    }
}

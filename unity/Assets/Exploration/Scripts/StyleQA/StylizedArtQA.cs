using System.Collections.Generic;
using Shepherd.Exploration.Lantern;
using UnityEngine;

namespace Shepherd.Exploration.StyleQA
{
    /// <summary>
    /// 正式美術風格可行性 QA（獨立場景 Assets/Exploration/Scenes/StylizedArtQA.unity）。
    /// 場景裡的資產、燈光、碰撞都由編輯器腳本 StylizedArtQAEditor 擺好；這個元件負責第一人稱走動與切換：
    ///   T：暖光（午後窗光＋檯燈）／冷光（陰天藍光）
    ///   M：材質比較——手繪風格 shader／URP Lit（同一張貼圖）／灰盒（沒有貼圖的灰色）
    ///   G：線框疊圖（證明是真正的 3D 幾何）
    ///   1～6：正面／側面／背面／45 度／第一人稱正常距離／第一人稱近距離（傳送到觀察點，之後可以自由走）
    ///   WASD 走、Shift 快走、滑鼠轉視角；Esc 放開滑鼠、點畫面繼續；按住 Alt 可以點按鈕；U 隱藏標籤。
    /// 移動參數沿用正式值（LanternPrototypeQA 的步行 1.5／快走 2.4）；不改任何全域設定。
    /// </summary>
    public sealed class StylizedArtQA : MonoBehaviour
    {
        public enum Lighting { Warm, Cool, Night }
        public enum MaterialMode { Painterly, UrpLit, Graybox }

        public static readonly string[] LightingNames = { "暖光（午後窗光＋檯燈）", "冷光（陰天藍光）", "夜晚（只有提燈）" };
        public static readonly string[] MaterialNames = { "手繪風格 shader", "URP Lit（同一張貼圖）", "灰盒對照" };
        public static readonly string[] ViewNames = { "正面", "側面", "背面", "45 度", "第一人稱・正常距離", "第一人稱・近距離" };

        [Header("由編輯器腳本指定")]
        public Transform Assets;          // 所有展示資產的父物件
        public Renderer[] WallRenderers;  // 牆（環繞視角時改成只投影）
        public Light Sun, Lamp;
        public Transform[] Views;         // 6 個觀察點（位置＝相機位置；forward＝看的方向）
        public Material WireMaterial;
        public Transform Spawn;
        public Renderer WindowBackdrop;   // 窗外的亮面（不受光、不投影），隨光線切換顏色
        public Renderer OrbitFloor;       // 只有地板的複本：環繞視角時牆改成只投影，地板仍要看得到

        [Header("第一人稱：左手提燈（Blender 模型）")]
        public Transform Viewmodel;       // 掛在相機下
        public Transform Arm;             // 左手＋前臂
        public Transform RightArm;        // 右手（放鬆的預備姿勢；之後要拿穩定器或其他東西）
        public Transform ForearmLeft, ForearmRight;   // 前臂（和手分開：手保持握姿）
        public Transform UpperArmLeft;                 // 上臂（手肘到肩膀）
        // 手腕在手模型裡的位置（Unity 座標，原點＝握把中心／右手的手心前方）
        public const float ViewScale = 0.8f;
        public static readonly Vector3 WristLeft = new Vector3(0f, 0.026f, 0.100f);
        public static readonly Vector3 WristRight = new Vector3(0f, 0.026f, 0.120f);
        public Transform LanternPivot;    // 手握的位置（提燈從這裡垂下、擺動）
        public Renderer LanternRenderer;
        public Light LanternLight;

        public Lighting LightMode { get; private set; }
        public MaterialMode MatMode { get; private set; }
        public bool Wireframe { get; private set; }
        public int ViewIndex { get; private set; } = -1;
        public FirstPersonController Player { get; private set; }
        public Camera Cam { get; private set; }
        public bool Ready { get; private set; }
        public bool LabelsVisible = true;

        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly List<Material[]> painterly = new List<Material[]>();
        private readonly List<Material[]> urpLit = new List<Material[]>();
        private Material[] grayOne;
        private readonly List<GameObject> wires = new List<GameObject>();
        private readonly LanternCursorLock cursor = new LanternCursorLock();
        private readonly LegacyExploreInput input = new LegacyExploreInput();
        private bool cursorUi;
        private GUIStyle label, hint, button;

        // 第一人稱提燈的狀態
        public bool ViewmodelVisible { get; private set; } = true;
        /// <summary>手現在要不要舉起來：按 F 開／關燈時的展示動作（ShowSeconds 秒），或按住滑鼠右鍵。</summary>
        public bool LanternRaised => holdRaise || showTimer > 0f;
        private bool holdRaise;
        private float showTimer;
        /// <summary>按 F 之後手舉起來展示提燈的時間（含抬起與停留），之後自動放回去。</summary>
        public const float ShowSeconds = 2.4f;
        public bool LanternOn { get; private set; } = true;
        public bool RightHandVisible = false;   // 右手先收起來，等決定要拿什麼再做握法（R 可以叫出來看）
        private float raise, lightK = 1f, lastYaw, lastPitch, swayPhase;
        private Vector2 swing, swingVel, handLag, handLagVel;
        // 手（握把）在鏡頭座標的位置（公尺；x 右、y 上、z 前）：
        // 平常走路＝手垂在身側提著燈、手肘約彎 90°，手在畫面外；按 F＝手自然抬起來，整盞燈和光都在畫面左半邊。
        public static readonly Vector3 HandLowered = new Vector3(-0.24f, -0.34f, 0.46f);
        // 舉燈：拳頭靠近畫面左邊（約 22% 處、高度不變），前臂很快就出畫面——畫面裡只看到手、手腕、袖口和一小段前臂（照參考圖的構圖）。
        public static readonly Vector3 HandRaised = new Vector3(-0.33f, -0.02f, 0.56f);
        // 兩節骨頭的 IK：肩膀（鏡頭座標）、手肘往外下方彎、上臂與前臂長度
        public static readonly Vector3 Shoulder = new Vector3(-0.2f, -0.26f, 0.04f);      // 平常提著
        public static readonly Vector3 ElbowPole = new Vector3(-0.6f, -0.6f, 0.0f);
        // 舉燈時：手臂的錨點（肩膀）放到畫面下方外面、往前一點，手肘往下、貼近身體，
        // 前臂從畫面底部斜斜往上接到手腕——手臂大部分在鏡頭外，畫面裡主要是提燈。骨頭長度不變。
        public static Vector3 ShoulderRaised = new Vector3(-0.22f, -0.40f, 0.20f);
        public static Vector3 ElbowPoleRaised = new Vector3(-0.30f, -1.0f, 0.15f);
        /// <summary>目前（依抬手程度內插）的肩膀與手肘方向，鏡頭座標。</summary>
        public Vector3 ShoulderNow { get; private set; }
        public const float UpperArmLength = 0.30f, ForearmLength = 0.29f;
        public const float ArmWidth = 0.85f;   // 袖子粗細的縮放（前臂、上臂）
        public const float ForearmMeshLength = 0.27f, UpperArmMeshLength = 0.30f;   // Blender 模型的長度
        public Vector3 ElbowPosition { get; private set; }

        private void Start()
        {
            if (Application.isPlaying) Setup();
        }

        /// <summary>建立玩家與相機、收集材質（編輯模式的截圖也會呼叫）。</summary>
        public void Setup()
        {
            if (Ready) return;
            Player = FirstPersonController.Create(transform, Spawn != null ? Spawn.position : Vector3.zero, Spawn != null ? Spawn.eulerAngles.y : 0f);
            Player.WalkSpeedValue = LanternPrototypeQA.WalkSpeed;
            Player.RunSpeedValue = LanternPrototypeQA.BriskSpeed;
            Player.Acceleration = LanternPrototypeQA.MoveAcceleration;
            Player.Deceleration = LanternPrototypeQA.MoveDeceleration;
            Player.SmoothSpeedOnly = true;
            Player.EyeHeightValue = 1.62f;
            var camGo = new GameObject("StyleQACamera");
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.fieldOfView = LanternPrototypeQA.Fovs[1];
            Cam.nearClipPlane = 0.03f;
            Cam.farClipPlane = 60f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color(0.09f, 0.08f, 0.09f);
            Cam.tag = "MainCamera";
            if (Application.isPlaying) camGo.AddComponent<AudioListener>();
#if SHEPHERD_URP
            var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(Cam);
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.renderShadows = true;
#endif
            CollectMaterials();
            if (Viewmodel != null)
            {
                Viewmodel.SetParent(Cam.transform, false);
                Viewmodel.localPosition = Vector3.zero;
                Viewmodel.localRotation = Quaternion.identity;
            }
            lastYaw = Player.Yaw; lastPitch = Player.Pitch;
            ApplyLighting(Lighting.Warm);
            ApplyMaterials(MaterialMode.Painterly);
            SetWireframe(false);
            SyncCamera();
            SettleViewmodel();
            Ready = true;
        }

        private void CollectMaterials()
        {
            renderers.Clear(); painterly.Clear(); urpLit.Clear();
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var gray = new Material(lit) { name = "StyleQA_Graybox" };
            gray.SetColor("_BaseColor", new Color(0.62f, 0.62f, 0.6f));
            gray.SetFloat("_Smoothness", 0.25f);
            grayOne = new[] { gray };
            if (Assets == null) return;
            foreach (var r in Assets.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.GetComponent<StyleQAWireTag>() != null) continue;
                renderers.Add(r);
                var src = r.sharedMaterials;
                painterly.Add(src);
                var copy = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    var m = new Material(lit) { name = src[i].name + "_URPLit" };
                    m.SetTexture("_BaseMap", src[i].GetTexture("_BaseMap"));
                    m.SetColor("_BaseColor", Color.white);
                    m.SetFloat("_Smoothness", src[i].GetFloat("_SpecStrength") > 0f ? 0.6f : 0.2f);
                    m.SetFloat("_Metallic", src[i].GetFloat("_SpecStrength") > 0.5f ? 0.6f : 0f);
                    if (src[i].GetFloat("_Cull") < 1f) m.SetFloat("_Cull", 0f);
                    copy[i] = m;
                }
                urpLit.Add(copy);
            }
        }

        public void ApplyLighting(Lighting l)
        {
            LightMode = l;
            bool warm = l == Lighting.Warm;
            bool night = l == Lighting.Night;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.fog = false;
            if (night)
            {
                RenderSettings.ambientSkyColor = new Color(0.12f, 0.13f, 0.19f);
                RenderSettings.ambientEquatorColor = new Color(0.08f, 0.085f, 0.12f);
                RenderSettings.ambientGroundColor = new Color(0.04f, 0.04f, 0.055f);
            }
            else if (warm)
            {
                RenderSettings.ambientSkyColor = new Color(0.40f, 0.36f, 0.42f);
                RenderSettings.ambientEquatorColor = new Color(0.31f, 0.26f, 0.25f);
                RenderSettings.ambientGroundColor = new Color(0.16f, 0.13f, 0.12f);
            }
            else
            {
                RenderSettings.ambientSkyColor = new Color(0.30f, 0.35f, 0.46f);
                RenderSettings.ambientEquatorColor = new Color(0.22f, 0.24f, 0.30f);
                RenderSettings.ambientGroundColor = new Color(0.10f, 0.10f, 0.12f);
            }
            if (Sun != null)
            {
                Sun.color = warm ? new Color(1f, 0.87f, 0.70f) : new Color(0.70f, 0.80f, 1f);
                Sun.intensity = warm ? 1.85f : 1.6f;
                // 窗戶在 -Z 那面牆：光往 +Z 照進房間。
                Sun.transform.rotation = warm ? Quaternion.Euler(36f, 22f, 0f) : Quaternion.Euler(52f, -12f, 0f);
                Sun.shadows = LightShadows.Soft;
                Sun.enabled = !night;
            }
            if (Lamp != null)
            {
                Lamp.enabled = warm;
                Lamp.color = new Color(1f, 0.78f, 0.5f);
            }
            if (WindowBackdrop != null)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_BaseColor", night ? new Color(0.07f, 0.09f, 0.14f) : warm ? new Color(1.0f, 0.88f, 0.70f) : new Color(0.62f, 0.70f, 0.82f));
                WindowBackdrop.SetPropertyBlock(mpb);
            }
            if (Cam != null) Cam.backgroundColor = night ? new Color(0.02f, 0.02f, 0.03f) : warm ? new Color(0.10f, 0.08f, 0.08f) : new Color(0.07f, 0.08f, 0.10f);
        }

        public void ApplyMaterials(MaterialMode mode)
        {
            MatMode = mode;
            for (int i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i];
                if (mode == MaterialMode.Painterly) r.sharedMaterials = painterly[i];
                else if (mode == MaterialMode.UrpLit) r.sharedMaterials = urpLit[i];
                else
                {
                    var g = new Material[painterly[i].Length];
                    for (int k = 0; k < g.Length; k++) g[k] = grayOne[0];
                    r.sharedMaterials = g;
                }
            }
        }

        public void SetWireframe(bool on)
        {
            Wireframe = on;
            if (on && wires.Count == 0 && WireMaterial != null)
            {
                foreach (var r in renderers)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    if (r.transform.name.StartsWith("SQA_Stage")) continue;
                    var go = new GameObject(r.name + "_Wire");
                    go.AddComponent<StyleQAWireTag>();
                    go.transform.SetParent(r.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = BuildWireMesh(mf.sharedMesh);
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = WireMaterial;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    wires.Add(go);
                }
            }
            foreach (var w in wires) if (w != null) w.SetActive(on);
        }

        /// <summary>線框：三角形的邊，但略過同一平面上兩個三角形之間的對角線（顯示實際的多邊形結構）。</summary>
        public static Mesh BuildWireMesh(Mesh src)
        {
            var v = src.vertices;
            var t = src.triangles;
            var edges = new Dictionary<long, List<Vector3>>();
            var keyPos = new Dictionary<Vector3, int>();
            int Id(Vector3 p)
            {
                var q = new Vector3(Mathf.Round(p.x * 10000f), Mathf.Round(p.y * 10000f), Mathf.Round(p.z * 10000f));
                if (!keyPos.TryGetValue(q, out int id)) { id = keyPos.Count; keyPos[q] = id; }
                return id;
            }
            var ids = new int[v.Length];
            for (int i = 0; i < v.Length; i++) ids[i] = Id(v[i]);
            var first = new Dictionary<long, int>();
            for (int f = 0; f < t.Length; f += 3)
            {
                var n = Vector3.Cross(v[t[f + 1]] - v[t[f]], v[t[f + 2]] - v[t[f]]).normalized;
                for (int e = 0; e < 3; e++)
                {
                    int a = t[f + e], b = t[f + (e + 1) % 3];
                    int ia = ids[a], ib = ids[b];
                    long key = ia < ib ? ((long)ia << 32) | (uint)ib : ((long)ib << 32) | (uint)ia;
                    if (!edges.TryGetValue(key, out var list)) { list = new List<Vector3>(); edges[key] = list; first[key] = a * 1 + 0; }
                    list.Add(n);
                }
            }
            var verts = new List<Vector3>();
            var idx = new List<int>();
            var posOf = new Dictionary<int, Vector3>();
            for (int i = 0; i < v.Length; i++) posOf[ids[i]] = v[i];
            foreach (var kv in edges)
            {
                var ns = kv.Value;
                bool flat = ns.Count == 2 && Vector3.Dot(ns[0], ns[1]) > 0.9995f;
                if (flat) continue;
                int ia = (int)(kv.Key >> 32), ib = (int)(kv.Key & 0xffffffff);
                idx.Add(verts.Count); verts.Add(posOf[ia]);
                idx.Add(verts.Count); verts.Add(posOf[ib]);
            }
            var m = new Mesh { name = src.name + "_Wire", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(verts);
            m.SetIndices(idx, MeshTopology.Lines, 0);
            return m;
        }

        /// <summary>傳送到觀察點：環繞視角（0～3）把牆改成只投影，讓背面也看得到；第一人稱（4、5）牆照常顯示。</summary>
        public void GoToView(int i)
        {
            if (Views == null || i < 0 || i >= Views.Length || Views[i] == null) return;
            ViewIndex = i;
            var t = Views[i];
            SetWallsVisible(i >= 4);
            if (Viewmodel != null) Viewmodel.gameObject.SetActive(ViewmodelVisible && i >= 4);
            if (i < 4)
            {
                // 環繞視角：只移動相機（有些位置在牆外），一走動就回到玩家身上。
                Cam.transform.SetPositionAndRotation(t.position, t.rotation);
                return;
            }
            var fwd = t.forward;
            float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(Mathf.Clamp(fwd.y, -1f, 1f)) * Mathf.Rad2Deg;
            Player.Teleport(t.position - Vector3.up * Player.EyeHeightValue, yaw, pitch);
            SyncCamera();
            SettleViewmodel();
        }

        public void SetViewmodelVisible(bool on)
        {
            ViewmodelVisible = on;
            if (Viewmodel != null) Viewmodel.gameObject.SetActive(on && !(ViewIndex >= 0 && ViewIndex < 4));
        }

        /// <summary>截圖／測試用：直接指定燈的開關與「一直舉著」。</summary>
        public void SetLantern(bool on, bool raised) { LanternOn = on; holdRaise = raised; showTimer = 0f; }

        /// <summary>F：開／關提燈，同時把手舉起來展示一下，ShowSeconds 秒後自動放回去。</summary>
        public void ToggleLantern()
        {
            LanternOn = !LanternOn;
            showTimer = ShowSeconds;
        }

        /// <summary>手舉起來的程度（0＝垂在下面、1＝舉起來）。</summary>
        public float RaiseAmount => raise;

        /// <summary>提燈目前的擺動角度（度；x＝前後、y＝左右）。測試與量測用。</summary>
        public Vector2 SwingDegrees => swing;

        /// <summary>立即套用提燈狀態、不擺動（傳送與截圖用）。</summary>
        public void SettleViewmodel()
        {
            if (Player == null) return;
            lastYaw = Player.Yaw; lastPitch = Player.Pitch;
            swing = swingVel = handLag = handLagVel = Vector2.zero;
            raise = LanternRaised ? 1f : 0f;
            lightK = LanternOn ? 1f : 0f;
            TickViewmodel(0f, 0f);
        }

        /// <summary>
        /// 左手提燈：手跟著鏡頭（走路時輕微上下），提燈從握把垂下、永遠朝下（重力），
        /// 轉頭時往反方向甩、走路時前後晃，用阻尼彈簧回正（和正式的 LanternRig 同一套手感）。
        /// </summary>
        public void TickViewmodel(float dt, float time)
        {
            if (Viewmodel == null || Arm == null || LanternPivot == null || Player == null) return;
            if (dt <= 0f) dt = 1f / 60f;
            showTimer = Mathf.Max(0f, showTimer - dt);
            // 抬手約 0.4 秒、放下約 0.6 秒（放下慢一點比較自然）
            raise = Mathf.MoveTowards(raise, LanternRaised ? 1f : 0f, dt * (LanternRaised ? 2.6f : 1.7f));
            float r = Mathf.SmoothStep(0f, 1f, raise);
            float speed = Player.LastHorizontalSpeed;
            swayPhase += speed * dt / 0.7f * Mathf.PI;
            float walk = Mathf.Clamp01(speed / 1.2f);
            // 走路：手一步一上下、兩步一左右（約 ±1.2 cm／±0.6 cm）；站著：很慢的呼吸起伏（約 ±3 mm）。
            var bob = new Vector3(Mathf.Sin(swayPhase) * 0.006f, -Mathf.Abs(Mathf.Sin(swayPhase)) * 0.012f, 0f) * walk;
            var breath = new Vector3(Mathf.Sin(time * 0.7f) * 0.0015f, Mathf.Sin(time * 1.15f) * 0.003f, 0f) * (1f - walk);
            float dYaw = Mathf.DeltaAngle(lastYaw, Player.Yaw), dPitch = Player.Pitch - lastPitch;
            lastYaw = Player.Yaw; lastPitch = Player.Pitch;
            // 手的慣性：轉視角時手稍微落後一點再跟上（阻尼彈簧），不會像釘在鏡頭上。
            var lagTarget = new Vector2(-dYaw * 0.0016f, dPitch * 0.0016f);
            handLagVel += (Vector2.ClampMagnitude(lagTarget, 0.04f) - handLag) * 90f * dt - handLagVel * 13f * dt;
            handLag += handLagVel * dt;
            // 抬手的路徑不是直線：中途稍微往前、往外帶一點弧度（像真的把燈提到面前）
            var arc = new Vector3(-0.02f, 0.0f, 0.05f) * Mathf.Sin(r * Mathf.PI);
            Arm.localPosition = Vector3.Lerp(HandLowered, HandRaised, r) + arc + bob + breath + new Vector3(handLag.x, handLag.y, 0f);
            // 提燈像鐘擺：轉頭往反方向甩、走路前後晃、站著也有一點點餘擺；阻尼不大，會來回擺幾下才停。
            // 依轉動「速度」甩（度／秒 × 0.1）：每秒轉 120° 約甩 12°。
            float yawRate = dYaw / dt, pitchRate = dPitch / dt;
            var push = new Vector2(pitchRate * 0.08f + Mathf.Sin(swayPhase) * 7f * walk + Mathf.Sin(time * 0.9f) * 1.2f * (1f - walk),
                                   -yawRate * 0.1f + Mathf.Cos(swayPhase * 0.5f) * 3f * walk);
            swingVel += (Vector2.ClampMagnitude(push, 24f) - swing) * 55f * dt - swingVel * 4.5f * dt;
            swing = Vector2.ClampMagnitude(swing + swingVel * dt, 30f);
            // 整組跟著視角（一般第一人稱的做法）：抬頭低頭時手在畫面裡的位置差不多；手保持掌心朝下握著提把，
            // 拳頭隨提燈的左右擺動稍微側傾，提把永遠在拳心裡。
            // 手：舉起來時手背稍微朝外轉一點（自然的腕部角度），放下時掌心朝下。
            // 舉起來時拳頭繞「提把」這條軸往前轉約 40°：手腕落到拳頭下方，前臂自然從下面接上來（手腕不會折）；
            // 提把仍在拳心、提燈仍垂直往下掛（提燈另外算旋轉）。
            var level = Player.ViewRotation * Quaternion.Euler(Mathf.Lerp(0f, -40f, r), Mathf.Lerp(0f, 8f, r), swing.y * 0.3f + Mathf.Lerp(0f, 4f, r));
            var handLevel = Player.ViewRotation * Quaternion.Euler(0f, Mathf.Lerp(0f, 8f, r), swing.y * 0.3f + Mathf.Lerp(0f, 4f, r));
            Arm.rotation = level * Quaternion.Euler(0f, 180f, 0f);
            // 第一人稱常見的做法：手和提燈畫得比真實尺寸小一點（一起縮放，握拳和提把的貼合不變），
            // 拳頭放在畫面左下、整盞燈都看得到（照參考圖的構圖）。前臂只縮粗細、拉長，從畫面角落外面伸進來。
            Arm.localScale = Vector3.one * ViewScale;
            LanternPivot.localScale = Vector3.one * ViewScale;
            if (ForearmRight != null) ForearmRight.localScale = new Vector3(ViewScale, ViewScale, ViewScale * 2.2f);
            if (RightArm != null) RightArm.localScale = Vector3.one * ViewScale;
            LanternPivot.position = Arm.position;
            SolveLeftArm();
            if (RightArm != null)
            {
                // 右手：在畫面右下角，手指微彎（左右對稱的姿勢），走路時和左手反相晃動。
                RightArm.gameObject.SetActive(RightHandVisible);
                RightArm.localPosition = new Vector3(0.25f, -0.2f, 0.5f) + new Vector3(-bob.x, bob.y * 0.8f, 0f);
                RightArm.localRotation = Quaternion.Euler(-30f, -28f, -38f) * Quaternion.Euler(0f, 180f, 0f);
                if (ForearmRight != null) ForearmRight.gameObject.SetActive(RightHandVisible);
                if (RightHandVisible) AimForearm(ForearmRight, RightArm, WristRight, new Vector3(0.42f, -0.8f, 0.2f));
            }
            // 提燈：垂直往下掛（不跟低頭抬頭），只繞「提把」這條軸前後擺——提把在拳心裡轉，不會穿出手指；星形紋那一面朝向玩家。
            LanternPivot.rotation = handLevel * Quaternion.AngleAxis(swing.x, Vector3.right) * Quaternion.Euler(0f, 180f, 0f);
            float breathe = 1f + Mathf.Sin(time * 2.3f) * 0.03f + (Mathf.PerlinNoise(time * 1.7f, 0.3f) - 0.5f) * 0.05f;
            lightK = Mathf.MoveTowards(lightK, LanternOn ? 1f : 0f, dt * 4f);
            if (LanternLight != null)
            {
                LanternLight.enabled = lightK > 0.01f;
                LanternLight.intensity = 1.25f * lightK * breathe;
            }
            if (LanternRenderer != null)
            {
                var mpb = new MaterialPropertyBlock();
                LanternRenderer.GetPropertyBlock(mpb);
                mpb.SetFloat("_Intensity", Mathf.Lerp(0.18f, 1.5f, lightK) * breathe);
                LanternRenderer.SetPropertyBlock(mpb);
            }
        }

        /// <summary>
        /// 左手臂的兩節骨頭 IK：已知肩膀（鏡頭座標固定）和手腕（手的位置），用餘弦定理算出手肘，
        /// 手肘往「外下方」彎（ElbowPole）。前臂從手腕對準手肘、上臂從手肘對準肩膀，長度照算出來的距離伸縮。
        /// 手垂下時手肘約彎 75～90°，舉起來時約 120°，都是自然的手臂角度。
        /// </summary>
        private void SolveLeftArm()
        {
            if (Cam == null || Arm == null) return;
            var cam = Cam.transform;
            float r = Mathf.SmoothStep(0f, 1f, raise);
            ShoulderNow = Vector3.Lerp(Shoulder, ShoulderRaised, r);
            var S = cam.TransformPoint(ShoulderNow);
            var W = Arm.TransformPoint(WristLeft);
            var P = cam.TransformPoint(Vector3.Lerp(ElbowPole, ElbowPoleRaised, r));
            var E = SolveElbow(S, W, P, UpperArmLength, ForearmLength);
            ElbowPosition = E;
            if (ForearmLeft != null)
            {
                var fe = E - W;
                ForearmLeft.position = W;
                ForearmLeft.rotation = Quaternion.LookRotation(fe.normalized, cam.up);
                ForearmLeft.localScale = new Vector3(ArmWidth, ArmWidth, fe.magnitude / ForearmMeshLength);
            }
            if (UpperArmLeft != null)
            {
                var es = S - E;
                UpperArmLeft.position = E;
                UpperArmLeft.rotation = Quaternion.LookRotation(es.normalized, cam.up);
                UpperArmLeft.localScale = new Vector3(ArmWidth, ArmWidth, es.magnitude / UpperArmMeshLength);
            }
        }

        /// <summary>兩節骨頭 IK：肩膀 S、手腕 W、手肘往 P 那一側彎；a＝上臂、b＝前臂長度。</summary>
        public static Vector3 SolveElbow(Vector3 S, Vector3 W, Vector3 P, float a, float b)
        {
            var sw = W - S;
            float d = Mathf.Clamp(sw.magnitude, Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
            var dir = sw.normalized;
            float along = (a * a - b * b + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            var toPole = P - S;
            var perp = (toPole - dir * Vector3.Dot(toPole, dir)).normalized;
            return S + dir * along + perp * h;
        }

        /// <summary>前臂接在手腕上，朝鏡頭座標裡的一個點（畫面下角外面）伸過去（右手的預備姿勢用）。</summary>
        private void AimForearm(Transform forearm, Transform hand, Vector3 wristLocal, Vector3 camTarget)
        {
            if (forearm == null || hand == null || Cam == null) return;
            var wrist = hand.TransformPoint(wristLocal);
            var dir = Cam.transform.TransformPoint(camTarget) - wrist;
            forearm.position = wrist;
            forearm.rotation = Quaternion.LookRotation(dir.normalized, Cam.transform.up);
        }

        public void SetWallsVisible(bool on)
        {
            if (WallRenderers == null) return;
            foreach (var r in WallRenderers)
                if (r != null) r.shadowCastingMode = on ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            if (WindowBackdrop != null) WindowBackdrop.enabled = on;
            if (OrbitFloor != null) OrbitFloor.enabled = !on;
        }

        public void SyncCamera()
        {
            if (Cam == null || Player == null) return;
            Cam.transform.SetPositionAndRotation(Player.EyePosition, Player.ViewRotation);
        }

        private void Update()
        {
            if (!Ready) return;
            cursorUi = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            cursor.UiNeedsCursor = cursorUi;
            cursor.ReportPointer(Input.mousePosition);
            cursor.Tick(Input.GetKeyDown(KeyCode.Escape), Input.GetMouseButtonDown(0), Input.GetMouseButton(0));
            if (Input.GetKeyDown(KeyCode.T)) ApplyLighting((Lighting)(((int)LightMode + 1) % 3));
            if (Input.GetKeyDown(KeyCode.M)) ApplyMaterials((MaterialMode)(((int)MatMode + 1) % 3));
            if (Input.GetKeyDown(KeyCode.G)) SetWireframe(!Wireframe);
            if (Input.GetKeyDown(KeyCode.U)) LabelsVisible = !LabelsVisible;
            for (int k = 0; k < 6; k++) if (Input.GetKeyDown(KeyCode.Alpha1 + k) || Input.GetKeyDown(KeyCode.Keypad1 + k)) GoToView(k);
            var inp = input.Read();
            bool moving = inp.Move.sqrMagnitude > 0.01f;
            if (moving && ViewIndex >= 0 && ViewIndex < 4) { SetWallsVisible(true); ViewIndex = -1; if (Viewmodel != null) Viewmodel.gameObject.SetActive(ViewmodelVisible); }
            if (Input.GetKeyDown(KeyCode.F)) ToggleLantern();
            holdRaise = Input.GetMouseButton(1);
            if (Input.GetKeyDown(KeyCode.H)) SetViewmodelVisible(!ViewmodelVisible);
            if (Input.GetKeyDown(KeyCode.R)) RightHandVisible = !RightHandVisible;
            bool orbit = ViewIndex >= 0 && ViewIndex < 4;
            Player.Step(Time.deltaTime, orbit ? ExploreInput.None : inp, cursor.LookActive && !orbit);
            if (!orbit) SyncCamera();
            TickViewmodel(Time.deltaTime, Time.time);
        }

        private void OnApplicationFocus(bool focus) => cursor.OnFocus(focus);

        private void OnGUI()
        {
            if (!Ready) return;
            if (label == null)
            {
                var font = LanternFonts.UI;
                label = new GUIStyle(GUI.skin.label) { fontSize = 17, font = font };
                label.normal.textColor = new Color(1f, 0.97f, 0.9f);
                hint = new GUIStyle(label) { fontSize = 13 };
                hint.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
                button = new GUIStyle(GUI.skin.button) { fontSize = 14, font = font };
            }
            if (!LabelsVisible) { GUI.Label(new Rect(10, 8, 200, 22), "U 顯示標籤", hint); return; }
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(6, 6, 470, 198), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(14, 10, 460, 24), "正式美術風格 QA（縫紉工作區）", label);
            GUI.Label(new Rect(14, 34, 460, 20), "材質：" + MaterialNames[(int)MatMode] + "　光線：" + LightingNames[(int)LightMode], hint);
            GUI.Label(new Rect(14, 52, 460, 20), "線框：" + (Wireframe ? "開" : "關") + "　觀察點：" + (ViewIndex >= 0 ? ViewNames[ViewIndex] : "自由走動"), hint);
            if (GUI.Button(new Rect(14, 76, 110, 26), "T 光線", button)) ApplyLighting((Lighting)(((int)LightMode + 1) % 3));
            if (GUI.Button(new Rect(130, 76, 110, 26), "M 材質", button)) ApplyMaterials((MaterialMode)(((int)MatMode + 1) % 3));
            if (GUI.Button(new Rect(246, 76, 110, 26), "G 線框", button)) SetWireframe(!Wireframe);
            for (int k = 0; k < 6; k++)
                if (GUI.Button(new Rect(14 + k * 76, 108, 72, 26), (k + 1) + " " + ViewNames[k].Replace("第一人稱・", ""), button)) GoToView(k);
            GUI.Label(new Rect(14, 140, 460, 20), "WASD 走・Shift 快走・Esc 放開滑鼠・按住 Alt 點按鈕・U 隱藏", hint);
            GUI.Label(new Rect(14, 158, 460, 20), "提燈：F 開／關（手會抬起來展示）・按住右鍵一直舉著・H 隱藏手（" + (LanternOn ? "亮" : "熄") + "）", hint);
            GUI.Label(new Rect(14, 176, 460, 20), "這是 Unity 實機畫面（真正的 3D 模型＋即時光影）", hint);
        }
    }
}

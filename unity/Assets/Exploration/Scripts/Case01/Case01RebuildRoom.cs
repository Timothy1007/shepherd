using System.Collections.Generic;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// 全屋重製 v2：新客廳的組裝（與舊 R3／Phase 1.8／1.9 完全分開，不引用舊建築 builder 或舊家具）。
    /// - 模型：Resources/Case01Rebuild/LivingRoom.fbx（tools/blender/case01_rebuild 產生）。命名 A_ 建築（MeshCollider）、F_ 家具（BoxCollider）、D_ 小物、E_ 空物件。
    /// - 材質：每個 RB_* 用 Standard，接 albedo／法線／金屬光滑度三張貼圖；玻璃、鏡面、螢幕、炭火另外處理。
    /// - 燈：檯燈、落地燈、壁爐（會微微跳動）、街燈從窗外斜照、月光、走廊燈、天花吊燈（預設關）；全部是即時燈、柔和陰影，可在 QA 中開關。
    /// - 間接光：編輯器烘焙的環境光貼圖（只有天空環境＋環境光遮蔽，讓牆角、家具底下有接地的陰影），燈本身不烘焙，才能即時開關。
    /// - 反射：房間中央一個反射探針（黃銅、玻璃、鏡面有房間的倒影）。
    /// </summary>
    public sealed class Case01RebuildRoom
    {
        public const string Folder = "Case01Rebuild/";
        public const string RootName = "Case01RebuildLivingRoom";

        public Transform Root { get; private set; }
        public readonly Dictionary<string, Transform> Marks = new Dictionary<string, Transform>();
        public readonly List<Light> Lamps = new List<Light>();
        public readonly List<Light> Outdoor = new List<Light>();
        public Light Pendant { get; private set; }
        public Light Fire { get; private set; }
        public ReflectionProbe Probe { get; private set; }
        public Renderer PhotoPrint { get; private set; }
        public Texture2D PhotoTexture { get; private set; }
        public readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        public int ArchitectureColliders { get; private set; }
        public int FurnitureColliders { get; private set; }

        public static bool Available { get { return Resources.Load<GameObject>(Folder + "LivingRoom") != null; } }

        /// <summary>在場景中找已建好（烘焙過）的客廳，沒有就新建一個。</summary>
        public static Case01RebuildRoom FindOrBuild(Transform parent)
        {
            var existing = GameObject.Find(RootName);
            var r = new Case01RebuildRoom();
            if (existing != null)
            {
                r.Root = existing.transform;
                r.Collect(false);
            }
            else r.Build(parent);
            return r;
        }

        public void Build(Transform parent)
        {
            var prefab = Resources.Load<GameObject>(Folder + "LivingRoom");
            var go = prefab != null ? Object.Instantiate(prefab) : new GameObject();
            go.name = RootName;
            Root = go.transform;
            if (parent != null) Root.SetParent(parent, false);
            Root.position = Vector3.zero;
            Root.rotation = Quaternion.identity;
            Collect(true);
        }

        private void Collect(bool create)
        {
            foreach (var t in Root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("E_")) Marks[t.name] = t;
                var r = t.GetComponent<Renderer>();
                if (r == null) continue;
                if (create) Remap(r);
                if (t.name.StartsWith("A_"))
                {
                    if (create && t.GetComponent<Collider>() == null) t.gameObject.AddComponent<MeshCollider>();
                    ArchitectureColliders++;
                }
                else if (t.name.StartsWith("F_"))
                {
                    if (create && t.GetComponent<Collider>() == null) t.gameObject.AddComponent<BoxCollider>();
                    FurnitureColliders++;
                }
                if (t.name == "D_PhotoPrint") PhotoPrint = r;
                // 燈具本身不投影（燈泡在燈罩裡，會把整面牆打成鋸齒狀的大陰影）；燈罩的亮度由自發光表現。
                if (create && (t.name == "D_TableLamp" || t.name == "D_FloorLamp" || t.name == "D_Pendant" || t.name == "D_CeilingRose" || t.name == "D_Coals")) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            PhotoTexture = Resources.Load<Texture2D>(Folder + "RB_Photo_A");
            if (create)
            {
                AddLights();
                AddProbe();
                AddPhotoHotspot();
            }
            else
            {
                foreach (var l in Root.GetComponentsInChildren<Light>(true))
                {
                    if (l.name == "Pendant") Pendant = l;
                    else if (l.name == "Fire")
                    {
                        Fire = l;
                        Lamps.Add(l);
                    }
                    else if (l.name == "Street" || l.name == "Moon") Outdoor.Add(l);
                    else if (l.name != "Hall") Lamps.Add(l);
                }
                Probe = Root.GetComponentInChildren<ReflectionProbe>(true);
                if (PhotoPrint != null) foreach (var m in PhotoPrint.sharedMaterials) if (m != null) Materials["RB_Photo"] = m;
            }
        }

        // ---------------- 材質 ----------------

        private void Remap(Renderer r)
        {
            var ms = r.sharedMaterials;
            for (int i = 0; i < ms.Length; i++)
            {
                if (ms[i] == null) continue;
                var n = ms[i].name.Replace(" (Instance)", "");
                if (!n.StartsWith("RB_")) continue;
                ms[i] = Get(n);
            }
            r.sharedMaterials = ms;
        }

        public Material Get(string n)
        {
            Material m;
            if (Materials.TryGetValue(n, out m) && m != null) return m;
            m = Create(n);
            Materials[n] = m;
            return m;
        }

        public static Material Create(string n)
        {
            var m = new Material(Shader.Find("Standard")) { name = n };
            switch (n)
            {
                case "RB_Glass":
                    m.color = new Color(0.55f, 0.6f, 0.62f, 0.16f);
                    SetFade(m);
                    m.SetFloat("_Glossiness", 0.96f);
                    return m;
                case "RB_Mirror":
                    // 舊鏡面：有反射探針時映出房間；沒有時也是暗灰的舊玻璃，不會變成一塊黑板。
                    m.color = new Color(0.42f, 0.43f, 0.42f);
                    m.SetFloat("_Metallic", 0.75f);
                    m.SetFloat("_Glossiness", 0.9f);
                    return m;
                case "RB_Screen":
                    m.color = new Color(0.01f, 0.012f, 0.015f);
                    m.SetFloat("_Glossiness", 0.88f);
                    return m;
                case "RB_Plastic":
                    m.color = new Color(0.035f, 0.035f, 0.04f);
                    m.SetFloat("_Glossiness", 0.55f);
                    return m;
                case "RB_Paper":
                    m.color = new Color(0.84f, 0.82f, 0.75f);
                    m.mainTexture = Resources.Load<Texture2D>(Folder + "RB_Ceiling_A");
                    m.SetFloat("_Glossiness", 0.15f);
                    return m;
                case "RB_Coal":
                    m.color = new Color(0.06f, 0.04f, 0.03f);
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(0.9f, 0.3f, 0.06f));
                    m.SetFloat("_Glossiness", 0.2f);
                    return m;
                case "RB_Photo":
                    m.mainTexture = Resources.Load<Texture2D>(Folder + "RB_Photo_A");
                    m.SetFloat("_Glossiness", 0.45f);
                    return m;
            }
            var a = Resources.Load<Texture2D>(Folder + n + "_A");
            var nm = Resources.Load<Texture2D>(Folder + n + "_N");
            var ms = Resources.Load<Texture2D>(Folder + n + "_MS");
            m.mainTexture = a;
            if (nm != null)
            {
                m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_BumpMap", nm);
                m.SetFloat("_BumpScale", 1f);
            }
            if (ms != null)
            {
                m.EnableKeyword("_METALLICGLOSSMAP");
                m.SetTexture("_MetallicGlossMap", ms);
                m.SetFloat("_GlossMapScale", 1f);
            }
            if (n == "RB_Shade")
            {
                // 燈罩：亮著時透出暖光（自發光很弱，主要靠燈本身）。
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.35f, 0.24f, 0.12f));
                m.SetTexture("_EmissionMap", a);
            }
            return m;
        }

        private static void SetFade(Material m)
        {
            m.SetFloat("_Mode", 2f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_ALPHABLEND_ON");
            m.renderQueue = 3000;
        }

        // ---------------- 燈 ----------------

        private Light Lamp(string name, string mark, LightType type, Color c, float intensity, float range, LightShadows shadows, float spot = 0f, Vector3? dir = null)
        {
            Transform t;
            var pos = Marks.TryGetValue(mark, out t) ? t.position : Vector3.up * 2f;
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            if (dir.HasValue) go.transform.rotation = Quaternion.LookRotation(dir.Value);
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows;
            l.shadowStrength = 0.9f;
            l.shadowBias = 0.03f;
            l.shadowNormalBias = 0.3f;
            l.shadowNearPlane = 0.1f;
            if (type == LightType.Spot) l.spotAngle = spot;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            return l;
        }

        private void AddLights()
        {
            var warm = new Color(1f, 0.72f, 0.45f);
            Lamps.Add(Lamp("TableLamp", "E_TableLamp", LightType.Point, warm, 2.3f, 6.5f, LightShadows.Soft));
            Lamps.Add(Lamp("FloorLamp", "E_FloorLamp", LightType.Point, new Color(1f, 0.76f, 0.52f), 2.1f, 7f, LightShadows.Soft));
            Fire = Lamp("Fire", "E_FireLight", LightType.Point, new Color(1f, 0.45f, 0.16f), 1.7f, 4.5f, LightShadows.Soft);
            Lamps.Add(Fire);
            Pendant = Lamp("Pendant", "E_Pendant", LightType.Point, new Color(1f, 0.82f, 0.62f), 2.0f, 7.5f, LightShadows.Soft);
            Pendant.enabled = false;
            Lamp("Hall", "E_HallLamp", LightType.Point, new Color(1f, 0.8f, 0.6f), 0.8f, 3.5f, LightShadows.None);
            // 窗外的街燈：鈉燈般的暖橘斜照進來，窗格的影子落在地板與牆上。
            Transform lamp, win;
            var street = Lamp("Street", "E_StreetLamp", LightType.Spot, new Color(1f, 0.66f, 0.36f), 3.2f, 12f, LightShadows.Soft, 62f);
            if (Marks.TryGetValue("E_StreetLamp", out lamp) && Marks.TryGetValue("E_MirrorProbe", out win))
                street.transform.rotation = Quaternion.LookRotation((win.position + Vector3.down * 1.2f - lamp.position).normalized);
            Outdoor.Add(street);
            var moon = Lamp("Moon", "E_Moon", LightType.Directional, new Color(0.55f, 0.65f, 0.95f), 0.22f, 0f, LightShadows.Soft);
            if (Marks.TryGetValue("E_Moon", out lamp) && Marks.TryGetValue("E_MirrorProbe", out win)) moon.transform.rotation = Quaternion.LookRotation((win.position + Vector3.down * 1.4f - lamp.position).normalized);
            Outdoor.Add(moon);
        }

        private void AddProbe()
        {
            var go = new GameObject("RoomProbe");
            go.transform.SetParent(Root, false);
            Transform t;
            go.transform.position = Marks.TryGetValue("E_MirrorProbe", out t) ? t.position : new Vector3(1.8f, 1.4f, 2f);
            Probe = go.AddComponent<ReflectionProbe>();
            Probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            Probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            Probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            Probe.size = new Vector3(5f, 3f, 5.5f);
            Probe.boxProjection = true;
            Probe.resolution = 256;
            Probe.intensity = 1f;
        }

        private void AddPhotoHotspot()
        {
            if (PhotoPrint == null) return;
            var go = new GameObject("Interact_Photo");
            go.transform.SetParent(Root, false);
            go.transform.position = PhotoPrint.bounds.center;
            var b = go.AddComponent<BoxCollider>();
            b.isTrigger = true;
            b.size = PhotoPrint.bounds.size + new Vector3(0.12f, 0.12f, 0.12f);
        }

        public void RefreshProbe()
        {
            if (Probe != null) Probe.RenderProbe();
        }

        /// <summary>環境燈（檯燈、落地燈、壁爐）開關。</summary>
        public void SetLamps(bool on)
        {
            foreach (var l in Lamps) if (l != null) l.enabled = on;
            foreach (var r in Root.GetComponentsInChildren<Renderer>())
                foreach (var m in r.sharedMaterials)
                    if (m != null && (m.name == "RB_Shade" || m.name == "RB_Coal")) m.SetColor("_EmissionColor", on ? (m.name == "RB_Coal" ? new Color(0.9f, 0.3f, 0.06f) : new Color(0.35f, 0.24f, 0.12f)) : Color.black);
        }

        /// <summary>環境光（三向：天花偏冷、牆面中性、地面偏暖），讓暗處仍看得出家具與牆面的層次，不是一片黑。</summary>
        public static void ApplyAmbient(float k)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.3f, 0.31f, 0.36f) * k;
            RenderSettings.ambientEquatorColor = new Color(0.25f, 0.23f, 0.21f) * k;
            RenderSettings.ambientGroundColor = new Color(0.15f, 0.12f, 0.09f) * k;
        }

        public void Tick(float t)
        {
            if (Fire != null && Fire.enabled) Fire.intensity = 1.5f + 0.3f * Mathf.PerlinNoise(t * 2.7f, 0.3f) + 0.1f * Mathf.PerlinNoise(t * 9f, 1.7f);
        }
    }
}

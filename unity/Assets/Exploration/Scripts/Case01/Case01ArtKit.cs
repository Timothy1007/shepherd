using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// v0.7 共用的美術套件：載入 Blender 程序模型（客廳 v0.6 的 Case01LivingRoom 與 Phase 1 的 Case01Phase1 兩個 Resources 資料夾），
    /// 依 FBX 材質名稱換成本套材質（同一組木材／布料／金屬，各場景色相與亮度一致），並提供「換皮」：
    /// 隱藏舊物件的 Renderer、把新模型掛在同一個 Transform 底下——碰撞、熱區與會移動的物件（餐椅、餐具）都保留。
    /// </summary>
    public sealed class Case01ArtKit
    {
        public const string LivingFolder = "Case01LivingRoom/";
        public const string Phase1Folder = "Case01Phase1/";

        private readonly Case01Palette palette;
        private readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        private readonly List<Renderer> hidden = new List<Renderer>();
        private readonly List<GameObject> added = new List<GameObject>();

        public bool Active { get; private set; } = true;
        public int SkinCount { get { return added.Count; } }
        public IList<GameObject> Added { get { return added; } }

        public static bool Available
        {
            get { return Resources.Load<GameObject>(Phase1Folder + "KitchenBase") != null && Resources.Load<GameObject>(LivingFolder + "Sofa") != null; }
        }

        public Case01ArtKit(Case01Palette p)
        {
            palette = p;
            T("LR_WoodMid", 0.32f);
            T("LR_WoodDark", 0.36f);
            T("LR_SofaFabric", 0.06f);
            T("LR_CushionCream", 0.05f);
            T("LR_CushionRed", 0.05f);
            T("LR_Paint", 0.18f);
            T("LR_Brass", 0.55f, 0.75f);
            T("LR_Ceramic", 0.45f);
            T("LR_Iron", 0.38f, 0.35f);
            T("LR_Linen", 0.05f);
            T("LR_Wallpaper", 0.08f);
            T("LR_Floor", 0.28f);
            T("LR_Ceiling", 0.05f);
            T("LR_Painting", 0.1f);
            T("LR_Plaid", 0.05f);
            T("P1_Tile", 0.3f);
            T("P1_Pavement", 0.15f);
            T("P1_Brick", 0.08f);
            T("P1_Road", 0.2f);
            T("P1_Slate", 0.35f);
            T("P1_Render", 0.1f);
            T("P1_WoodPaint", 0.25f);
            T("P1_KitchenUnit", 0.3f);
            T("P1_Worktop", 0.35f);
            T("P1_Steel", 0.6f, 0.7f);
            T("P1_Enamel", 0.45f);
            T("P1_Leather", 0.35f);
            T("P1_Bedding", 0.05f);
            T("P1_Mattress", 0.05f);
            T("P1_DoorGreen", 0.4f);
            T("P1_Metal", 0.4f, 0.5f);
            T("P1_Stone", 0.1f);
            T("P1_Coat", 0.05f);
            T("P1_CoatRed", 0.05f);
            Plain("P1_GlassDark", new Color(0.05f, 0.06f, 0.08f), 0.92f);
            Plain("LR_Plastic", new Color(0.07f, 0.07f, 0.08f), 0.55f);
            Plain("LR_Screen", new Color(0.02f, 0.025f, 0.03f), 0.92f);
            var lantern = palette.Own(ArenaArt.Mat(new Color(1f, 0.85f, 0.6f), 0.2f, new Color(2.0f, 1.45f, 0.8f)));
            lantern.name = "P1_Lantern";
            mats["P1_Lantern"] = lantern;
        }

        private Material T(string name, float smooth, float metal = 0f)
        {
            var folder = name.StartsWith("P1_") ? Phase1Folder : LivingFolder;
            var m = palette.Own(ArenaArt.Mat(Color.white, smooth, null, metal));
            m.name = name;
            m.mainTexture = Resources.Load<Texture2D>(folder + name);
            mats[name] = m;
            return m;
        }

        private void Plain(string name, Color c, float smooth)
        {
            var m = palette.Own(ArenaArt.Mat(c, smooth));
            m.name = name;
            mats[name] = m;
        }

        public Material Get(string name)
        {
            Material m;
            return mats.TryGetValue(name, out m) ? m : null;
        }

        /// <summary>同一張貼圖、指定平鋪次數的材質副本（長檯面、牆面、地板方片用）。</summary>
        public Material Tiled(string name, Vector2 tiling)
        {
            var m = palette.Own(new Material(Get(name)) { name = name + "_tiled" });
            m.mainTextureScale = tiling;
            return m;
        }

        /// <summary>實例化模型並依材質名稱換成本套材質。child：只取 FBX 裡的某個物件。</summary>
        public Transform Place(string model, Transform parent, Vector3 localPos, float localYaw, string child = null)
        {
            var prefab = Resources.Load<GameObject>((Resources.Load<GameObject>(Phase1Folder + model) != null ? Phase1Folder : LivingFolder) + model);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab);
            go.name = model;
            var t = go.transform;
            if (child != null)
            {
                var c = t.Find(child);
                c.SetParent(null, false);
                ArenaArt.Kill(go);
                t = c;
                go = c.gameObject;
            }
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localRotation = Quaternion.Euler(0f, localYaw, 0f);
            Remap(go);
            go.layer = parent != null ? parent.gameObject.layer : 0;
            foreach (var c in go.GetComponentsInChildren<Transform>(true)) c.gameObject.layer = go.layer;
            return t;
        }

        public void Remap(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    if (ms[i] == null) continue;
                    Material m;
                    if (mats.TryGetValue(ms[i].name.Replace(" (Instance)", ""), out m)) ms[i] = m;
                }
                r.sharedMaterials = ms;
            }
        }

        /// <summary>把模型所有材質換成同一個（例如皮革版的椅墊）。onlySlot：只換名稱相符的材質。</summary>
        public void Recolor(Transform t, string fromMat, string toMat)
        {
            var to = Get(toMat);
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                    if (ms[i] != null && ms[i].name.StartsWith(fromMat)) ms[i] = to;
                r.sharedMaterials = ms;
            }
        }

        /// <summary>換皮：隱藏 target 底下的舊 Renderer（keep 名稱除外），把新模型掛在 target 下。回傳新模型。</summary>
        public Transform Skin(Transform target, string model, Vector3 localPos, float localYaw, Vector3? scale = null, params string[] keep)
        {
            if (target == null) return null;
            HideOld(target, keep);
            var t = Place(model, target, localPos, localYaw);
            if (t == null) return null;
            if (scale.HasValue)
            {
                // 換算到 target 的本地縮放（舊物件常用縮放過的 Primitive）。
                var ls = target.lossyScale;
                var s = scale.Value;
                t.localScale = new Vector3(s.x / Mathf.Max(1e-4f, ls.x), s.y / Mathf.Max(1e-4f, ls.y), s.z / Mathf.Max(1e-4f, ls.z));
            }
            else
            {
                var ls = target.lossyScale;
                t.localScale = new Vector3(1f / Mathf.Max(1e-4f, ls.x), 1f / Mathf.Max(1e-4f, ls.y), 1f / Mathf.Max(1e-4f, ls.z));
            }
            added.Add(t.gameObject);
            t.gameObject.SetActive(Active);
            return t;
        }

        /// <summary>只隱藏舊外觀（不加新模型）。</summary>
        public void HideOld(Transform target, params string[] keep)
        {
            foreach (var r in target.GetComponentsInChildren<Renderer>(true))
            {
                bool k = false;
                foreach (var n in keep)
                {
                    if (r.name == n) { k = true; break; }
                    var kc = FindChild(target, n);
                    if (kc != null && r.transform.IsChildOf(kc)) { k = true; break; }
                }
                if (k || hidden.Contains(r)) continue;
                hidden.Add(r);
                r.enabled = !Active;
            }
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        /// <summary>新增純裝飾（不換皮）。</summary>
        public Transform Add(string model, Transform parent, Vector3 localPos, float localYaw)
        {
            var t = Place(model, parent, localPos, localYaw);
            if (t != null)
            {
                added.Add(t.gameObject);
                t.gameObject.SetActive(Active);
            }
            return t;
        }

        public void Register(GameObject go)
        {
            added.Add(go);
            go.SetActive(Active);
        }

        /// <summary>新（true）／舊（false）切換：舊 Renderer 與新模型互換顯示。</summary>
        public void SetActive(bool art)
        {
            Active = art;
            foreach (var r in hidden) if (r != null) r.enabled = !art;
            foreach (var g in added) if (g != null) g.SetActive(art);
        }

        /// <summary>單面方片（法線 normal），UV 以公尺平鋪。</summary>
        public Transform Quad(Transform parent, string name, Vector3 center, Vector3 normal, Vector3 up, float w, float h, Material m, float tile, Vector2 uvOrigin)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.LookRotation(-normal, up);
            var mesh = new Mesh { name = name };
            float hw = w / 2, hh = h / 2;
            mesh.vertices = new[] { new Vector3(-hw, -hh, 0), new Vector3(hw, -hh, 0), new Vector3(hw, hh, 0), new Vector3(-hw, hh, 0) };
            var o = uvOrigin / tile;
            mesh.uv = new[] { o, o + new Vector2(w / tile, 0), o + new Vector2(w / tile, h / tile), o + new Vector2(0, h / tile) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.layer = parent != null ? parent.gameObject.layer : 0;
            Register(go);
            return go.transform;
        }

        /// <summary>依矩形範圍貼牆面：對每段現有的牆／門楣／窗裙（Renderer 範圍）在房間那一側貼一片，門窗洞自然留空。</summary>
        public int DressWalls(Transform wallsRoot, Transform parent, float x0, float x1, float z0, float z1, float y0, float ceiling, Material m)
        {
            int n = 0;
            foreach (var rend in wallsRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                var nm = rend.name;
                if (nm != "Wall" && nm != "Lintel" && nm != "WindowHead" && nm != "WindowApron") continue;
                var b = rend.bounds;
                float ya = Mathf.Max(y0, b.min.y), yb = Mathf.Min(ceiling, b.max.y);
                if (yb - ya < 0.01f) continue;
                bool w = Mathf.Abs(b.max.x - x0) < 0.02f, e = Mathf.Abs(b.min.x - x1) < 0.02f;
                bool s = Mathf.Abs(b.max.z - z0) < 0.02f, nn = Mathf.Abs(b.min.z - z1) < 0.02f;
                if (w || e)
                {
                    float za = Mathf.Max(z0, b.min.z), zb = Mathf.Min(z1, b.max.z);
                    if (zb - za < 0.01f) continue;
                    Quad(parent, "WallDress", new Vector3(w ? x0 + 0.004f : x1 - 0.004f, (ya + yb) / 2, (za + zb) / 2), w ? Vector3.right : Vector3.left, Vector3.up, zb - za, yb - ya, m, 1f,
                        new Vector2(w ? za : -zb, ya));
                    n++;
                }
                else if (s || nn)
                {
                    float xa = Mathf.Max(x0, b.min.x), xb = Mathf.Min(x1, b.max.x);
                    if (xb - xa < 0.01f) continue;
                    Quad(parent, "WallDress", new Vector3((xa + xb) / 2, (ya + yb) / 2, s ? z0 + 0.004f : z1 - 0.004f), s ? Vector3.forward : Vector3.back, Vector3.up, xb - xa, yb - ya, m, 1f,
                        new Vector2(s ? -xb : xa, ya));
                    n++;
                }
            }
            return n;
        }
    }
}

using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration.Case01
{
    /// <summary>檢視物件的種類（共用架構）：本輪實作 Photo；Document（多頁）、Object3D（實物旋轉）、Screen（電視／電腦畫面）預留。</summary>
    public enum Case01InspectKind { Photo, Document, Object3D, Screen }

    /// <summary>
    /// 一件可以拿起近看的物件：世界中的載體（拿起時隱藏）、正面版本（例如正常／異常）、背面、實際尺寸、背面手寫字。
    /// 新增文件、書、電視時只要多一種 Kind 的建構方式；檢視台、鏡頭、操作與返回都共用。
    /// </summary>
    public sealed class Case01InspectItem
    {
        public string Id, Title;
        public Case01InspectKind Kind = Case01InspectKind.Photo;
        public Renderer WorldCarrier;
        public readonly List<Texture2D> Fronts = new List<Texture2D>();
        public int Variant;
        public Texture2D Back;
        public string BackWriting;
        /// <summary>實際尺寸（公尺，寬×高）。</summary>
        public Vector2 Size = new Vector2(0.15f, 0.1125f);
        public Texture2D Front { get { return Fronts.Count == 0 ? null : Fronts[Mathf.Clamp(Variant, 0, Fronts.Count - 1)]; } }
    }

    /// <summary>
    /// 近看檢視：E 拿起 → 物件從世界位置被拿到眼前（短暫推近）→ 在獨立的檢視台上以專用燈光呈現（不受手電筒或房間燈過曝影響），
    /// 背景房間調暗但仍看得到 → 滑鼠傾斜、滾輪放大、放大後左鍵拖曳移動、空白鍵翻面 → E／右鍵放回原位、恢復探索。
    /// 檢視台在世界遠處（y = −300），只由檢視鏡頭（第 27 層）看到，主鏡頭照常渲染房間。檢視期間停止行走（由 QA 的輸入過濾負責）。
    /// </summary>
    public sealed class Case01Inspector
    {
        public const int Layer = 27;
        public static readonly Vector3 StagePos = new Vector3(0f, -300f, 0f);
        public const float MaxZoom = 3.5f;

        public bool Active { get; private set; }
        public Case01InspectItem Item { get; private set; }
        public bool Flipped { get; private set; }
        public float Zoom { get; private set; } = 1f;
        public Vector2 Pan { get; private set; }
        public Vector2 Tilt { get; private set; }
        /// <summary>拿起／放回的過場進度（0～1）。</summary>
        public float Blend { get; private set; }
        public Camera StageCamera { get { return cam; } }
        public Transform Sheet { get { return sheet; } }
        public int Opened { get; private set; }

        private readonly Case01Palette p;
        private Transform root, sheet, pivot;
        private Camera cam;
        private Material frontMat, backMat;
        private Text backWriting;
        private Light key, fill, rim;
        private float flipAngle, flipTarget;
        private bool closing;
        private Canvas hintCanvas;
        private Text hint;
        private Transform dim;
        private Material dimMat;

        public Case01Inspector(Case01Palette palette, Transform parent)
        {
            p = palette;
            root = ArenaArt.Group("InspectStage", parent, Vector3.zero);
            root.position = StagePos;
            // 檢視鏡頭：只畫第 27 層，在主鏡頭之後（只清深度）。
            var cgo = new GameObject("InspectCamera");
            cgo.transform.SetParent(root, false);
            cam = cgo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Depth;
            cam.cullingMask = 1 << Layer;
            cam.depth = 50;
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 5f;
            cam.enabled = false;
            // 背景調暗：鏡頭前的半透明黑幕（房間仍隱約可見）。
            dimMat = p.Own(ArenaArt.FadeMat(new Color(0.02f, 0.02f, 0.025f, 0.62f)));
            dim = ArenaArt.Prim(PrimitiveType.Quad, cgo.transform, new Vector3(0f, 0f, 1.5f), new Vector3(3f, 3f, 1f), dimMat, null, "Dim", false, false);
            // 燈：柔和的主光（偏暖、斜上方）、補光、背面邊緣光。只照第 27 層。
            key = StageLight("Key", new Vector3(-0.35f, 0.4f, -0.2f), new Color(1f, 0.95f, 0.88f), 1.25f, LightType.Spot);
            fill = StageLight("Fill", new Vector3(0.45f, -0.1f, -0.35f), new Color(0.8f, 0.86f, 1f), 0.45f, LightType.Point);
            rim = StageLight("Rim", new Vector3(0f, 0.2f, 0.45f), new Color(1f, 0.95f, 0.9f), 0.35f, LightType.Point);
            pivot = ArenaArt.Group("Pivot", root, Vector3.zero);
            SetLayer(root);
            root.gameObject.SetActive(false);
        }

        private Light StageLight(string name, Vector3 pos, Color c, float intensity, LightType type)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            go.transform.LookAt(root.position);
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = c;
            l.intensity = intensity;
            l.range = 2.5f;
            l.spotAngle = 60f;
            l.cullingMask = 1 << Layer;
            l.shadows = LightShadows.None;
            return l;
        }

        private static void SetLayer(Transform t)
        {
            foreach (var x in t.GetComponentsInChildren<Transform>(true)) x.gameObject.layer = Layer;
        }

        // ---------------- 相片：雙面、微微彎曲的相紙 ----------------

        private void BuildPhoto(Case01InspectItem it)
        {
            if (sheet != null) ArenaArt.Kill(sheet.gameObject);
            sheet = ArenaArt.Group("Print", pivot, Vector3.zero);
            frontMat = p.Own(ArenaArt.Mat(Color.white, 0.62f));
            frontMat.mainTexture = it.Front;
            backMat = p.Own(ArenaArt.Mat(Color.white, 0.12f));
            backMat.mainTexture = it.Back;
            var edge = p.Own(ArenaArt.Mat(new Color(0.93f, 0.91f, 0.86f), 0.2f));
            var go = new GameObject("PrintMesh");
            go.transform.SetParent(sheet, false);
            go.AddComponent<MeshFilter>().sharedMesh = PrintMesh(it.Size, 0.0009f);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { frontMat, backMat, edge };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // 背面的手寫字（鉛筆）：世界畫布，貼在背面。
            if (!string.IsNullOrEmpty(it.BackWriting))
            {
                var holder = ArenaArt.Group("BackWritingHolder", sheet, new Vector3(0f, 0f, 0.0012f));
                holder.localRotation = Quaternion.Euler(0f, 180f, 0f);
                var q = ArenaArt.Prim(PrimitiveType.Quad, holder, new Vector3(-it.Size.x * 0.12f, -it.Size.y * 0.18f, 0f), new Vector3(it.Size.x * 0.62f, it.Size.y * 0.42f, 1f), backMat, null, "WritingArea", false, false);
                q.GetComponent<Renderer>().enabled = false;
                Case01Graphics.Write(q, it.BackWriting, new Color(0.25f, 0.24f, 0.26f, 0.92f), 0.04f, true, 30, TextAnchor.UpperLeft);
                backWriting = q.GetComponentInChildren<Text>();
            }
            SetLayer(sheet);
        }

        /// <summary>相紙網格：前面（材質 0）、背面（1）、四邊（2）；整張沿寬度方向有極輕微的彎曲（相紙放久了的弧度）。</summary>
        public static Mesh PrintMesh(Vector2 size, float thickness)
        {
            int nx = 16, ny = 12;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var n = new List<Vector3>();
            var front = new List<int>();
            var back = new List<int>();
            var edges = new List<int>();
            System.Func<float, float> bend = x => 0.004f * (1f - Mathf.Pow(x * 2f / size.x, 2f));
            for (int side = 0; side < 2; side++)
            {
                int start = v.Count;
                float z = side == 0 ? -thickness * 0.5f : thickness * 0.5f;
                for (int j = 0; j <= ny; j++)
                    for (int i = 0; i <= nx; i++)
                    {
                        float x = (i / (float)nx - 0.5f) * size.x, y = (j / (float)ny - 0.5f) * size.y;
                        v.Add(new Vector3(x, y, z - bend(x)));
                        uv.Add(new Vector2(side == 0 ? i / (float)nx : 1f - i / (float)nx, j / (float)ny));
                        n.Add(side == 0 ? Vector3.back : Vector3.forward);
                    }
                for (int j = 0; j < ny; j++)
                    for (int i = 0; i < nx; i++)
                    {
                        int a = start + j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                        if (side == 0) front.AddRange(new[] { a, c, b, b, c, d });
                        else back.AddRange(new[] { a, b, c, b, d, c });
                    }
            }
            // 四邊。
            int per = (nx + 1) * (ny + 1);
            System.Action<int, int> quad = (a, b) =>
            {
                int s = v.Count;
                v.Add(v[a]); v.Add(v[b]); v.Add(v[b + per]); v.Add(v[a + per]);
                var dir = (v[s + 1] - v[s]).normalized;
                var nn = Vector3.Cross(dir, Vector3.forward).normalized;
                for (int k = 0; k < 4; k++)
                {
                    n.Add(nn);
                    uv.Add(Vector2.zero);
                }
                edges.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            };
            for (int i = 0; i < nx; i++)
            {
                quad(i + 1, i);
                quad(ny * (nx + 1) + i, ny * (nx + 1) + i + 1);
            }
            for (int j = 0; j < ny; j++)
            {
                quad(j * (nx + 1), (j + 1) * (nx + 1));
                quad((j + 1) * (nx + 1) + nx, j * (nx + 1) + nx);
            }
            var m = new Mesh { name = "PrintSheet" };
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.subMeshCount = 3;
            m.SetTriangles(front, 0);
            m.SetTriangles(back, 1);
            m.SetTriangles(edges, 2);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // ---------------- 開始／結束 ----------------

        public void Begin(Case01InspectItem it, Camera main)
        {
            Item = it;
            Active = true;
            closing = false;
            Blend = 0f;
            Zoom = 1f;
            Pan = Vector2.zero;
            Tilt = Vector2.zero;
            Flipped = false;
            flipAngle = flipTarget = 0f;
            Opened++;
            BuildPhoto(it);
            if (it.WorldCarrier != null) it.WorldCarrier.enabled = false;
            root.gameObject.SetActive(true);
            cam.enabled = true;
            if (main != null) cam.fieldOfView = Mathf.Min(main.fieldOfView, 34f);
            EnsureHint();
            Apply();
        }

        /// <summary>放回：短暫的放下過場後，世界中的載體重新出現。</summary>
        public void End()
        {
            if (!Active) return;
            closing = true;
        }

        private void Finish()
        {
            Active = false;
            closing = false;
            cam.enabled = false;
            root.gameObject.SetActive(false);
            if (Item != null && Item.WorldCarrier != null) Item.WorldCarrier.enabled = true;
            if (hintCanvas != null) hintCanvas.enabled = false;
        }

        public void Flip()
        {
            if (!Active || closing) return;
            Flipped = !Flipped;
            flipTarget = Flipped ? 180f : 0f;
        }

        public void SetVariant(int v)
        {
            if (Item == null) return;
            Item.Variant = Mathf.Clamp(v, 0, Mathf.Max(0, Item.Fronts.Count - 1));
            if (frontMat != null) frontMat.mainTexture = Item.Front;
        }

        /// <summary>滑鼠移動（未放大＝傾斜；放大後按住左鍵＝移動）、滾輪縮放。</summary>
        public void Feed(Vector2 mouse, float wheel, bool drag)
        {
            if (!Active || closing) return;
            if (Mathf.Abs(wheel) > 0.001f) Zoom = Mathf.Clamp(Zoom * (1f + wheel * 0.18f), 1f, MaxZoom);
            if (drag && Zoom > 1.05f)
            {
                var lim = Item.Size * 0.5f * (1f - 1f / Zoom);
                Pan = new Vector2(Mathf.Clamp(Pan.x - mouse.x * 0.004f, -lim.x, lim.x), Mathf.Clamp(Pan.y - mouse.y * 0.004f, -lim.y, lim.y));
            }
            else Tilt = new Vector2(Mathf.Clamp(Tilt.x + mouse.x * 1.4f, -24f, 24f), Mathf.Clamp(Tilt.y + mouse.y * 1.4f, -18f, 18f));
            if (Zoom <= 1.05f) Pan = Vector2.MoveTowards(Pan, Vector2.zero, 0.01f);
        }

        public void Tick(float dt)
        {
            if (!Active) return;
            Blend = Mathf.MoveTowards(Blend, closing ? 0f : 1f, dt / 0.28f);
            flipAngle = Mathf.MoveTowards(flipAngle, flipTarget, dt * 520f);
            // 傾斜慢慢回正（放開滑鼠時相紙回到正對）。
            Tilt = Vector2.Lerp(Tilt, Vector2.zero, 1f - Mathf.Exp(-dt * 1.8f));
            Apply();
            if (closing && Blend <= 0f) Finish();
            if (hint != null && Active)
            {
                hint.text = Zoom > 1.05f ? "滾輪 縮放｜按住左鍵拖曳 移動｜空白鍵 翻面｜E 放回" : "滑鼠 傾斜｜滾輪 放大｜空白鍵 翻面｜E 放回";
                hint.color = new Color(0.9f, 0.88f, 0.82f, 0.75f * Blend);
            }
        }

        private void Apply()
        {
            if (Item == null || sheet == null) return;
            float h = Item.Size.y;
            float dist = h / 0.72f * 0.5f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float e = Blend * Blend * (3f - 2f * Blend);
            cam.transform.localPosition = new Vector3(Pan.x, Pan.y, -dist / Zoom);
            cam.transform.localRotation = Quaternion.identity;
            // 拿起：從畫面下方、斜著、較小，推到眼前正對。
            pivot.localPosition = new Vector3(0f, Mathf.Lerp(-h * 0.9f, 0f, e), Mathf.Lerp(dist * 0.6f, 0f, e));
            pivot.localRotation = Quaternion.Euler(Mathf.Lerp(38f, 0f, e) - Tilt.y, Tilt.x + flipAngle, Mathf.Lerp(-8f, 0f, e));
            dimMat.color = new Color(0.02f, 0.02f, 0.025f, 0.62f * e);
            dim.localPosition = new Vector3(0f, 0f, dist / Zoom + 0.4f);
            key.transform.localPosition = new Vector3(-0.35f, 0.4f, -0.25f);
            key.transform.LookAt(root.position);
        }

        private void EnsureHint()
        {
            if (hintCanvas == null)
            {
                var go = new GameObject("InspectHint", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                go.transform.SetParent(root.parent, false);
                hintCanvas = go.GetComponent<Canvas>();
                hintCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                hintCanvas.sortingOrder = 45;
                var sc = go.GetComponent<CanvasScaler>();
                sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                sc.referenceResolution = new Vector2(1920f, 1080f);
                sc.matchWidthOrHeight = 0.5f;
                var r = Case01SpatialQA.Rect((RectTransform)go.transform, "Hint", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(900f, 34f));
                hint = Case01SpatialQA.Txt(r, 19, TextAnchor.MiddleCenter, new Color(0.9f, 0.88f, 0.82f, 0.75f));
            }
            hintCanvas.enabled = true;
        }

        public string HintText { get { return hint != null && hintCanvas != null && hintCanvas.enabled ? hint.text : null; } }

        /// <summary>QA：檢視中的相片在畫面上的高度比例（約 0.72＝可以完整看清；放大後超過 1）。</summary>
        public float ScreenHeightFraction
        {
            get
            {
                if (!Active || Item == null) return 0f;
                float dist = Mathf.Abs(cam.transform.localPosition.z - pivot.localPosition.z);
                float visible = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                return Item.Size.y / visible;
            }
        }

        public void Dispose()
        {
            if (root != null) ArenaArt.Kill(root.gameObject);
            if (hintCanvas != null) ArenaArt.Kill(hintCanvas.gameObject);
        }
    }
}

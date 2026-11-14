using UnityEngine;
using Mode = Shepherd.Exploration.Lantern.LanternReveal.Mode;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 一樓餐廳美術品質樣板（使用者 2026-11-15「場景美術品質提升指令」）：
    /// 位置、大小、門洞、謎題、動線都不變；提升建築細節、家具造型、生活配置、材質與光影。
    /// A 建築：護牆板（下半）＋腰線＋上半灰綠灰泥、天花線、門框、門檻、天花圓盤。
    /// B 家具：車削桌腳的胡桃木餐桌、軟墊坐面的木椅、下櫃＋玻璃碗櫃、布燈罩吊燈、壁燈。
    /// C 生活：桌上的熱水瓶、面紙、醬油、辣椒醬、筷筒；碗櫃上的茶盤、茶葉罐、水果盤、相框、小盆栽；靠牆的折疊椅；地毯。
    /// D 光影：暖色吊燈與壁燈、家具底下的接觸陰影。
    /// 不放宗教擺設（使用者要求）。
    /// </summary>
    public sealed partial class LanternGraybox
    {
        /// <summary>QA 對照用：false ＝ 建出改之前的餐廳（舊桌椅、舊櫃子、沒有建築細節）。正式遊戲一律 true。</summary>
        public static bool DiningSampleOn = true;
        /// <summary>餐廳的範圍（世界座標，QA 用）。</summary>
        public Bounds V2DiningRoom { get; private set; }
        public Light V2DiningPendantLight { get; private set; }
        private Material blobMat;

        private void V2DiningSample()
        {
            float f = FurnitureScale;
            float H = HP(0f, V2Ground, 0f).y - 0.25f;      // 一樓淨高（天花板下緣；天花板厚 0.25）
            float xW = HP(0f, 0f, 0f).x + 0.125f;          // 西牆（外牆）室內面
            float xE = HP(4.6f, 0f, 0f).x - 0.06f;         // 東牆（走廊）室內面
            float zS = HP(0f, 0f, 5f).z + 0.06f;           // 南牆（店面後牆）室內面
            float zN = HP(0f, 0f, V2Main).z - 0.125f;      // 北牆（往廚房；這面是外牆厚度）室內面
            var room = new Bounds();
            room.SetMinMax(new Vector3(xW, 0f, zS), new Vector3(xE, H, zN));
            V2DiningRoom = room;
            float dE0 = HP(0f, 0f, 6.0f).z, dE1 = HP(0f, 0f, 6.9f).z;   // 東牆的門（往走廊）
            float dN0 = HP(1.2f, 0f, 0f).x, dN1 = HP(2.1f, 0f, 0f).x;   // 北牆的門（往廚房）
            float doorH = HP(0f, 2.25f, 0f).y;

            // ---------- 材質 ----------
            var wainscot = Mat("DN_Wainscot", Mode.Dual, new Color(0.9f, 0.86f, 0.77f), new Color(0.5f, 0.5f, 0.5f), null, null, 0.35f, 0.3f, 0.003f);
            var upper = PH("DN_UpperWall", "painted_plaster_wall", "Tex", 1.6f, new Color(0.7f, 0.76f, 0.66f), new Color(0.5f, 0.52f, 0.54f), 0.25f);
            var trim = Mat("DN_Trim", Mode.Dual, new Color(0.94f, 0.91f, 0.84f), new Color(0.55f, 0.55f, 0.55f), null, null, 0.45f, 0.3f, 0.002f);
            var woodTrim = PH("DN_DoorTrim", "dark_wood", "Tex", 1.0f, new Color(0.62f, 0.42f, 0.3f), new Color(0.45f, 0.45f, 0.45f), 0.55f);
            var plate = Mat("DN_SwitchPlate", Mode.Dual, new Color(0.95f, 0.94f, 0.9f), new Color(0.6f, 0.6f, 0.6f), null, null, 0.6f, 0.4f, 0f);
            var scuff = Mat("DN_ChairScuff", Mode.Dual, new Color(0.42f, 0.4f, 0.36f), new Color(0.3f, 0.3f, 0.3f), null, null, 0.2f, 0.2f, 0f);

            // ---------- A 建築細節 ----------
            // 沿牆一段一段做（避開門洞）：護牆板 0.12～0.9、腰線、上半灰泥 0.92～H-0.12、天花線。
            void Band(bool alongX, float fixedC, float inward, float a0, float a1)
            {
                if (a1 - a0 < 0.05f) return;
                float mid = (a0 + a1) / 2f, len = a1 - a0;
                Vector3 At(float y, float off) => alongX ? new Vector3(mid, y, fixedC + inward * off) : new Vector3(fixedC + inward * off, y, mid);
                Vector3 Sz(float h, float d) => alongX ? new Vector3(len, h, d) : new Vector3(d, h, len);
                Box("DN_WainscotPanel", At(0.51f, 0.008f), Sz(0.78f, 0.016f), wainscot, false);
                // 護牆板上的直條（每 0.6 m 一條），像一片片的木板。
                for (float s = a0 + 0.3f; s < a1 - 0.1f; s += 0.6f)
                {
                    var p = alongX ? new Vector3(s, 0.51f, fixedC + inward * 0.018f) : new Vector3(fixedC + inward * 0.018f, 0.51f, s);
                    Box("DN_WainscotStile", p, alongX ? new Vector3(0.025f, 0.72f, 0.008f) : new Vector3(0.008f, 0.72f, 0.025f), trim, false);
                }
                Box("DN_DadoRail", At(0.91f, 0.022f), Sz(0.045f, 0.03f), trim, false);
                Box("DN_UpperWall", At((0.935f + H - 0.12f) / 2f, 0.004f), Sz(H - 0.12f - 0.935f, 0.008f), upper, false);
                Box("DN_PictureRail", At(2.35f, 0.012f), Sz(0.025f, 0.018f), trim, false);
                Box("DN_Cornice", At(H - 0.06f, 0.035f), Sz(0.12f, 0.07f), trim, false);
                Box("DN_CorniceStep", At(H - 0.13f, 0.015f), Sz(0.03f, 0.03f), trim, false);
            }
            Band(false, xW, 1f, zS, zN);                                  // 西牆
            Band(true, zS, 1f, xW, xE);                                   // 南牆
            Band(false, xE, -1f, zS, dE0 - 0.08f);                         // 東牆（門的兩邊）
            Band(false, xE, -1f, dE1 + 0.08f, zN);
            Box("DN_UpperWallOverDoorE", new Vector3(xE - 0.004f, (doorH + 0.1f + H - 0.12f) / 2f, (dE0 + dE1) / 2f), new Vector3(0.008f, H - 0.12f - doorH - 0.1f, dE1 - dE0 + 0.16f), upper, false);
            Box("DN_CorniceOverDoorE", new Vector3(xE - 0.035f, H - 0.06f, (dE0 + dE1) / 2f), new Vector3(0.07f, 0.12f, dE1 - dE0 + 0.16f), trim, false);
            Band(true, zN, -1f, xW, dN0 - 0.08f);                          // 北牆（門的兩邊）
            Band(true, zN, -1f, dN1 + 0.08f, xE);
            Box("DN_UpperWallOverDoorN", new Vector3((dN0 + dN1) / 2f, (doorH + 0.1f + H - 0.12f) / 2f, zN - 0.004f), new Vector3(dN1 - dN0 + 0.16f, H - 0.12f - doorH - 0.1f, 0.008f), upper, false);
            Box("DN_CorniceOverDoorN", new Vector3((dN0 + dN1) / 2f, H - 0.06f, zN - 0.035f), new Vector3(dN1 - dN0 + 0.16f, 0.12f, 0.07f), trim, false);
            // 門框（有厚度的木框）與門檻。
            void Architrave(bool alongX, float fixedC, float inward, float a0, float a1)
            {
                Vector3 J(float a) => alongX ? new Vector3(a, doorH / 2f, fixedC + inward * 0.015f) : new Vector3(fixedC + inward * 0.015f, doorH / 2f, a);
                var js = alongX ? new Vector3(0.075f, doorH, 0.03f) : new Vector3(0.03f, doorH, 0.075f);
                Box("DN_Architrave", J(a0 - 0.0375f), js, woodTrim, false);
                Box("DN_Architrave", J(a1 + 0.0375f), js, woodTrim, false);
                var hp = alongX ? new Vector3((a0 + a1) / 2f, doorH + 0.045f, fixedC + inward * 0.017f) : new Vector3(fixedC + inward * 0.017f, doorH + 0.045f, (a0 + a1) / 2f);
                Box("DN_ArchitraveHead", hp, alongX ? new Vector3(a1 - a0 + 0.2f, 0.09f, 0.034f) : new Vector3(0.034f, 0.09f, a1 - a0 + 0.2f), woodTrim, false);
            }
            Architrave(false, xE, -1f, dE0, dE1);
            Architrave(true, zN, -1f, dN0, dN1);
            Box("DN_ThresholdE", new Vector3(xE + 0.06f, 0.008f, (dE0 + dE1) / 2f), new Vector3(0.14f, 0.016f, dE1 - dE0), woodTrim, false);
            Box("DN_ThresholdN", new Vector3((dN0 + dN1) / 2f, 0.008f, zN + 0.125f), new Vector3(dN1 - dN0, 0.016f, 0.27f), woodTrim, false);
            // 開關、插座。
            // 開關在門的北側（門往南開，開著時不會被門板擋住）。
            Box("DN_LightSwitch", new Vector3(xE - 0.012f, 1.25f, dE1 + 0.24f), new Vector3(0.012f, 0.085f, 0.085f), plate, false);
            Box("DN_LightSwitchRocker", new Vector3(xE - 0.02f, 1.25f, dE1 + 0.24f), new Vector3(0.008f, 0.03f, 0.018f), plate, false);
            Box("DN_Socket", new Vector3(xW + 0.012f, 0.32f, HP(0f, 0f, 6.1f).z), new Vector3(0.012f, 0.085f, 0.145f), plate, false);
            Box("DN_Socket2", new Vector3((xW + xE) / 2f + 0.6f, 0.32f, zS + 0.012f), new Vector3(0.145f, 0.085f, 0.012f), plate, false);

            // ---------- B 家具 ----------
            var table = HP(2.2f, 0f, 7.5f);
            var rug = CustomModel("DN_Rug", "dn_rug", table, 90f, false);
            CustomModel("DN_Table", "dn_table", table, 90f);
            // 四張椅子：西側兩張、東側一張靠好；東側另一張被推開、歪向一邊（那個週日留下的痕跡），後面的牆上有一道椅背撞到的痕。
            CustomModel("DN_ChairW1", "dn_chair", table + new Vector3(-0.66f, 0f, -0.36f), 90f);
            CustomModel("DN_ChairW2", "dn_chair", table + new Vector3(-0.66f, 0f, 0.36f), 90f);
            CustomModel("DN_ChairE1", "dn_chair", table + new Vector3(0.66f, 0f, -0.36f), -90f);
            var pushed = table + new Vector3(1.12f, 0f, 0.5f);
            CustomModel("DN_ChairPushed", "dn_chair", pushed, -90f + 38f);
            Box("DN_WallScuff", new Vector3(xE - 0.003f, 0.88f, pushed.z + 0.12f), new Vector3(0.004f, 0.05f, 0.22f), scuff, false, new Vector3(4f, 0f, 0f));
            // 碗櫃（下櫃＋玻璃上櫃），靠西牆，取代原本的舊櫃子。
            var sb = new Vector3(xW + 0.02f, 0f, HP(0f, 0f, 7.5f).z);
            CustomModel("DN_Sideboard", "dn_sideboard", sb, 90f);
            CustomModel("DN_SideboardDressing", "dn_sideboard_dressing", sb + Vector3.up * (0.85f * f), 90f, false);
            // 吊燈（天花圓盤＋布燈罩）與兩盞壁燈（碗櫃兩側）。
            CustomModel("DN_Pendant", "dn_pendant", new Vector3(table.x, H, table.z), 0f, false);
            CustomModel("DN_SconceA", "dn_sconce", new Vector3(xW, 0f, sb.z - 1.05f), 90f, false);
            CustomModel("DN_SconceB", "dn_sconce", new Vector3(xW, 0f, sb.z + 1.05f), 90f, false);
            // 靠北牆的備用折疊椅。
            CustomModel("DN_FoldingChair", "dn_folding_chair", new Vector3(HP(3.75f, 0f, 0f).x, 0f, zN), 180f, false);

            // ---------- C 生活配置 ----------
            CustomModel("DN_TableDressing", "dn_table_dressing", table + Vector3.up * (0.76f * f), 90f, false);
            // 南牆電視櫃上方：再兩張家庭照（和原本那張排成一組）。
            var frame = Mat("DN_WallFrame", Mode.Dual, new Color(0.32f, 0.2f, 0.12f), new Color(0.5f, 0.5f, 0.5f), null, null, 0.4f, 0.4f, 0f);
            var photo = Mat("DN_WallPhoto", Mode.Dual, Color.white, new Color(0.6f, 0.6f, 0.6f), LanternReveal.Photo(true), null, 0.3f, 0.3f, 0f);
            foreach (var (dx, dy, w, h) in new[] { (-0.55f, 1.62f, 0.26f, 0.34f), (0.55f, 1.78f, 0.3f, 0.24f) })
            {
                var c = new Vector3(HP(1.4f, 0f, 0f).x + dx, dy, zS + 0.012f);
                Box("DN_FamilyFrame", c, new Vector3(w, h, 0.02f), frame, false);
                Box("DN_FamilyPhoto", c + new Vector3(0f, 0f, 0.012f), new Vector3(w - 0.05f, h - 0.05f, 0.004f), photo, false);
            }

            // ---------- D 光影 ----------
            // 暖色吊燈（燈罩下緣約在 2.45 m）、壁燈；原本那顆冷色的燈泡和燈罩藏起來。
            V2DiningPendantLight = AddLight("DN_PendantLight", LightType.Point, new Vector3(table.x, H - 0.95f, table.z), new Color(1f, 0.78f, 0.55f), 2.4f, 6.5f, false, LightShadows.Soft);
            V2DiningPendantLight.shadowStrength = 0.75f;
            var down = AddLight("DN_PendantDown", LightType.Spot, new Vector3(table.x, H - 1.0f, table.z), new Color(1f, 0.8f, 0.58f), 3.2f, 4f, false, LightShadows.Soft, 95f, Vector3.down);
            down.innerSpotAngle = 50f;
            foreach (var z in new[] { sb.z - 1.05f, sb.z + 1.05f })
                AddLight("DN_SconceLight", LightType.Point, new Vector3(xW + 0.18f, 1.72f, z), new Color(1f, 0.76f, 0.5f), 0.7f, 2.4f, false, LightShadows.None);
            foreach (var t in Into.GetComponentsInChildren<Transform>(true))
                if (t.name == "V2DiningLightBulb" || t.name == "V2DiningLightShade") { var r = t.GetComponent<Renderer>(); if (r != null) r.enabled = false; }
            // 接觸陰影：家具底下一圈很淡的影子（讓家具「坐」在地上）。
            Blob(table, new Vector2(1.0f, 1.7f), 90f, 0.32f);
            foreach (var c in new[] { table + new Vector3(-0.66f, 0f, -0.36f), table + new Vector3(-0.66f, 0f, 0.36f), table + new Vector3(0.66f, 0f, -0.36f), pushed })
                Blob(c, new Vector2(0.56f, 0.56f), 0f, 0.28f);
            Blob(sb + new Vector3(0.24f, 0f, 0f), new Vector2(0.62f, 1.55f), 0f, 0.34f);
        }

        /// <summary>接觸陰影：貼在地上的柔邊深色面（不影響碰撞與謎題）。</summary>
        private void Blob(Vector3 at, Vector2 size, float yaw, float alpha)
        {
            if (blobMat == null)
            {
                const int N = 64;
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = "ContactShadow", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                var px = new Color[N * N];
                for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)) * 0.6f + Mathf.Sqrt(u * u + v * v) * 0.4f;
                    px[y * N + x] = new Color(0f, 0f, 0f, Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f));
                }
                tex.SetPixels(px); tex.Apply();
                var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                blobMat = new Material(sh != null ? sh : Shader.Find("Sprites/Default")) { name = "DN_ContactShadow", hideFlags = HideFlags.DontSave };
                blobMat.SetFloat("_Surface", 1f); blobMat.SetFloat("_Blend", 0f);
                blobMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                blobMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                blobMat.SetFloat("_ZWrite", 0f);
                blobMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                blobMat.renderQueue = 2990;
                blobMat.SetTexture("_BaseMap", tex); blobMat.SetTexture("_MainTex", tex);
            }
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "DN_ContactShadow";
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(Into, false);
            q.transform.position = at + Vector3.up * 0.016f;
            q.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            q.transform.localScale = new Vector3(size.x, size.y, 1f);
            var r = q.GetComponent<Renderer>();
            var m = new Material(blobMat) { hideFlags = HideFlags.DontSave };
            var c = new Color(0.05f, 0.03f, 0.02f, alpha);
            m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}

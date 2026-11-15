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
        /// <summary>
        /// 餐廳美術實驗（docs/CH17_3D_ART_DIRECTION_AND_PIPELINE.md 第 4 節第 1、2 步），只給 QA 對照用，正式遊戲一律 0。
        /// 1 ＝ E1 材質：只用現有 Poly Haven 貼圖，把餐廳的單色材質換成有紋理、法線、粗糙度的版本。
        /// 2 ＝ E2 環境光：餐廳裡加幾盞不投影的補光模擬牆面反彈的光（反射探針試過但沒有生效，見 DiningExperimentLight）。
        /// 3 ＝ 兩個都開。
        /// </summary>
        public static int DiningArtExperiment = 0;
        private static bool DiningE1 => (DiningArtExperiment & 1) != 0;
        private static bool DiningE2 => (DiningArtExperiment & 2) != 0;
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
            if (DiningE1)
            {
                // E1：護牆板、線板改成「刷過油漆的表面」（灰泥貼圖的細微起伏＋粗糙度變化），不再是一片純色。
                wainscot = PH("DN_WainscotTex", "painted_plaster_wall", "Tex", 1.2f, new Color(0.97f, 0.93f, 0.84f), new Color(0.5f, 0.5f, 0.5f), 0.42f);
                trim = PH("DN_TrimTex", "painted_plaster_wall", "Tex", 0.8f, new Color(1f, 0.97f, 0.9f), new Color(0.55f, 0.55f, 0.55f), 0.5f);
            }

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
            // ---------- A2 建築層次（第三版，使用者 2026-11-15 確認的方案） ----------
            // 下面的平面座標用「離西牆／南牆幾公尺」表示：X 往東、Y 往北。
            float Xw(float X) => xW + X;
            float Ys(float Y) => zS + Y;
            var iron = Mat("DN_CastIron", Mode.Dual, new Color(0.07f, 0.07f, 0.075f), new Color(0.12f, 0.12f, 0.13f), null, null, 0.45f, 0.4f, 0f);
            iron.SetFloat("_Metallic", 0.7f);
            var fireboard = Mat("DN_Fireboard", Mode.Dual, new Color(0.16f, 0.14f, 0.13f), new Color(0.2f, 0.2f, 0.2f), null, null, 0.1f, 0.1f, 0.004f);
            var tile = Mat("DN_FireTile", Mode.Dual, new Color(0.18f, 0.36f, 0.3f), new Color(0.35f, 0.36f, 0.36f), null, null, 0.85f, 0.5f, 0.002f);
            var hearthM = PH("DN_Hearth", "floor_tiles_06", "Tex", 1.2f, new Color(0.72f, 0.42f, 0.3f), new Color(0.45f, 0.45f, 0.45f), 0.35f);
            var gilt = Mat("DN_Gilt", Mode.Dual, new Color(0.72f, 0.56f, 0.3f), new Color(0.5f, 0.5f, 0.5f), null, null, 0.6f, 0.5f, 0f);
            gilt.SetFloat("_Metallic", 0.8f);
            // 鏡子：場景沒有反射探針，全金屬會變成黑洞；用帶灰綠的亮面玻璃代替（看得出是鏡面，但不搶眼）。
            var mirror = Mat("DN_Mirror", Mode.Dual, new Color(0.56f, 0.62f, 0.62f), new Color(0.5f, 0.52f, 0.54f), null, null, 0.88f, 0.8f, 0f);
            var radM = Mat("DN_Radiator", Mode.Dual, new Color(0.88f, 0.86f, 0.8f), new Color(0.55f, 0.55f, 0.55f), null, null, 0.5f, 0.4f, 0f);
            var cupboardM = Mat("DN_AlcoveCupboard", Mode.Dual, new Color(0.86f, 0.82f, 0.72f), new Color(0.5f, 0.5f, 0.5f), null, null, 0.4f, 0.3f, 0.003f);
            var china = Mat("DN_AlcoveChina", Mode.Dual, new Color(0.95f, 0.94f, 0.9f), new Color(0.6f, 0.6f, 0.6f), null, null, 0.85f, 0.5f, 0f);
            var blueChina = Mat("DN_AlcoveBlue", Mode.Dual, new Color(0.35f, 0.45f, 0.65f), new Color(0.5f, 0.5f, 0.5f), null, null, 0.85f, 0.5f, 0f);
            var album = Mat("DN_AlcoveAlbum", Mode.Dual, new Color(0.42f, 0.12f, 0.1f), new Color(0.4f, 0.4f, 0.4f), null, null, 0.4f, 0.3f, 0f);
            var album2 = Mat("DN_AlcoveAlbum2", Mode.Dual, new Color(0.14f, 0.2f, 0.34f), new Color(0.4f, 0.4f, 0.4f), null, null, 0.4f, 0.3f, 0f);
            var brass = Mat("DN_Knob", Mode.Dual, new Color(0.8f, 0.6f, 0.32f), new Color(0.5f, 0.5f, 0.5f), null, null, 0.7f, 0.5f, 0f);
            brass.SetFloat("_Metallic", 1f);
            if (DiningE1)
            {
                // E1：鑄鐵（帶斑駁的粗糙度和法線）、磁磚（有磚縫的貼圖）、封爐板、暖氣片與壁龕櫃的漆面。
                var anom = new Color(0.5f, 0.5f, 0.5f);
                iron = PH("DN_CastIronTex", "concrete_floor_worn_001", "Tex", 0.6f, new Color(0.16f, 0.16f, 0.17f), anom, 0.5f);
                iron.SetFloat("_Metallic", 0.55f);
                fireboard = PH("DN_FireboardTex", "plastered_wall_03", "Tex", 0.8f, new Color(0.26f, 0.23f, 0.21f), anom, 0.15f);
                tile = PH("DN_FireTileTex", "long_white_tiles", "Tex", 0.3f, new Color(0.3f, 0.6f, 0.5f), anom, 0.85f);
                radM = PH("DN_RadiatorTex", "painted_plaster_wall", "Tex", 0.6f, new Color(0.95f, 0.93f, 0.86f), anom, 0.55f);
                cupboardM = PH("DN_AlcoveCupboardTex", "painted_plaster_wall", "Tex", 1.0f, new Color(0.93f, 0.88f, 0.77f), anom, 0.45f);
            }

            // (1) 過去改建時留下的承重橫樑和兩側短牆：位置在走廊門北側（距南牆 2.6 m），橫跨共用牆到走廊牆。
            //     樑往下只有 0.24 m（樑底 ≈ 3.0 m，比玩家眼睛高 1.4 m），不會壓迫。之後蓋二樓時，二樓的隔間牆可以落在這根樑上方。
            const float beamY0 = 2.6f, beamY1 = 2.85f, nibW = 0.6f, nibE = 0.45f;
            float zb0 = Ys(beamY0), zb1 = Ys(beamY1);
            Box("DN_Beam", new Vector3((xW + xE) / 2f, H - 0.12f, (zb0 + zb1) / 2f), new Vector3(xE - xW, 0.24f, zb1 - zb0), trim, false);
            Box("DN_BeamMould", new Vector3((xW + xE) / 2f, H - 0.245f, (zb0 + zb1) / 2f), new Vector3(xE - xW, 0.02f, zb1 - zb0 + 0.04f), trim, false);
            Box("DN_NibW", new Vector3(xW + nibW / 2f, H / 2f, (zb0 + zb1) / 2f), new Vector3(nibW, H, zb1 - zb0), upper, true);
            Box("DN_NibE", new Vector3(xE - nibE / 2f, H / 2f, (zb0 + zb1) / 2f), new Vector3(nibE, H, zb1 - zb0), upper, true);
            Band(true, zb0, -1f, xW, xW + nibW); Band(true, zb1, 1f, xW, xW + nibW); Band(false, xW + nibW, 1f, zb0, zb1);
            Band(true, zb0, -1f, xE - nibE, xE); Band(true, zb1, 1f, xE - nibE, xE); Band(false, xE - nibE, -1f, zb0, zb1);
            // 開關改裝在東邊短牆朝南的那一面（走進門就摸得到）。
            Box("DN_LightSwitch", new Vector3(xE - 0.2f, 1.25f, zb0 - 0.012f), new Vector3(0.085f, 0.085f, 0.012f), plate, false);
            Box("DN_LightSwitchRocker", new Vector3(xE - 0.2f, 1.25f, zb0 - 0.02f), new Vector3(0.018f, 0.03f, 0.008f), plate, false);
            Box("DN_Socket", new Vector3(xW + 0.012f, 0.32f, HP(0f, 0f, 6.1f).z), new Vector3(0.012f, 0.085f, 0.145f), plate, false);
            Box("DN_Socket2", new Vector3((xW + xE) / 2f + 0.6f, 0.32f, zS + 0.012f), new Vector3(0.145f, 0.085f, 0.012f), plate, false);

            // (2) 共用牆上的煙囪壁柱和封起來的舊壁爐（保留鑄鐵框、壁爐架、爐台；不能用）。
            const float brY0 = 3.88f, brY1 = 5.28f, brD = 0.45f;
            float zc = Ys((brY0 + brY1) / 2f), bx = xW + brD;
            Box("DN_ChimneyBreast", new Vector3(xW + brD / 2f, H / 2f, zc), new Vector3(brD, H, brY1 - brY0), upper, true);
            Band(false, bx, 1f, Ys(brY0), Ys(brY1)); Band(true, Ys(brY0), -1f, xW, bx); Band(true, Ys(brY1), 1f, xW, bx);
            Box("DN_FireInsert", new Vector3(bx + 0.015f, 0.5f, zc), new Vector3(0.03f, 1.0f, 0.9f), iron, false);
            Box("DN_FireBoard", new Vector3(bx + 0.032f, 0.42f, zc), new Vector3(0.006f, 0.56f, 0.5f), fireboard, false);
            Box("DN_FireHood", new Vector3(bx + 0.045f, 0.73f, zc), new Vector3(0.03f, 0.07f, 0.58f), iron, false);
            Box("DN_FireFret", new Vector3(bx + 0.05f, 0.14f, zc), new Vector3(0.025f, 0.1f, 0.58f), iron, false);
            foreach (var s in new[] { -1f, 1f })
            {
                Box("DN_FireTiles", new Vector3(bx + 0.033f, 0.5f, zc + s * 0.37f), new Vector3(0.008f, 0.84f, 0.12f), tile, false);
                for (int i = 1; i < 6; i++) Box("DN_TileJoint", new Vector3(bx + 0.038f, 0.08f + i * 0.14f, zc + s * 0.37f), new Vector3(0.002f, 0.006f, 0.12f), fireboard, false);
                Box("DN_SurroundPilaster", new Vector3(bx + 0.035f, 0.55f, zc + s * 0.56f), new Vector3(0.07f, 1.1f, 0.16f), woodTrim, false);
            }
            Box("DN_SurroundFrieze", new Vector3(bx + 0.035f, 1.03f, zc), new Vector3(0.07f, 0.16f, 1.28f), woodTrim, false);
            Box("DN_Mantel", new Vector3(bx + 0.09f, 1.135f, zc), new Vector3(0.2f, 0.04f, 1.42f), woodTrim, false);
            Box("DN_Hearth", new Vector3(bx + 0.175f, 0.012f, zc), new Vector3(0.35f, 0.024f, 1.3f), hearthM, false);
            Box("DN_OvermantelFrame", new Vector3(bx + 0.012f, 1.6f, zc), new Vector3(0.02f, 0.55f, 0.7f), gilt, false);
            Box("DN_OvermantelMirror", new Vector3(bx + 0.024f, 1.6f, zc), new Vector3(0.004f, 0.47f, 0.62f), mirror, false);
            // 壁爐架上：兩張小相框（原本的掛鐘移到壁柱上、鏡子上方）。
            var frameM = Mat("DN_MantelFrame", Mode.Dual, new Color(0.3f, 0.2f, 0.12f), new Color(0.5f, 0.5f, 0.5f), null, null, 0.4f, 0.4f, 0f);
            var photoM = Mat("DN_MantelPhoto", Mode.Dual, Color.white, new Color(0.6f, 0.6f, 0.6f), LanternReveal.Photo(true), null, 0.3f, 0.3f, 0f);
            foreach (var (dz, w, h, rot) in new[] { (-0.45f, 0.16f, 0.2f, -8f), (0.42f, 0.2f, 0.15f, 10f) })
            {
                var c = new Vector3(bx + 0.1f, 1.155f + h / 2f, zc + dz);
                var e = new Vector3(0f, 90f + rot, -8f);
                Box("DN_MantelFrame", c, new Vector3(w, h, 0.015f), frameM, false, e);
                var ph = Box("DN_MantelPhoto", c, new Vector3(w - 0.04f, h - 0.04f, 0.004f), photoM, false, e);
                ph.position += ph.forward * 0.009f;
            }
            foreach (var t in Into.GetComponentsInChildren<Transform>(true))
                if (t.name == "V2DiningClock") { t.position = new Vector3(bx + 0.005f, 0f, zc); break; }

            // (3) 壁柱兩側的壁龕：北側是嵌入式櫃子（收碗盤、相簿），南側是鑄鐵暖氣片（管線外露，後來加裝的中央暖氣）。
            float ac0 = Ys(brY1) + 0.01f, ac1 = zN - 0.01f, acz = (ac0 + ac1) / 2f, acw = ac1 - ac0;
            Box("DN_AlcoveBase", new Vector3(xW + 0.21f, 0.42f, acz), new Vector3(0.42f, 0.84f, acw), cupboardM, true);
            Box("DN_AlcoveTop", new Vector3(xW + 0.225f, 0.855f, acz), new Vector3(0.45f, 0.03f, acw + 0.01f), woodTrim, false);
            foreach (var s in new[] { -1f, 1f })
            {
                Box("DN_AlcoveDoor", new Vector3(xW + 0.425f, 0.43f, acz + s * acw / 4f), new Vector3(0.01f, 0.7f, acw / 2f - 0.04f), cupboardM, false);
                Box("DN_AlcoveDoorPanel", new Vector3(xW + 0.432f, 0.43f, acz + s * acw / 4f), new Vector3(0.006f, 0.56f, acw / 2f - 0.14f), trim, false);
                Box("DN_AlcoveKnob", new Vector3(xW + 0.44f, 0.6f, acz + s * 0.04f), new Vector3(0.015f, 0.02f, 0.02f), brass, false);
            }
            foreach (var y in new[] { 1.32f, 1.74f })
                Box("DN_AlcoveShelf", new Vector3(xW + 0.15f, y, acz), new Vector3(0.3f, 0.025f, acw), trim, false);
            // 層架上：盤子、碗、玻璃罐、相簿（不多，看得出是平常在用的）。
            for (int i = 0; i < 4; i++) Cyl("DN_AlcovePlate", new Vector3(xW + 0.15f, 1.345f + i * 0.012f, acz - 0.28f), new Vector3(0.24f, 0.006f, 0.24f), i % 2 == 0 ? china : blueChina);
            for (int i = 0; i < 3; i++) Cyl("DN_AlcoveBowl", new Vector3(xW + 0.15f, 1.36f + i * 0.035f, acz + 0.05f), new Vector3(0.13f, 0.02f, 0.13f), china);
            Cyl("DN_AlcoveJar", new Vector3(xW + 0.14f, 1.42f, acz + 0.3f), new Vector3(0.1f, 0.08f, 0.1f), blueChina);
            for (int i = 0; i < 4; i++) Box("DN_AlcoveAlbum", new Vector3(xW + 0.15f, 1.9f, acz - 0.3f + i * 0.065f), new Vector3(0.24f, 0.29f, 0.055f), i % 2 == 0 ? album : album2, false);
            Box("DN_AlcoveBox", new Vector3(xW + 0.16f, 1.82f, acz + 0.22f), new Vector3(0.22f, 0.13f, 0.26f), cupboardM, false);
            Cyl("DN_AlcoveTeapot", new Vector3(xW + 0.2f, 0.93f, acz + 0.2f), new Vector3(0.14f, 0.06f, 0.14f), blueChina);
            Box("DN_AlcoveTray", new Vector3(xW + 0.22f, 0.875f, acz - 0.15f), new Vector3(0.3f, 0.012f, 0.22f), woodTrim, false);

            float rz = Ys((beamY1 + brY0) / 2f);
            for (int i = 0; i < 13; i++)
                Box("DN_RadiatorColumn", new Vector3(xW + 0.08f, 0.47f, rz - 0.36f + i * 0.06f), new Vector3(0.09f, 0.6f, 0.04f), radM, false);
            Box("DN_RadiatorTop", new Vector3(xW + 0.08f, 0.76f, rz), new Vector3(0.05f, 0.03f, 0.78f), radM, false);
            Box("DN_RadiatorBottom", new Vector3(xW + 0.08f, 0.18f, rz), new Vector3(0.05f, 0.03f, 0.78f), radM, false);
            foreach (var s in new[] { -1f, 1f })
            {
                Cyl("DN_RadiatorPipe", new Vector3(xW + 0.08f, 0.09f, rz + s * 0.45f), new Vector3(0.022f, 0.09f, 0.022f), radM);
                Cyl("DN_RadiatorPipeH", new Vector3(xW + 0.08f, 0.18f, rz + s * 0.415f), new Vector3(0.022f, 0.035f, 0.022f), radM, new Vector3(90f, 0f, 0f));
                Cyl("DN_RadiatorValve", new Vector3(xW + 0.08f, 0.24f, rz + s * 0.45f), new Vector3(0.035f, 0.03f, 0.035f), s < 0 ? brass : radM);
            }

            // ---------- B 家具（第三版：不新增，只重新安排） ----------
            // 用餐區（橫樑北側）：餐桌改東西向、正對壁爐；碗櫃移到東牆、正對壁爐；西端留走道通往廚房門。
            // 起居區（橫樑南側）：只是一家人平常坐一下的角落（電視、扶手椅、小茶几、書櫃），不是完整的客廳——主要客廳在二樓。
            var table = new Vector3(Xw(3.05f), 0f, Ys(4.45f));
            var rug = CustomModel("DN_Rug", "dn_rug", table, 0f, false);
            if (rug != null) rug.localScale = new Vector3(f * 1.18f, f, f * 1.32f);
            CustomModel("DN_Table", "dn_table", table, 0f);
            var chairN1 = table + new Vector3(-0.45f, 0f, 0.66f);
            var chairN2 = table + new Vector3(0.47f, 0f, 0.64f);
            var chairS1 = table + new Vector3(-0.46f, 0f, -0.66f);
            CustomModel("DN_ChairN1", "dn_chair", chairN1, 180f);
            CustomModel("DN_ChairN2", "dn_chair", chairN2, 183f);
            CustomModel("DN_ChairS1", "dn_chair", chairS1, -2f);
            // 被推開的那張椅子（那個週日留下的痕跡）：往後推、歪向一邊。
            var pushed = table + new Vector3(0.62f, 0f, -1.12f);
            CustomModel("DN_ChairPushed", "dn_chair", pushed, 32f);
            // 桌上那頓飯（劇情物件）跟著桌子移過來，轉成東西向。
            foreach (var t in Into.GetComponentsInChildren<Transform>(true))
                if (t.name == "V2DiningSetting") { t.position = table + Vector3.up * (0.76f * f); t.rotation = Quaternion.Euler(0f, 90f, 0f); break; }
            CustomModel("DN_TableDressing", "dn_table_dressing", table + Vector3.up * (0.76f * f), 0f, false);
            CustomModel("DN_Pendant", "dn_pendant", new Vector3(table.x, H, table.z), 0f, false);

            var sb = new Vector3(xE - 0.02f, 0f, Ys(4.02f));
            CustomModel("DN_Sideboard", "dn_sideboard", sb, -90f);
            CustomModel("DN_SideboardDressing", "dn_sideboard_dressing", sb + Vector3.up * (0.85f * f), -90f, false);
            // 壁燈移到煙囪壁柱兩側。
            CustomModel("DN_SconceA", "dn_sconce", new Vector3(bx, 0f, zc - 0.58f), 90f, false);
            CustomModel("DN_SconceB", "dn_sconce", new Vector3(bx, 0f, zc + 0.58f), 90f, false);
            // 獎狀：用餐區東牆靠北（碗櫃旁邊）。
            CustomModel("DN_Award", "dn_award", new Vector3(xE, 0f, Ys(5.65f)), -90f, false);
            // 廚房門旁量身高的鉛筆線（生活紀錄）。
            var ink = Mat("DN_PencilMark", Mode.Dual, new Color(0.22f, 0.2f, 0.2f), new Color(0.3f, 0.3f, 0.3f), null, null, 0.3f, 0.3f, 0f);
            float markX = dN1 + 0.27f;
            foreach (var (y, label) in new[] { (0.98f, "1989"), (1.1f, "1991"), (1.22f, "1993"), (1.33f, "1995"), (1.43f, "1997") })
            {
                var mk = Box("DN_HeightMark", new Vector3(markX, y, zN - 0.006f), new Vector3(0.12f, 0.006f, 0.003f), ink, false);
                WallText(mk, label, Quaternion.identity, new Vector3(0.1f, 0.008f, -0.004f), 0.0065f, new Color(0.12f, 0.1f, 0.1f));
            }

            // 起居區：電視櫃（原本的）、扶手椅轉向電視、小茶几、書櫃、角落的盆栽；天花板中間有自己的圓盤和小吊燈。
            var arm = new Vector3(Xw(1.35f), 0f, Ys(1.85f));
            var armT = CustomModel("DN_Armchair", "dn_armchair", arm, 165f);
            var side = new Vector3(Xw(0.42f), 0f, Ys(1.95f));
            var sideT = CustomModel("DN_SideTable", "dn_side_table", side, 20f);
            var slipT = CustomModel("DN_Slippers", "dn_slippers", arm + new Vector3(0.12f, 0f, -0.55f), 170f, false);
            var plant = new Vector3(Xw(0.32f), 0f, Ys(0.34f));
            CustomModel("DN_FloorPlant", "dn_floor_plant", plant, 40f);
            var books = new Vector3(HP(2.85f, 0f, 0f).x, 0f, zS);
            var booksT = CustomModel("DN_Bookcase", "dn_bookcase", books, 0f);
            var sitPendant = new Vector3(Xw(2.6f), H, Ys(1.35f));
            CustomModel("DN_PendantSitting", "dn_pendant", sitPendant, 0f, false);

            // ---------- C 生活配置 ----------
            // 南牆電視櫃上方：再兩張家庭照（和原本那張排成一組）。
            var frame = Mat("DN_WallFrame", Mode.Dual, new Color(0.32f, 0.2f, 0.12f), new Color(0.5f, 0.5f, 0.5f), null, null, 0.4f, 0.4f, 0f);
            var photo = Mat("DN_WallPhoto", Mode.Dual, Color.white, new Color(0.6f, 0.6f, 0.6f), LanternReveal.Photo(true), null, 0.3f, 0.3f, 0f);
            var famFrames = new System.Collections.Generic.List<(Transform f, Transform p)>();
            foreach (var (dx, dy, w, h) in new[] { (-0.55f, 1.62f, 0.26f, 0.34f), (0.55f, 1.78f, 0.3f, 0.24f) })
            {
                var c = new Vector3(HP(1.4f, 0f, 0f).x + dx, dy, zS + 0.012f);
                var ff = Box("DN_FamilyFrame", c, new Vector3(w, h, 0.02f), frame, false);
                var fp = Box("DN_FamilyPhoto", c + new Vector3(0f, 0f, 0.012f), new Vector3(w - 0.05f, h - 0.05f, 0.004f), photo, false);
                famFrames.Add((ff, fp));
            }

            // ---------- D 光影 ----------
            V2DiningPendantLight = AddLight("DN_PendantLight", LightType.Point, new Vector3(table.x, H - 0.95f, table.z), new Color(1f, 0.78f, 0.55f), 2.4f, 6.5f, false, LightShadows.Soft);
            V2DiningPendantLight.shadowStrength = 0.75f;
            var down = AddLight("DN_PendantDown", LightType.Spot, new Vector3(table.x, H - 1.0f, table.z), new Color(1f, 0.8f, 0.58f), 3.2f, 4f, false, LightShadows.Soft, 95f, Vector3.down);
            down.innerSpotAngle = 50f;
            AddLight("DN_SittingLight", LightType.Point, new Vector3(sitPendant.x, H - 0.95f, sitPendant.z), new Color(1f, 0.78f, 0.56f), 1.4f, 5f, false, LightShadows.None);
            foreach (var z in new[] { zc - 0.58f, zc + 0.58f })
                AddLight("DN_SconceLight", LightType.Point, new Vector3(bx + 0.18f, 1.72f, z), new Color(1f, 0.76f, 0.5f), 0.7f, 2.4f, false, LightShadows.None);
            var sideBulb = FindChild(sideT, "LampBulb");
            if (sideBulb != null) AddLight("DN_SideLampLight", LightType.Point, sideBulb.position, new Color(1f, 0.74f, 0.48f), 0.8f, 2.4f, false, LightShadows.None);
            foreach (var t in Into.GetComponentsInChildren<Transform>(true))
                if (t.name == "V2DiningLightBulb" || t.name == "V2DiningLightShade") { var r = t.GetComponent<Renderer>(); if (r != null) r.enabled = false; }
            // 接觸陰影。
            Blob(table, new Vector2(1.0f, 2.0f), 0f, 0.32f);
            foreach (var c in new[] { chairN1, chairN2, chairS1, pushed })
                Blob(c, new Vector2(0.56f, 0.56f), 0f, 0.28f);
            Blob(sb + new Vector3(-0.24f, 0f, 0f), new Vector2(0.62f, 1.55f), 0f, 0.34f);
            var armBlob = Blob(arm, new Vector2(0.75f, 0.75f), 165f, 0.3f);
            var sideBlob = Blob(side, new Vector2(0.55f, 0.55f), 0f, 0.26f);
            Blob(plant, new Vector2(0.5f, 0.5f), 0f, 0.3f);
            var booksBlob = Blob(books + new Vector3(0f, 0f, 0.18f), new Vector2(1.15f, 0.42f), 0f, 0.3f);
            Blob(new Vector3(xW + 0.22f, 0f, acz), new Vector2(0.5f, acw), 0f, 0.3f);
            Blob(new Vector3(bx + 0.1f, 0f, zc), new Vector2(0.3f, 1.4f), 0f, 0.25f);

            if (DiningE1) DiningExperimentModelMaterials(room);
            if (DiningE2) DiningExperimentLight(table, sitPendant, H);
            DiningLayoutB(Xw, Ys, H, zc, bx, zb0, zb1, nibW, nibE, booksT, booksBlob, armT, armBlob, slipT, sideT, sideBlob, famFrames, upper, woodTrim, brass);
        }

        // ---------------- 美術實驗（只在 QA 開） ----------------

        /// <summary>E1：Blender 家具的單色材質，依名稱換成有紋理的版本（顏色沿用原本的）。只動餐廳裡 DN_ 開頭的模型。</summary>
        private void DiningExperimentModelMaterials(Bounds room)
        {
            var cache = new System.Collections.Generic.Dictionary<Material, Material>();
            var area = room; area.Expand(0.3f);
            foreach (var r in Into.GetComponentsInChildren<Renderer>(true))
            {
                if (!area.Contains(r.bounds.center)) continue;
                bool dn = false;
                for (var t = r.transform; t != null && t != Into; t = t.parent) if (t.name.StartsWith("DN_")) { dn = true; break; }
                if (!dn) continue;
                var mats = r.sharedMaterials; bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || !m.name.StartsWith("LC_DN_")) continue;
                    if (!cache.TryGetValue(m, out var n)) { n = DiningExperimentMaterialFor(m); cache[m] = n; }
                    if (n != null) { mats[i] = n; changed = true; }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        private Material DiningExperimentMaterialFor(Material src)
        {
            string key = src.name.Substring(3);
            var col = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : Color.gray;
            var anom = new Color(0.5f, 0.5f, 0.5f);
            string tex = null; float tile = 1f, smooth = src.HasProperty("_Smoothness") ? src.GetFloat("_Smoothness") : 0.3f;
            // 布料：坐墊、燈罩、地毯、靠墊。
            // 地毯不換：它本身有設計好的圖案，套通用格紋會改掉設計。
            if (key.Contains("Rug")) return null;
            if (key.Contains("Fabric") || key.Contains("Cushion") || key.Contains("Shade") || key.Contains("Knit")) { tex = "fabric_pattern_07"; tile = 0.35f; smooth = 0.08f; }
            // 刷漆的木頭／金屬：書櫃以外的漆面、相框紙板。
            else if (key.Contains("Paint") || key.Contains("Cream") || key.Contains("Med") || key.Contains("Radio")) { tex = "painted_plaster_wall"; tile = 0.6f; }
            // 木頭（沒有貼圖的書架、框）。
            else if (key.Contains("Frame") && !key.Contains("Gilt")) { tex = "fine_grained_wood"; tile = 0.5f; }
            // 陶瓷與玻璃：不加貼圖，只把光滑度拉開（釉面亮、素燒霧）。
            else if (key.Contains("Porcelain") || key.Contains("Celadon") || key.Contains("BlueWhite") || key.Contains("Glaze")) { var m2 = new Material(src) { name = src.name + "_E1" }; m2.SetFloat("_Smoothness", 0.93f); return m2; }
            else if (key.Contains("Terracotta") || key.Contains("Soil")) { tex = "plastered_wall_03"; tile = 0.4f; smooth = 0.1f; }
            if (tex == null || !LanternReveal.HasPH(tex, "Tex")) return null;
            var m = PH(src.name + "_E1", tex, "Tex", tile, col * 1.08f, anom, smooth);
            if (src.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", src.GetColor("_EmissionColor"));
            if (src.HasProperty("_AnomalyEmission")) m.SetColor("_AnomalyEmission", src.GetColor("_AnomalyEmission"));
            if (src.HasProperty("_Metallic")) m.SetFloat("_Metallic", src.GetFloat("_Metallic"));
            return m;
        }

        /// <summary>
        /// E2：沒有烘焙 GI，所以用幾盞不投影、很弱的補光模擬「光打到地板和天花板再反彈」。
        /// 反射探針也試過（自訂立方體貼圖、即時探針兩種），在目前的 Forward+ 與執行時建立的探針下都沒有生效（金屬鏡面仍是黑的），
        /// 已記錄為待查問題，這一輪不放探針。
        /// </summary>
        private void DiningExperimentLight(Vector3 table, Vector3 sitPendant, float H)
        {
            var warm = new Color(1f, 0.82f, 0.64f);
            AddLight("DN_E2_FloorBounce", LightType.Point, new Vector3(table.x, 0.35f, table.z), warm, 0.35f, 4.2f, false, LightShadows.None);
            AddLight("DN_E2_CeilingBounce", LightType.Point, new Vector3(table.x, H - 0.35f, table.z), new Color(1f, 0.9f, 0.8f), 0.45f, 4.8f, false, LightShadows.None);
            AddLight("DN_E2_SittingBounce", LightType.Point, new Vector3(sitPendant.x, H - 0.4f, sitPendant.z), new Color(0.95f, 0.9f, 0.85f), 0.35f, 4.2f, false, LightShadows.None);
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        /// <summary>接觸陰影：貼在地上的柔邊深色面（不影響碰撞與謎題）。</summary>
        private Transform Blob(Vector3 at, Vector2 size, float yaw, float alpha)
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
            return q.transform;
        }
    }
}

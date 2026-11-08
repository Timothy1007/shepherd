using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// v0.2 室內生活細節、H0～H3 階段異變物件、校正後的實體證物 FP-001。
    /// 只放在既有房間內（不改牆、樓板、門、樓梯、通路、出生點、戰鬥區與既有互動熱區）。
    /// 各房間的異變是否已套用由 <see cref="Case01HouseQA"/> 決定（玩家不在該房間時才切換，避免當面硬閃）。
    /// </summary>
    public sealed class Case01Decor
    {
        // ---- H1：客廳 ----
        /// <summary>H1 時從書架上消失的兩本書（H3 歸位）。</summary>
        public readonly List<GameObject> LivingMissingBooks = new List<GameObject>();
        /// <summary>客廳北牆相框：照片內容（H0）／空白（H1 起，H3 仍空白＝殘留）。</summary>
        public GameObject LivingFramePhoto;
        public GameObject LivingFrameBlank;

        // ---- H1：餐廳 ----
        /// <summary>餐桌布巾（H1 不見，H3 回來）。</summary>
        public GameObject DiningRunner;
        /// <summary>托盤上第二個茶杯（H1 起消失，H3 仍缺＝殘留）。</summary>
        public GameObject TraySecondCup;
        public Vector3 ChairHomePos, ChairMovedPos;
        public Quaternion ChairHomeRot, ChairMovedRot;

        // ---- H2：書房 ----
        /// <summary>書架上重複出現的同一本書（H2 出現，H3 消失）。</summary>
        public GameObject StudyDuplicateBook;
        public readonly List<Transform> FileBoxes = new List<Transform>();
        public readonly List<Vector3> FileBoxHome = new List<Vector3>();
        public readonly List<Vector3> FileBoxShuffled = new List<Vector3>();
        public readonly List<Quaternion> FileBoxShuffledRot = new List<Quaternion>();
        /// <summary>H2：書房地上散落的紙張（核穩定後收起）。</summary>
        public GameObject StudyScatteredPapers;
        /// <summary>夾層裡散落的書、照片、紙張（形成演出的來源意象；只是外觀，不擋熱區與通道）。</summary>
        public Transform MezzanineScatterRoot;

        // ---- H2：樓梯平台 ----
        /// <summary>儲藏室門外多出來的紙箱（H2 出現，H3 消失）。</summary>
        public GameObject LandingExtraBoxes;
        public Transform LandingPicture;
        public Vector3 PictureHomePos, PictureMovedPos;
        public Quaternion PictureHomeRot, PictureMovedRot;

        // ---- 可操作物件 ----
        public Transform TeaTinLid;
        public Vector3 TeaLidClosedPos, TeaLidOpenPos;
        public Quaternion TeaLidClosedRot, TeaLidOpenRot;

        // ---- H3：校正後的證物 ----
        /// <summary>夾層中的 FP-001（台座＋照片），只在核穩定後、尚未拾取時顯示。</summary>
        public GameObject EvidenceRoot;
        /// <summary>照片模型（檢視模式複製這個物件）。</summary>
        public Transform EvidenceModel;

        // ---- 目前套用的狀態 ----
        public bool LivingChanged { get; private set; }
        public bool DiningChanged { get; private set; }
        public bool StudyChanged { get; private set; }
        public bool LandingChanged { get; private set; }
        public bool LivingStable { get; private set; }
        public bool DiningStable { get; private set; }
        public bool StudyStable { get; private set; }
        public bool LandingStable { get; private set; }
        public bool TeaTinOpen { get; private set; }

        private Transform chair;

        internal void BindChair(Transform c)
        {
            chair = c;
            ChairHomePos = c.localPosition;
            ChairHomeRot = c.localRotation;
            ChairMovedPos = ChairHomePos + new Vector3(0.6f, 0f, 0.38f);
            ChairMovedRot = Quaternion.Euler(0f, 28f, 0f);
        }

        public void ApplyLiving(bool changed, bool stabilized)
        {
            LivingChanged = changed;
            LivingStable = stabilized;
            foreach (var b in LivingMissingBooks) b.SetActive(!changed || stabilized);
            LivingFramePhoto.SetActive(!changed);
            LivingFrameBlank.SetActive(changed);
        }

        public void ApplyDining(bool changed, bool stabilized)
        {
            DiningChanged = changed;
            DiningStable = stabilized;
            DiningRunner.SetActive(!changed || stabilized);
            TraySecondCup.SetActive(!changed);
            bool moved = changed && !stabilized;
            if (chair != null)
            {
                chair.localPosition = moved ? ChairMovedPos : ChairHomePos;
                chair.localRotation = moved ? ChairMovedRot : ChairHomeRot;
            }
        }

        public void ApplyStudy(bool changed, bool stabilized)
        {
            StudyChanged = changed;
            StudyStable = stabilized;
            bool on = changed && !stabilized;
            StudyDuplicateBook.SetActive(on);
            if (StudyScatteredPapers != null) StudyScatteredPapers.SetActive(on);
            for (int i = 0; i < FileBoxes.Count; i++)
            {
                FileBoxes[i].localPosition = on ? FileBoxShuffled[i] : FileBoxHome[i];
                FileBoxes[i].localRotation = on ? FileBoxShuffledRot[i] : Quaternion.identity;
            }
        }

        public void ApplyLanding(bool changed, bool stabilized)
        {
            LandingChanged = changed;
            LandingStable = stabilized;
            bool on = changed && !stabilized;
            LandingExtraBoxes.SetActive(on);
            LandingPicture.localPosition = on ? PictureMovedPos : PictureHomePos;
            LandingPicture.localRotation = on ? PictureMovedRot : PictureHomeRot;
        }

        public void SetTeaTin(bool open)
        {
            TeaTinOpen = open;
            TeaTinLid.localPosition = open ? TeaLidOpenPos : TeaLidClosedPos;
            TeaTinLid.localRotation = open ? TeaLidOpenRot : TeaLidClosedRot;
        }

        public void SetEvidenceVisible(bool visible)
        {
            EvidenceRoot.SetActive(visible);
        }
    }

    public static class Case01HouseDecor
    {
        private static Material wood, darkWood, pale, fabric, fabric2, paper, paperOld, card, metal, ceramic, leaf, pot, cardboard, glow, lampShade, frameMat;
        private static Material[] bookMats;

        public static Case01Decor Build(Case01HouseLayout L, Transform root)
        {
            var d = new Case01Decor();
            P = L.Palette ?? new Case01Palette();
            Materials();
            var rng = new System.Random(2026);
            System.Func<float, float, float> R = (a, b) => a + (float)rng.NextDouble() * (b - a);

            Entrance(L, root);
            Kitchen(L, d, root);
            Living(L, d, root, R);
            Dining(L, d, root);
            Bedroom(L, root, R);
            Study(L, d, root, R);
            Landing(L, d, root);
            MezzanineScatter(root, R);
            Evidence(L, d, root);

            d.ApplyLiving(false, false);
            d.ApplyDining(false, false);
            d.ApplyStudy(false, false);
            d.ApplyLanding(false, false);
            d.SetTeaTin(false);
            d.SetEvidenceVisible(false);
            return d;
        }

        private static Case01Palette P;

        private static void Materials()
        {
            wood = P.Get("wood_warm");
            darkWood = P.Get("wood_dark");
            pale = P.Get("fabric_cream");
            fabric = P.Get("fabric_red");
            fabric2 = P.Get("fabric_green");
            paper = P.Get("paper");
            paperOld = P.Get("paper_old");
            card = P.Tint("paper", new Color(0.95f, 0.66f, 0.56f));
            metal = P.Get("brass");
            ceramic = P.Get("ceramic");
            leaf = P.Get("leaf");
            pot = P.Get("pot");
            cardboard = P.Get("cardboard");
            glow = P.Get("glow");
            lampShade = P.Get("lamp_shade");
            frameMat = P.Get("frame_wood");
            bookMats = new Material[7];
            for (int i = 0; i < bookMats.Length; i++) bookMats[i] = P.Get("book_" + i);
        }

        /// <summary>本次建置擁有的純色小道具材質（重建時釋放）。</summary>
        private static Material M(Color c, float smooth = 0.15f, Color? emission = null, float metallic = 0f)
        {
            return P.Own(ArenaArt.Mat(c, smooth, emission, metallic));
        }

        // ---------------- 一樓 ----------------

        private static void Entrance(Case01HouseLayout L, Transform root)
        {
            var g = ArenaArt.Group("Entrance", root, Vector3.zero);
            // 鞋櫃上的鑰匙盤。
            Cyl(g, new Vector3(5.7f, 0.915f, 0.35f), new Vector3(0.22f, 0.012f, 0.22f), ceramic, "KeyTray");
            Box(g, new Vector3(5.66f, 0.93f, 0.33f), new Vector3(0.05f, 0.006f, 0.02f), metal, "Key", new Vector3(0f, 30f, 0f));
            Box(g, new Vector3(5.74f, 0.93f, 0.38f), new Vector3(0.05f, 0.006f, 0.02f), metal, "Key", new Vector3(0f, -50f, 0f));
            // 傘架與兩把傘。
            Solid(PrimitiveType.Cylinder, g, new Vector3(5.38f, 0.25f, 1.15f), new Vector3(0.24f, 0.25f, 0.24f), darkWood, "UmbrellaStand");
            Cyl(g, new Vector3(5.36f, 0.6f, 1.12f), new Vector3(0.05f, 0.45f, 0.05f), bookMats[1], "Umbrella", new Vector3(6f, 0f, 4f));
            Cyl(g, new Vector3(5.42f, 0.55f, 1.2f), new Vector3(0.045f, 0.4f, 0.045f), bookMats[0], "Umbrella", new Vector3(-5f, 0f, -7f));
            // 掛鉤與外套。
            for (int i = 0; i < 3; i++)
                Cyl(g, new Vector3(5.12f, 1.7f, 3.5f + i * 0.3f), new Vector3(0.025f, 0.06f, 0.025f), metal, "Hook", new Vector3(0f, 0f, 90f));
            Prim(PrimitiveType.Capsule, g, new Vector3(5.2f, 1.25f, 3.5f), new Vector3(0.35f, 0.45f, 0.18f), fabric, "Coat");
            Prim(PrimitiveType.Capsule, g, new Vector3(5.2f, 1.3f, 4.1f), new Vector3(0.3f, 0.4f, 0.15f), fabric2, "Scarf");
            // 地墊。
            Box(g, new Vector3(7.1f, 0.006f, 0.75f), new Vector3(1.2f, 0.012f, 0.7f), fabric, "DoorMat");
        }

        private static void Kitchen(Case01HouseLayout L, Case01Decor d, Transform root)
        {
            var g = ArenaArt.Group("Kitchen", root, Vector3.zero);
            // 調味罐（不同顏色與高度）。
            Color[] spice = { new Color(0.7f, 0.3f, 0.15f), new Color(0.85f, 0.75f, 0.3f), new Color(0.3f, 0.5f, 0.25f), new Color(0.9f, 0.9f, 0.88f) };
            for (int i = 0; i < spice.Length; i++)
                Cyl(g, new Vector3(0.35f + (i % 2) * 0.1f, 0.96f + i * 0.004f, 3.15f + i * 0.13f), new Vector3(0.07f, 0.05f + i * 0.01f, 0.07f),
                    M(spice[i], 0.6f), "SpiceJar");
            // 碗盤架。
            var rack = ArenaArt.Group("DishRack", g, new Vector3(0.45f, 0.9f, 1.2f));
            Box(rack, new Vector3(0f, 0.01f, 0f), new Vector3(0.32f, 0.02f, 0.4f), metal, "Base");
            for (int i = 0; i < 4; i++) Cyl(rack, new Vector3(0f, 0.12f, -0.15f + i * 0.1f), new Vector3(0.22f, 0.006f, 0.22f), ceramic, "Plate", new Vector3(90f, 0f, 0f));
            // 茶葉盒（可以打開／蓋上）。
            var tin = ArenaArt.Group("TeaTin", g, new Vector3(0.55f, 0.9f, 2.6f));
            Cyl(tin, new Vector3(0f, 0.09f, 0f), new Vector3(0.12f, 0.09f, 0.12f), M(new Color(0.2f, 0.35f, 0.35f), 0.6f, null, 0.4f), "Body");
            d.TeaTinLid = Cyl(tin, Vector3.zero, new Vector3(0.13f, 0.012f, 0.13f), M(new Color(0.75f, 0.6f, 0.25f), 0.6f, null, 0.6f), "Lid");
            d.TeaLidClosedPos = new Vector3(0f, 0.185f, 0f);
            d.TeaLidClosedRot = Quaternion.identity;
            d.TeaLidOpenPos = new Vector3(0.1f, 0.12f, 0.02f);
            d.TeaLidOpenRot = Quaternion.Euler(0f, 0f, 75f);
            Cyl(tin, new Vector3(0f, 0.17f, 0f), new Vector3(0.1f, 0.005f, 0.1f), M(new Color(0.25f, 0.18f, 0.1f), 0.1f), "Leaves");
            Case01HouseBuilder.Hotspot(L, g, Case01Ids.TeaTin, "茶葉盒", new Vector3(0.55f, 1.0f, 2.6f), new Vector3(0.3f, 0.3f, 0.3f));
            // 小圓桌與凳子。
            Solid(PrimitiveType.Cylinder, g, new Vector3(2.0f, 0.36f, 1.4f), new Vector3(0.8f, 0.36f, 0.8f), wood, "KitchenTable");
            Cyl(g, new Vector3(2.0f, 0.745f, 1.4f), new Vector3(0.62f, 0.01f, 0.62f), pale, "TableCloth");
            Solid(PrimitiveType.Cylinder, g, new Vector3(2.75f, 0.23f, 1.1f), new Vector3(0.34f, 0.23f, 0.34f), darkWood, "Stool");
            Cyl(g, new Vector3(1.85f, 0.8f, 1.45f), new Vector3(0.12f, 0.05f, 0.12f), ceramic, "Bowl");
        }

        private static void Living(Case01HouseLayout L, Case01Decor d, Transform root, System.Func<float, float, float> R)
        {
            var g = ArenaArt.Group("Living", root, Vector3.zero);
            // 西牆書架與不同顏色、高度的書。
            // 避開西牆的窗（z 7.0～8.0）。
            var shelf = ArenaArt.Group("Bookshelf", g, new Vector3(0.26f, 0f, 8.9f));
            Solid(PrimitiveType.Cube, shelf, new Vector3(-0.1f, 0.9f, 0f), new Vector3(0.12f, 1.8f, 1.6f), darkWood, "Back");
            Solid(PrimitiveType.Cube, shelf, new Vector3(0.02f, 0.9f, -0.79f), new Vector3(0.34f, 1.8f, 0.04f), darkWood, "Side");
            Solid(PrimitiveType.Cube, shelf, new Vector3(0.02f, 0.9f, 0.79f), new Vector3(0.34f, 1.8f, 0.04f), darkWood, "Side");
            for (int s = 0; s < 4; s++) Box(shelf, new Vector3(0.02f, 0.05f + s * 0.45f, 0f), new Vector3(0.34f, 0.03f, 1.56f), darkWood, "Shelf");
            for (int s = 0; s < 3; s++)
            {
                float z = -0.72f;
                int n = 0;
                while (z < 0.68f)
                {
                    // 最上層（視線高度）中間兩本較厚、顏色明亮的書：H1 時消失，留下明顯的空位。
                    bool marked = s == 2 && (n == 4 || n == 5);
                    float w = marked ? 0.09f : R(0.035f, 0.07f), h = marked ? 0.36f : R(0.24f, 0.38f);
                    var mat = marked ? bookMats[n == 4 ? 3 : 5] : bookMats[(n + s * 3) % bookMats.Length];
                    var b = Box(shelf, new Vector3(0.02f, 0.07f + s * 0.45f + h * 0.5f, z + w * 0.5f), new Vector3(0.24f, h, w), mat, "Book",
                        new Vector3(n % 7 == 3 ? 8f : 0f, 0f, 0f));
                    if (marked) d.LivingMissingBooks.Add(b.gameObject);
                    z += w + 0.004f;
                    n++;
                    if (n == 9 && s == 2) z += 0.18f;
                }
            }
            // 夾著便箋、微微突出的那本書（可調查）。
            Box(shelf, new Vector3(0.12f, 0.07f + 0.9f + 0.15f, 0.55f), new Vector3(0.24f, 0.3f, 0.05f), bookMats[3], "BookWithNote");
            Box(shelf, new Vector3(0.12f, 0.07f + 0.9f + 0.32f, 0.56f), new Vector3(0.2f, 0.06f, 0.008f), paper, "NoteTab");
            Case01HouseBuilder.Hotspot(L, g, Case01Ids.LivingBookNote, "夾著便箋的書", new Vector3(0.45f, 1.15f, 9.45f), new Vector3(0.25f, 0.45f, 0.3f));

            // 北牆相框：H0 照片，H1 起空白（核修好後仍空白）。
            // 避開北牆的窗（x 2.0～3.0）。
            var frame = ArenaArt.Group("LivingFrame", g, new Vector3(1.1f, 1.75f, 9.9f));
            Box(frame, Vector3.zero, new Vector3(0.62f, 0.46f, 0.04f), frameMat, "Frame");
            d.LivingFrameBlank = Box(frame, new Vector3(0f, 0f, -0.022f), new Vector3(0.54f, 0.38f, 0.005f), pale, "BlankPrint").gameObject;
            var photo = ArenaArt.Group("Photo", frame, Vector3.zero);
            Box(photo, new Vector3(0f, 0.06f, -0.025f), new Vector3(0.54f, 0.26f, 0.005f), M(new Color(0.45f, 0.62f, 0.75f), 0.1f), "Sky");
            Box(photo, new Vector3(0f, -0.12f, -0.025f), new Vector3(0.54f, 0.14f, 0.005f), M(new Color(0.75f, 0.68f, 0.5f), 0.1f), "Sand");
            Box(photo, new Vector3(-0.07f, -0.04f, -0.03f), new Vector3(0.05f, 0.18f, 0.004f), frameMat, "FigureA");
            Box(photo, new Vector3(0.05f, -0.05f, -0.03f), new Vector3(0.045f, 0.15f, 0.004f), frameMat, "FigureB");
            d.LivingFramePhoto = photo.gameObject;

            // 閱讀燈、毛毯、植物、衣架、茶几雜物。
            var lamp = ArenaArt.Group("ReadingLamp", g, new Vector3(4.15f, 0f, 9.45f));
            Cyl(lamp, new Vector3(0f, 0.02f, 0f), new Vector3(0.3f, 0.02f, 0.3f), metal, "Base");
            Solid(PrimitiveType.Cylinder, lamp, new Vector3(0f, 0.75f, 0f), new Vector3(0.04f, 0.75f, 0.04f), metal, "Pole");
            Cyl(lamp, new Vector3(0f, 1.55f, 0f), new Vector3(0.36f, 0.14f, 0.36f), lampShade, "Shade");
            L.LampLights.Add(ArenaArt.PointLight(lamp, new Vector3(0f, 1.4f, -0.15f), new Color(1f, 0.74f, 0.45f), 3.2f, 0.6f, false));
            Box(g, new Vector3(2.9f, 0.82f, 9.15f), new Vector3(0.9f, 0.03f, 0.55f), fabric2, "Blanket", new Vector3(0f, 12f, 4f));
            Box(g, new Vector3(3.3f, 0.6f, 8.85f), new Vector3(0.5f, 0.4f, 0.03f), fabric2, "BlanketDrape", new Vector3(10f, 12f, 0f));
            var plant = ArenaArt.Group("Plant", g, new Vector3(4.5f, 0f, 5.55f));
            Solid(PrimitiveType.Cylinder, plant, new Vector3(0f, 0.18f, 0f), new Vector3(0.36f, 0.18f, 0.36f), pot, "Pot");
            Prim(PrimitiveType.Sphere, plant, new Vector3(0f, 0.6f, 0f), new Vector3(0.5f, 0.55f, 0.5f), leaf, "Leaves");
            Prim(PrimitiveType.Sphere, plant, new Vector3(0.12f, 0.85f, 0.05f), new Vector3(0.3f, 0.32f, 0.3f), leaf, "Leaves");
            var rackGo = ArenaArt.Group("CoatRack", g, new Vector3(0.45f, 0f, 5.55f));
            Solid(PrimitiveType.Cylinder, rackGo, new Vector3(0f, 0.85f, 0f), new Vector3(0.05f, 0.85f, 0.05f), darkWood, "Pole");
            Cyl(rackGo, new Vector3(0f, 0.03f, 0f), new Vector3(0.4f, 0.03f, 0.4f), darkWood, "Foot");
            Prim(PrimitiveType.Capsule, rackGo, new Vector3(0.08f, 1.25f, 0.02f), new Vector3(0.34f, 0.42f, 0.16f), bookMats[1], "Coat");
            Cyl(g, new Vector3(2.2f, 0.45f, 8.05f), new Vector3(0.08f, 0.05f, 0.08f), ceramic, "Mug");
            Box(g, new Vector3(2.55f, 0.405f, 8.2f), new Vector3(0.28f, 0.01f, 0.2f), bookMats[6], "Magazine", new Vector3(0f, 18f, 0f));
            Box(g, new Vector3(2.4f, 0.41f, 7.95f), new Vector3(0.05f, 0.02f, 0.16f), frameMat, "Remote", new Vector3(0f, -25f, 0f));
            // 朋友寄來的卡片（可調查）。
            Box(g, new Vector3(2.85f, 0.47f, 8.05f), new Vector3(0.16f, 0.12f, 0.005f), card, "FriendCard", new Vector3(-12f, 160f, 0f));
            Case01HouseBuilder.Hotspot(L, g, Case01Ids.FriendCard, "茶几上的卡片", new Vector3(2.85f, 0.5f, 8.05f), new Vector3(0.3f, 0.25f, 0.3f));
            Box(g, new Vector3(2.5f, 0.006f, 7.6f), new Vector3(2.4f, 0.012f, 1.6f), fabric, "Rug");
        }

        private static void Dining(Case01HouseLayout L, Case01Decor d, Transform root)
        {
            var g = ArenaArt.Group("Dining", root, Vector3.zero);
            var table = L.DiningChairNorth.parent;
            // 布巾（桌面中央）與餐墊。
            d.DiningRunner = Box(table, new Vector3(0f, 0.754f, 0f), new Vector3(1.75f, 0.004f, 0.32f), M(new Color(0.7f, 0.25f, 0.22f), 0.05f), "TableRunner").gameObject;
            Box(table, new Vector3(0f, 0.752f, -0.32f), new Vector3(0.6f, 0.004f, 0.4f), pale, "PlacematSouth");
            Box(L.DiningSecondSet.transform, new Vector3(0f, 0.002f, 0f), new Vector3(0.6f, 0.004f, 0.4f), pale, "PlacematNorth");
            d.BindChair(L.DiningChairNorth);
            // 餐具櫃上的托盤與兩人份茶具。
            var tray = ArenaArt.Group("TeaTray", g, new Vector3(9.62f, 0.9f, 8.15f));
            Box(tray, new Vector3(0f, 0.01f, 0f), new Vector3(0.34f, 0.02f, 0.46f), metal, "Tray");
            Prim(PrimitiveType.Sphere, tray, new Vector3(0f, 0.1f, 0.1f), new Vector3(0.16f, 0.14f, 0.16f), ceramic, "Teapot");
            Cyl(tray, new Vector3(-0.09f, 0.12f, 0.1f), new Vector3(0.03f, 0.05f, 0.03f), ceramic, "Spout", new Vector3(0f, 0f, 50f));
            Cyl(tray, new Vector3(0.05f, 0.05f, -0.1f), new Vector3(0.08f, 0.035f, 0.08f), ceramic, "Teacup");
            d.TraySecondCup = Cyl(tray, new Vector3(-0.07f, 0.05f, -0.15f), new Vector3(0.08f, 0.035f, 0.08f), ceramic, "Teacup2").gameObject;
            // 西北角小櫃子與購物清單（可調查）。
            Solid(PrimitiveType.Cube, g, new Vector3(5.45f, 0.4f, 9.45f), new Vector3(0.55f, 0.8f, 0.6f), wood, "SmallCabinet");
            Box(g, new Vector3(5.45f, 0.805f, 9.4f), new Vector3(0.12f, 0.004f, 0.2f), paper, "ShoppingList", new Vector3(0f, 14f, 0f));
            Cyl(g, new Vector3(5.6f, 0.86f, 9.6f), new Vector3(0.1f, 0.06f, 0.1f), M(new Color(0.4f, 0.55f, 0.7f), 0.7f), "Vase");
            Case01HouseBuilder.Hotspot(L, g, Case01Ids.ShoppingList, "櫃子上的購物清單", new Vector3(5.45f, 0.9f, 9.4f), new Vector3(0.35f, 0.25f, 0.4f));
        }

        // ---------------- 二樓 ----------------

        private static void Bedroom(Case01HouseLayout L, Transform root, System.Func<float, float, float> R)
        {
            float y = Case01HouseBuilder.FloorHeight;
            var g = ArenaArt.Group("Bedroom", root, Vector3.zero);
            // 床頭小燈（暖光）。
            Cyl(g, new Vector3(1.0f, y + 0.62f, 9.65f), new Vector3(0.1f, 0.02f, 0.1f), metal, "LampBase");
            Cyl(g, new Vector3(1.0f, y + 0.72f, 9.65f), new Vector3(0.02f, 0.1f, 0.02f), metal, "LampStem");
            Cyl(g, new Vector3(1.0f, y + 0.86f, 9.65f), new Vector3(0.18f, 0.08f, 0.18f), lampShade, "LampShade");
            ArenaArt.PointLight(g, new Vector3(1.0f, y + 0.9f, 9.4f), ArenaArt.LampWarm, 3.2f, 0.7f, false);
            // 床上的衣物、地上的書。
            Box(g, new Vector3(2.0f, y + 0.52f, 8.0f), new Vector3(0.45f, 0.03f, 0.55f), bookMats[1], "Shirt", new Vector3(0f, 25f, 0f));
            Box(g, new Vector3(2.4f, y + 0.535f, 7.85f), new Vector3(0.3f, 0.02f, 0.6f), pale, "Scarf", new Vector3(0f, -40f, 0f));
            for (int i = 0; i < 3; i++)
                Box(g, new Vector3(3.65f, y + 0.03f + i * 0.05f, 9.5f), new Vector3(0.3f, 0.045f, 0.22f), bookMats[i + 2], "FloorBook", new Vector3(0f, R(-20f, 20f), 0f));
            // 衣櫃。
            Solid(PrimitiveType.Cube, g, new Vector3(0.4f, y + 1.0f, 6.0f), new Vector3(0.6f, 2.0f, 1.2f), wood, "Wardrobe");
            Box(g, new Vector3(0.71f, y + 1.0f, 6.0f), new Vector3(0.01f, 1.9f, 0.01f), darkWood, "WardrobeGap");
            // 小書桌：旅行路線（可調查）。
            Solid(PrimitiveType.Cube, g, new Vector3(4.3f, y + 0.36f, 9.45f), new Vector3(0.8f, 0.72f, 0.5f), darkWood, "WritingTable");
            Box(g, new Vector3(4.25f, y + 0.725f, 9.42f), new Vector3(0.42f, 0.004f, 0.3f), paperOld, "TravelRoute", new Vector3(0f, -8f, 0f));
            for (int i = 0; i < 4; i++)
                Box(g, new Vector3(4.1f + i * 0.09f, y + 0.729f, 9.35f + (i % 2) * 0.05f), new Vector3(0.05f, 0.002f, 0.008f), bookMats[0], "RouteDash", new Vector3(0f, 20f, 0f));
            Case01HouseBuilder.Hotspot(L, g, Case01Ids.TravelRoute, "書桌上的旅行路線", new Vector3(4.25f, y + 0.85f, 9.4f), new Vector3(0.5f, 0.3f, 0.4f));
            // 床上的工作邀請信（可調查）。
            Box(g, new Vector3(2.85f, y + 0.515f, 8.95f), new Vector3(0.22f, 0.006f, 0.12f), paper, "InvitationLetter", new Vector3(0f, 30f, 0f));
            Case01HouseBuilder.Hotspot(L, g, Case01Ids.JobLetter, "床上的信", new Vector3(2.85f, y + 0.6f, 8.95f), new Vector3(0.35f, 0.25f, 0.3f));
        }

        private static void Study(Case01HouseLayout L, Case01Decor d, Transform root, System.Func<float, float, float> R)
        {
            float y = Case01HouseBuilder.FloorHeight;
            var g = ArenaArt.Group("Study", root, Vector3.zero);
            // 書架西面的書（避開校正記錄的位置 z 7.25～7.75、y 4.1～4.6）。
            float[] rows = { y + 0.25f, y + 0.75f, y + 1.75f };
            int n = 0;
            foreach (var ry in rows)
            {
                float z = 6.6f;
                while (z < 8.4f)
                {
                    float w = R(0.04f, 0.075f), h = R(0.25f, 0.36f);
                    Box(g, new Vector3(9.4f, ry + h * 0.5f, z + w * 0.5f), new Vector3(0.18f, h, w), bookMats[n % bookMats.Length], "StudyBook");
                    z += w + 0.006f;
                    n++;
                }
            }
            // H0 就有的一本紅色舊書（最上層前端），H2 時在另一端出現一模一樣的第二本。
            Box(g, new Vector3(9.36f, rows[2] + 0.2f, 6.55f), new Vector3(0.2f, 0.4f, 0.08f), bookMats[0], "RedLedger");
            d.StudyDuplicateBook = Box(g, new Vector3(9.36f, rows[2] + 0.2f, 8.45f), new Vector3(0.2f, 0.4f, 0.08f), bookMats[0], "RedLedgerDuplicate").gameObject;
            // 檔案盒（西牆下，分色標籤）：H2 重排。
            Color[] tabs = { new Color(0.8f, 0.25f, 0.2f), new Color(0.25f, 0.45f, 0.8f), new Color(0.85f, 0.75f, 0.25f) };
            for (int i = 0; i < 3; i++)
            {
                var home = new Vector3(5.38f, y, 8.35f + i * 0.45f);
                var box = ArenaArt.Group("FileBox_" + i, g, home);
                Solid(PrimitiveType.Cube, box, new Vector3(0f, 0.17f, 0f), new Vector3(0.36f, 0.34f, 0.4f), cardboard, "Box");
                Box(box, new Vector3(0.185f, 0.25f, 0f), new Vector3(0.01f, 0.06f, 0.14f), M(tabs[i], 0.3f), "Tab");
                d.FileBoxes.Add(box);
                d.FileBoxHome.Add(home);
            }
            d.FileBoxShuffled.Add(new Vector3(5.38f, y, 9.25f));
            d.FileBoxShuffled.Add(new Vector3(5.4f, y, 8.35f));
            d.FileBoxShuffled.Add(new Vector3(5.36f, y, 8.8f));
            d.FileBoxShuffledRot.Add(Quaternion.Euler(0f, 9f, 0f));
            d.FileBoxShuffledRot.Add(Quaternion.Euler(0f, -14f, 0f));
            d.FileBoxShuffledRot.Add(Quaternion.Euler(0f, 4f, 0f));
            Case01HouseBuilder.Hotspot(L, g, Case01Ids.FileBoxes, "檔案盒", new Vector3(5.5f, y + 0.45f, 8.8f), new Vector3(0.3f, 0.2f, 1.2f));
            // 桌角分色文件夾、修復工具（可調查）、檯燈、舊照片。
            for (int i = 0; i < 4; i++)
                Box(g, new Vector3(8.2f, y + 0.765f + i * 0.012f, 9.45f), new Vector3(0.3f, 0.01f, 0.22f), M(i % 2 == 0 ? new Color(0.7f, 0.65f, 0.5f) : tabs[i % 3], 0.2f), "Folder",
                    new Vector3(0f, i * 4f - 6f, 0f));
            var kit = ArenaArt.Group("RepairKit", g, new Vector3(8.25f, y + 0.755f, 9.15f));
            Box(kit, Vector3.zero, new Vector3(0.3f, 0.004f, 0.18f), fabric2, "Cloth");
            Cyl(kit, new Vector3(-0.08f, 0.012f, 0f), new Vector3(0.015f, 0.07f, 0.015f), wood, "Brush", new Vector3(90f, 30f, 0f));
            Box(kit, new Vector3(0.04f, 0.01f, 0.02f), new Vector3(0.01f, 0.006f, 0.12f), metal, "Tweezers", new Vector3(0f, -20f, 0f));
            Box(kit, new Vector3(0.1f, 0.008f, -0.03f), new Vector3(0.025f, 0.004f, 0.14f), pale, "BoneFolder", new Vector3(0f, 10f, 0f));
            Case01HouseBuilder.Hotspot(L, g, Case01Ids.RepairKit, "修復工具", new Vector3(8.25f, y + 0.85f, 9.15f), new Vector3(0.35f, 0.2f, 0.25f));
            var desk = ArenaArt.Group("DeskLamp", g, new Vector3(6.68f, y + 0.75f, 9.65f));
            Cyl(desk, new Vector3(0f, 0.015f, 0f), new Vector3(0.14f, 0.015f, 0.14f), metal, "Base");
            Cyl(desk, new Vector3(0f, 0.2f, 0f), new Vector3(0.02f, 0.2f, 0.02f), metal, "Arm", new Vector3(0f, 0f, 12f));
            Cyl(desk, new Vector3(0.06f, 0.4f, 0f), new Vector3(0.14f, 0.06f, 0.14f), glow, "Head", new Vector3(0f, 0f, 30f));
            L.LampLights.Add(ArenaArt.PointLight(desk, new Vector3(0.1f, 0.3f, -0.15f), new Color(1f, 0.9f, 0.75f), 2.4f, 0.55f, false));
            var pic = ArenaArt.Group("OldPhoto", g, new Vector3(7.3f, y + 1.35f, 9.91f));
            Box(pic, Vector3.zero, new Vector3(0.24f, 0.18f, 0.02f), frameMat, "Frame");
            Box(pic, new Vector3(0f, 0f, -0.012f), new Vector3(0.2f, 0.14f, 0.004f), paperOld, "Print");
            Box(g, new Vector3(7.0f, y + 0.006f, 7.4f), new Vector3(1.8f, 0.012f, 1.2f), fabric2, "StudyRug");
            var scatter = ArenaArt.Group("StudyScatteredPapers", g, Vector3.zero);
            for (int i = 0; i < 7; i++)
                Box(scatter, new Vector3(R(6.0f, 8.6f), y + 0.014f + i * 0.001f, R(6.2f, 8.2f)), new Vector3(0.21f, 0.002f, 0.29f), i % 3 == 0 ? paperOld : paper, "LoosePaper",
                    new Vector3(0f, R(0f, 360f), 0f));
            d.StudyScatteredPapers = scatter.gameObject;
        }

        /// <summary>夾層四角與牆邊散落的書、照片、紙張（避開核、△○□ 門洞、FP-001 台座與通道）。</summary>
        private static void MezzanineScatter(Transform root, System.Func<float, float, float> R)
        {
            var c = Case01HouseBuilder.MezzCenter;
            var g = ArenaArt.Group("MezzanineScatter", root, Vector3.zero);
            Vector3[] spots =
            {
                c + new Vector3(-5.0f, 0f, -4.9f), c + new Vector3(5.0f, 0f, -4.9f), c + new Vector3(-5.0f, 0f, 4.8f), c + new Vector3(4.9f, 0f, 4.9f),
                c + new Vector3(-2.2f, 0f, -5.5f), c + new Vector3(2.4f, 0f, -5.4f), c + new Vector3(2.6f, 0f, 5.5f), c + new Vector3(-5.5f, 0f, 2.4f),
                c + new Vector3(5.5f, 0f, -2.4f),
            };
            for (int s = 0; s < spots.Length; s++)
            {
                var pile = ArenaArt.Group("Pile_" + s, g, spots[s]);
                int books = s < 4 ? 3 : 1;
                for (int i = 0; i < books; i++)
                    Box(pile, new Vector3(R(-0.1f, 0.1f), 0.025f + i * 0.045f, R(-0.1f, 0.1f)), new Vector3(0.24f, 0.04f, 0.17f), bookMats[(s + i) % bookMats.Length], "Book",
                        new Vector3(0f, R(0f, 360f), 0f));
                for (int i = 0; i < 4; i++)
                    Box(pile, new Vector3(R(-0.6f, 0.6f), 0.004f + i * 0.001f, R(-0.6f, 0.6f)), new Vector3(0.21f, 0.002f, 0.29f), i % 2 == 0 ? paper : paperOld, "Paper",
                        new Vector3(R(-3f, 3f), R(0f, 360f), 0f));
                // 照片：小相紙＋模糊的影像。
                var photo = ArenaArt.Group("Photo", pile, new Vector3(R(-0.4f, 0.4f), 0.006f, R(-0.4f, 0.4f)));
                photo.localRotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
                Box(photo, Vector3.zero, new Vector3(0.12f, 0.003f, 0.09f), pale, "PhotoCard");
                Box(photo, new Vector3(0f, 0.002f, 0f), new Vector3(0.1f, 0.002f, 0.07f), M(new Color(0.35f, 0.38f, 0.4f), 0.3f), "PhotoImage");
            }
        }

        private static void Landing(Case01HouseLayout L, Case01Decor d, Transform root)
        {
            float y = Case01HouseBuilder.FloorHeight;
            var g = ArenaArt.Group("Landing", root, Vector3.zero);
            Box(g, new Vector3(6.9f, y + 0.006f, 2.4f), new Vector3(1.3f, 0.012f, 2.4f), fabric, "Runner");
            // 南牆壁燈。
            Box(g, new Vector3(5.75f, y + 1.9f, 0.1f), new Vector3(0.08f, 0.2f, 0.06f), metal, "SconceBracket");
            Cyl(g, new Vector3(5.75f, y + 2.05f, 0.16f), new Vector3(0.14f, 0.08f, 0.14f), glow, "SconceShade");
            L.LampLights.Add(ArenaArt.PointLight(g, new Vector3(5.75f, y + 1.95f, 0.35f), new Color(0.9f, 0.88f, 1f), 3f, 0.45f, false));
            // 南牆小置物架與花瓶。
            Box(g, new Vector3(5.55f, y + 1.35f, 0.16f), new Vector3(0.5f, 0.03f, 0.18f), darkWood, "WallShelf");
            Cyl(g, new Vector3(5.45f, y + 1.46f, 0.16f), new Vector3(0.08f, 0.08f, 0.08f), M(new Color(0.35f, 0.5f, 0.6f), 0.7f), "Vase");
            Prim(PrimitiveType.Sphere, g, new Vector3(5.66f, y + 1.42f, 0.16f), new Vector3(0.09f, 0.09f, 0.09f), pot, "Ornament");
            // 西牆掛畫：H2 歪斜並往下滑。
            d.LandingPicture = ArenaArt.Group("LandingPicture", g, new Vector3(5.09f, y + 1.65f, 4.0f));
            // 正面（本地 -z）朝東，面向樓梯平台。
            d.LandingPicture.localRotation = Quaternion.Euler(0f, -90f, 0f);
            Box(d.LandingPicture, Vector3.zero, new Vector3(0.7f, 0.5f, 0.03f), frameMat, "Frame");
            Box(d.LandingPicture, new Vector3(0f, 0f, -0.018f), new Vector3(0.62f, 0.42f, 0.004f), M(new Color(0.35f, 0.45f, 0.4f), 0.1f), "Landscape");
            Box(d.LandingPicture, new Vector3(0.1f, -0.06f, -0.022f), new Vector3(0.3f, 0.12f, 0.004f), M(new Color(0.55f, 0.5f, 0.35f), 0.1f), "Hill");
            d.PictureHomePos = d.LandingPicture.localPosition;
            d.PictureHomeRot = d.LandingPicture.localRotation;
            d.PictureMovedPos = d.PictureHomePos + new Vector3(0f, -0.22f, -0.3f);
            d.PictureMovedRot = Quaternion.Euler(0f, -90f, 13f);
            // 儲藏室門外的紙箱；H2 多出兩個。
            Solid(PrimitiveType.Cube, g, new Vector3(5.42f, y + 0.2f, 1.0f), new Vector3(0.5f, 0.4f, 0.45f), cardboard, "Carton");
            var extra = ArenaArt.Group("ExtraCartons", g, Vector3.zero);
            Solid(PrimitiveType.Cube, extra, new Vector3(5.45f, y + 0.18f, 1.55f), new Vector3(0.46f, 0.36f, 0.42f), cardboard, "CartonExtra", new Vector3(0f, 8f, 0f));
            Solid(PrimitiveType.Cube, extra, new Vector3(5.42f, y + 0.6f, 1.02f), new Vector3(0.38f, 0.3f, 0.36f), cardboard, "CartonStacked", new Vector3(0f, -12f, 0f));
            d.LandingExtraBoxes = extra.gameObject;
        }

        // ---------------- 夾層：FP-001 ----------------

        private static void Evidence(Case01HouseLayout L, Case01Decor d, Transform root)
        {
            var c = Case01HouseBuilder.MezzCenter;
            var g = ArenaArt.Group("EvidenceFP001", root, c + new Vector3(-2.6f, 0f, 1.2f));
            Solid(PrimitiveType.Cylinder, g, new Vector3(0f, 0.45f, 0f), new Vector3(0.18f, 0.45f, 0.18f), frameMat, "Stand");
            Cyl(g, new Vector3(0f, 0.91f, 0f), new Vector3(0.4f, 0.02f, 0.4f), M(new Color(0.3f, 0.25f, 0.2f), 0.4f, ArenaArt.LampWarm * 0.5f), "Plate");
            d.EvidenceModel = BuildPhotoModel(g);
            d.EvidenceModel.localPosition = new Vector3(0f, 1.2f, 0f);
            // 正面朝向夾層中央（核的方向）。
            d.EvidenceModel.localRotation = Quaternion.Euler(-12f, -66f, 0f);
            ArenaArt.PointLight(g, new Vector3(0f, 1.6f, -0.5f), ArenaArt.LampWarm, 3f, 0.9f, false);
            Case01HouseBuilder.Hotspot(L, g, Case01Ids.EvidencePhoto, "夾層裡的照片", new Vector3(0f, 1.15f, 0f), new Vector3(0.6f, 0.6f, 0.6f));
            d.EvidenceRoot = g.gameObject;
        }

        /// <summary>
        /// FP-001 照片模型：正面（本地 -z）兩人並肩、第二人只剩空白輪廓；背面（+z）鉛筆字與撕掉的一角。
        /// 正反兩面都是真的幾何與文字，檢視模式可轉到背面看。
        /// </summary>
        private static Material PaperCard()
        {
            return P != null ? P.PaperStandard(new Color(0.9f, 0.87f, 0.78f)) : ArenaArt.Mat(new Color(0.88f, 0.85f, 0.76f), 0.1f);
        }

        public static Transform BuildPhotoModel(Transform parent)
        {
            var m = ArenaArt.Group("FP001_Photo", parent, Vector3.zero);
            Box(m, Vector3.zero, new Vector3(0.3f, 0.22f, 0.012f), PaperCard(), "Card");
            // 正面。
            Box(m, new Vector3(0f, 0.01f, -0.007f), new Vector3(0.26f, 0.17f, 0.002f), M(new Color(0.55f, 0.6f, 0.58f), 0.1f), "Image");
            Box(m, new Vector3(0f, -0.045f, -0.009f), new Vector3(0.26f, 0.06f, 0.002f), M(new Color(0.48f, 0.4f, 0.32f), 0.1f), "Floor");
            Box(m, new Vector3(-0.045f, 0.0f, -0.011f), new Vector3(0.035f, 0.11f, 0.002f), M(new Color(0.2f, 0.17f, 0.17f), 0.1f), "FigureA");
            var outline = M(new Color(0.92f, 0.92f, 0.88f), 0.1f, new Color(0.25f, 0.25f, 0.22f));
            Box(m, new Vector3(0.025f, 0.0f, -0.011f), new Vector3(0.004f, 0.1f, 0.002f), outline, "OutlineL");
            Box(m, new Vector3(0.055f, 0.0f, -0.011f), new Vector3(0.004f, 0.1f, 0.002f), outline, "OutlineR");
            Box(m, new Vector3(0.04f, 0.05f, -0.011f), new Vector3(0.034f, 0.004f, 0.002f), outline, "OutlineTop");
            // 背面：鉛筆字＋撕掉的一角。
            Box(m, new Vector3(0.12f, -0.085f, 0.0065f), new Vector3(0.06f, 0.05f, 0.002f), M(new Color(0.08f, 0.07f, 0.06f), 0.1f), "TornCorner",
                new Vector3(0f, 0f, 35f));
            var back = Case01Hud.WorldLabel(m, new Vector3(-0.01f, 0.02f, 0.0075f), 180f, "週日。兩個人。\n<size=18>（第二行的字被擦掉了）</size>", 28, new Color(0.25f, 0.22f, 0.2f), 300f);
            back.rectTransform.parent.localScale = Vector3.one * 0.0008f;
            ((RectTransform)back.rectTransform.parent).sizeDelta = new Vector2(300f, 180f);
            back.fontStyle = FontStyle.Italic;
            return m;
        }

        // ---------------- 小工具 ----------------

        private static Transform Box(Transform p, Vector3 pos, Vector3 scale, Material m, string name, Vector3? euler = null)
        {
            return ArenaArt.Prim(PrimitiveType.Cube, p, pos, scale, m, euler, name, false, false);
        }

        private static Transform Cyl(Transform p, Vector3 pos, Vector3 scale, Material m, string name, Vector3? euler = null)
        {
            return ArenaArt.Prim(PrimitiveType.Cylinder, p, pos, scale, m, euler, name, false, false);
        }

        private static Transform Prim(PrimitiveType t, Transform p, Vector3 pos, Vector3 scale, Material m, string name)
        {
            return ArenaArt.Prim(t, p, pos, scale, m, null, name, false, true);
        }

        private static Transform Solid(PrimitiveType t, Transform p, Vector3 pos, Vector3 scale, Material m, string name, Vector3? euler = null)
        {
            return ArenaArt.Prim(t, p, pos, scale, m, euler, name, true, true);
        }
    }
}

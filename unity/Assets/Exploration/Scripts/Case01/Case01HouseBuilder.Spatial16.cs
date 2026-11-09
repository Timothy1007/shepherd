using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// Phase 1.6 空間結構灰盒 QA 的三個方案（只用於「Phase 1.6 空間結構灰盒 QA」的房屋實例；正式 Demo、其他 QA 都不呼叫）。
    /// A＝最新 v0.5 A 原樣（不做任何幾何修改）；B＝不改外框與分區，只改開口位置／寬度、門框深度與遮擋；
    /// C＝有限重分區的過渡空間（前廊、半遮層架、緊湊廚房、樓梯收束、二樓到臥室的短走廊），外框仍是 10×10 m。
    /// </summary>
    public enum Case01SpatialVariant
    {
        A,
        B,
        C,
    }

    public static partial class Case01HouseBuilder
    {
        /// <summary>Phase 1.6 方案的尺寸（公尺；文件與測試共用）。</summary>
        public static class Spatial16
        {
            // B：玄關→客廳門（同位置，1.4 → 1.0 m）；客廳↔餐廳 2.4 m 置中寬開口 → 1.2 m 西側偏置門洞（壁爐旁）。
            public const float BLivingDoorW = 1.0f;
            public const float BDiningDoorX = 1.85f, BDiningDoorW = 1.2f;
            public const float LiningDepth = 0.3f;
            // B／C：冰箱移到餐廳→廚房門的北側（進門後轉身才看到紙條）。
            public static readonly Vector3 FridgeByDoor = new Vector3(5.8f, 0f, 9.0f);
            // B／C：樓梯起步旁的牆垛（x8.65，z4.4～5.3），從廚房門口看不到樓梯。
            public const float StairNibZ1 = 5.3f;
            // C：前廊隔牆（z1.6，1.0 m 內門）；客廳門移到 z2.6（1.0 m）；廚房門 0.9 m。
            public const float VestibuleZ = 1.6f, VestibuleDoorW = 1.0f;
            public const float CLivingDoorZ = 2.6f, CLivingDoorW = 1.0f;
            public const float CKitchenDoorW = 0.9f;
            // C：客廳↔餐廳寬開口的西半邊放開放層架（半遮），東半邊 1.2 m 通行。
            public const float CScreenX0 = 1.5f, CScreenX1 = 2.7f;
            // C：二樓平台 → 短走廊（x3.3～5、z3.8～5）→ 臥室門（z5、x4.15、0.9 m）。
            public const float CorridorX0 = 3.3f, CorridorZ0 = 3.8f, CorridorDoorZ = 4.4f, CorridorDoorW = 0.95f;
            public const float BedroomDoorX = 4.15f, BedroomDoorW = 0.9f;
        }

        private static readonly HashSet<string> wallPieceNames = new HashSet<string> { "Wall", "Lintel", "WindowHead", "WindowApron", "Skirting", "Casing" };

        /// <summary>
        /// 在剛建好的方案 A（V05A）房屋上套用 Phase 1.6 方案。必須在 <see cref="Build"/> 之後立刻呼叫（共用同一次建置的材質與窗洞登記）。
        /// A 不做任何事。只改牆段、開口、門框深度與少數家具位置；所有熱區、H0～H3 物件、樓梯、失序門與異常空間不變。
        /// </summary>
        public static void ApplySpatialVariant(Case01HouseLayout L, Case01SpatialVariant v)
        {
            if (v == Case01SpatialVariant.A || L == null) return;
            if (L.Floorplan != Case01Floorplan.V05A) throw new System.ArgumentException("Phase 1.6 方案只建立在方案 A（V05A）上");
            P = L.Palette;
            Materials();
            MaterialsV05();
            trimWalls = true;
            try
            {
                var g1 = L.Root.Find("GroundFloor");
                var g2 = L.Root.Find("SecondFloor");
                if (v == Case01SpatialVariant.B) SpatialB(L, g1, g2);
                else SpatialC(L, g1, g2);
                Physics.SyncTransforms();
                HideOrphanMarks(L);
            }
            finally
            {
                trimWalls = false;
            }
            Physics.SyncTransforms();
        }

        // ---------------- B：錯位開口與視線遮擋（不改外框與分區） ----------------

        private static void SpatialB(Case01HouseLayout L, Transform g1, Transform g2)
        {
            float hx0 = V05A.HallX0, lz1 = V05A.LivingZ1;
            // B1 玄關→客廳：位置不變，1.4 m → 1.0 m，客廳側 0.3 m 門框深度（像穿過一道厚牆進到前廳）。
            RemoveWallLine(g1, false, hx0, 0f, 10f);
            WallXWide(g1, hx0, 0f, 10f, 0f, 2.8f, wallMat, new Vector2(V05A.LivingDoorZ, Spatial16.BLivingDoorW), new Vector2(V05A.DiningKitchenDoorZ, DoorWidth));
            Lining(g1, false, hx0, V05A.LivingDoorZ, Spatial16.BLivingDoorW, Spatial16.LiningDepth, -1f, 0f, "LivingDoor");
            // B2 客廳↔餐廳：置中 2.4 m 寬開口 → 壁爐旁的 1.2 m 偏置門洞（餐廳側 0.3 m 門框）。從客廳入口看不到餐桌。
            RemoveWallLine(g1, true, lz1, 0f, hx0);
            WallZWide(g1, lz1, 0f, hx0, 0f, 2.8f, wallMat, new Vector2(Spatial16.BDiningDoorX, Spatial16.BDiningDoorW));
            Lining(g1, true, lz1, Spatial16.BDiningDoorX, Spatial16.BDiningDoorW, Spatial16.LiningDepth, 1f, 0f, "DiningDoor");
            // 扶手椅往東 0.15 m，壁爐與咖啡桌之間留出走到門洞的通道。
            MoveRigid(L, Child(g1, "Armchair"), new Vector3(2.75f, 0f, 4.5f), 200f);
            // B3 餐廳→廚房：門不變；冰箱移到門的北側（正面朝東），進門先看到備餐檯，轉身才看到紙條。舊冰箱位置放高櫃。
            MoveRigid(L, Child(g1, "Fridge"), Spatial16.FridgeByDoor, 90f, Case01Ids.SundayNote);
            Larder(g1, new Vector3(9.62f, 0f, 9.6f), -90f);
            // B4 廚房→樓梯：樓梯起步旁的牆垛，樓梯要走到廚房東側才出現。
            WallX(g1, V05A.HallX1, V05A.HallZ1, Spatial16.StairNibZ1, 0f, 2.8f, wallMat);
            // B5 二樓：書房門（平台側）與臥室門（書房側）加門框深度；第三扇門維持平貼外牆、沒有門框（和正常門的差異）。
            Lining(g2, true, 5f, StudyDoorX, DoorWidth, Spatial16.LiningDepth, -1f, FloorHeight, "StudyDoor");
            Lining(g2, false, 5f, 7.5f, DoorWidth, 0.25f, 1f, FloorHeight, "BedroomDoor");
        }

        // ---------------- C：過渡空間與空間節奏（有限重分區，外框不變） ----------------

        private static void SpatialC(Case01HouseLayout L, Transform g1, Transform g2)
        {
            float hx0 = V05A.HallX0, hx1 = V05A.HallX1, y = FloorHeight;
            // C1 前廊（收）：前門與內門之間 1.5 m 深的前廊；內門 1.0 m，正對玄關北牆的掛畫。
            WallZWide(g1, Spatial16.VestibuleZ, hx0, hx1, 0f, 2.8f, wallMat, new Vector2(7.1f, Spatial16.VestibuleDoorW));
            Lining(g1, true, Spatial16.VestibuleZ, 7.1f, Spatial16.VestibuleDoorW, 0.2f, 1f, 0f, "VestibuleDoor");
            MoveRigid(L, Child(g1, "UnderStairCupboard"), new Vector3(hx1 - 0.085f, 0f, 2.25f), -90f);
            // C2 客廳門移到內玄關（z2.6，1.0 m，客廳側門框深度）；從門口看到的終點是壁爐（前景是沙發背）。
            RemoveWallLine(g1, false, hx0, 0f, 10f);
            WallXWide(g1, hx0, 0f, 10f, 0f, 2.8f, wallMat, new Vector2(Spatial16.CLivingDoorZ, Spatial16.CLivingDoorW), new Vector2(V05A.DiningKitchenDoorZ, Spatial16.CKitchenDoorW));
            Lining(g1, false, hx0, Spatial16.CLivingDoorZ, Spatial16.CLivingDoorW, Spatial16.LiningDepth, -1f, 0f, "LivingDoor");
            Lining(g1, false, hx0, V05A.DiningKitchenDoorZ, Spatial16.CKitchenDoorW, 0.25f, 1f, 0f, "KitchenDoor");
            MoveRigid(L, Child(g1, "ShoeBench"), new Vector3(hx0 + 0.3f, 0f, 3.7f), 90f);
            MoveRigid(L, Child(g1, "CoatHooks"), new Vector3(hx0 + 0.09f, 1.72f, 3.7f), 90f);
            MoveRigid(L, Child(g1, "Calendar"), new Vector3(hx0 - 0.085f, 1.5f, 3.75f), 90f, Case01Ids.Calendar);
            MoveRigid(L, Child(g1, "Armchair"), new Vector3(2.3f, 0f, 4.45f), 200f);
            // C3 客廳↔餐廳（半遮）：寬開口保留，西半邊放開放式層架（矮碰撞，視線可以從層板間穿過），東半邊 1.2 m 通行。
            OpenShelf(g1, new Vector3((Spatial16.CScreenX0 + Spatial16.CScreenX1) * 0.5f, 0f, V05A.LivingZ1), Spatial16.CScreenX1 - Spatial16.CScreenX0);
            // C4 廚房（較密）：0.9 m 入口＋門框深度；冰箱在門邊；東北角高櫃；水槽東側的半島工作檯把廚房分成備餐區與收納角。
            MoveRigid(L, Child(g1, "Fridge"), Spatial16.FridgeByDoor, 90f, Case01Ids.SundayNote);
            Larder(g1, new Vector3(9.62f, 0f, 9.6f), -90f);
            Peninsula(g1, new Vector3(8.6f, 0f, 8.75f));
            // C5 樓梯（收束）：起步旁牆垛＋起步上方的低樑，走進去才往上打開。
            WallX(g1, hx1, V05A.HallZ1, Spatial16.StairNibZ1, 0f, 2.8f, wallMat);
            Solid(g1, new Vector3((hx1 + WallThickness * 0.5f + 10f) * 0.5f, 2.475f, Spatial16.StairNibZ1), new Vector3(10f - hx1 - WallThickness * 0.5f, 0.65f, WallThickness), wallMat, "Bulkhead");
            // C6 二樓：平台西側新開短走廊（佔用不開放儲藏室的北緣），盡頭轉向臥室門；書房門加門框深度。
            RemoveWallLine(g2, false, 5f, 0f, 10f);
            WallXWide(g2, 5f, 0f, 10f, y, FloorHeight, wallUp, new Vector2(2.5f, DoorWidth), new Vector2(Spatial16.CorridorDoorZ, Spatial16.CorridorDoorW), new Vector2(7.5f, DoorWidth));
            RemoveWallLine(g2, true, 5f, 0f, 10f);
            WallZWide(g2, 5f, 0f, 10f, y, FloorHeight, wallUp, new Vector2(Spatial16.BedroomDoorX, Spatial16.BedroomDoorW), new Vector2(StudyDoorX, DoorWidth));
            WallZ(g2, Spatial16.CorridorZ0, Spatial16.CorridorX0, 5f, y, FloorHeight, wallUp);
            WallX(g2, Spatial16.CorridorX0, Spatial16.CorridorZ0, 5f, y, FloorHeight, wallUp);
            Lining(g2, true, 5f, StudyDoorX, DoorWidth, Spatial16.LiningDepth, -1f, y, "StudyDoor");
            // 走廊屬於樓梯平台（房間判定放在最前面，優先於儲藏室）。
            var b = new Bounds();
            b.SetMinMax(new Vector3(Spatial16.CorridorX0, 2.75f, Spatial16.CorridorZ0), new Vector3(5f, 6.5f, 5f));
            L.Rooms.Insert(0, new Case01RoomVolume { Room = Case01Room.Landing, Bounds = b });
            // 平台西牆的掛畫讓出走廊入口（H2 的歪斜位移跟著一起改）。
            var d = L.Decor;
            if (d != null && d.LandingPicture != null)
            {
                var off = d.PictureMovedPos - d.PictureHomePos;
                d.PictureHomePos = new Vector3(d.PictureHomePos.x, d.PictureHomePos.y, 3.48f);
                d.PictureMovedPos = d.PictureHomePos + off;
                d.LandingPicture.localPosition = d.PictureHomePos;
            }
        }

        // ---------------- 工具 ----------------

        private static Transform Child(Transform g, string name)
        {
            // 客廳家具在 LivingOld 群組裡（v0.6），所以往下找。
            foreach (var c in g.GetComponentsInChildren<Transform>(true)) if (c.name == name && c != g) return c;
            throw new System.InvalidOperationException("找不到 " + name);
        }

        /// <summary>移除一條牆線（固定 z＝alongX，或固定 x）在 a0～a1 範圍內的牆段、楣、踢腳與門框（之後用新的開口重建）。</summary>
        private static void RemoveWallLine(Transform g, bool alongX, float line, float a0, float a1)
        {
            var kill = new List<GameObject>();
            foreach (Transform c in g)
            {
                if (!wallPieceNames.Contains(c.name)) continue;
                var p = c.localPosition;
                var s = c.localScale;
                float perp = alongX ? p.z : p.x, along = alongX ? p.x : p.z, thick = alongX ? s.z : s.x;
                if (Mathf.Abs(perp - line) > 0.2f || thick > 0.2f) continue;
                if (along < a0 - 0.05f || along > a1 + 0.05f) continue;
                kill.Add(c.gameObject);
            }
            foreach (var k in kill)
            {
                k.SetActive(false);
                ArenaArt.Kill(k);
            }
        }

        /// <summary>門框深度：開口兩側的側板（有碰撞）＋上方楣板，從牆面往 side 方向凸出 depth；開口淨寬不變。</summary>
        private static void Lining(Transform g, bool alongX, float line, float c, float w, float depth, float side, float y, string name)
        {
            float face = line + side * WallThickness * 0.5f;
            float mid = face + side * depth * 0.5f;
            System.Func<float, float, Vector3> P3 = (along, perp) => alongX ? new Vector3(along, 0f, perp) : new Vector3(perp, 0f, along);
            System.Func<float, float, float, Vector3> S3 = (len, h, dep) => alongX ? new Vector3(len, h, dep) : new Vector3(dep, h, len);
            foreach (var e in new[] { -1f, 1f })
            {
                var pos = P3(c + e * (w * 0.5f + 0.025f), mid) + Vector3.up * (y + DoorHeight * 0.5f + 0.05f);
                Solid(g, pos, S3(0.05f, DoorHeight + 0.1f, depth), trim, "Lining_" + name);
            }
            Deco(g, P3(c, mid) + Vector3.up * (y + DoorHeight + 0.06f), S3(w + 0.1f, 0.12f, depth), trim, "Lining_" + name, true);
        }

        /// <summary>把家具群組移到新位置（整組旋轉），一併移動列出的調查熱區（熱區和家具分開掛在樓層群組下）。</summary>
        private static void MoveRigid(Case01HouseLayout L, Transform obj, Vector3 pos, float yaw, params string[] hotspots)
        {
            var oldPos = obj.position;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var delta = rot * Quaternion.Inverse(obj.rotation);
            foreach (var id in hotspots)
            {
                var h = L.Interactables[id].transform;
                h.position = pos + delta * (h.position - oldPos);
                h.rotation = delta * h.rotation;
            }
            obj.position = pos;
            obj.rotation = rot;
        }

        private static void Larder(Transform g, Vector3 pos, float yaw)
        {
            var t = Group(g, "Larder", pos, yaw);
            Box(t, new Vector3(0f, 1.05f, 0f), new Vector3(0.62f, 2.1f, 0.6f), P.Get("ceramic"), "Body");
            Box(t, new Vector3(0f, 1.05f, 0.305f), new Vector3(0.01f, 2.0f, 0.01f), darkWood, "DoorGap");
            foreach (var x in new[] { -0.06f, 0.06f }) Box(t, new Vector3(x, 1.0f, 0.31f), new Vector3(0.015f, 0.14f, 0.02f), steelM, "Handle");
            Block(t, new Vector3(0f, 1.05f, 0f), new Vector3(0.62f, 2.1f, 0.6f), "LarderBlock");
        }

        /// <summary>半島工作檯（南北向，接在北側檯面的東端）。</summary>
        private static void Peninsula(Transform g, Vector3 pos)
        {
            var t = Group(g, "KitchenPeninsula", pos, 0f);
            Box(t, new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.82f, 1.3f), P.Get("ceramic"), "Cabinets");
            Box(t, new Vector3(0f, 0.885f, 0f), new Vector3(0.66f, 0.04f, 1.34f), stoneM, "Worktop");
            Box(t, new Vector3(0f, 0.93f, -0.25f), new Vector3(0.4f, 0.03f, 0.28f), wood, "ChoppingBoard");
            Cyl(t, new Vector3(0.05f, 0.95f, 0.25f), new Vector3(0.24f, 0.05f, 0.24f), P.Get("ceramic"), "Bowl");
            Block(t, new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.9f, 1.3f), "PeninsulaBlock");
        }

        /// <summary>開放式層架（沒有背板）：視線可從層板之間穿過。碰撞只到 1.0 m（擋人、不擋眼高的視線）。</summary>
        private static void OpenShelf(Transform g, Vector3 pos, float width)
        {
            var t = Group(g, "HalfScreenShelf", pos, 0f);
            float hw = width * 0.5f;
            const float dep = 0.34f, h = 1.85f;
            foreach (var x in new[] { -hw + 0.02f, hw - 0.02f }) Box(t, new Vector3(x, h * 0.5f, 0f), new Vector3(0.04f, h, dep), wood, "Side");
            float[] shelves = { 0.08f, 0.5f, 0.95f, 1.4f, 1.83f };
            foreach (var s in shelves) Box(t, new Vector3(0f, s, 0f), new Vector3(width - 0.04f, 0.03f, dep - 0.02f), wood, "Shelf");
            var rng = new System.Random(16);
            for (int row = 1; row < 3; row++)
            {
                float x = -hw + 0.08f;
                while (x < hw - 0.3f)
                {
                    float bw = 0.03f + (float)rng.NextDouble() * 0.04f, bh = 0.22f + (float)rng.NextDouble() * 0.12f;
                    Box(t, new Vector3(x + bw * 0.5f, shelves[row] + 0.015f + bh * 0.5f, 0f), new Vector3(bw, bh, 0.2f), bookM[(int)(x * 37f + row) & 3], "Book");
                    x += bw + 0.004f;
                    if (rng.NextDouble() < 0.12) x += 0.25f;
                }
            }
            Cyl(t, new Vector3(hw - 0.2f, shelves[3] + 0.1f, 0f), new Vector3(0.12f, 0.1f, 0.12f), potM, "Pot");
            Box(t, new Vector3(-hw + 0.25f, shelves[3] + 0.11f, 0.02f), new Vector3(0.18f, 0.2f, 0.02f), frameM, "SmallFrame", new Vector3(-6f, 0f, 0f));
            Block(t, new Vector3(0f, 0.5f, 0f), new Vector3(width, 1.0f, dep), "ShelfBlock");
        }

        /// <summary>舊化的牆面污痕：新開口處已經沒有牆的就藏起來（不讓污痕浮在門洞裡）。</summary>
        private static void HideOrphanMarks(Case01HouseLayout L)
        {
            var aging = L.Root.Find("Aging");
            if (aging == null) return;
            var buf = new Collider[16];
            foreach (var t in aging.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Stain" && t.name != "Peel") continue;
                int n = Physics.OverlapSphereNonAlloc(t.position, 0.06f, buf, ~0, QueryTriggerInteraction.Ignore);
                bool wall = false;
                for (int i = 0; i < n; i++)
                {
                    var c = buf[i];
                    if (c.enabled && c.gameObject.activeInHierarchy && (c.name == "Wall" || c.name == "WindowApron" || c.name.StartsWith("Lining_"))) wall = true;
                }
                if (!wall) t.localScale = Vector3.zero;
            }
        }
    }
}

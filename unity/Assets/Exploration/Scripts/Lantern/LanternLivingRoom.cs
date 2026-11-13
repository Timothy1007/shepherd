using Shepherd.Battle.UI.Arena;
using UnityEngine;
using Mode = Shepherd.Exploration.Lantern.LanternReveal.Mode;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 客廳樣板（房間配方 v0.1，見 docs/level 第 10 節）：
    /// 身分＝母女一起看電視、也在這裡吵架的地方。
    /// 專屬的提燈謎題＝聚光照壁爐上的全家福（已有）；地標＝全家福（有一盞小畫燈，從玄關就看得到）。
    /// 光的對照驚喜＝沙發上沒織完的圍巾（暗處只有防塵布）、兩個馬克杯（暗處只剩一個倒著的）。
    /// 生活痕跡＝馬克杯（一個有口紅印）、遙控器上的便條、門框上的身高刻痕。
    /// 會反應的東西＝立燈（要有電）、壁爐的防火屏、窗簾、電視、靠墊、掉在地上的書、壁爐上的鐘。
    /// 秘密＝防火屏後面、灰裡燒了一半的照片。
    /// 所有文字都是暫定，之後統一改寫。
    /// </summary>
    public sealed partial class LanternGraybox
    {
        public int LivingInteractables { get; private set; }

        private void HouseLivingRoom()
        {
            float f = FurnitureScale;
            int before = Toggles.Count + Inspectables.Count + Tetherables.Count;
            var ceramic = Mat("LL_Ceramic", Mode.RealOnly, new Color(0.92f, 0.9f, 0.85f), Color.black, null, null, 0.75f, 0.5f, 0f);
            var ceramic2 = Mat("LL_CeramicBlue", Mode.RealOnly, new Color(0.35f, 0.45f, 0.62f), Color.black, null, null, 0.75f, 0.5f, 0f);
            var lipstick = Mat("LL_Lipstick", Mode.RealOnly, new Color(0.62f, 0.12f, 0.14f), Color.black, null, null, 0.4f, 0.4f, 0f);
            var ruinMug = Mat("LL_RuinMug", Mode.AnomalyOnly, Color.black, new Color(0.45f, 0.47f, 0.48f), null, null, 0.3f, 0.2f, 0.004f);
            var wool = Mat("LL_Wool", Mode.RealOnly, new Color(0.55f, 0.13f, 0.16f), Color.black, Resources.Load<Texture2D>("PH/Tex/quatrefoil_jacquard_fabric_diff"), null, 0.05f, 0.1f, 0f);
            var paper = Mat("LL_Note", Mode.RealOnly, new Color(1f, 0.92f, 0.5f), Color.black, null, null, 0.1f, 0.1f, 0f);
            var plastic = Mat("LL_Remote", Mode.RealOnly, new Color(0.08f, 0.08f, 0.09f), Color.black, null, null, 0.5f, 0.5f, 0f);
            var pencil = Mat("LL_Pencil", Mode.RealOnly, new Color(0.15f, 0.12f, 0.1f), Color.black, null, null, 0.2f, 0.2f, 0f);
            float table = 0.42f * f;

            // 1. 兩個馬克杯（一個有口紅印）；廢墟裡只剩一個倒在桌上。
            Vector3 Mug(string name, float x, float z, Material m)
            {
                var p = HP(x, 0f, z) + Vector3.up * table;
                Cyl(name, p + Vector3.up * 0.05f, new Vector3(0.085f, 0.05f, 0.085f), m);
                Box(name + "Handle", p + new Vector3(0.055f, 0.05f, 0f), new Vector3(0.02f, 0.055f, 0.012f), m, false);
                return p;
            }
            Mug("MugA", 1.48f, 1.82f, ceramic);
            var b = Mug("MugB", 1.72f, 1.9f, ceramic2);
            Cyl("MugLipstick", b + new Vector3(-0.03f, 0.1f, 0.02f), new Vector3(0.025f, 0.003f, 0.012f), lipstick);
            Cyl("RuinMug", HP(1.55f, 0f, 1.86f) + Vector3.up * (table + 0.04f), new Vector3(0.085f, 0.05f, 0.085f), ruinMug, new Vector3(0f, 30f, 90f));

            // 2. 遙控器與便條。
            var remote = new GameObject("RemoteNote").transform;
            remote.SetParent(Into, false);
            remote.position = HP(1.78f, 0f, 2.32f) + Vector3.up * table;
            Box("Remote", remote.position + Vector3.up * 0.012f, new Vector3(0.05f, 0.02f, 0.17f), plastic, false, new Vector3(0f, 18f, 0f), remote);
            Box("StickyNote", remote.position + new Vector3(0.005f, 0.024f, 0.02f), new Vector3(0.045f, 0.002f, 0.045f), paper, false, new Vector3(0f, 25f, 0f), remote);
            AddInspect(remote.gameObject, LanternInspectable.Kind.Pages, "遙控器上的便條", null, null, new[]
            {
                "（暫定）黃色便條，字寫得很大：\n\n「媽：看新聞按 3。\n不要再按到紅色那顆了。」",
            }, "");

            // 3. 門框上的身高刻痕（客廳通往玄關的門）。暗處的門框是剝落的木板，刻痕不見了。
            var jamb = HP(3.72f, 0f, 2.3f) + new Vector3(-(IntT / 2f + 0.028f), 0f, -TrimW / 2f);
            var marks = new GameObject("HeightMarks").transform;
            marks.SetParent(Into, false);
            marks.position = jamb + Vector3.up * 1.2f;
            for (int i = 0; i < 7; i++)
            {
                float hgt = 0.82f + i * 0.13f + (i % 2) * 0.02f;
                Box("Mark", jamb + Vector3.up * hgt, new Vector3(0.004f, 0.006f, 0.05f + (i % 3) * 0.008f), pencil, false, null, marks);
            }
            var mc = marks.gameObject.AddComponent<BoxCollider>();
            mc.size = new Vector3(0.1f, 1.0f, 0.12f);
            mc.center = Vector3.up * 0.0f;
            AddInspect(marks.gameObject, LanternInspectable.Kind.Pages, "門框上的刻痕", null, null, new[]
            {
                "（暫定）門框上一道一道鉛筆線。\n最低一條旁邊寫著：艾琳，五歲。",
                "（暫定）最高一條寫著「十八」。\n再往上，什麼都沒有了。",
            }, "");

            // 4. 沙發上沒織完的圍巾（光裡才看得到；暗處只有防塵布）。
            var scarf = new GameObject("UnfinishedScarf").transform;
            scarf.SetParent(Into, false);
            scarf.position = HP(2.48f, 0f, 2.55f) + Vector3.up * 0.46f * f;
            Box("Scarf", scarf.position + Vector3.up * 0.01f, new Vector3(0.16f, 0.018f, 0.55f), wool, false, new Vector3(0f, 12f, 0f), scarf);
            Box("ScarfTail", scarf.position + new Vector3(0.05f, -0.1f, 0.3f), new Vector3(0.16f, 0.2f, 0.016f), wool, false, new Vector3(0f, 12f, 0f), scarf);
            Ball("Yarn", scarf.position + new Vector3(-0.12f, 0.05f, -0.2f), Vector3.one * 0.1f, wool, scarf);
            Cyl("Needle", scarf.position + new Vector3(0.02f, 0.03f, -0.22f), new Vector3(0.006f, 0.17f, 0.006f), pencil, new Vector3(80f, 20f, 0f), scarf);
            Cyl("Needle", scarf.position + new Vector3(-0.03f, 0.03f, -0.2f), new Vector3(0.006f, 0.17f, 0.006f), pencil, new Vector3(75f, -15f, 0f), scarf);
            AddInspect(scarf.gameObject, LanternInspectable.Kind.Object, "沒織完的圍巾", null, null, null, "（暫定）織到一半，針還插在上面。顏色是她會喜歡的那種。");

            // 5. 立燈（要有電才會亮）。
            var lampAt = HP(0.32f, 0f, 1.25f);
            var lampRoot = new GameObject("FloorLamp").transform;
            lampRoot.SetParent(Into, false);
            lampRoot.position = lampAt;
            Cyl("LampBase", lampAt + Vector3.up * 0.015f, new Vector3(0.28f, 0.015f, 0.28f), hDark, null, lampRoot);
            Cyl("LampPole", lampAt + Vector3.up * 0.75f, new Vector3(0.03f, 0.75f, 0.03f), hDark, null, lampRoot);
            Cyl("LampShade", lampAt + Vector3.up * 1.52f, new Vector3(0.36f, 0.13f, 0.36f), hFabric, null, lampRoot);
            var lc = lampRoot.gameObject.AddComponent<BoxCollider>();
            lc.center = new Vector3(0f, 0.85f, 0f);
            lc.size = new Vector3(0.4f, 1.7f, 0.4f);
            var ll = new GameObject("FloorLampLight").AddComponent<Light>();
            ll.transform.SetParent(Into, false);
            ll.transform.position = lampAt + Vector3.up * 1.45f;
            ll.type = LightType.Point; ll.range = 3.8f; ll.intensity = 1.6f; ll.color = WarmLamp; ll.shadows = LightShadows.Soft; ll.enabled = false;
            var lt = AddToggle(lampRoot.gameObject, "立燈", LanternToggle.Kind.Light, null, Vector3.zero, Vector3.zero, Quaternion.identity, Quaternion.identity, new[] { ll }, "打開", "關掉");
            lt.RequiresPower = true;

            // 6. 壁爐的防火屏（轉開）＋秘密：灰裡燒了一半的照片。
            var hearth = HP(0.62f, 0f, 2.1f);
            var hinge = new GameObject("FireScreenHinge").transform;
            hinge.SetParent(Into, false);
            hinge.position = hearth + new Vector3(0.22f, 0f, -0.42f * f);
            var screen = Box("FireScreen", hinge.position + new Vector3(0f, 0.36f, 0.42f * f), new Vector3(0.025f, 0.7f, 0.82f * f), hDark, true, null, hinge);
            Box("FireScreenTrim", hinge.position + new Vector3(0f, 0.72f, 0.42f * f), new Vector3(0.04f, 0.04f, 0.86f * f), hTrim, false, null, hinge);
            AddToggle(screen.gameObject, "壁爐的防火屏", LanternToggle.Kind.Rotate, hinge, Vector3.zero, Vector3.zero, Quaternion.identity, Quaternion.Euler(0f, -105f, 0f), null, "移開", "擺回去");
            var ash = Mat("LL_Ash", Mode.Dual, new Color(0.12f, 0.11f, 0.1f), new Color(0.1f, 0.1f, 0.1f), null, null, 0.05f, 0.05f, 0f);
            Box("Ash", hearth + new Vector3(-0.08f, 0.035f, 0f), new Vector3(0.36f, 0.03f, 0.7f), ash, false);
            var burnt = Mat("LL_BurntPhoto", Mode.RealOnly, Color.white, Color.black, LanternReveal.Photo(true), null, 0.2f, 0.2f, 0f);
            burnt.SetColor("_EmissionColor", new Color(0.35f, 0.18f, 0.06f));
            var frag = Box("BurntPhoto", hearth + new Vector3(-0.05f, 0.055f, 0.05f), new Vector3(0.12f, 0.004f, 0.09f), burnt, true, new Vector3(0f, 35f, 6f));
            var secret = AddInspect(frag.gameObject, LanternInspectable.Kind.Pages, "燒了一半的照片", null, null, new[]
            {
                "（暫定）照片只剩下半邊：兩雙鞋，一大一小，站在海邊。\n\n背面有字，燒得只剩「……別告訴媽」。",
            }, "");
            secret.Secret = true;

            // 7. 可以抓、可以丟的東西：扶手椅上的靠墊、掉在地上的書。
            var cushion = Tether("LivingCushion", "靠墊", HP(1.25f, 0f, 1.45f), new Vector3(0.4f, 0.14f, 0.4f), 0.6f);
            Box("CushionBody", cushion.transform.position + Vector3.up * 0.07f, new Vector3(0.4f, 0.14f, 0.4f), hFabric, false, new Vector3(0f, 35f, 0f), cushion.transform);
            var book = Tether("FallenBook", "掉在地上的書", HP(0.75f, 0f, 3.35f), new Vector3(0.24f, 0.05f, 0.17f), 0.8f);
            Box("BookBody", book.transform.position + Vector3.up * 0.025f, new Vector3(0.24f, 0.05f, 0.17f), hLeather, false, new Vector3(0f, 40f, 0f), book.transform);

            // 地標：壁爐上的全家福有一盞小畫燈（從玄關就看得到這一點暖光）。
            var pl = new GameObject("PhotoLight").AddComponent<Light>();
            pl.transform.SetParent(Into, false);
            pl.transform.position = HP(0.9f, 2.2f, 1.75f);
            pl.transform.rotation = Quaternion.LookRotation((HP(0.48f, 1.3f, 1.75f) - pl.transform.position).normalized);
            pl.type = LightType.Spot; pl.spotAngle = 40f; pl.innerSpotAngle = 20f; pl.range = 3f; pl.intensity = 2.2f; pl.color = new Color(1f, 0.82f, 0.55f); pl.shadows = LightShadows.Soft;

            LivingInteractables = Toggles.Count + Inspectables.Count + Tetherables.Count - before;
        }
    }
}

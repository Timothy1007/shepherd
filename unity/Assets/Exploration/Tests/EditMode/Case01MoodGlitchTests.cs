using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shepherd.Exploration.Case01;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// v0.4 Stage B：H0→H3 光色衰退（H3 不回暖：主燈、小燈、燈泡自發光、環境光、核與夾層都不回暖）、
    /// 階段漸變、舊化痕跡、失序門訊號故障（狀態對應、降低閃爍、不影響穿門、雜音開關）。
    /// </summary>
    public sealed class Case01MoodGlitchTests
    {
        private const float Dt = 1f / 30f;
        private readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var r in roots)
                if (r != null)
                {
                    var qa = r.GetComponent<Case01HouseQA>();
                    if (qa != null) qa.RestoreAtmosphere();
                    Object.DestroyImmediate(r);
                }
            roots.Clear();
        }

        private Case01HouseQA NewQa(Case01Checkpoint? cp = null, Case01QaMode? mode = null)
        {
            var go = new GameObject("Case01MoodTest");
            roots.Add(go);
            var qa = go.AddComponent<Case01HouseQA>();
            qa.Build(false, null, null, cp, mode);
            return qa;
        }

        private static void Run(Case01HouseQA qa, float s)
        {
            for (float t = 0; t < s; t += Dt) qa.Tick(Dt, Case01Input.None);
        }

        private static bool NotWarm(Color c) { return c.b >= c.r - 0.005f; }

        [Test]
        public void MoodRules_H3IsColdNeutral_NeverBackToH0()
        {
            var rooms = new[] { Case01Room.Entrance, Case01Room.Kitchen, Case01Room.Living, Case01Room.Dining, Case01Room.Landing, Case01Room.Study, Case01Room.Bedroom };
            var warm = new Color(1f, 0.72f, 0.42f);
            foreach (var r in rooms)
            {
                Color c0, c1, c2, c3;
                float i0, i1, i2, i3;
                Case01Mood.RoomLight(0, r, warm, 1f, out c0, out i0);
                Case01Mood.RoomLight(1, r, warm, 1f, out c1, out i1);
                Case01Mood.RoomLight(2, r, warm, 1f, out c2, out i2);
                Case01Mood.RoomLight(3, r, warm, 1f, out c3, out i3);
                Assert.AreEqual(warm, c0, "H0 保持原本暖光");
                Assert.LessOrEqual(c1.r - c1.b, c0.r - c0.b, r + "：H1 開始變冷");
                Assert.Less(c2.r - c2.b, c1.r - c1.b + 0.001f, r + "：H2 更冷");
                Assert.IsTrue(NotWarm(c3), r + "：H3 冷白／中性偏冷（不回暖）" + c3);
                Assert.Less(i3, 0.8f, r + "：H3 不恢復 H0 亮度");
                Assert.Less(i2, i1, r + "：H2 比 H1 暗");
            }
            Color dl, ll;
            float di, li;
            Case01Mood.RoomLight(1, Case01Room.Dining, warm, 1f, out dl, out di);
            Case01Mood.RoomLight(1, Case01Room.Study, warm, 1f, out ll, out li);
            Assert.Greater(dl.b - dl.r, ll.b - ll.r, "H1 餐廳／客廳先受影響");
            for (int n = 0; n < 4; n++)
            {
                Color lc;
                float lh3;
                Case01Mood.Lamp(3, n, warm, 1f, out lc, out lh3);
                Assert.Less(lh3, 0.35f, "H3 小燈不再亮回來");
            }
            Assert.AreEqual(0, Case01Mood.AgingLevel(0, Case01Room.Dining));
            Assert.AreEqual(3, Case01Mood.AgingLevel(3, Case01Room.Dining), "H3 舊化保留");
        }

        [Test]
        public void H3_AllHouseLights_BulbsAmbientCoreMezz_StayCold_AndRestoreOnExit()
        {
            var savedSky = RenderSettings.ambientSkyColor;
            var qa = NewQa(null, Case01QaMode.NarrativeV04);
            qa.ApplyAtmosphere();
            Run(qa, 0.2f);
            var L0 = qa.Layout;
            var h0Dining = L0.RoomLights[Case01Room.Dining].color;
            Assert.IsFalse(NotWarm(h0Dining), "H0 暖光（基準）");

            qa.Tick(Dt, new Case01Input { Function = 6 });
            qa.Tick(Dt, new Case01Input { Function = 6 });
            qa.Tick(Dt, new Case01Input { Function = 6 });
            Assert.AreEqual(3, qa.ArtStage);
            Assert.AreEqual(3, qa.LightStage);
            Run(qa, 3f);
            var L = qa.Layout;
            foreach (var kv in L.RoomLights)
            {
                Assert.IsTrue(NotWarm(kv.Value.color), "H3 主燈不回暖：" + kv.Key + " " + kv.Value.color);
                Assert.Less(kv.Value.intensity, L.RoomLightIntensity[kv.Key] * 0.8f, "H3 不恢復 H0 亮度：" + kv.Key);
            }
            Assert.Greater(L.LampLights.Count, 2);
            foreach (var lamp in L.LampLights) Assert.Less(lamp.intensity, 1.1f, "小燈不再亮回來");
            foreach (var b in L.Bulbs)
            {
                Assert.AreNotSame(qa.Palette.Get("glow"), b.sharedMaterial, "燈泡自發光換成冷色");
                var e = b.sharedMaterial.GetColor("_EmissionColor");
                Assert.IsTrue(NotWarm(e), "燈泡自發光不回暖 " + e);
            }
            Assert.IsTrue(NotWarm(L.CoreLight.color), "核穩定＝冷白");
            foreach (var m in L.MezzLights) Assert.IsTrue(NotWarm(m.color), "夾層燈不回暖");
            Assert.IsTrue(NotWarm(RenderSettings.ambientSkyColor), "環境光偏冷");
            Assert.IsTrue(L.Decor.DiningStable, "物件位置可恢復（但不回 H0 光色）");

            qa.RestoreAtmosphere();
            Assert.AreEqual(savedSky, RenderSettings.ambientSkyColor, "離開時還原環境光");
        }

        [Test]
        public void StageChange_BlendsOverAboutASecond_NotHardCut()
        {
            var qa = NewQa();
            Run(qa, 0.2f);
            var dining = qa.Layout.RoomLights[Case01Room.Dining];
            var before = dining.color;
            // H0 → H1：看過兩副餐具、離開、改寫（流程正常操作）。
            qa.Flow.Interact(Case01Ids.Table);
            qa.Flow.EnterRoom(Case01Room.Dining, Case01Room.Entrance);
            qa.Flow.ApplyDiningRewrite();
            Assert.AreEqual(1, qa.LightStage);
            Color target;
            float ti;
            Case01Mood.RoomLight(1, Case01Room.Dining, qa.Layout.RoomLightColor[Case01Room.Dining], qa.Layout.RoomLightIntensity[Case01Room.Dining], out target, out ti);
            qa.Tick(Dt, Case01Input.None);
            float step1 = Vector4.Distance(dining.color, target);
            Assert.Greater(step1, Vector4.Distance(before, target) * 0.5f, "第一個影格不硬切");
            Run(qa, 2f);
            Assert.Less(Vector4.Distance(dining.color, target), 0.02f, "約 2 秒內到位");
        }

        [Test]
        public void Aging_PerStage_NoColliders_AndOnlySwitchesWhenPlayerAway()
        {
            var h0 = NewQa();
            var a0 = h0.Layout.Aging;
            Assert.IsNotNull(a0);
            foreach (var r in a0.Rooms) Assert.AreEqual(0, a0.VisibleCount(r), "H0 沒有舊化：" + r);
            Assert.AreEqual(0, a0.Rooms.Count(r => r == Case01Room.Storage));
            foreach (var c in h0.Layout.Root.Find("Aging").GetComponentsInChildren<Collider>(true)) Assert.Fail("舊化痕跡不應有碰撞體：" + c.name);

            var h1 = NewQa(null, Case01QaMode.NarrativeV04);
            h1.SetArtStage(1);
            var a1 = h1.Layout.Aging;
            Assert.Greater(a1.VisibleCount(Case01Room.Dining), 0, "H1：餐廳先出現灰塵");
            Assert.Greater(a1.VisibleCount(Case01Room.Living), 0);
            Assert.AreEqual(0, a1.VisibleCount(Case01Room.Study), "H1：書房還沒有");

            var h2 = NewQa(Case01Checkpoint.S30);
            var a2 = h2.Layout.Aging;
            foreach (var r in a2.Rooms)
            {
                Assert.GreaterOrEqual(a2.VisibleCount(r), 7, "H2：普遍冷清（灰塵、污痕、剝落、紙張）：" + r);
                Assert.Greater(a2.PaperCount(r), 0, "H2：紙張散落：" + r);
            }

            var h3 = NewQa(null, Case01QaMode.NarrativeV04);
            h3.SetArtStage(3);
            var a3 = h3.Layout.Aging;
            foreach (var r in a3.Rooms)
            {
                Assert.Greater(a3.VisibleCount(r), 0, "H3：灰塵與污痕保留：" + r);
                Assert.AreEqual(0, a3.PaperCount(r), "H3：紙張收走：" + r);
            }

            // 玩家站在餐廳時進入 H1：餐廳的痕跡不當面出現，離開後才出現。
            var live = NewQa();
            live.Player.Teleport(new Vector3(6.6f, 0.05f, 6.2f), 180f);
            Run(live, 0.2f);
            Assert.AreEqual(Case01Room.Dining, live.CurrentRoom);
            live.Flow.Interact(Case01Ids.Table);
            live.Flow.EnterRoom(Case01Room.Dining, Case01Room.Entrance);
            live.Flow.ApplyDiningRewrite();
            Run(live, 0.2f);
            Assert.AreEqual(0, live.Layout.Aging.VisibleCount(Case01Room.Dining), "不當面硬閃");
            live.Player.Teleport(new Vector3(6.5f, 0.05f, 2.4f), 180f);
            Run(live, 0.2f);
            Assert.Greater(live.Layout.Aging.VisibleCount(Case01Room.Dining), 0, "離開後才出現");
        }

        [Test]
        public void PortalStates_FollowRoutes_GlitchShader_ReduceFlicker_CorrectedStops()
        {
            var s30 = NewQa(Case01Checkpoint.S30);
            Assert.AreEqual(Case01PortalState.Wrong, s30.PortalState(Case01Door.StudyDoor), "錯接的書房門");
            var study = s30.Layout.Portal(Case01Door.StudyDoor);
            Assert.IsTrue(study.Membrane.activeSelf);
            Assert.IsNull(study.Membrane.GetComponent<Collider>(), "膜沒有碰撞體：不擋穿門");
            Assert.AreEqual("Shepherd/Case01PortalGlitch", study.MembraneMat.shader.name, "訊號故障 shader（不是紫色平面）");
            Run(s30, 0.1f);
            Assert.AreEqual(1f, study.MembraneMat.GetFloat("_Glitch"), 1e-3);
            Assert.Greater(study.Hum.volume, 0f, "錯接門有輕微電流聲");
            s30.ReduceFlicker = true;
            Run(s30, 0.1f);
            Assert.Less(study.MembraneMat.GetFloat("_Speed"), 0.5f, "降低閃爍：噪點變慢");
            Assert.Less(study.MembraneMat.GetFloat("_Glitch"), 1f);

            var s40 = NewQa(Case01Checkpoint.S40);
            Assert.AreEqual(Case01PortalState.Unstable, s40.PortalState(Case01Door.MezzTriangle));
            Assert.AreEqual(Case01PortalState.Dormant, s40.PortalState(Case01Door.StudyDoor), "一般房門不是故障門");
            Assert.AreEqual(Case01PortalState.Dormant, s40.PortalState(Case01Door.Mezz17));

            var v03 = NewQa(null, Case01QaMode.CoreV03);
            Assert.AreEqual(Case01PortalState.Wrong, v03.PortalState(Case01Door.MezzTriangle), "戰後 △ 錯接");
            v03.Flow.Traverse(Case01Door.MezzTriangle);
            v03.Flow.Interact(Case01Ids.Core);
            v03.Flow.Choose(Case01IdsV03.ChoiceAnchor, 0);
            Assert.AreEqual(Case01PortalState.Anchored, v03.PortalState(Case01Door.MezzTriangle), "錨定後");
            v03.Flow.Choose(Case01IdsV03.ChoiceSecond, 0);
            v03.Flow.Traverse(Case01Door.Mezz17);
            Assert.AreEqual(Case01PortalState.Corrected, v03.PortalState(Case01Door.MezzTriangle), "校正後");
            Run(v03, 0.1f);
            var tri = v03.Layout.Portal(Case01Door.MezzTriangle);
            Assert.AreEqual(0f, tri.MembraneMat.GetFloat("_Glitch"), 1e-3, "故障停止");
            Assert.AreEqual(0f, tri.Hum.volume, 1e-4, "雜音停止");
            Assert.IsTrue(v03.Flow.PortalActive(Case01Door.MezzTriangle), "仍是可走的通路（不封門）");

            var reduced = Case01Glitch.For(Case01PortalState.Wrong, true);
            Assert.AreEqual(0f, reduced.JitterChance, "降低閃爍：門框不跳動");
        }

        [Test]
        public void Portal_StillTraversable_WithGlitchMembrane()
        {
            var qa = NewQa(Case01Checkpoint.S30);
            var p = qa.Layout.Portal(Case01Door.StudyDoor);
            qa.Player.Teleport(new Vector3(Case01HouseBuilder.StudyDoorX, Case01HouseBuilder.FloorHeight + 0.05f, 4.2f), 0f);
            Run(qa, 0.1f);
            for (int i = 0; i < 90 && qa.Mode == Case01Mode.Exploring; i++)
                qa.Tick(Dt, Case01Input.From(new ExploreInput { Move = new Vector2(0f, 1f) }));
            for (int i = 0; i < 60 && qa.Mode != Case01Mode.Exploring; i++) qa.Tick(Dt, Case01Input.None);
            Assert.AreEqual(Case01Room.Dining, qa.CurrentRoom, "穿過故障門仍照路由傳送");
            Assert.IsTrue(p.Membrane.activeSelf);
        }

        [Test]
        public void NarrativeQa_F9_CyclesGlitchStates()
        {
            var qa = NewQa(null, Case01QaMode.NarrativeV04);
            var expect = new[]
            {
                new KeyValuePair<Case01Door, Case01PortalState>(Case01Door.StudyDoor, Case01PortalState.Wrong),
                new KeyValuePair<Case01Door, Case01PortalState>(Case01Door.MezzTriangle, Case01PortalState.Unstable),
                new KeyValuePair<Case01Door, Case01PortalState>(Case01Door.MezzTriangle, Case01PortalState.Wrong),
                new KeyValuePair<Case01Door, Case01PortalState>(Case01Door.MezzTriangle, Case01PortalState.Anchored),
                new KeyValuePair<Case01Door, Case01PortalState>(Case01Door.MezzTriangle, Case01PortalState.Corrected),
            };
            for (int i = 0; i < expect.Length; i++)
            {
                qa.Tick(Dt, new Case01Input { Function = 9 });
                Assert.AreEqual(i, qa.GlitchPreview);
                Assert.AreEqual(expect[i].Value, qa.PortalState(expect[i].Key), "預覽 " + (i + 1));
                Assert.AreNotEqual(Case01Room.None, qa.CurrentRoom);
            }
        }
    }
}

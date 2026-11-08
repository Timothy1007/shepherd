using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shepherd.Exploration.Case01;
using UnityEditor;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>v0.3 Art Pass：材質資產、Shader 相容、貼圖可讀、材質生命週期、燈光階段、渲染狀態還原。</summary>
    public sealed class Case01ArtTests
    {
        private const float Dt = 1f / 30f;
        private readonly List<GameObject> roots = new List<GameObject>();

        private Case01HouseQA NewQa(Case01Checkpoint? cp = null)
        {
            var go = new GameObject("Case01ArtTest");
            roots.Add(go);
            var qa = go.AddComponent<Case01HouseQA>();
            qa.Build(false, null, 1, cp);
            return qa;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r);
            roots.Clear();
        }

        private static readonly string[] Keys =
        {
            "floor_wood", "floor_wood_up", "wall_plaster", "wall_paper", "ceiling", "trim_paint", "wood_dark", "wood_warm", "frame_wood",
            "fabric_blue", "fabric_red", "fabric_green", "fabric_cream", "paper", "paper_old", "book_0", "book_6", "cardboard", "ceramic", "brass",
            "metal", "glass_night", "pot", "leaf", "lamp_shade", "glow", "mezz_floor", "mezz_wall", "core_body", "rift", "frame_glow", "crystal",
            "court_floor", "court_wall",
        };

        [Test]
        public void ArtSet_LoadsFromResources_WithSupportedShaderAndTextures()
        {
            var set = Resources.Load<Case01ArtSet>(Case01ArtSet.ResourcePath);
            Assert.IsNotNull(set, "Case01ArtSet 在 Resources 中");
            var shader = Shader.Find("Shepherd/Case01/Triplanar");
            Assert.IsNotNull(shader);
            Assert.IsTrue(shader.isSupported, "內建渲染管線可用的 shader（沒有粉紅材質）");
            foreach (var k in Keys)
            {
                var m = set.Find(k);
                Assert.IsNotNull(m, k);
                Assert.AreEqual(shader, m.shader, k);
                Assert.IsNotNull(m.GetTexture("_MainTex"), k + " 有主貼圖");
                Assert.IsTrue(AssetDatabase.Contains(m), k + " 是材質資產");
            }
            Assert.IsNotNull(set.PaperTexture);
            // 法線貼圖的匯入類型正確；顏色貼圖為 sRGB、可平鋪。
            foreach (var k in new[] { "floor_wood", "wall_plaster", "wood_dark", "fabric_red", "court_wall" })
            {
                var m = set.Find(k);
                var n = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(m.GetTexture("_BumpMap")));
                Assert.AreEqual(TextureImporterType.NormalMap, n.textureType, k);
                var a = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(m.GetTexture("_MainTex")));
                Assert.IsTrue(a.sRGBTexture, k);
                Assert.AreEqual(TextureWrapMode.Repeat, a.wrapMode, k);
            }
            // 光滑度／顏色有實際差異（不是全部同一種純色）。
            Assert.Greater(set.Find("ceramic").GetFloat("_Glossiness"), set.Find("fabric_red").GetFloat("_Glossiness") + 0.4f);
            Assert.Greater(set.Find("brass").GetFloat("_Metallic"), 0.5f);
        }

        [Test]
        public void House_UsesCase01Materials_NoMissingOrErrorShaders()
        {
            var qa = NewQa();
            Assert.IsTrue(qa.Palette.HasArt);
            var renderers = qa.Layout.Root.GetComponentsInChildren<Renderer>(true);
            int triplanar = 0;
            foreach (var r in renderers)
            {
                foreach (var m in r.sharedMaterials)
                {
                    Assert.IsNotNull(m, r.name + " 沒有遺失材質");
                    Assert.AreNotEqual("Hidden/InternalErrorShader", m.shader.name, r.name);
                    if (m.shader.name == "Shepherd/Case01/Triplanar") triplanar++;
                }
            }
            Assert.Greater(triplanar, renderers.Length / 2, "大部分房屋物件使用 Case01 材質");
            var floor = qa.Layout.Root.Find("GroundFloor/Floor").GetComponent<Renderer>();
            Assert.AreEqual("M_floor_wood", floor.sharedMaterial.name);
            Assert.IsNotNull(qa.Layout.Root.Find("GroundFloor/Skirting"), "有踢腳線");
            Assert.IsNotNull(qa.Layout.Root.Find("GroundFloor/Casing"), "有門框");
            Assert.IsNotNull(qa.Layout.Root.Find("GroundFloor/Pendant_Dining"), "燈光有看得到的燈具");
        }

        [Test]
        public void SharedAssets_AreNotModifiedAtRuntime_AndOwnedMaterialsAreReleased()
        {
            var set = Resources.Load<Case01ArtSet>(Case01ArtSet.ResourcePath);
            var coreAsset = set.Find("core_body");
            var before = coreAsset.GetColor("_EmissionColor");
            var qa = NewQa(Case01Checkpoint.S50);
            var owned = qa.Palette;
            Assert.AreNotSame(coreAsset, qa.Layout.CoreMat, "核使用獨立副本");
            qa.Flow.Interact(Case01Ids.Core);
            qa.JumpTo(Case01Checkpoint.S40);
            Assert.AreEqual(before, coreAsset.GetColor("_EmissionColor"), "共用資產沒有被改");
            Assert.AreEqual(0, owned.OwnedCount, "跳轉後舊的衍生材質全部釋放");
            Assert.AreNotSame(owned, qa.Palette);
            Assert.Greater(qa.Palette.OwnedCount, 0);
        }

        [Test]
        public void NoNewMaterialsPerFrame()
        {
            var qa = NewQa(Case01Checkpoint.S30);
            for (int i = 0; i < 5; i++) qa.Tick(Dt, Case01Input.None);
            int count = Resources.FindObjectsOfTypeAll<Material>().Length;
            for (int i = 0; i < 90; i++) qa.Tick(Dt, Case01Input.None);
            Assert.AreEqual(count, Resources.FindObjectsOfTypeAll<Material>().Length);
        }

        [Test]
        public void Lights_FollowHouseStage_AndReduceFlickerDampensLanding()
        {
            var qa = NewQa();
            var L = qa.Layout;
            qa.Tick(Dt, Case01Input.None);
            Assert.AreEqual(L.RoomLightColor[Case01Room.Dining], L.RoomLights[Case01Room.Dining].color, "H0：餐廳原本的暖光");
            var warmStudy = L.RoomLights[Case01Room.Study].color;

            var s30 = NewQa(Case01Checkpoint.S30);
            s30.Tick(Dt, Case01Input.None);
            var Ls = s30.Layout;
            Assert.AreNotEqual(Ls.RoomLightColor[Case01Room.Dining], Ls.RoomLights[Case01Room.Dining].color, "H1：餐廳光色反常");
            Assert.Greater(Ls.RoomLights[Case01Room.Study].color.b - Ls.RoomLights[Case01Room.Study].color.r, warmStudy.b - warmStudy.r, "H2：書房轉冷");
            Assert.Greater(Ls.RoomLights[Case01Room.Study].intensity, 0.6f, "仍看得清線索");

            float Range(Case01HouseQA q)
            {
                float lo = 9f, hi = -9f;
                for (int i = 0; i < 300; i++)
                {
                    q.Tick(Dt, Case01Input.None);
                    float v = q.Layout.LandingLight.intensity;
                    lo = Mathf.Min(lo, v);
                    hi = Mathf.Max(hi, v);
                }
                return hi - lo;
            }
            float normal = Range(s30);
            s30.ReduceFlicker = true;
            float reduced = Range(s30);
            Assert.Greater(normal, 0.01f);
            Assert.Less(reduced, normal * 0.5f, "降低閃爍");
            Assert.Less(normal, 0.4f, "低頻、小幅度，不刺眼");
            s30.LightShadowsEnabled = false;
            s30.Tick(Dt, Case01Input.None);
            Assert.IsTrue(s30.Layout.HouseLights.All(l => l.shadows == LightShadows.None), "可關閉即時陰影");
        }

        [Test]
        public void Atmosphere_IsRestored()
        {
            var mode = RenderSettings.ambientMode;
            var sky = RenderSettings.ambientSkyColor;
            var qa = NewQa();
            qa.ApplyAtmosphere();
            Assert.AreEqual(UnityEngine.Rendering.AmbientMode.Trilight, RenderSettings.ambientMode);
            qa.RestoreAtmosphere();
            Assert.AreEqual(mode, RenderSettings.ambientMode);
            Assert.AreEqual(sky, RenderSettings.ambientSkyColor);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shepherd.Battle.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Battle.Tests
{
    /// <summary>Combat Lab 的縮放、字級、溢出、重疊與對比度檢查。</summary>
    public sealed class CombatLabLayoutTests
    {
        private static readonly string[] MainPanels =
            { "BattleInfo", "Message", "SchoolAndEnemies", "SequenceAndPreview", "HandAndActions", "BattleLog" };

        private GameObject root;
        private CombatLabView view;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("CombatLabLayoutTest");
            view = root.AddComponent<CombatLabView>();
            view.Build(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        private CanvasScaler Scaler { get { return view.Canvas.GetComponent<CanvasScaler>(); } }

        private IEnumerable<Text> AllTexts()
        {
            return root.GetComponentsInChildren<Text>(true);
        }

        /// <summary>把畫布固定成指定螢幕尺寸換算後的版面大小，並重新排版。</summary>
        private void LayoutAt(int screenWidth, int screenHeight)
        {
            float scale = CombatLabView.ScaleFactorFor(Scaler, screenWidth, screenHeight);
            Scaler.enabled = false;
            view.Canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)view.Canvas.transform;
            rt.sizeDelta = new Vector2(screenWidth / scale, screenHeight / scale);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            view.Refresh();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        /// <summary>讓編排、預覽、紀錄、已解除前的敵人資訊都有實際內容。</summary>
        private void Populate()
        {
            var s = view.Session;
            var hand = s.Snapshot().Hand;
            s.AddCard(hand[0].Id);
            s.AddCard(hand[1].Id);
            s.Commit();
            hand = s.Snapshot().Hand;
            s.SelectSchool(Core.SchoolKind.Process);
            s.AddCard(hand[0].Id);
            s.AddCard(hand[1].Id);
            s.AddCard(hand[2].Id);
            s.Preview();
        }

        private static bool InsideScrollContent(Text t)
        {
            var scroll = t.GetComponentInParent<ScrollRect>();
            return scroll != null && t.transform.IsChildOf(scroll.content);
        }

        [Test]
        public void CanvasScaler_ScaleWithScreenSize_PixelPerfect()
        {
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, Scaler.uiScaleMode);
            Assert.AreEqual(new Vector2(1600f, 900f), Scaler.referenceResolution);
            Assert.AreEqual(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight, Scaler.screenMatchMode);
            Assert.AreEqual(0.5f, Scaler.matchWidthOrHeight, 1e-5f);
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, view.Canvas.renderMode);
            Assert.IsTrue(view.Canvas.pixelPerfect, "文字頂點需對齊像素");
            Assert.AreEqual(1.2f, CombatLabView.ScaleFactorFor(Scaler, 1920, 1080), 1e-4f);
            Assert.AreEqual(0.8f, CombatLabView.ScaleFactorFor(Scaler, 1280, 720), 1e-4f);
        }

        [Test]
        public void NoTransformScaling_NoBestFit()
        {
            // Canvas 根節點的縮放由 Canvas／CanvasScaler 自行管理；其下所有元件都不得再以 Transform 縮放。
            foreach (var rt in view.Canvas.GetComponentsInChildren<RectTransform>(true))
            {
                if (rt == view.Canvas.transform) continue;
                Assert.AreEqual(Vector3.one, rt.localScale, rt.name + " 不應以 Transform 縮放");
            }
            foreach (var t in AllTexts())
                Assert.IsFalse(t.resizeTextForBestFit, t.name + " 不應使用 Best Fit 自動縮字");
        }

        [Test]
        public void FontSizes_MeetMinimum_AndPhysicalSizeIsReadable()
        {
            Populate();
            view.Refresh();
            Assert.IsTrue(AllTexts().All(t => t.font != null), "所有文字都必須有字型");
            float s1080 = CombatLabView.ScaleFactorFor(Scaler, 1920, 1080);
            float s720 = CombatLabView.ScaleFactorFor(Scaler, 1280, 720);
            foreach (var t in AllTexts())
            {
                Assert.GreaterOrEqual(t.fontSize, CombatLabStyle.MinFont, t.name + " 字級過小");
                Assert.GreaterOrEqual(t.fontSize * s1080, 19f, t.name + " 在 1920×1080 小於 19px");
                Assert.GreaterOrEqual(t.fontSize * s720, 12.8f, t.name + " 在 1280×720 小於 12.8px");
            }
            Assert.GreaterOrEqual(CombatLabStyle.Body * s1080, 22f, "一般內文在 1920×1080 約 22px");
            Assert.GreaterOrEqual(CombatLabStyle.Important * s1080, 28f, "重要數值在 1920×1080 至少 28px");
        }

        [Test]
        public void ChineseGlyphs_HaveMetrics()
        {
            var font = AllTexts().First(x => x.font != null).font;
            const string sample = "錨定值擬態過程哲學關係論實在燃素基體擬合劑敵人預覽紀錄調度";
            const int size = 24;
            font.RequestCharactersInTexture(sample, size);
            foreach (char c in sample)
            {
                CharacterInfo info;
                Assert.IsTrue(font.GetCharacterInfo(c, out info, size), "字型缺少「" + c + "」");
                Assert.Greater(info.advance, size * 0.8f, "「" + c + "」字寬異常（可能缺字）");
            }
        }

        [TestCase(1920, 1080)]
        [TestCase(1280, 720)]
        [TestCase(1920, 1200)]
        [TestCase(1366, 768)]
        public void NoTextOverflow(int width, int height)
        {
            Populate();
            LayoutAt(width, height);
            var problems = new List<string>();
            foreach (var t in AllTexts())
            {
                if (!t.gameObject.activeInHierarchy || string.IsNullOrEmpty(t.text) || InsideScrollContent(t)) continue;
                var rect = t.rectTransform.rect;
                float needed = LayoutUtility.GetPreferredHeight(t.rectTransform);
                if (needed > rect.height + 2f)
                    problems.Add(t.transform.parent.name + "/" + t.name + " 需要 " + needed.ToString("0") + "，只有 "
                        + rect.height.ToString("0") + "：" + t.text.Replace("\n", " ⏎ "));
            }
            Assert.IsEmpty(problems, width + "×" + height + " 文字超出框線：\n" + string.Join("\n", problems));
        }

        [TestCase(1920, 1080)]
        [TestCase(1280, 720)]
        public void MainPanels_DoNotOverlap(int width, int height)
        {
            LayoutAt(width, height);
            var rects = new List<KeyValuePair<string, Rect>>();
            foreach (var name in MainPanels)
            {
                var rt = root.GetComponentsInChildren<RectTransform>(true).First(r => r.name == name);
                var c = new Vector3[4];
                rt.GetWorldCorners(c);
                var r = Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
                Assert.Greater(r.width, 100f, name);
                Assert.Greater(r.height, 30f, name);
                rects.Add(new KeyValuePair<string, Rect>(name, r));
            }
            for (int i = 0; i < rects.Count; i++)
            for (int j = i + 1; j < rects.Count; j++)
            {
                var a = rects[i].Value;
                var b = rects[j].Value;
                float ox = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
                float oy = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                Assert.IsFalse(ox > 0.5f && oy > 0.5f, rects[i].Key + " 與 " + rects[j].Key + " 重疊");
            }
        }

        [Test]
        public void HandAndEnemyButtons_AreLargeEnough()
        {
            LayoutAt(1920, 1080);
            foreach (var b in view.HandButtons.Where(x => x.gameObject.activeSelf))
            {
                var r = ((RectTransform)b.transform).rect;
                Assert.GreaterOrEqual(r.height, 120f, b.name + " 高度");
                Assert.GreaterOrEqual(r.width, 120f, b.name + " 寬度");
            }
            foreach (var b in view.EnemyButtons)
                Assert.GreaterOrEqual(((RectTransform)b.transform).rect.height, 110f, b.name);
            foreach (var b in new[] { view.CommitButton, view.RedrawButton, view.ResetButton })
                Assert.GreaterOrEqual(((RectTransform)b.transform).rect.height, 50f, b.name);
        }

        [TestCase(1920, 1080)]
        [TestCase(1280, 720)]
        [TestCase(1366, 768)]
        public void CardTooltip_StaysOnScreen_AboveTheCard(int width, int height)
        {
            var s = view.Session;
            s.AddCard(s.Snapshot().Hand[0].Id);
            LayoutAt(width, height);
            var canvasRect = WorldRect((RectTransform)view.Canvas.transform);
            for (int i = 0; i < view.HandIds.Count; i++)
            {
                view.ShowCardTooltip(i);
                Canvas.ForceUpdateCanvases();
                view.ShowCardTooltip(i);
                var tip = WorldRect(view.Tooltip);
                var card = WorldRect((RectTransform)view.HandButtons[i].transform);
                Assert.IsTrue(canvasRect.Contains(tip.min) && canvasRect.Contains(tip.max),
                    width + "×" + height + " 第 " + (i + 1) + " 張的說明框超出畫面");
                Assert.GreaterOrEqual(tip.yMin, card.yMax - 0.5f, "說明框不遮住被指著的牌");
                var t = view.Tooltip.GetComponentInChildren<Text>();
                Assert.LessOrEqual(LayoutUtility.GetPreferredHeight(t.rectTransform), t.rectTransform.rect.height + 2f,
                    "說明框文字超出框線");
                Assert.GreaterOrEqual(t.fontSize, CombatLabStyle.Body);
            }
        }

        private static Rect WorldRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        [Test]
        public void InteractableButtons_HaveSufficientContrast()
        {
            Populate();
            view.Refresh();
            view.RedrawButton.onClick.Invoke(); // 進入調度模式，檢查高亮配色
            foreach (var b in root.GetComponentsInChildren<Button>(true))
            {
                if (!b.interactable || !b.gameObject.activeInHierarchy) continue;
                var t = b.GetComponentInChildren<Text>();
                var bg = ((Image)b.targetGraphic).color;
                float ratio = Contrast(t.color, bg);
                float need = t.fontSize >= CombatLabStyle.Button ? 3f : 4.5f;
                Assert.GreaterOrEqual(ratio, need, b.name + " 文字對比不足：" + ratio.ToString("0.00"));
            }
        }

        private static float Contrast(Color a, Color b)
        {
            float la = Luminance(a), lb = Luminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        private static float Luminance(Color c)
        {
            return 0.2126f * Lin(c.r) + 0.7152f * Lin(c.g) + 0.0722f * Lin(c.b);
        }

        private static float Lin(float v)
        {
            return v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
        }
    }
}

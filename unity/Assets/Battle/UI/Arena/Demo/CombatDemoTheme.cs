using Shepherd.Battle.Core;
using UnityEngine;

namespace Shepherd.Battle.UI.Arena
{
    /// <summary>
    /// Case01 Demo 戰鬥 HUD 的視覺主題：深炭黑、去飽和黃銅、象牙白＋三元素辨識色。
    /// 顏色之外每個元素都有符號（▲／■／◆），色弱也能分辨。尺寸以參考解析度 1600×900 為單位。
    /// 之後換正式美術時只需替換這裡的數值。
    /// </summary>
    public static class CombatDemoTheme
    {
        // ---- 色票 ----
        public static readonly Color Charcoal = new Color(0.055f, 0.06f, 0.07f, 0.94f);
        public static readonly Color Panel = new Color(0.085f, 0.09f, 0.1f, 0.9f);
        public static readonly Color PanelSoft = new Color(0.12f, 0.125f, 0.135f, 0.92f);
        public static readonly Color Brass = new Color(0.79f, 0.65f, 0.41f, 1f);
        public static readonly Color BrassDim = new Color(0.45f, 0.38f, 0.26f, 1f);
        public static readonly Color Ivory = new Color(0.94f, 0.91f, 0.84f, 1f);
        public static readonly Color Muted = new Color(0.66f, 0.66f, 0.63f, 1f);
        public static readonly Color Faint = new Color(0.4f, 0.4f, 0.4f, 1f);
        public static readonly Color Danger = new Color(0.93f, 0.45f, 0.38f, 1f);
        public static readonly Color Shield = new Color(0.58f, 0.76f, 0.95f, 1f);
        public static readonly Color Good = new Color(0.6f, 0.9f, 0.62f, 1f);
        public static readonly Color Anchor = new Color(0.8f, 0.3f, 0.26f, 1f);
        public static readonly Color Disorder = new Color(0.62f, 0.4f, 0.86f, 1f);
        public static readonly Color Primary = new Color(0.62f, 0.5f, 0.27f, 1f);

        // ---- 尺寸 ----
        public const float Margin = 16f;
        public const float CardWidth = 104f;
        public const float CardHeight = 152f;
        public const float CardSpacing = 8f;
        public const float SequenceWidth = 760f;
        public const float SequenceHeight = 312f;
        public const float SlotWidth = 180f;
        public const float SlotHeight = 104f;
        public const float PlateWidth = 252f;
        public const float DetailWidth = 470f;

        public const int FontSmall = 16;
        public const int FontBody = 18;
        public const int FontLabel = 20;
        public const int FontTitle = 23;
        public const int FontBig = 30;

        public static Color ElementColor(ElementKind e)
        {
            switch (e)
            {
                case ElementKind.Ember: return new Color(0.86f, 0.46f, 0.28f, 1f);
                case ElementKind.Base: return new Color(0.42f, 0.6f, 0.86f, 1f);
                default: return new Color(0.38f, 0.76f, 0.6f, 1f);
            }
        }

        /// <summary>牌身底色（元素色壓暗，文字仍清楚）。</summary>
        public static Color CardBody(ElementKind e)
        {
            return Color.Lerp(new Color(0.08f, 0.085f, 0.095f, 0.97f), ElementColor(e), 0.22f);
        }

        public static string ElementSymbol(ElementKind e)
        {
            switch (e)
            {
                case ElementKind.Ember: return "▲";
                case ElementKind.Base: return "■";
                default: return "◆";
            }
        }

        public static string Hex(Color c)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(c);
        }
    }
}

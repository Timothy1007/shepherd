using UnityEngine;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 遊戲用的字型：思源黑體繁中（Noto Sans TC Regular，SIL OFL 1.1，見 Resources/Fonts/NotoSansTC-OFL.txt）。
    /// 找不到時退回 Unity 內建字型。
    /// </summary>
    public static class LanternFonts
    {
        private static Font ui;

        /// <summary>場景裡的字用的材質（會被牆擋住）。找不到 shader 時退回字型內建材質。</summary>
        public static Material WorldMaterial(Font font, Color color, string name = "WorldText")
        {
            var sh = Shader.Find("Shepherd/WorldText");
            var m = sh != null ? new Material(sh) : new Material(font.material);
            m.name = name;
            m.hideFlags = HideFlags.DontSave;
            m.mainTexture = font.material.mainTexture;
            m.color = color;
            return m;
        }

        public static Font UI
        {
            get
            {
                if (ui == null) ui = Resources.Load<Font>("Fonts/NotoSansTC-Regular");
                if (ui == null) ui = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return ui;
            }
        }
    }
}

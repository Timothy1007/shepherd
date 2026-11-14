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

using System.Text;
using Shepherd.Battle.Core;

namespace Shepherd.Battle.UI
{
    /// <summary>
    /// 手牌說明框的文字（平面 Combat Lab 與 3D Combat Lab 共用）。
    /// 效果數值讀自規則集，預估來自 <see cref="CombatLabSession.ForecastWithCard"/>（同一個 RoundResolver）。
    /// </summary>
    public static class CombatLabCardInfo
    {
        private static readonly SchoolKind[] Schools = { SchoolKind.Structure, SchoolKind.Process, SchoolKind.Relation };

        public static string Tooltip(CombatLabSession session, int cardId, bool redrawMode, string targetHint)
        {
            var state = session.Controller.CloneState();
            var card = state.Card(cardId);
            var school = session.SelectedSchool;
            var rules = session.Rules;
            var sb = new StringBuilder();
            sb.Append("<size=").Append(CombatLabStyle.Important).Append("><b>").Append(CombatLabText.Element(card.Element))
              .Append("・").Append(CombatLabText.EffectName(school, card.Element)).Append("</b></size>")
              .Append("　<size=").Append(CombatLabStyle.Small).Append(">").Append(CombatLabText.School(school))
              .Append("　#").Append(cardId).Append("</size>\n");
            sb.Append(CombatLabText.EffectDetail(rules, school, card.Element)).Append('\n');
            if (session.CardNeedsTarget(card.Element))
                sb.Append("目標：").Append(session.SelectedTargetId.HasValue ? session.EnemyName(session.SelectedTargetId.Value) : "（無）")
                  .Append("（").Append(targetHint).Append("）\n");
            else
                sb.Append("不需要目標\n");
            sb.Append("<color=#C8CCD4>接在它後面的牌　").Append(CombatLabText.ResidueHint(rules, card.Element)).Append("</color>\n");

            sb.Append("<b>── 加入後本回合預估 ──</b>\n");
            if (redrawMode)
            {
                sb.Append("調度模式：點擊會把這張洗回牌庫並重抽一張");
            }
            else
            {
                string reason;
                var with = session.ForecastWithCard(cardId, out reason);
                if (with == null)
                {
                    sb.Append(reason);
                    if (session.IsStaged(cardId)) sb.Append("（點擊可移除）");
                }
                else
                {
                    var now = session.Forecast;
                    sb.Append("錨定值 ").Append(CombatLabText.Change(with.AnchorBefore, with.AnchorAfter))
                      .Append("　擬態 ").Append(CombatLabText.Change(with.ShieldBefore, with.ShieldAfter, false)).Append('\n');
                    foreach (var e in with.Enemies)
                    {
                        if (e.ResolvedBefore) continue;
                        sb.Append(e.Name).Append(" 失序值 ").Append(CombatLabText.Change(e.ValueBefore, e.ValueAfter, false));
                        if (e.ResolvedAfter) sb.Append(CombatLabText.Colored(" 解除", CombatLabText.Good));
                        var old = now != null ? now.Enemy(e.Id) : null;
                        if (old != null && old.ValueAfter != e.ValueAfter)
                            sb.Append("<color=#C8CCD4>（比不加 ").Append(CombatLabText.Signed(e.ValueAfter - old.ValueAfter)).Append("）</color>");
                        sb.Append('\n');
                    }
                    if (now != null && now.AnchorAfter != with.AnchorAfter)
                        sb.Append("錨定值比不加 ").Append(CombatLabText.Signed(with.AnchorAfter - now.AnchorAfter)).Append('\n');
                    sb.Append("<color=#C8CCD4>含承接、三連與敵方回合；尚未結算</color>");
                }
            }
            sb.Append("\n<color=#C8CCD4>其他學派：");
            bool first = true;
            foreach (var other in Schools)
            {
                if (other == school) continue;
                if (!first) sb.Append("；");
                first = false;
                sb.Append(CombatLabText.School(other)).Append("・").Append(CombatLabText.EffectName(other, card.Element))
                  .Append(" ").Append(CombatLabText.EffectShort(rules, other, card.Element));
            }
            sb.Append("</color>");
            return sb.ToString();
        }
    }
}

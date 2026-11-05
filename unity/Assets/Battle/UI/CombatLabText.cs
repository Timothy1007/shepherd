using System;
using Shepherd.Battle.Core;

namespace Shepherd.Battle.UI
{
    /// <summary>
    /// Combat Lab 的繁體中文顯示文字。只負責把 BattleEvent 與狀態轉成文字，不計算任何數值。
    /// </summary>
    public static class CombatLabText
    {
        public static string School(SchoolKind s)
        {
            switch (s)
            {
                case SchoolKind.Structure: return "實在論";
                case SchoolKind.Process: return "過程哲學";
                case SchoolKind.Relation: return "關係論";
            }
            return s.ToString();
        }

        public static string Element(ElementKind e)
        {
            switch (e)
            {
                case ElementKind.Ember: return "燃素";
                case ElementKind.Base: return "基體";
                case ElementKind.Binder: return "擬合劑";
            }
            return e.ToString();
        }

        /// <summary>該學派下這張牌的效果名稱（規格第四節）。</summary>
        public static string EffectName(SchoolKind s, ElementKind e)
        {
            if (s == SchoolKind.Structure) return e == ElementKind.Ember ? "施壓" : e == ElementKind.Base ? "構形" : "適配";
            if (s == SchoolKind.Process) return e == ElementKind.Ember ? "啟動" : e == ElementKind.Base ? "穩定" : "轉移";
            return e == ElementKind.Ember ? "激化" : e == ElementKind.Base ? "固化" : "連結";
        }

        public static string Outcome(BattleOutcome o)
        {
            switch (o)
            {
                case BattleOutcome.Victory: return "勝利";
                case BattleOutcome.Defeat: return "戰敗";
            }
            return "進行中";
        }

        public static string Signed(int n)
        {
            return n >= 0 ? "+" + n : n.ToString();
        }

        public static string Source(string src)
        {
            if (string.IsNullOrEmpty(src)) return "";
            int idx;
            if (src.StartsWith("card:") && int.TryParse(src.Substring(5), out idx)) return "第 " + (idx + 1) + " 張";
            if (src.StartsWith("residue:") && int.TryParse(src.Substring(8), out idx)) return "承接（第 " + (idx + 1) + " 張）";
            if (src.StartsWith("enemy:")) return "E" + src.Substring(6) + " 攻擊";
            switch (src)
            {
                case "passive:Conduction": return "傳導";
                case "passive:Divert": return "分流／回流";
                case "passive:Response": return "回應";
                case "passive:Feedback": return "回饋";
                case "passive:Continuation": return "延續";
                case "phase:Correction": return "持續校正";
                case "phase:RoundEnd": return "回合結束";
                case "sequence": return "序列";
                case "deck": return "牌庫";
                case "player": return "玩家";
            }
            return src;
        }

        // ---------------- 牌的效果說明（數值全部讀自規則集） ----------------

        /// <summary>牌面上的一行簡述（未計承接與三連）。</summary>
        public static string EffectShort(RuleSet rules, SchoolKind s, ElementKind e)
        {
            var p = rules.Params;
            int v = rules.PrimaryValue(s, e);
            switch (s)
            {
                case SchoolKind.Structure:
                    if (e == ElementKind.Ember) return "壓制 " + v + "＋轉化";
                    if (e == ElementKind.Base) return "擬態 +" + v;
                    return "擬態 +" + v + "、連結 +" + p.StructureBinderLink;
                case SchoolKind.Process:
                    if (e == ElementKind.Ember) return "校正 " + v + "、壓制 " + v;
                    if (e == ElementKind.Base) return "校正 " + v + "、擬態 +" + p.ProcessBaseShield;
                    return "校正 " + v + "、回復 " + p.ProcessBinderHeal;
                default:
                    if (e == ElementKind.Ember) return "壓制 " + v + "＋斷連";
                    if (e == ElementKind.Base) return "連結 +" + v + "、束縛";
                    return "連結 +" + v;
            }
        }

        /// <summary>規格第四節效果表的逐步說明，數值取自規則集。</summary>
        public static string EffectDetail(RuleSet rules, SchoolKind s, ElementKind e)
        {
            var p = rules.Params;
            int v = rules.PrimaryValue(s, e);
            switch (s)
            {
                case SchoolKind.Structure:
                    if (e == ElementKind.Ember)
                        return "① 轉化：消耗擬態錨定，最多 " + p.StructureConvertBase + "＋" + p.StructureConvertPerLink
                            + "×目標連結層數\n② 壓制目標 " + v + "＋轉化量";
                    if (e == ElementKind.Base) return "擬態錨定 +" + v;
                    return "① 擬態錨定 +" + v + "\n② 若本回合預告攻擊總量大於當前擬態錨定，再 +" + p.StructureBinderThreatBonus
                        + "\n③ 目標連結 +" + p.StructureBinderLink;
                case SchoolKind.Process:
                    if (e == ElementKind.Ember) return "① 目標校正 +" + v + " 層\n② 立即壓制目標 " + v;
                    if (e == ElementKind.Base)
                        return "① 目標校正 +" + v + " 層\n② 本回合校正結算不衰減\n③ 擬態錨定 +" + p.ProcessBaseShield;
                    return "① 目標校正 +" + v + " 層\n② 回復錨定值 " + p.ProcessBinderHeal + "（上限 " + p.MaxAnchor
                        + "）\n③ 目標連結 +" + p.ProcessBinderLink;
                default:
                    if (e == ElementKind.Ember)
                        return "① 斷連：消耗目標最多 " + p.RelationSeverMaxLayers + " 層連結\n② 壓制目標 " + v + "＋"
                            + p.RelationSeverBonusPerLayer + "×消耗層數";
                    if (e == ElementKind.Base) return "① 目標連結 +" + v + "\n② 目標本回合束縛 +" + p.RelationBaseBind;
                    return "目標連結 +" + v;
            }
        }

        /// <summary>這張牌對緊接的下一張牌的承接效果。</summary>
        public static string ResidueHint(RuleSet rules, ElementKind e)
        {
            var p = rules.Params;
            if (e == ElementKind.Ember) return "反應：下一張的主要數值 +" + p.ResidueEmberBonus;
            if (e == ElementKind.Base) return "結構：下一張結算後，擬態錨定 +" + p.ResidueBaseShield;
            return "關聯：下一張結算後，連結最少的敵人連結 +" + p.ResidueBinderLink;
        }

        // ---------------- 預估與結算摘要 ----------------

        public const string Bad = "#FF9A8F";
        public const string Good = "#9BE59B";

        public static string Colored(string text, string color)
        {
            return "<color=" + color + ">" + text + "</color>";
        }

        /// <summary>「a → b（±n）」；變少用紅色、變多用綠色（lowerIsBad＝false 時相反）。</summary>
        public static string Change(int before, int after, bool lowerIsBad = true)
        {
            if (before == after) return before.ToString();
            int d = after - before;
            string color = (d < 0) == lowerIsBad ? Bad : Good;
            return before + " → " + Colored("<b>" + after + "</b>（" + Signed(d) + "）", color);
        }

        /// <summary>這個敵人的攻擊對玩家的影響（白話）。</summary>
        public static string EnemyImpact(EnemyForecast e, BattleOutcome outcome)
        {
            if (e.ResolvedBefore) return "已解除";
            if (!e.Acted)
            {
                if (e.ResolvedAfter) return Colored("在行動前就會被解除，不會攻擊", Good);
                return outcome == BattleOutcome.Victory ? Colored("戰鬥先勝利，不會攻擊", Good) : "戰鬥先結束，不會攻擊";
            }
            if (e.Attack <= 0) return "本回合不攻擊";
            var parts = new System.Collections.Generic.List<string>();
            if (e.Diverted > 0) parts.Add("分流 " + e.Diverted);
            if (e.ShieldAbsorbed > 0) parts.Add("擬態擋 " + e.ShieldAbsorbed);
            parts.Add(e.AnchorLoss > 0 ? Colored("錨定值 −" + e.AnchorLoss, Bad) : Colored("錨定值無損", Good));
            string s = "攻擊 " + e.Attack + " → " + string.Join("、", parts.ToArray());
            if (e.Counter > 0) s += "，回應反擊 " + e.Counter;
            return s;
        }

        /// <summary>整個敵方回合對玩家的影響（一行）。</summary>
        public static string DamageSummary(RoundForecast f)
        {
            int attack = f.TotalAttack;
            if (attack <= 0) return "敵方沒有造成攻擊";
            var sb = new System.Text.StringBuilder();
            sb.Append("敵方攻擊共 ").Append(attack).Append("：擬態吸收 ").Append(f.TotalShieldAbsorbed)
              .Append("，錨定值 −").Append(f.TotalAnchorLoss);
            int diverted = attack - f.TotalShieldAbsorbed - f.TotalAnchorLoss;
            if (diverted > 0) sb.Append("，分流 ").Append(diverted);
            return sb.ToString();
        }

        /// <summary>把一個事件轉成一行紀錄。enemyName／cardName 只用來查名稱。</summary>
        public static string Event(BattleEvent e, Func<int, string> enemyName, Func<int, string> cardName)
        {
            string src = Source(e.Source);
            int attacker;
            if (e.Source != null && e.Source.StartsWith("enemy:") && int.TryParse(e.Source.Substring(6), out attacker))
                src = (enemyName != null ? enemyName(attacker) : "E" + attacker) + " 的攻擊：";
            string enemy = e.EnemyId == 0 ? "" : (enemyName != null ? enemyName(e.EnemyId) : "E" + e.EnemyId);
            switch (e.Kind)
            {
                case EventKind.Resonance:
                    return "★ 共鳴（三連）：" + Element((ElementKind)e.Amount) + " × " + e.Value + "，主要數值 ×1.5（無條件捨去）";
                case EventKind.CardResolved:
                    return "▶ " + src + " " + Element((ElementKind)e.Amount) + "：主要數值 " + e.Value
                        + (enemy == "" ? "" : "，目標 " + enemy);
                case EventKind.CardFizzled:
                    return "▶ " + src + " " + Element((ElementKind)e.Amount) + "：目標 " + (enemy == "" ? "?" : enemy)
                        + " 已解除，敵方指向效果失效";
                case EventKind.Pressure:
                    return "  " + src + " 壓制 " + enemy + "：名義 " + e.Amount + "，實際 " + e.Value;
                case EventKind.Conduction:
                    return "  傳導 → " + enemy + "：" + e.Amount + "（實際 " + e.Value + "）";
                case EventKind.ShieldChanged:
                    return "  " + src + " 擬態錨定 " + Signed(e.Amount) + " → " + e.Value;
                case EventKind.AnchorChanged:
                    return "  " + src + " 錨定值 " + Signed(e.Amount) + " → " + e.Value;
                case EventKind.CorrectionChanged:
                    return "  " + src + " " + enemy + " 校正 " + Signed(e.Amount) + " → " + e.Value;
                case EventKind.LinkChanged:
                    return "  " + src + " " + enemy + " 連結 " + Signed(e.Amount) + " → " + e.Value;
                case EventKind.BindChanged:
                    return "  " + src + " " + enemy + " 束縛 " + Signed(e.Amount) + " → " + e.Value;
                case EventKind.EnemyResolved:
                    return "  ✖ " + enemy + " 解除";
                case EventKind.EnemyActed:
                    return "◆ " + enemy + " 行動：攻擊 " + e.Amount;
                case EventKind.Redrawn:
                    return "↻ 調度：" + Card(cardName, e.Amount) + " 洗回牌庫，抽到 " + Card(cardName, e.Value);
                case EventKind.Refilled:
                    return "  補牌 " + e.Amount + " 張，手牌 " + e.Value + " 張";
                case EventKind.Reshuffled:
                    return "  棄牌區 " + e.Amount + " 張洗回牌庫";
                case EventKind.Victory:
                    return "■ 勝利：所有敵人已解除";
                case EventKind.Defeat:
                    return "■ 戰敗：錨定值 " + e.Value;
            }
            return e.ToString();
        }

        private static string Card(Func<int, string> cardName, int id)
        {
            return cardName != null ? cardName(id) : "#" + id;
        }
    }
}

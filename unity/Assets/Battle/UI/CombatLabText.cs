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

        /// <summary>把一個事件轉成一行紀錄。enemyName／cardName 只用來查名稱。</summary>
        public static string Event(BattleEvent e, Func<int, string> enemyName, Func<int, string> cardName)
        {
            string src = Source(e.Source);
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

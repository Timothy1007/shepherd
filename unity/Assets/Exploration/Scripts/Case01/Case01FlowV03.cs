using System.Collections.Generic;
using System.Text;

namespace Shepherd.Exploration.Case01
{
    /// <summary>核修復玩法：v0.2（既有三段式，預設）／v0.3（關係校正，實驗 QA 模式）。</summary>
    public enum Case01CorrectionMode
    {
        LegacyV02,
        RevisedV03,
    }

    public static class Case01IdsV03
    {
        public const string ChoiceAnchor = "v03_anchor";
        public const string ChoiceSecond = "v03_second";
        public const string ChoiceUndo = "v03_undo";
    }

    /// <summary>v0.3 狀態旗標（QA 內部；與 §3 的 13 個旗標並存，對應關係見 <see cref="Case01Flow"/> 的 v0.3 說明）。</summary>
    public static class Case01FlagsV03
    {
        public const string ObservedMismatch = "case01.v03_observed_mismatch";
        public const string EvidenceSupported = "case01.v03_evidence_supported";
        public const string ChosenAnchor = "case01.v03_anchor";
        public const string SecondLinkCorrected = "case01.v03_second_link";
        public const string CommittedByTraversal = "case01.v03_committed";
    }

    /// <summary>
    /// 核修復 v0.3「關係校正」（第十七號門專屬的失序法則，不是全遊戲固定步驟）：
    /// 異域會把門口與去向交換。觀察證明「現在通往哪裡」，舊地圖與艾琳的記號提供「原本應通往哪裡」。
    /// 錨點只固定一條可信關係（△→書房）；另一條錯接（○ 多出來的玄關去向）要交給 17 號參照才會停止跳動；
    /// 兩條同時成立後，親自走過其中一條即寫回校正，核立即局部穩定。
    ///
    /// 狀態與既有旗標的對應：observedMismatch＝走過錯接的 △（樓梯平台或夾層）；evidenceSupported＝舊地圖＋合照＋艾琳手記；
    /// chosenAnchor → 同時設定 route_anchor_set；secondLinkCorrected → 同時設定 missing_link_restored（17 號門出現）；
    /// committedByTraversal → 同時設定 core_stabilized 與 house_state=temporarily_stable。
    /// 錯誤選擇只「預演」矛盾，不改路由、不傳送，一鍵撤銷。
    /// </summary>
    public sealed partial class Case01Flow
    {
        public Case01CorrectionMode Correction { get; private set; }

        private int previewConflict;
        private bool circleObserved;

        public Case01Flow(Case01CorrectionMode mode) : this()
        {
            Correction = mode;
        }

        /// <summary>目前預演中的矛盾（0＝沒有；1～4 對應不同錯誤選擇）。只影響顯示，不改路由。</summary>
        public int PreviewConflict { get { return previewConflict; } }
        public bool V03Anchored { get { return Has(Case01FlagsV03.ChosenAnchor); } }
        public bool V03SecondLink { get { return Has(Case01FlagsV03.SecondLinkCorrected); } }
        public bool V03Committed { get { return Has(Case01FlagsV03.CommittedByTraversal); } }

        /// <summary>已親自見證 △ 錯接（樓梯平台的書房門或夾層的 △ 門通往餐廳）。</summary>
        public bool V03Observed
        {
            get { return Has(Case01Flags.RouteMismatchSeen) || LastSeen(Case01Door.MezzTriangle) == Case01Room.Dining; }
        }

        public bool V03EvidenceReady
        {
            get { return Has(Case01Flags.MapObserved) && Has(Case01Flags.PhotoObserved) && Has(Case01Flags.EllieStudyKnown); }
        }

        private void OnBattleWonV03()
        {
            // 戰後的現況：△ 錯連餐廳；○ 在餐廳／玄關之間跳動；17 不存在。
            routes[Case01Door.MezzTriangle] = Case01Room.Dining;
            routes[Case01Door.MezzCircle] = Case01Room.Entrance;
        }

        // ---------------- 裝置 ----------------

        /// <summary>校正裝置的三條關係：現況（已證實／未知）與記錄（推定／未知）。不在玩家觀測前揭露答案。</summary>
        public string DeviceReadout()
        {
            var sb = new StringBuilder();
            sb.Append("<b><color=#E6BD52>校正裝置</color></b>　<color=#8E98A6>現況＝你親眼看到的；記錄＝證據推定的</color>\n");
            string triNow = V03Anchored ? "書房（已固定）" : V03Observed ? "餐廳（已證實：你走過）" : "未知（走進△門看看）";
            string triRec = Has(Case01Flags.EllieStudyKnown) ? "書房（推定：艾琳手記、門框記號）" : "未知（艾琳的手記在二樓書房）";
            string cirNow = V03SecondLink ? "餐廳（已固定）" : circleObserved ? "餐廳／玄關之間跳動（已證實）" : "未知（可走進○門確認）";
            string squNow = V03SecondLink ? "玄關（17 號門已出現）" : "不存在（北牆沒有門）";
            string squRec = Has(Case01Flags.MapObserved) && Has(Case01Flags.PhotoObserved)
                ? "玄關＝十七號的門（推定：舊地圖補畫的一格＋合照）"
                : "未知（舊地圖在二樓書房、合照在一樓餐廳）";
            sb.Append("<b>△書房門</b>\n　現況：").Append(triNow).Append("\n　記錄：").Append(triRec).Append('\n');
            sb.Append("<b>○餐廳門</b>\n　現況：").Append(cirNow).Append("\n　記錄：餐廳（推定：記號○）\n");
            sb.Append("<b>□17</b>\n　現況：").Append(squNow).Append("\n　記錄：").Append(squRec);
            return sb.ToString();
        }

        private Case01Response CoreV03()
        {
            if (previewConflict != 0) return PreviewResponse();
            var readout = DeviceReadout();
            if (!V03EvidenceReady)
            {
                var missing = new List<string>();
                if (!Has(Case01Flags.MapObserved)) missing.Add("街區舊地圖（二樓書房）");
                if (!Has(Case01Flags.PhotoObserved)) missing.Add("合照（一樓餐廳）");
                if (!Has(Case01Flags.EllieStudyKnown)) missing.Add("艾琳的手記（二樓書房）");
                return new Case01Response { Text = readout + "\n\n記錄還推不出來。去查：" + string.Join("、", missing.ToArray()) + "（從樓梯平台的第三扇門回來）" };
            }
            if (!V03Observed)
                return new Case01Response { Text = readout + "\n\n△ 現在到底通往哪裡？走進左側的 △ 門親眼確認（就在旁邊）。" };
            Session.Set(Case01FlagsV03.ObservedMismatch);
            if (Session.Set(Case01FlagsV03.EvidenceSupported))
                Session.AddJournal("v03_device", "校正裝置：三條關係", "△ 現在錯連餐廳，但記錄指向書房；○ 在餐廳與玄關之間跳動；17 在地圖上存在，現場卻沒有門。");
            if (!V03Anchored)
            {
                return new Case01Response
                {
                    Choice = new Case01Choice
                    {
                        Id = Case01IdsV03.ChoiceAnchor, Prompt = readout + "\n\n錨點只能固定一條可信的關係。要固定哪一條？",
                        Options = new[] { "△→書房", "○→餐廳", "17→不存在" },
                    },
                };
            }
            if (!V03SecondLink)
            {
                return new Case01Response
                {
                    Choice = new Case01Choice
                    {
                        Id = Case01IdsV03.ChoiceSecond,
                        Prompt = readout + "\n\n△ 已固定，但 ○ 仍在餐廳與玄關之間跳動──它多出來的「玄關」去向不屬於它。要交給哪一條關係？",
                        Options = new[] { "交給 17 號參照（□）", "交給 △（書房）", "留在 ○（繼續變動）" },
                    },
                };
            }
            return new Case01Response
            {
                Text = readout + "\n\n兩條關係都已校正。走過其中一條──△ 門（書房）或北牆的 17 號門（玄關）──把校正寫回。",
            };
        }

        private static readonly string[] PreviewTexts =
        {
            "",
            "預演：固定 ○→餐廳……△ 被擠到玄關，書房從關聯圖上消失。\n但艾琳的手記和門框記號都證明書房存在──矛盾。",
            "預演：固定「17 不存在」……舊地圖上補畫的那一格、合照背面的「週日。兩個人。」都失去對應，核的裂隙反而擴大──矛盾。",
            "預演：把玄關去向交給 △……書房已被 △ 固定，兩條關係擠進同一個房間，△ 開始鬆動──矛盾。",
            "預演：留在 ○……○ 會一直在餐廳和玄關之間跳動，17 號的缺口仍是空的──核無法穩定。",
        };

        private Case01Response PreviewResponse()
        {
            return new Case01Response
            {
                Choice = new Case01Choice
                {
                    Id = Case01IdsV03.ChoiceUndo, Prompt = "<color=#FF8C8C>" + PreviewTexts[previewConflict] + "</color>\n\n（只是預演，路線沒有改變）",
                    Options = new[] { "撤銷，回到裝置" },
                },
            };
        }

        private Case01Response ChooseV03(string id, int index)
        {
            if (CoreStabilized || !Has(Case01Flags.BattleWon)) return Case01Response.None;
            if (id == Case01IdsV03.ChoiceUndo)
            {
                if (previewConflict == 0) return Case01Response.None;
                previewConflict = 0;
                var back = CoreV03();
                back.WorldChanged = true;
                back.Toast = "已撤銷預演";
                return back;
            }
            if (previewConflict != 0 || !V03EvidenceReady || !V03Observed) return Case01Response.None;
            if (id == Case01IdsV03.ChoiceAnchor && !V03Anchored)
            {
                if (index != 0)
                {
                    previewConflict = index == 1 ? 1 : 2;
                    Session.AddJournal("v03_preview_" + previewConflict, "預演的矛盾", PreviewTexts[previewConflict]);
                    var p = PreviewResponse();
                    p.WorldChanged = true;
                    return p;
                }
                Session.Set(Case01FlagsV03.ChosenAnchor);
                Session.Set(Case01Flags.RouteAnchorSet);
                Session.Advance(Case01Stage.LinkRestoration);
                routes[Case01Door.MezzTriangle] = Case01Room.Study;
                Session.AddJournal("v03_anchor", "錨點：△→書房", "依艾琳的手記與門框記號，固定 △→書房。○ 仍在跳動。");
                return new Case01Response
                {
                    Text = "△→書房 固定了。△ 門框的光穩定下來。\n但 ○ 還在餐廳和玄關之間跳動──再看一次裝置。",
                    Toast = "錨點：△→書房", WorldChanged = true,
                };
            }
            if (id == Case01IdsV03.ChoiceSecond && V03Anchored && !V03SecondLink)
            {
                if (index != 0)
                {
                    previewConflict = index == 1 ? 3 : 4;
                    Session.AddJournal("v03_preview_" + previewConflict, "預演的矛盾", PreviewTexts[previewConflict]);
                    var p = PreviewResponse();
                    p.WorldChanged = true;
                    return p;
                }
                Session.Set(Case01FlagsV03.SecondLinkCorrected);
                Session.Set(Case01Flags.MissingLinkRestored);
                routes[Case01Door.MezzCircle] = Case01Room.Dining;
                Session.AddJournal("v03_second", "第二條關係：17→玄關", "○ 多出來的玄關去向交給 17 號參照。△→書房 與 17→玄關 同時成立，北牆出現 17 號門。");
                return new Case01Response
                {
                    Text = "○ 停止跳動，回到餐廳。多出來的玄關去向接到了 17──北牆出現一扇真正的門。\n現在 △→書房、17→玄關 同時成立。走過其中一條，把校正寫回。",
                    Toast = "17→玄關 已接上", WorldChanged = true,
                };
            }
            return Case01Response.None;
        }

        // ---------------- 穿門 ----------------

        private Case01Response TraverseV03(Case01Door door, Case01Room dest, Case01Response r)
        {
            switch (door)
            {
                case Case01Door.MezzTriangle:
                    if (!V03Anchored && dest == Case01Room.Dining && Has(Case01Flags.BattleWon))
                        Session.AddJournal("v03_saw_triangle", "親眼確認：△ 門", "夾層的 △ 門通往了餐廳，不是書房。");
                    if (V03SecondLink && !V03Committed && dest == Case01Room.Study) Commit(r);
                    break;
                case Case01Door.MezzCircle:
                    if (Has(Case01Flags.BattleWon) && !V03SecondLink)
                    {
                        if (!circleObserved) Session.AddJournal("v03_saw_circle", "親眼確認：○ 門", "○ 門這次通往" + Case01Texts.RoomName(dest) + "；再走一次會換到另一邊。");
                        circleObserved = true;
                        routes[door] = dest == Case01Room.Entrance ? Case01Room.Dining : Case01Room.Entrance;
                    }
                    break;
                case Case01Door.Mezz17:
                    if (V03SecondLink && !V03Committed) Commit(r);
                    break;
            }
            return r;
        }

        private void Commit(Case01Response r)
        {
            Session.Set(Case01FlagsV03.CommittedByTraversal);
            Session.Set(Case01Flags.CoreStabilized);
            Session.SetValue(Case01Session.HouseStateKey, Case01Session.HouseTemporarilyStable);
            Session.Advance(Case01Stage.CoreStabilized);
            routes[Case01Door.StudyDoor] = Case01Room.Study;
            Session.AddJournal("v03_commit", Case01Texts.Step3Title, "走過已校正的連結，校正被寫回。" + Case01Texts.Step3Journal);
            r.CommitEffect = true;
            r.WorldChanged = true;
            r.Toast = "校正寫回：核已局部穩定";
            r.Text = "門框一格格扣回原位，低沉的接合聲穿過整棟房子。\n核穩定了──但只是暫時的。";
        }
    }
}

using System.Collections.Generic;

namespace Shepherd.Exploration.Case01
{
    public enum Case01Room { None, Entrance, Kitchen, Living, Dining, Landing, Study, Bedroom, Storage, Mezzanine, Courtyard }

    /// <summary>可能變成「錯接通路」的門。其他房門都是一般的實體門洞。</summary>
    public enum Case01Door
    {
        /// <summary>二樓樓梯平台 → 書房（初始為實體連通）。</summary>
        StudyDoor,
        /// <summary>樓梯平台外牆上浮現的第三扇門 → 夾層。</summary>
        ThirdDoor,
        /// <summary>夾層的 △ 門。</summary>
        MezzTriangle,
        /// <summary>夾層的 ○ 門。</summary>
        MezzCircle,
        /// <summary>夾層北牆的 17 號門（□）。</summary>
        Mezz17,
        /// <summary>夾層南側 → 回到樓梯平台。</summary>
        MezzBack,
        /// <summary>異常中庭的裂口 → 夾層（戰勝後開啟）。</summary>
        CourtyardTear,
    }

    public enum Case01Mark { None, Triangle, Circle, Square }

    public enum Case01Checkpoint { S00, S30, S40, S50 }

    /// <summary>可互動物件的 ID（場景中的 <see cref="Case01Interactable"/> 使用）。</summary>
    public static class Case01Ids
    {
        public const string Table = "table";
        public const string Photo = "photo";
        public const string SundayNote = "sunday_note";
        public const string Tea = "tea";
        public const string Calendar = "calendar";
        public const string FrontDoor = "front_door";
        public const string Map = "map";
        public const string EllieNotes = "ellie_notes";
        public const string OldRecord = "old_record";
        public const string Postcard = "postcard";
        public const string StorageDoor = "storage_door";
        public const string StudyMark = "study_mark";
        public const string ThirdDoor = "third_door";
        public const string Core = "mezz_core";
        public const string MezzTriangleMark = "mezz_mark_triangle";
        public const string MezzCircleMark = "mezz_mark_circle";
        public const string Wall17 = "mezz_wall17";

        // v0.2 生活細節與證物。
        public const string TeaTin = "tea_tin";
        public const string LivingBookNote = "living_book_note";
        public const string FriendCard = "friend_card";
        public const string ShoppingList = "shopping_list";
        public const string TravelRoute = "travel_route";
        public const string JobLetter = "job_letter";
        public const string RepairKit = "repair_kit";
        public const string FileBoxes = "file_boxes";
        public const string EvidencePhoto = "evidence_photo";

        public const string ChoiceOverride = "override_study_door";
        public const string ChoiceAnchor = "anchor_link";
        public const string ChoiceFrontDoor = "front_door";
    }

    public sealed class Case01Choice
    {
        public string Id;
        public string Prompt;
        public string[] Options;
    }

    /// <summary>一次互動／事件的結果：要顯示的文字與要求場景執行的演出。</summary>
    public sealed class Case01Response
    {
        public string Text;
        /// <summary>短觀察（準星下方的小字，不開側邊面板）。</summary>
        public string Note;
        /// <summary>要進入 3D 檢視的證物 ID。</summary>
        public string Inspect;
        public string Toast;
        public string Radio;
        public Case01Choice Choice;
        /// <summary>要把玩家安全傳送到的房間（錯誤操作的回饋、通路目的地）。</summary>
        public Case01Room? Redirect;
        public bool BeginDefense;
        public bool PullToCourtyard;
        public bool Restart;
        public bool Exit;
        /// <summary>房屋狀態有變化，場景需要刷新外觀。</summary>
        public bool WorldChanged;
        /// <summary>v0.3：這次穿門把校正寫回（場景播放門框歸位與接合聲）。</summary>
        public bool CommitEffect;

        public static readonly Case01Response None = new Case01Response();
    }

    /// <summary>
    /// 〈第十七號門〉室內 QA 的遊玩規則（純邏輯，不依賴場景，可直接單元測試）。
    /// 線索、門的連結映射、核的三段修復與戰鬥前後條件都在這裡；場景只負責呈現與傳送。
    /// 本關的通路術式與核修復是為試玩採用的暫定方案，不是普遍的異域規則。
    /// </summary>
    public sealed partial class Case01Flow
    {
        public Case01Session Session { get; private set; }

        private readonly Dictionary<Case01Door, Case01Room> routes = new Dictionary<Case01Door, Case01Room>();
        private readonly Dictionary<Case01Door, Case01Room> lastSeen = new Dictionary<Case01Door, Case01Room>();
        private bool armed;
        private bool wrongActive;
        private bool diningLeftAfterObserve;
        private bool rewriteAnnounced;
        private bool confirmedSinceSwap;
        private bool triangleVerified;
        private bool door17Verified;
        private int circleFlip;

        public Case01Flow()
        {
            Session = new Case01Session();
            routes[Case01Door.StudyDoor] = Case01Room.Study;
            routes[Case01Door.ThirdDoor] = Case01Room.Mezzanine;
            routes[Case01Door.MezzTriangle] = Case01Room.Study;
            routes[Case01Door.MezzCircle] = Case01Room.Dining;
            routes[Case01Door.Mezz17] = Case01Room.Entrance;
            routes[Case01Door.MezzBack] = Case01Room.Landing;
            routes[Case01Door.CourtyardTear] = Case01Room.Mezzanine;
        }

        private bool Has(string f) { return Session.Has(f); }

        // ---------------- 查詢（供場景刷新外觀） ----------------

        /// <summary>書房線索已足夠，樓梯平台開始出現異常提示（還沒錯接）。</summary>
        public bool WrongConnectionArmed { get { return armed; } }
        /// <summary>樓梯平台的書房門已經錯接過（S30 開始）。</summary>
        public bool WrongConnectionActive { get { return wrongActive; } }
        public bool StairRouteRestored { get { return Has(Case01Flags.StairRouteRestored); } }
        public bool DefenseTriggered { get { return Has(Case01Flags.DefenseTriggered); } }
        public bool CoreStabilized { get { return Has(Case01Flags.CoreStabilized); } }
        /// <summary>夾層處於反制後、核尚未穩定的危險狀態。</summary>
        public bool DefenseActive { get { return DefenseTriggered && !CoreStabilized; } }
        public bool DiningShowsTwoSets { get { return !Has(Case01Flags.TableRewritten); } }
        /// <summary>已看過兩副餐具並離開餐廳：場景可在玩家看不到餐桌時改寫成一副。</summary>
        public bool DiningRewritePending { get { return diningLeftAfterObserve && !Has(Case01Flags.TableRewritten); } }
        public bool ThirdDoorVisible { get { return armed || wrongActive || StairRouteRestored; } }
        public bool ThirdDoorOpen { get { return StairRouteRestored; } }
        public bool Door17Open { get { return Has(Case01Flags.MissingLinkRestored); } }
        public bool CourtyardTearOpen { get { return Has(Case01Flags.BattleWon); } }
        /// <summary>補回 17 之後，已親自走過 △ 或 ○ 門確認新的去向。</summary>
        public bool SwapConfirmed { get { return confirmedSinceSwap; } }
        public bool TriangleVerified { get { return triangleVerified; } }
        public bool Door17Verified { get { return door17Verified; } }

        public static Case01Mark MarkOf(Case01Door door)
        {
            switch (door)
            {
                case Case01Door.StudyDoor:
                case Case01Door.MezzTriangle: return Case01Mark.Triangle;
                case Case01Door.MezzCircle: return Case01Mark.Circle;
                case Case01Door.Mezz17: return Case01Mark.Square;
                default: return Case01Mark.None;
            }
        }

        /// <summary>艾琳的記號對應的房間（手記：△ 書房／○ 餐廳／□ 玄關）。</summary>
        public static Case01Room RoomOfMark(Case01Mark m)
        {
            switch (m)
            {
                case Case01Mark.Triangle: return Case01Room.Study;
                case Case01Mark.Circle: return Case01Room.Dining;
                case Case01Mark.Square: return Case01Room.Entrance;
                default: return Case01Room.None;
            }
        }

        /// <summary>目前這扇門通往哪裡。</summary>
        public Case01Room Destination(Case01Door door) { return routes[door]; }

        /// <summary>上次實際走過這扇門時到達的房間（沒走過為 None）。</summary>
        public Case01Room LastSeen(Case01Door door)
        {
            Case01Room r;
            return lastSeen.TryGetValue(door, out r) ? r : Case01Room.None;
        }

        /// <summary>這扇門現在是否是「傳送通路」（否則是實體門洞或封閉）。</summary>
        public bool PortalActive(Case01Door door)
        {
            switch (door)
            {
                case Case01Door.StudyDoor: return routes[door] != Case01Room.Study;
                case Case01Door.ThirdDoor: return StairRouteRestored;
                case Case01Door.Mezz17: return Door17Open;
                case Case01Door.CourtyardTear: return CourtyardTearOpen;
                default: return true;
            }
        }

        /// <summary>門的記號與目前去向是否相符。</summary>
        public bool MatchesMark(Case01Door door)
        {
            var m = MarkOf(door);
            return m == Case01Mark.None || RoomOfMark(m) == routes[door];
        }

        // ---------------- 互動 ----------------

        public Case01Response Interact(string id)
        {
            switch (id)
            {
                case Case01Ids.Table: return Table();
                case Case01Ids.Photo: return Photo();
                case Case01Ids.SundayNote:
                    Session.AddJournal("sunday_note", Case01Texts.SundayNoteTitle, Case01Texts.SundayNote);
                    return new Case01Response { Text = Case01Texts.SundayNote };
                case Case01Ids.Tea: return new Case01Response { Text = Case01Texts.Tea };
                case Case01Ids.Calendar: return new Case01Response { Text = Case01Texts.Calendar };
                case Case01Ids.Postcard: return new Case01Response { Text = Case01Texts.Postcard };
                case Case01Ids.StorageDoor: return new Case01Response { Text = Case01Texts.Storage };
                case Case01Ids.Map: return StudyClue(Case01Flags.MapObserved, "map", Case01Texts.MapTitle, Case01Texts.Map);
                case Case01Ids.EllieNotes:
                    var r = StudyClue(Case01Flags.EllieStudyKnown, "ellie_notes", Case01Texts.EllieTitle, Case01Texts.Ellie);
                    Session.Advance(Case01Stage.StudyDiscovery);
                    return r;
                case Case01Ids.OldRecord: return StudyClue(Case01Flags.OldCorrectionRecord, "old_record", Case01Texts.RecordTitle, Case01Texts.Record);
                case Case01Ids.StudyMark: return StudyMark();
                case Case01Ids.ThirdDoor:
                    return new Case01Response { Text = StairRouteRestored ? Case01Texts.ThirdDoorOpen : Case01Texts.ThirdDoorSealed };
                case Case01Ids.Core: return Core();
                case Case01Ids.MezzTriangleMark: return new Case01Response { Text = Case01Texts.MezzTriangleMark + "\n" + SeenText(Case01Door.MezzTriangle) };
                case Case01Ids.MezzCircleMark: return new Case01Response { Text = Case01Texts.MezzCircleMark + "\n" + SeenText(Case01Door.MezzCircle) };
                case Case01Ids.Wall17: return new Case01Response { Text = Door17Open ? Case01Texts.Wall17After : Case01Texts.Wall17Before };
                case Case01Ids.LivingBookNote: return new Case01Response { Note = Case01Texts.LivingBookNote };
                case Case01Ids.RepairKit: return new Case01Response { Note = Case01Texts.RepairKit };
                case Case01Ids.FileBoxes:
                    return new Case01Response { Note = WrongConnectionActive && !CoreStabilized ? Case01Texts.FileBoxesShuffled : Case01Texts.FileBoxes };
                case Case01Ids.FriendCard: return LifeClue("friend_card", Case01Texts.FriendCardTitle, Case01Texts.FriendCard);
                case Case01Ids.ShoppingList: return LifeClue("shopping_list", Case01Texts.ShoppingListTitle, Case01Texts.ShoppingList);
                case Case01Ids.TravelRoute: return LifeClue("travel_route", Case01Texts.TravelRouteTitle, Case01Texts.TravelRoute);
                case Case01Ids.JobLetter: return LifeClue("job_letter", Case01Texts.JobLetterTitle, Case01Texts.JobLetter);
                case Case01Ids.EvidencePhoto: return CollectFamilyPhoto();
                case Case01Ids.FrontDoor:
                    return new Case01Response
                    {
                        Choice = CoreStabilized
                            ? new Case01Choice
                            {
                                Id = Case01Ids.ChoiceFrontDoor,
                                Prompt = Case01Texts.FrontDoorAfter + (FamilyPhotoCollected ? "" : Case01Texts.ExitEvidenceHint),
                                Options = new[] { "離開十七號（結束 QA）", "再看看" },
                            }
                            : new Case01Choice { Id = Case01Ids.ChoiceFrontDoor, Prompt = Case01Texts.FrontDoorBefore, Options = new[] { "重新開始 QA（回到玄關起點）", "繼續調查" } },
                    };
                default: return Case01Response.None;
            }
        }

        // ---------------- v0.2 生活細節、環境觀察、證物 ----------------

        /// <summary>短觀察，同時記入日誌（可重讀）。</summary>
        private Case01Response LifeClue(string journalId, string title, string text)
        {
            bool isNew = Session.AddJournal(journalId, title, text);
            return new Case01Response { Note = text, Toast = isNew ? "已記入日誌：" + title : null };
        }

        /// <summary>FP-001 是否已在本次 QA 收藏。</summary>
        public bool FamilyPhotoCollected { get { return Session.HasEvidence(Case01Texts.EvidenceId); } }

        /// <summary>FP-001 只在核穩定後、尚未拾取時出現在夾層。</summary>
        public bool FamilyPhotoAvailable { get { return CoreStabilized && !FamilyPhotoCollected; } }

        private Case01Response CollectFamilyPhoto()
        {
            if (!FamilyPhotoAvailable) return Case01Response.None;
            Session.CollectEvidence(Case01Texts.EvidenceId);
            Session.Set(Case01Texts.EvidenceKey);
            Session.AddJournal("evidence_fp001", Case01Texts.FamilyPhotoTitle,
                Case01Texts.FamilyPhotoSource + "\n" + Case01Texts.FamilyPhotoDescription + "\n" + Case01Texts.FamilyPhotoUnresolved);
            return new Case01Response { Toast = Case01Texts.FamilyPhotoToast, Inspect = Case01Texts.EvidenceId, WorldChanged = true };
        }

        /// <summary>玩家回訪時看到的安靜異變：寫入日誌（含原始觀察），每項只寫一次。</summary>
        public bool NoteEnvironment(string id)
        {
            switch (id)
            {
                case "env_living": return Session.AddJournal(id, Case01Texts.EnvLivingTitle, Case01Texts.EnvLiving);
                case "env_dining": return Session.AddJournal(id, Case01Texts.EnvDiningTitle, Case01Texts.EnvDining);
                case "env_study": return Session.AddJournal(id, Case01Texts.EnvStudyTitle, Case01Texts.EnvStudy);
                case "env_landing": return Session.AddJournal(id, Case01Texts.EnvLandingTitle, Case01Texts.EnvLanding);
                case "env_after": return Session.AddJournal(id, Case01Texts.EnvAfterTitle, Case01Texts.EnvAfter);
                default: return false;
            }
        }

        private string SeenText(Case01Door door)
        {
            var seen = LastSeen(door);
            return seen == Case01Room.None ? Case01Texts.NotConfirmed : Case01Texts.ConfirmedPrefix + Case01Texts.RoomName(seen) + "。";
        }

        private Case01Response Table()
        {
            if (!Has(Case01Flags.TableRewritten))
            {
                bool first = Session.Set(Case01Flags.TableTwo);
                Session.AddJournal("table_two", Case01Texts.TableTwoTitle, Case01Texts.TableTwo);
                Session.Advance(Case01Stage.ObservedDining);
                return new Case01Response { Text = Case01Texts.TableTwo, Toast = first ? "已記入日誌：兩副餐具" : null };
            }
            return new Case01Response { Text = Case01Texts.TableOne + "\n<color=#9BB8E5>日誌：第一次看到的是兩副。</color>" };
        }

        private Case01Response Photo()
        {
            if (CoreStabilized)
            {
                bool first = Session.Set(Case01Flags.EvidenceArchived);
                Session.AddJournal("evidence_photo", Case01Texts.EvidenceTitle, Case01Texts.Evidence);
                return new Case01Response { Text = Case01Texts.PhotoFaded, Toast = first ? "證物已歸檔" : null };
            }
            bool isNew = Session.Set(Case01Flags.PhotoObserved);
            Session.AddJournal("photo_two", Case01Texts.PhotoTitle, Case01Texts.Photo);
            Session.Advance(Case01Stage.ObservedDining);
            return new Case01Response { Text = Case01Texts.Photo, Toast = isNew ? "已記入日誌：合照" : null };
        }

        private Case01Response StudyClue(string flag, string journalId, string title, string text)
        {
            bool isNew = Session.Set(flag);
            Session.AddJournal(journalId, title, text);
            var r = new Case01Response { Text = text, Toast = isNew ? "已記入日誌：" + title : null };
            if (!armed && Has(Case01Flags.EllieStudyKnown) && Has(Case01Flags.OldCorrectionRecord))
            {
                armed = true;
                r.Toast = Case01Texts.ArmedToast;
                r.Radio = Case01Texts.ArmedRadio;
                r.WorldChanged = true;
                Session.AddJournal("radio_armed", Case01Texts.Companion, Case01Texts.ArmedRadio);
            }
            return r;
        }

        private Case01Response StudyMark()
        {
            if (!wrongActive) return new Case01Response { Text = Case01Texts.StudyMarkPlain + (Has(Case01Flags.EllieStudyKnown) ? "（手記：△＝書房）" : "") };
            if (StairRouteRestored)
                return new Case01Response { Text = DefenseActive ? Case01Texts.StudyMarkDefense : Case01Texts.StudyMarkRestored };
            if (!Has(Case01Flags.RouteMismatchSeen)) return new Case01Response { Text = Case01Texts.StudyMarkUnverified };
            return new Case01Response
            {
                Choice = new Case01Choice { Id = Case01Ids.ChoiceOverride, Prompt = Case01Texts.OverridePrompt, Options = Case01Texts.OverrideOptions },
            };
        }

        private Case01Response Core()
        {
            if (CoreStabilized) return new Case01Response { Text = Case01Texts.CoreStable };
            if (!DefenseTriggered)
            {
                Session.Set(Case01Flags.DefenseTriggered);
                Session.Advance(Case01Stage.AnomalyDefense);
                // 反制：已歸位的書房門重新連錯（但「已覆寫成功」的記錄保留，第三扇門維持開啟，不會卡關）。
                routes[Case01Door.StudyDoor] = Case01Room.Dining;
                Session.AddJournal("radio_defense", Case01Texts.Companion, Case01Texts.DefenseRadio);
                return new Case01Response
                {
                    Text = Case01Texts.CoreStart, Toast = Case01Texts.DefenseToast, Radio = Case01Texts.DefenseRadio,
                    BeginDefense = true, WorldChanged = true,
                };
            }
            if (!Has(Case01Flags.BattleWon)) return new Case01Response { Text = Case01Texts.CoreBattlePending, PullToCourtyard = true };
            if (Correction == Case01CorrectionMode.RevisedV03) return CoreV03();

            // ---- 第一段：補回被省略的 17 參照 ----
            if (!Has(Case01Flags.MissingLinkRestored))
            {
                var missing = new List<string>();
                if (!Has(Case01Flags.MapObserved)) missing.Add(Case01Texts.CoreNeedMap);
                if (!Has(Case01Flags.PhotoObserved)) missing.Add(Case01Texts.CoreNeedPhoto);
                if (missing.Count > 0) return new Case01Response { Text = Case01Texts.CoreNeedEvidence + string.Join("、", missing.ToArray()) };
                Session.Set(Case01Flags.MissingLinkRestored);
                Session.Advance(Case01Stage.LinkRestoration);
                routes[Case01Door.MezzTriangle] = Case01Room.Dining;
                routes[Case01Door.MezzCircle] = Case01Room.Study;
                confirmedSinceSwap = false;
                Session.AddJournal("step1", Case01Texts.Step1Title, Case01Texts.Step1Journal);
                return new Case01Response { Text = Case01Texts.Step1, Toast = "北牆出現了 17 號門", WorldChanged = true };
            }

            // ---- 第二段：依記號選錨點 ----
            if (!Has(Case01Flags.RouteAnchorSet))
            {
                if (!confirmedSinceSwap) return new Case01Response { Text = Case01Texts.Step2NeedConfirm };
                return new Case01Response
                {
                    Choice = new Case01Choice { Id = Case01Ids.ChoiceAnchor, Prompt = Case01Texts.AnchorPrompt, Options = Case01Texts.AnchorOptions },
                };
            }

            // ---- 第三段：在兩條已驗證的關係上補缺口 ----
            var notYet = new List<string>();
            if (!triangleVerified) notYet.Add(Case01Texts.Step3NeedTriangle);
            if (!door17Verified) notYet.Add(Case01Texts.Step3NeedDoor17);
            if (notYet.Count > 0) return new Case01Response { Text = Case01Texts.Step3NeedVerify + string.Join("、", notYet.ToArray()) };
            Session.Set(Case01Flags.CoreStabilized);
            Session.SetValue(Case01Session.HouseStateKey, Case01Session.HouseTemporarilyStable);
            Session.Advance(Case01Stage.CoreStabilized);
            routes[Case01Door.StudyDoor] = Case01Room.Study;
            routes[Case01Door.MezzCircle] = Case01Room.Dining;
            Session.AddJournal("step3", Case01Texts.Step3Title, Case01Texts.Step3Journal);
            return new Case01Response { Text = Case01Texts.Step3, Toast = Case01Texts.Step3Toast, WorldChanged = true };
        }

        // ---------------- 選擇 ----------------

        /// <summary>回應選項（index 從 0 起；-1＝取消）。</summary>
        public Case01Response Choose(string choiceId, int index)
        {
            if (index < 0) return Case01Response.None;
            switch (choiceId)
            {
                case Case01Ids.ChoiceOverride: return Override(index);
                case Case01Ids.ChoiceAnchor: return Anchor(index);
                case Case01IdsV03.ChoiceAnchor:
                case Case01IdsV03.ChoiceSecond:
                case Case01IdsV03.ChoiceUndo:
                    return ChooseV03(choiceId, index);
                case Case01Ids.ChoiceFrontDoor:
                    if (index != 0) return Case01Response.None;
                    if (!CoreStabilized) return new Case01Response { Restart = true };
                    // 案卷自動保留主要證據（隱藏證物不影響結束）。
                    Session.Set(Case01Flags.EvidenceArchived);
                    Session.AddJournal("evidence_photo", Case01Texts.EvidenceTitle, Case01Texts.Evidence);
                    Session.Advance(Case01Stage.Exited);
                    return new Case01Response { Exit = true };
                default: return Case01Response.None;
            }
        }

        private Case01Response Override(int index)
        {
            if (!wrongActive || StairRouteRestored || !Has(Case01Flags.RouteMismatchSeen)) return Case01Response.None;
            var mark = index == 0 ? Case01Mark.Triangle : index == 1 ? Case01Mark.Circle : Case01Mark.Square;
            routes[Case01Door.StudyDoor] = RoomOfMark(mark);
            if (mark == Case01Mark.Triangle)
            {
                Session.Set(Case01Flags.StairRouteRestored);
                Session.AddJournal("route_restored", Case01Texts.RouteRestoredTitle, Case01Texts.RouteRestored);
                return new Case01Response { Text = Case01Texts.OverrideOk, Toast = "樓梯關聯已重設，第三扇門打開了", WorldChanged = true };
            }
            return new Case01Response { Text = mark == Case01Mark.Circle ? Case01Texts.OverrideCircle : Case01Texts.OverrideSquare, WorldChanged = true };
        }

        private Case01Response Anchor(int index)
        {
            if (!Has(Case01Flags.MissingLinkRestored) || Has(Case01Flags.RouteAnchorSet) || !confirmedSinceSwap) return Case01Response.None;
            if (index != 0)
            {
                Session.AddJournal("anchor_wrong", Case01Texts.AnchorWrongTitle, Case01Texts.AnchorWrongJournal);
                return new Case01Response { Text = Case01Texts.AnchorWrong, Redirect = Case01Room.Dining };
            }
            Session.Set(Case01Flags.RouteAnchorSet);
            routes[Case01Door.MezzTriangle] = Case01Room.Study;
            routes[Case01Door.MezzCircle] = Case01Room.Entrance;
            triangleVerified = false;
            Session.AddJournal("anchor", Case01Texts.AnchorTitle, "依艾琳的記號，把△門錨定到書房。○門仍在變動。");
            return new Case01Response { Text = Case01Texts.AnchorOk, Toast = "錨點已設定：△→書房", WorldChanged = true };
        }

        // ---------------- 移動與通路 ----------------

        /// <summary>玩家穿過一扇作用中的通路門；回傳目的地（Redirect）。</summary>
        public Case01Response Traverse(Case01Door door)
        {
            if (!PortalActive(door)) return Case01Response.None;
            var dest = routes[door];
            lastSeen[door] = dest;
            var r = new Case01Response { Redirect = dest };
            if (Correction == Case01CorrectionMode.RevisedV03 && door != Case01Door.StudyDoor) return TraverseV03(door, dest, r);

            switch (door)
            {
                case Case01Door.StudyDoor:
                    if (dest != Case01Room.Study && Session.Set(Case01Flags.RouteMismatchSeen))
                    {
                        Session.AddJournal("route_mismatch", Case01Texts.MismatchTitle, Case01Texts.Mismatch);
                        r.Toast = "已記入日誌：路徑與標記不符";
                    }
                    break;
                case Case01Door.MezzTriangle:
                case Case01Door.MezzCircle:
                    if (Has(Case01Flags.MissingLinkRestored))
                    {
                        if (!confirmedSinceSwap && !Has(Case01Flags.RouteAnchorSet))
                        {
                            confirmedSinceSwap = true;
                            Session.AddJournal("confirm_swap", "確認：門的去向被扭轉",
                                "補回 17 之後，" + Case01Texts.MarkSymbol(MarkOf(door)) + "門通往了" + Case01Texts.RoomName(dest) + "。");
                        }
                        if (door == Case01Door.MezzTriangle && Has(Case01Flags.RouteAnchorSet) && dest == Case01Room.Study && !triangleVerified)
                        {
                            triangleVerified = true;
                            Session.AddJournal("verify_triangle", "驗證：△→書房", "錨定後走過△門，確實到達書房。");
                        }
                        // 「另一路仍可變動」：錨定後、穩定前，○ 門每走一次就換一次去向。
                        if (door == Case01Door.MezzCircle && Has(Case01Flags.RouteAnchorSet) && !CoreStabilized)
                        {
                            circleFlip++;
                            routes[door] = circleFlip % 2 == 1 ? Case01Room.Dining : Case01Room.Entrance;
                        }
                    }
                    break;
                case Case01Door.Mezz17:
                    if (!door17Verified)
                    {
                        door17Verified = true;
                        Session.AddJournal("verify_17", "驗證：17 號門 → 玄關", "走過 17 號門，回到了玄關。");
                    }
                    break;
            }
            return r;
        }

        /// <summary>玩家從一個房間走到另一個房間（含傳送到達）。</summary>
        public Case01Response EnterRoom(Case01Room from, Case01Room to)
        {
            if (from == to) return Case01Response.None;
            var r = new Case01Response();
            if (from == Case01Room.Dining && Has(Case01Flags.TableTwo)) diningLeftAfterObserve = true;

            if (to == Case01Room.Dining && Has(Case01Flags.TableRewritten) && !rewriteAnnounced)
            {
                rewriteAnnounced = true;
                Session.AddJournal("table_rewritten", Case01Texts.TableRewrittenTitle, Case01Texts.TableRewrittenJournal);
                Session.AddJournal("radio_one_set", Case01Texts.Companion, Case01Texts.RadioOneSet);
                Session.Advance(Case01Stage.DiningContradiction);
                r.Radio = Case01Texts.RadioOneSet;
                r.Toast = "已記入日誌：餐具只剩一副（原始觀測保留）";
            }

            if (from == Case01Room.Study && armed && !wrongActive)
            {
                wrongActive = true;
                routes[Case01Door.StudyDoor] = Case01Room.Dining;
                Session.Advance(Case01Stage.WrongConnection);
                r.Toast = Case01Texts.ActivatedToast;
                r.WorldChanged = true;
            }

            if (to == Case01Room.Mezzanine && Session.Advance(Case01Stage.MezzanineDiscovered))
            {
                Session.AddJournal("mezzanine", Case01Texts.MezzTitle, Case01Texts.MezzJournal);
                r.Toast = "發現：不存在的夾層";
            }
            return r;
        }

        /// <summary>場景確認玩家看不到餐桌後呼叫：兩副餐具改寫成一副（只發生一次）。</summary>
        public bool ApplyDiningRewrite()
        {
            if (!DiningRewritePending) return false;
            Session.Set(Case01Flags.TableRewritten);
            return true;
        }

        // ---------------- 戰鬥 ----------------

        /// <summary>中庭戰鬥只能在異域反制之後、戰勝之前開始。</summary>
        public bool CanStartBattle { get { return DefenseTriggered && !Has(Case01Flags.BattleWon); } }

        public void OnBattleStarted()
        {
            Session.Set(Case01Flags.BattleStarted);
            Session.Advance(Case01Stage.Battle);
        }

        public Case01Response OnBattleWon()
        {
            if (!Session.Set(Case01Flags.BattleWon)) return Case01Response.None;
            Session.Advance(Case01Stage.PostBattle);
            Session.AddJournal("battle_won", Case01Texts.BattleWonTitle, Case01Texts.BattleWon);
            if (Correction == Case01CorrectionMode.RevisedV03) OnBattleWonV03();
            return new Case01Response { Toast = Case01Texts.BattleWonToast, WorldChanged = true };
        }

        public void OnBattleLost()
        {
            Session.AddJournal("battle_lost", Case01Texts.BattleLostTitle, Case01Texts.BattleLost);
        }

        // ---------------- QA 檢查點 ----------------

        /// <summary>美術 QA：建立房屋 H0～H3 的狀態（以正常流程的操作重播；H3＝核已局部穩定）。</summary>
        public static Case01Flow ForHouseStage(int h)
        {
            if (h <= 0) return new Case01Flow();
            if (h == 1)
            {
                var f1 = new Case01Flow();
                f1.Interact(Case01Ids.Table);
                f1.Interact(Case01Ids.Photo);
                f1.EnterRoom(Case01Room.Dining, Case01Room.Entrance);
                f1.ApplyDiningRewrite();
                f1.EnterRoom(Case01Room.Entrance, Case01Room.Dining);
                return f1;
            }
            if (h == 2) return ForCheckpoint(Case01Checkpoint.S30);
            var f = ForCheckpoint(Case01Checkpoint.S50);
            f.Interact(Case01Ids.Core);
            f.Traverse(Case01Door.MezzCircle);
            f.Interact(Case01Ids.Core);
            f.Choose(Case01Ids.ChoiceAnchor, 0);
            f.Traverse(Case01Door.MezzTriangle);
            f.Traverse(Case01Door.Mezz17);
            f.Interact(Case01Ids.Core);
            return f;
        }

        /// <summary>建立指定檢查點的狀態：以正常流程的同一組操作重播，旗標、日誌與通路都一致。</summary>
        public static Case01Flow ForCheckpoint(Case01Checkpoint cp)
        {
            return ForCheckpoint(cp, Case01CorrectionMode.LegacyV02);
        }

        public static Case01Flow ForCheckpoint(Case01Checkpoint cp, Case01CorrectionMode mode)
        {
            var f = new Case01Flow(mode);
            if (cp == Case01Checkpoint.S00) return f;
            f.Interact(Case01Ids.Table);
            f.Interact(Case01Ids.Photo);
            f.EnterRoom(Case01Room.Dining, Case01Room.Entrance);
            f.ApplyDiningRewrite();
            f.EnterRoom(Case01Room.Entrance, Case01Room.Dining);
            f.Interact(Case01Ids.Map);
            f.Interact(Case01Ids.EllieNotes);
            f.Interact(Case01Ids.OldRecord);
            f.EnterRoom(Case01Room.Study, Case01Room.Landing);
            if (cp == Case01Checkpoint.S30) return f;
            f.Traverse(Case01Door.StudyDoor);
            f.Choose(Case01Ids.ChoiceOverride, 0);
            f.Traverse(Case01Door.ThirdDoor);
            f.EnterRoom(Case01Room.Landing, Case01Room.Mezzanine);
            if (cp == Case01Checkpoint.S40) return f;
            f.Interact(Case01Ids.Core);
            f.OnBattleStarted();
            f.OnBattleWon();
            return f;
        }
    }
}

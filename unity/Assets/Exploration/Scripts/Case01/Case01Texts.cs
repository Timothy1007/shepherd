namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// 室內 QA 的玩家可見文字（B 版碎片化觀測，暫定文案，可依試玩修改）。
    /// 集中在這裡，不寫進 MonoBehaviour；開發者幕後真相不放進任何一句。
    /// </summary>
    public static class Case01Texts
    {
        public const string Companion = "同伴（通訊）";

        // ---------------- S00／S10 一樓 ----------------
        public const string TableTwoTitle = "餐桌：兩副餐具";
        public const string TableTwo = "桌上擺著兩人份的餐具。\n兩個盤子、兩副刀叉，其中一個杯子還沒用過。";
        public const string TableOne = "桌上只剩一副餐具。\n另一個位置的桌面比較乾淨，像是剛被收走──但椅子靠得很整齊。";
        public const string TableRewrittenTitle = "餐桌：再訪時只剩一副";
        public const string TableRewrittenJournal = "再回到餐廳時，桌上只剩一副餐具。\n原始觀測仍保留：第一次看到的是兩副。";
        public const string RadioOneSet = "我這裡的記錄只有一副。你確定剛才看見兩副？";

        public const string PhotoTitle = "合照";
        public const string Photo = "相框裡是兩個人並肩站著，臉模糊得認不出來。\n背面用鉛筆寫著：「週日。兩個人。」";
        public const string PhotoFaded = "照片裡第二個人影淡得只剩輪廓，但輪廓還在。\n案卷仍記著：這裡原本是兩個人。";

        public const string SundayNoteTitle = "紙條";
        public const string SundayNote = "紙條：「週日不用再帶那麼多菜了。」\n字跡很用力，最後一筆劃破了紙。";
        public const string Tea = "流理台上有兩個茶杯。一個洗過倒扣著，另一個裡面的茶早就涼了。";
        public const string Calendar = "牆上的日曆，每個週日都用紅筆圈起來。最近幾週的圈越畫越淡。";

        public const string FrontDoorBefore = "QA 出口：返回／重來。要回到玄關起點重新開始嗎？";
        public const string FrontDoorAfter = "門外的街道很安靜。要離開十七號嗎？";

        // ---------------- S20 二樓 ----------------
        public const string MapTitle = "街區舊地圖";
        public const string Map = "街區舊地圖。16 號與 18 號之間，有人用鉛筆補畫了一格，寫著「17」。\n官方版本裡沒有這一格。";
        public const string EllieTitle = "艾琳的手記";
        public const string Ellie =
            "「我在追的是失序本身，不是巧合。」\n" +
            "「又收到一份不知道誰寄來的資料，它說 17 號的記錄被刪過。」\n" +
            "最後一頁：「門框記號：△書房／○餐廳／□玄關。房子弄錯路的時候，在記號下寫『歸位』，門會短暫照記號走。」";
        public const string RecordTitle = "殘缺的校正記錄";
        public const string Record =
            "第一次校正的記錄，大半被塗黑。殘存一行：\n「……17 的參照被省略，以維持 16 與 18 相鄰……」\n旁邊是艾琳的筆跡：「缺口在樓梯之間。」";
        public const string Postcard = "床頭有一張沒寄出的明信片：「媽，下週日我晚點到。」";
        public const string Storage = "儲藏室的門鎖著。（QA：此房間不開放）";
        public const string ArmedToast = "樓梯平台那邊傳來木板擠壓的聲音……";
        public const string ArmedRadio = "你那邊的平面圖在跳動。樓梯平台附近，好像多了一個方向。";
        public const string ActivatedToast = "身後的門框輕輕一震。";

        // ---------------- S30 樓梯錯接 ----------------
        public const string StudyMarkPlain = "門框上有艾琳用粉筆畫的△。";
        public const string StudyMarkUnverified = "門框上的△還在，但門後飄來的是餐廳的味道。\n先親自走一次，確認這扇門現在通往哪裡。";
        public const string OverridePrompt = "門框上是△，門卻通往餐廳。\n要依照哪個記號寫下「歸位」？";
        public static readonly string[] OverrideOptions = { "△（書房）", "○（餐廳）", "□（玄關）" };
        public const string OverrideOk = "你在△下寫了「歸位」。門框裡的空氣換了味道──是書房的紙張味。\n同時，外牆上的第三扇門打開了。";
        public const string OverrideCircle = "你寫下○。門照著記號走，仍通往餐廳──這和門框上的△不符。\n可以再覆寫一次。";
        public const string OverrideSquare = "你寫下□。門現在通往玄關。安全，但和門框上的△不符。\n可以再覆寫一次。";
        public const string StudyMarkRestored = "△下面是你寫的「歸位」。";
        public const string StudyMarkDefense = "△下面是你寫的「歸位」，但門又被扭開了。修復核之前，這扇門不可靠。";
        public const string MismatchTitle = "路徑與標記不符";
        public const string Mismatch = "標著△（書房）的門，通往了一樓餐廳。";
        public const string RouteRestoredTitle = "樓梯關聯已重設";
        public const string RouteRestored = "依艾琳的記號，在書房門的△下寫了「歸位」，門回到書房。外牆上的第三扇門因此打開。";
        public const string ThirdDoorSealed = "外牆上浮現一道門框，沒有記號，推不開。\n像是在等其他的門先回到原位。";
        public const string ThirdDoorOpen = "第三扇門開著。門後是不該存在的空間。";

        // ---------------- S40／S50 夾層與核 ----------------
        public const string MezzTitle = "不存在的夾層";
        public const string MezzJournal = "樓梯平台外牆的門後，是一個平面圖上沒有的夾層。三個門框分別標著△、○，北牆上有被劃掉一半的「17」。";
        public const string CoreStart = "核懸在舊校正術式的斷口上。你伸手去碰斷掉的標記──\n整個夾層猛然翻轉。";
        public const string DefenseToast = "異域在反制：已經歸位的門又被扭開，舊規則正在回來。";
        public const string DefenseRadio = "所有房間的連結都在亂跳！你那邊有東西在成形──小心。";
        public const string CoreBattlePending = "核周圍的空間又一次翻轉──失序體還在中庭。";
        public const string BattleWonTitle = "失序體平息";
        public const string BattleWon = "中庭裡的失序體平息了。但核還沒有修復──戰勝不等於校正完成。";
        public const string BattleWonToast = "失序體平息了。裂口通回夾層，核還沒有修復。";
        public const string BattleLostTitle = "戰敗";
        public const string BattleLost = "在中庭被擊退。回到中庭入口，可以再試一次。";

        public const string CoreNeedEvidence = "要補上被省略的 17，需要能證明它存在的資料。\n還缺：";
        public const string CoreNeedMap = "街區舊地圖（二樓書房）";
        public const string CoreNeedPhoto = "合照（一樓餐廳）";
        public const string Step1 = "你照著舊地圖補上的那一格，把「17」寫回術式。\n北牆出現了一扇真正的門。\n但核同時把△和○兩扇門的去向扭在一起了。";
        public const string Step1Title = "17 的參照已補回";
        public const string Step1Journal = "依舊地圖與合照，把被省略的「17」寫回術式。夾層北牆出現 17 號門（□，通往玄關）。核把△／○兩扇門的去向互換了。";
        public const string Step2NeedConfirm = "△和○的去向被扭過了。先親自走進其中一扇，確認它現在通往哪裡。";
        public const string AnchorPrompt = "選一條連結作為錨點固定下來。另一條仍會變動。";
        public static readonly string[] AnchorOptions = { "把△門錨定到書房", "把△門錨定到餐廳", "把○門錨定到書房" };
        public const string AnchorOk = "錨點落下。△門固定通往書房；○門還在變動。\n走一次△門與 17 號門，確認兩條關係後再回到核。";
        public const string AnchorWrong = "錨點和記號衝突，空間把你推了出去。";
        public const string AnchorWrongTitle = "錨點錯誤";
        public const string AnchorWrongJournal = "錨點和艾琳的記號衝突，被推回餐廳。△屬於書房。";
        public const string AnchorTitle = "錨點：△→ 書房";
        public const string Step3NeedVerify = "兩條關係還沒驗證完。還沒走過：";
        public const string Step3NeedTriangle = "錨定後的△門";
        public const string Step3NeedDoor17 = "17 號門";
        public const string Step3 = "你沿著兩條已驗證的關係，補上第一次校正留下的缺口。\n重疊的房間輪廓一格格退回原位，燈光不再閃爍。\n房子安靜下來──但只是暫時的。";
        public const string Step3Title = "核：局部穩定";
        public const string Step3Journal = "補上第一次校正的缺口。十七號暫時穩定；街區沒有永久復原，艾琳的下落仍然不明。";
        public const string Step3Toast = "核已暫時穩定。從 17 號門回到玄關。";
        public const string CoreStable = "核安靜地懸著。房屋暫時穩定。";
        public const string MezzTriangleMark = "門框上的△。";
        public const string MezzCircleMark = "門框上的○。";
        public const string Wall17Before = "北牆上用粉筆寫著□和「17」，被劃掉了一半。";
        public const string Wall17After = "17 號門（□）。通往玄關。";
        public const string ConfirmedPrefix = "上次走過：通往";
        public const string NotConfirmed = "還沒走過。";

        public const string EvidenceTitle = "已歸檔：十七號的合照";
        public const string Evidence = "合照已收入案卷。照片裡的第二個人影只剩輪廓，但案卷保留了「原本有兩人」的觀測。";
        public const string EndTitle = "〈第十七號門〉室內 QA 結束";
        public const string EndBody = "房屋：暫時穩定\n艾琳：下落未明\n街區沒有永久復原。";

        public static string RoomName(Case01Room r)
        {
            switch (r)
            {
                case Case01Room.Entrance: return "玄關";
                case Case01Room.Kitchen: return "廚房";
                case Case01Room.Living: return "客廳";
                case Case01Room.Dining: return "餐廳";
                case Case01Room.Landing: return "樓梯平台";
                case Case01Room.Study: return "書房";
                case Case01Room.Bedroom: return "臥室";
                case Case01Room.Storage: return "儲藏室";
                case Case01Room.Mezzanine: return "夾層";
                case Case01Room.Courtyard: return "異常中庭";
                default: return "";
            }
        }

        public static string MarkSymbol(Case01Mark m)
        {
            switch (m)
            {
                case Case01Mark.Triangle: return "△";
                case Case01Mark.Circle: return "○";
                case Case01Mark.Square: return "□";
                default: return "";
            }
        }

        public static string StageName(Case01Stage s)
        {
            switch (s)
            {
                case Case01Stage.EnteredHome: return "S00 進入家屋";
                case Case01Stage.ObservedDining: return "S00 觀察餐廳";
                case Case01Stage.DiningContradiction: return "S10 餐具矛盾";
                case Case01Stage.StudyDiscovery: return "S20 書房調查";
                case Case01Stage.WrongConnection: return "S30 樓梯錯接";
                case Case01Stage.MezzanineDiscovered: return "S30 發現夾層";
                case Case01Stage.AnomalyDefense: return "S40 異域反制";
                case Case01Stage.Battle: return "S40 戰鬥";
                case Case01Stage.PostBattle: return "S40 戰後";
                case Case01Stage.LinkRestoration: return "S50 連結修復";
                case Case01Stage.CoreStabilized: return "S50 核穩定";
                default: return "S60 結束";
            }
        }
    }
}

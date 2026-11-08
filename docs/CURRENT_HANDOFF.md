# 2026-11-08｜Case01 v0.3 實作進度（本段最新）
**A. 材質／燈光（已實作、已測）**：管線確認為 **Built-in Render Pipeline、Gamma 色彩空間、無 URP／後處理**。新增 `unity/Assets/Exploration/Case01Art/`：`Shaders/Case01Triplanar.shader`（Built-in Standard 光照的世界座標 triplanar Surface Shader，解決 Primitive UV 拉伸）、`Textures/`（22 張程序生成 PNG：木地板、家具木紋、灰泥、壁紙、漆面木作、織物、紙、書布、紙箱、石磚、磚牆、暗灰泥、裂隙、核脈絡、釉面，含 8 張法線貼圖）、`Materials/`（39 個 `M_*.mat`）、`Resources/Case01ArtSet.asset`（執行時載入）。重建：選單 `Shepherd/Case01/重建美術素材` 或 `-executeMethod Shepherd.Exploration.Editor.Case01ArtGenerator.Generate`（全部自製、無第三方授權）。程式：`Case01Palette`（共用資產＋本次建置擁有的衍生材質，F1／跳轉時釋放；核與裂隙線用獨立副本，不改共用資產）、`Case01ArtSet`。房屋加踢腳線、門框（第三扇門除外）、窗框／窗櫺／窗台、一樓灰泥天花板、各房吊燈（燈具與光源位置一致）、閱讀燈／檯燈／壁燈補光；環境光改三色（Trilight），離開時還原。燈光依 H 階段調整：H1 餐廳光色偏移並慢慢起伏、客廳略暗；H2 書房轉冷變暗（線索仍可讀）、樓梯平台低頻偏紫；H3 回到基準暖光（殘留物件不變）。`Case01HouseQA` Inspector 新增「降低閃爍」「驚嚇強度」「即時陰影」。只有餐廳、客廳主燈有柔和陰影。
**測試（Stage A）**：EditMode 212/212（新增 `Case01ArtTests` 6 項）、PlayMode 4/4；Unity 2022.3.18f1 batchmode，無編譯錯誤或警告。


---

# 2026-11-08｜Case01 公開 Demo 首戰：正式戰鬥 UI＋情境式教學
使用者確認：現有戰鬥規則基本清楚，但目前 3D Combat Lab HUD 太像測試版；公開 Demo 的戰鬥必須是核心玩法與新手教學，**允許大幅重做戰鬥介面呈現，但嚴禁私改規則與卡牌數值**。
新增 Claude Code 實作任務：`docs/story/case01_seventeenth_door/CASE01_COMBAT_DEMO_UI_TUTORIAL_CLAUDE_TASK_V0_3.md`。已讀當前 `unity/Assets/Battle/UI/Arena/CombatArenaView.cs`、`CombatArenaView.Hud.cs`、`docs/UNITY_BATTLE_V0.1_SPEC.md`：目前 7 手牌／3 AP／三學派／序列／承接／三連／真實預估及敵方預告已存在。工作為 Case01 專屬正式 HUD、首戰四節點非強制教學、術式演出、獨立兩敵 QA 模式。保持既有 CombatLab3D 與 AnomalyStreetDemo、BattleController / RoundResolver、Case01 住宅與核修復流程不變。
**本輪只提交文件，未改 Unity 程式或執行測試，也未呼叫 Claude Code 實作**。其任務與同日的材質光影、失序體形成及核修復 v0.3 文檔並列，Claude 須拆成可獨立測試與提交的增量，避免同時碰同檔覆蓋。


---

# 2026-11-08｜Case01 真人試玩後 v0.3 開發任務
使用者已實際試玩室內 v0.2：音效關閉後驚嚇感大幅下降；一般調查仍可保留文字與短註解、僅 FP-001 做旋轉檢視；房屋需要正式材質／貼圖／燈光精修；失序體希望由夾層的書籍／照片／紙張凝聚成形，配特殊音效／短音樂，但**暫不追逐玩家**。核修復目前存在四問題：簡單、跑腿、成果回饋弱、因果不易理解。
本輪已新增並同步 **兩份供 Claude Code 實作的 v0.3 任務規格**：
- `docs/story/case01_seventeenth_door/CASE01_ART_PASS_AND_MANIFESTATION_CLAUDE_TASK_V0_3.md`：先做 Case01 材質光影 Art Pass，再做失序體凝聚動畫／音效；保留溫馨家居→異變侵蝕→局部修復的階段性美術；不更改戰鬥、房屋布局、現有修核規則。
- `docs/story/case01_seventeenth_door/CASE01_CORE_CORRECTION_REDESIGN_V0_3.md`：新核校正規則「比對矛盾→固定 △→書房→修正第二關係／17→玄關→一次穿門提交→可見空間復位」，先以獨立 v0.3 QA 模式提供與 v0.2 A/B 比較，**不得直接取代已運作的 v0.2**。目標是減少跑腿並提升推理與回饋。
**執行順序**：材質光照 → 形成演出；核修復 v0.3 另獨立模式實作與測試。兩份規格內的效果是**待 Claude Code 實裝**，不是本次已完成的 Unity 修改；本次僅提交 docs，未改 .cs / .unity / assets，未執行 Unity 測試，也未直接啟動 Claude Code。Claude 請讀取兩份文件、現有 main 實作及文內 QA/保護範圍，再進行測試與提交。主線 / 戰鬥 / 街區事務所本輪不改。

---

# 2026-11-08｜Case01 室內 v0.2（生活細節／H0～H3 異變／FP-001）已實裝（本段最新）
依 `CASE01_ENVIRONMENT_EXPANSION_CLAUDE_TASK_V0_2.md` 修改現有 `Case01HouseInteriorQA.unity`（入口、選單、F1～F4 不變）。**未改**：牆／門／樓梯／portal／出生點／戰鬥區／鏡頭、△○□ 記號與錨點、核三段修復與其提示文案、既有擠壓音效與觸發點、AnomalyStreetDemo、CombatLab3D、戰鬥 Core。
- **生活細節**（新檔 `Scripts/Case01/Case01HouseDecor.cs`，程式生成，圓柱／球／膠囊／薄片混用）：玄關鑰匙盤、傘架與傘、掛鉤外套、地墊；廚房調味罐、碗盤架、**茶葉盒（E 真的打開／蓋上）**、小圓桌與凳子；客廳西牆書架（數十本不同顏色高度的書）、北牆相框、閱讀燈、毛毯、植物、衣架、茶几雜物；餐廳布巾、餐墊、托盤與兩人份茶具、小櫃子；臥室床頭燈（暖光）、衣物、地上的書、衣櫃、小書桌；書房書架上的書、紅色舊冊、三個分色標籤檔案盒、文件夾、修復工具、檯燈、舊照片、地毯；樓梯平台地毯、壁燈、置物架與花瓶、掛畫、紙箱。
- **新增可調查**（短觀察顯示在準星下方，不開側邊面板；有意義的寫入日誌）：書中便箋、朋友卡片、購物清單、茶葉盒、旅行路線（艾琳的個人願望）、工作邀請信（延後工作、主動查十七號）、修復工具、檔案盒。原有主線熱區全部保留。
- **H0～H3**（玩家不在該房間時才切換，不當面硬閃；F2～F4 重建時直接套用；回訪寫入日誌並保留原始觀察；不另播驚嚇音）：H1（餐具改寫後）客廳書架最上層少兩本、相框變空白；餐廳布巾消失、北側椅子被推開、托盤只剩一個茶杯。H2（樓梯錯接後）書房書架兩端出現一模一樣的紅色舊冊、檔案盒重排；平台多兩個紙箱、掛畫歪斜下滑。H3（核穩定後）書、布巾、椅子、檔案盒、紙箱、掛畫歸位；**殘留**：客廳相框仍空白、托盤仍一個茶杯、餐桌仍一副、合照第二人影仍淡。QA 面板顯示「房屋 H0～H3」。
- **FP-001 `case01.family_photo`**：只在 `core_stabilized` 後出現在夾層（西側台座）。準星＋E 拾取 → 實物從場景移除、加入本次 QA 證物收藏與日誌 → 3D 檢視（移動滑鼠旋轉、滾輪縮放、E／Esc 收起；背面有真的鉛筆字與撕角）；之後按 I 可再檢視，J 日誌頂端列出收藏。文字明確區分「餐廳原件（前期調查，留在原處）」與「夾層的異常實體殘留」；不宣告艾琳生死或組織真相。可選：不拿也能從前門結束（出口提示會提醒）。**保存範圍：只在本次 Play 的 Session；F1 重開或重啟 Unity 後清空，尚無跨案件存檔。**
- **測試（clean copy＝git HEAD＋本次修改，Unity 2022.3.18f1 batchmode）**：EditMode 206/206（新增 `Case01EnvironmentTests` 10 項：各房物件數與形狀、新調查點可走到、茶葉盒開關、H0／H1／H2／H3 物件 active／位置、離開房間才切換、F2～F4 一致、FP-001 只拿一次／檢視／I 再看／F1 重置可再取得、不拿也能結束；原 Case01 22 項與 AnomalyStreetDemo、CombatLab3D 等既有測試同時通過）；PlayMode 4/4。1920×1080、1280×720 截圖已檢查。
- **未完成**：未經真人鍵鼠驗收（尤其 3D 檢視的滑鼠旋轉手感）；只有 FP-001 一件可拿，校正編號牌 `case01.correction_tag` 未做；H2 沒有另做新音效；物件仍是 Primitive 組合（比 v0.1 豐富但非正式美術）。

---

# 2026-11-08｜Case01 試玩回饋：住宅細節與異變 v0.2 已下達開發任務
**使用者已要求實裝，交給 Claude Code 按規格修改現有可玩室內 QA**。本輪已建立開發任務文檔 `docs/story/case01_seventeenth_door/CASE01_ENVIRONMENT_EXPANSION_CLAUDE_TASK_V0_2.md`。
**本次限定修改**：不擴建房間、不改 △／○／□ 的二樓記號、錨點與三段核修復；增加一二樓低面數生活家具／可調查物件、H0～H3 階段性可見環境差異（靜默變化和既有音效共同存在）、修復核後在不存在的夾層顯現並可拾取／於 QA 中回看 `FP-001` 家庭合照的異常實體證物。1～2 件 3D 拿起旋轉檢視屬次優先，不得阻擋主要增量。核修復後應允許玩家巡查並拾取證物，再選擇結束，不讓 EndPanel 提前鎖住。
**已核對現有工程**：`Case01HouseBuilder.cs` 程式化生成住宅、`Case01HouseQA.cs` 控制探索與戰鬥、`Case01Hud.cs` 管理日誌與調查 UI。先沿用這套實作增量，不能換掉整個系統。
**交付狀態**：本回合 GitHub **僅新增 Claude Code 開發規格與交接文件**，**沒有直接觸發 Claude 執行，也沒有改 Unity .cs / Scene 或執行 Unity 測試**。不能因此稱遊戲已完成 v0.2。Claude Code 必須按任務書在自己的環境實際修改、測試、提交 main，之後才算實裝。
**QA 既有入口**：`unity/Assets/Exploration/Scenes/Case01HouseInteriorQA.unity`／Unity 菜單 `Shepherd/Case01/開啟室內 QA 場景`；F1 原始玄關、F2 樓梯錯接、F3 夾層、F4 戰後修復。此入口尚未包含 v0.2 變更。
**GitHub**：`Timothy1007/shepherd`／`main`；本地同步 `git pull origin main`。舊 Web 的 `npm.cmd run build`、`npm.cmd run preview` 不啟動 Unity。

---

# 2026-11-07｜Case01 室內 Graybox QA v0.1 已實作（本段最新）
依 `docs/story/case01_seventeenth_door/CASE01_INTERIOR_GRAYBOX_CLAUDE_TASK_V0_1.md` 建立**可玩**的獨立 QA 場景（Claude Code 實作，已在本機 Unity 2022.3.18f1 batchmode 測試）。
- **Scene**：`unity/Assets/Exploration/Scenes/Case01HouseInteriorQA.unity`（只含一個 `Case01HouseQA` 元件，房屋、玩家、HUD 於 Play 時以程式建立；未加入 Build Settings）。選單：`Shepherd/Case01/開啟室內 QA 場景`、`重新產生室內 QA 場景`、`截圖檢查`。
- **流程（S00～S60 全段可玩）**：玄關起點 → 餐廳兩副餐具／合照 → 離開後在玩家看不到餐桌時改寫成一副，再訪時同伴通訊、日誌保留「原本兩副」→ 二樓書房（舊地圖、艾琳手記：主動研究失序＋不明來歷資料、殘缺校正記錄）→ 離開書房後書房門錯接到一樓餐廳（親自走過才記錄「路徑與標記不符」）→ 在門框 △ 下覆寫「歸位」（○／□ 為可恢復的錯誤）→ 外牆第三扇門通往不存在的夾層 → 碰核觸發一次異域反制（已歸位的門重新連錯、燈光轉紅）→ 被拉進異常中庭，走近失序體才進入**既有 CombatArenaView 嵌入式戰鬥**（現有兩敵、3 AP／7 手牌、規則與數值未改）→ 勝利回到遭遇前位置，`battle_won` 只設一次、敵人不重生；戰敗「返回檢查點重試」→ 經裂口回夾層，核三段修復：①依舊地圖＋合照補回 17（北牆出現可走的 17 號門）②核把 △／○ 去向互換，須親自走過確認後依記號選錨點（錯誤錨點送回餐廳可重試）③走過錨定後的 △ 門與 17 號門才可補缺口 → `core_stabilized`、`house_state=temporarily_stable`、房間輪廓復位、燈光轉暖、前門變成出口 → 合照最後改變、證物歸檔 → 結束畫面「房屋暫時穩定／艾琳下落未明」。
- **操作**：WASD 移動、滑鼠視角、Shift 奔跑、準星＋E 調查／互動（面板開著時對同一物件按 E 關閉）、1–3 選擇、J 日誌、Esc 釋放滑鼠；QA 跳轉 F1 S00 玄關／F2 S30 樓梯錯接／F3 S40 夾層（反制前）／F4 S50 戰後修復；前門在修復前是「QA 返回／重來」。
- **程式**：`unity/Assets/Exploration/Scripts/Case01/`（`Case01Flow` 純邏輯規則與門連結映射、`Case01Session` 最小記錄器與 13 個旗標、`Case01Texts` 暫定文案、`Case01HouseBuilder` Graybox 空間、`Case01HouseQA` 第一人稱／互動／傳送／戰鬥銜接、`Case01Hud`）。沿用既有 `FirstPersonController`、`ExploreHud`、`EncounterZone`、`ArenaArt`、`CombatArenaView`（只用已存在的公開接口）；**未修改** AnomalyStreetDemo、CombatLab3D、戰鬥 Core、卡牌數值、golden cases、SampleScene、Build Settings。戰鬥中的玩家沿用原本的 Primitive 替身。
- **測試**：EditMode 196/196（新增 Case01FlowTests 12 項、Case01HouseQATests 10 項，含用真實移動輸入從玄關走到結束的完整流程、戰敗重試、F1～F4 跳轉、隔牆不可調查、場景檔無 Missing Script、舊場景仍指向原腳本）；PlayMode 4/4（新增實際載入場景走進餐廳調查、F3 → 碰核 → 中庭 → 既有戰鬥出牌）。1920×1080 與 1280×720 截圖已檢查 E 提示、字幕、日誌、選項、QA 面板可讀。
- **尚未完成／妥協**：沒有在有視窗的 Unity 中用實體鍵盤滑鼠手動遊玩（batchmode 只能用腳本輸入；滑鼠鎖定需手動確認）；文案、核外觀、三段修復、房屋格局都是暫定，需試玩調整；聲音提示只有一段程式產生的低頻擠壓聲；沒有事務所／街區銜接、完整存檔、母親或同伴對話系統、跨案件證物保存；中庭以外的房間（廚房、客廳、臥室）只有少量可選調查；戰鬥 HUD 仍顯示 Combat Lab 的「重置戰鬥」按鈕（沿用既有 HUD）。
- **注意**：本機另有未提交的「Tripo 主角模型放進 AnomalyStreetDemo」修改與 `art-source/`，**不在本次提交內**。

---

# 2026-11-07｜已交付 Claude Code 的室內 Graybox 實作腳本
使用者希望**先讓 Claude 做可玩原型，後續再依遊玩體驗調整**。本次已在 GitHub `main` 新增：
`docs/story/case01_seventeenth_door/CASE01_INTERIOR_GRAYBOX_CLAUDE_TASK_V0_1.md`
這是**Claude Code 可直接執行的開發任務書**，不是遊戲的完成品。讀取它與 `CASE01_AUTHOR_BIBLE_V0_5.md`、`docs/UNITY_BATTLE_V0.1_SPEC.md`、`CASE01_IMPLEMENTATION.md`，先盤點現有 Unity 代碼再實作。
**首個遊玩切片**：室內一樓玄關／客廳／餐廳／廚房、二樓臥室／書房／樓梯，餐桌兩副變一副、女兒研究線索、樓梯錯接、暫定不存在的夾層核介入點、空間覆寫／錨定、異域反制、既有第三人稱卡牌戰鬥、戰後非戰鬥式核校正、固定局部穩定結尾。QA 須獨立 Scene，可重置／跳階段，不破壞 `AnomalyStreetDemo`、`CombatLab3D` 或卡牌數值。
**狀態**：GitHub 已新增的是文檔；目前**尚未建立可玩住宅 Scene、執行 Unity QA 或提供遊戲測試網址**。Claude 完工後須依任務書 commit/push main、逐項回報實際測試與未完成處。
**界線**：兩層樓與不存在的夾層、三段式修復、核視覺為 **可改的關卡假設**，非「所有異域核皆如此」的宇宙設定；艾琳生死、首次校正的秘密、人工失序組織內容仍未定案。
本地 `git pull origin main`。舊 Web 的 `npm.cmd run build`／`npm.cmd run preview` 不是 Unity 啟動命令。

---

# CURRENT HANDOFF｜2026-11-07 Case01 A v0.5 審核完成（本段最新）
**已同步至 GitHub main 的劇情文件**：新增 `docs/story/case01_seventeenth_door/CASE01_AUTHOR_BIBLE_V0_5.md`，並更新 CASE01 STORY／LEVEL_DESIGN／DIALOGUES／IMPLEMENTATION、STORY_BIBLE、MAIN_ARC、FORESHADOWING_TRACKER。請先讀**新 A 版**再看歷史 v0.3／v0.2／v0.1。
**使用者七項明確決策**：①母女既有真實傷害也有溫暖；②女兒艾琳**為研究異域而主動深入**，可能受未決的引導者／記錄吸引，非僅為鄰居犧牲；③**第一次校正涉及被隱瞞的決策**，後案揭露；④失序體是異域維持核所支撐的異常秩序時出現的具象化干預反應；⑤第一案依線索**修復舊校正術式**；⑥**固定主結局**，選擇影響對話與證物；⑦情感收束，世界觀留謎。
**世界觀方向**：失序 → 核逐漸凝結／維持異常狀態 → 異域形成相對穩定異常秩序 → 玩家威脅其穩定時失序體可能干預。這是設計框架，不是所有細節均定理；不必每次察覺異常即開戰，核不必有意識。第一人稱不同認知可透過同伴對話和少量現場物件差異呈現，不用做多組 POV。
**重要的候選而非已確認設定**：本案核可能維持「16 與 18 相鄰卻仍存 17 的殘留關聯」；玩家補完舊術式對應以穩定街道。**這個具體核解法還需使用者審核**。艾琳生死／去處、引導者身分、第一次校正的被隱瞞決策內容、母親保留記憶的原因、正式人物名字、角色台詞均未定。
**玩家故事形式**：A 版供開發者掌握因果；實機 B 版透過環境、短對話／紀錄、可操作證據和矛盾比對呈現，不做長篇灌輸。
**程式狀態**：本輪僅 docs，未改 Unity、C#、既有戰鬥規則、QA 場景、全域 UI，也未執行 Unity EditMode／PlayMode、build 或網頁 preview；沒有新的測試網址。接下來先確認候選核與術式修復的因果，再製作 B 版玩家可見片段與獨立 QA。
**本地更新**：`git pull origin main`；舊 Web 專案如需驗證可用 `npm.cmd run build`、`npm.cmd run preview`，**不是 Unity 遊戲啟動指令**。
---

# 2026-11-06｜Case01 v0.3 劇情方向已同步（本段最新）
**已確認的創作方向**：〈第十七號門〉以「母親尋找逐漸被遺忘的女兒」展開，週日晚餐／兩副餐具、房屋顯現、進入室內隨調查逐步改寫、接近真相才發生戰鬥、處理異域後帶回可跨案件回看的證物。候選副標題為《被遺忘的晚餐》。案件敘事可融合懸疑、驚悚、溫柔與冒險；人性的細節比為反轉而反轉重要。
**玩家命運干預原則**：可以提供選擇，讓玩家決定如何介入、調查、保存證物或對外說明，但**有些遺憾無法彌補，選擇不必都能救回所有人**。具體選項與分支、女兒結局、過去是否有人主動選擇、核／殘留校正原因仍需設計，**不可將提案當已定案的真相**。
**最新設計入口**：`docs/story/case01_seventeenth_door/CASE01_STORY.md` 最上方 v0.3。其他檔案以其最新標題更新段為準；下方 v0.2、v0.1 是沿革紀錄。
**下階段立刻要做**：母女兩人角色設計——外在與私下個性、母女互動與矛盾、生活細節、談吐、屋內可以被玩家觀察到的痕跡；之後才建立秘密真相時間線並更新對話與 Unity 事件。
**程式狀態**：本輪只改 `docs/`；沒有執行 Unity、建事務所／屋內場景、修改 C#、戰鬥、模型或 UI。舊 `AnomalyStreetDemo` 仍是可玩 QA 場景；Case01 新 Scene 與可執行測試網址尚不存在。
**開發分支**：`Timothy1007/shepherd` / `main`。先讀 `docs/DEVELOPMENT_HANDOFF_2026-10-20.md`（舊 Web 交接）、`docs/UNITY_BATTLE_V0.1_SPEC.md`（現行戰鬥）與目前故事文件。不得以舊段落覆蓋最新狀態。
**本地更新**：`git pull origin main`；`npm.cmd run build`、`npm.cmd run preview` 僅舊 Web 工程，非 Unity 執行方式。

---


# 2026-11-06 最新更新｜跨案件敘事與 Case01 v0.2（本段優先）
**本次已同步 GitHub main：**新增 `docs/story/MAIN_ARC.md`、`docs/story/FORESHADOWING_TRACKER.md`；修改 STORY_BIBLE 及 Case01 STORY、LEVEL_DESIGN、DIALOGUES、IMPLEMENTATION 的優先級說明。原 v0.1 內容保留在文件後段作歷史參考，不再直接作為正式故事的實作依據。

**確認的方向**：
1. 每個案件可混合懸疑／驚悚／情感／都市冒險；每案有階段性結局，但可以留跨案件回聲。
2. 事務所將保存少量可跨案檢視的異域遺留證物，未來可能改變對話／線索或增補支線；主線不能因漏拿隱藏物卡死。
3. 每個伏筆需要編號、來源、當下呈現、可能回應位置及答案狀態；未決伏筆**禁止**寫成已有最終真相。

**Case01 最新候選體驗**：事務所接母親尋女委託 → 街道調查 → 17 號樓逐漸顯現 → 進入小型 Graybox 室內探索與逐步異變 → 接近真相後失序體顯現／戰鬥 → 獨立處理異域 → 留一件跨案件證物並返事務所。**女兒真相、她是否犧牲、失序體本體與核解除條件尚未定案**。候選名『失像者／空框者』僅外觀提案，不是正式敵人分類。照片或鑰匙二選一的收集品也未定案。

**尚未實作**：事務所、房屋室內、任務／對話／證物系統及這個案件的完整遊玩流程。本輪只提交 `docs/`，沒有執行 Unity EditMode／PlayMode 或截圖 QA；舊可玩街區仍應保留。

**下一步**：先完成 Case01 室內關鍵線索、失序動機、核之處理、結局的因果設計；再同步重寫對話節點與完整實作 state machine。Claude 可先做獨立通用互動 QA，但不可依 v0.1 的『街口開戰→拾鑰匙』直接做新版主線。

**本地同步**：`git pull origin main`；`npm.cmd run build`／`npm.cmd run preview` 是舊 Web 測試指令，非 Unity 遊戲運行方式。

---

## 以下為先前交接記錄（仍保留，但受本段新決策覆蓋）

# 《牧羊人：荒野之歌》CURRENT HANDOFF
更新日期：2026-11-06｜正式 Repo：`Timothy1007/shepherd`｜Branch：`main`

> **每次新對話先讀這份文件。** 它記錄最新跨對話進度與設計決策；有衝突時依最新已確認的專項規格，不以舊桌遊交接推翻 Unity 戰鬥。
> 此份內容截至本次 GitHub 設計文件提交；**本回合只新增文件，不代表已在 Unity 實作主線。**

## 1. 本輪完成
- 新增 `docs/story/STORY_BIBLE.md`：已確認世界觀與候選設定分層。
- 新增 `docs/story/case01_seventeenth_door/CASE01_STORY.md`：〈第十七號門〉案件完整節點草案。
- 新增 `docs/story/case01_seventeenth_door/CASE01_LEVEL_DESIGN.md`：場景、線索、折返、戰鬥、核、結案與 QA 條件。
- 新增 `docs/story/case01_seventeenth_door/CASE01_DIALOGUES.md`：逐段對話／調查文字 ID 初稿。
- 新增 `docs/story/case01_seventeenth_door/CASE01_IMPLEMENTATION.md`：供 Claude Code 的架構邊界與分期實作要求。
- 本輪未修改 C#、Unity Scene、模型、卡牌效果、測試資料或正式遊戲 UI。

## 2. 目前已確認並應沿用的產品方向
- 正式專案在 `main`；`v2` 舊測試版不得覆蓋 `main`。
- Unity 2022.3.18f1；3D 異域探索採**第一人稱**；卡牌戰鬥採**第三人稱戰術鏡頭**；戰鬥後返回探索。
- 現有可玩場景：`unity/Assets/Exploration/Scenes/AnomalyStreetDemo.unity`。現有 `CombatLab3D` 保留為戰鬥 QA。
- 整體仍先使用 Graybox 及舊角色佔位，風格化半寫實的 Tripo 主角模型保留作將來替換，不優先精修，不改場景美術風格。
- 戰鬥依 `docs/UNITY_BATTLE_V0.1_SPEC.md`：三元素燃素／基體／擬合劑、暫稱三學派實在論／過程哲學／關係論、每回合可換學派、3 AP、7 手牌、27 張試玩牌庫、承接與三連。數值目前不因單次試玩感受而改。
- 事務所是主角的家與未來同伴據點；初版 Demo 從**主角已在事務所**開始，不立即製作加入事務所的前傳。
- 第一案件先用同一個 Graybox 街區做調查、遭遇、核處理，不需要新建完整城市。

## 3. 尚未定案，請勿當作事實
- 「第十七號門」名稱、鑰匙作為核的載體、街道折返、三線索、結尾筆記：**新提案，待使用者逐項審核**。
- 失序起源、牧羊人制度、主角加入事務所前史、同伴姓名與背景、正式城市／組織名。
- 核的普遍定義、是否必有物理邊界、失序體真實本質、官方說法的真偽。
- 三學派正式名稱（曾討論候選：原律／恆續／共軛；正典／恆續／繫律；歸正／迭代／轉導）。
- 不要將舊版『耕耘／守望／收割』或『羊／糧／錢』誤認為目前 Unity 規則。

## 4. 已有技術實作 vs 尚未實作
| 項目 | 狀態 |
| --- | --- |
| Unity v0.1 戰鬥 Core、卡牌及預覽 | 既有實作（依 GitHub main 及先前測試） |
| CombatLab3D 戰鬥場景 | 既有測試場景 |
| AnomalyStreetDemo 第一人稱街區探索與戰鬥銜接 | 既有測試場景 |
| Tripo 外部角色的測試匯入 | 本機 Claude Code 曾回報可用；**本輪未重新驗證是否已同步至 main** |
| 事務所可玩 Scene | 未實作 |
| 跨 Scene 任務進度／互動／對話／調查手冊 | 尚未實作（本輪僅設計文件） |
| 第十七號門事件／核／序章入口 | 尚未實作（本輪僅設計文件） |
| 完整 Demo 的 PlayMode 與實機測試 | 尚未開始 |

## 5. 下一個開發階段
1. **先由使用者審核** Case01 故事與核、邊界、搭檔、結尾伏筆；修正文案後標記已採用。
2. 交由 Claude Code 依 `CASE01_IMPLEMENTATION.md` 的 Phase A 建立獨立小型 QA：第一人稱互動、委託、通訊／對話、線索、跨 Scene 旗標；不得直接覆寫既有可玩場景。
3. QA 通過後進行 Phase B：灰盒事務所 → 街區案件 → 現有戰鬥 → 核 → 返回事務所。
4. 最後 Phase C：文本／操作／鏡頭／特效與試玩打磨。
5. 每階段更新本文件與相關故事設計；實際測試／提交狀態須以工具結果為準。

## 6. 對新對話的開場指令
「請閱讀 `docs/CURRENT_HANDOFF.md`、`docs/story/STORY_BIBLE.md`、`docs/story/case01_seventeenth_door/` 全部文件和 `docs/UNITY_BATTLE_V0.1_SPEC.md`，先告訴我哪些已定案、哪些待審、Unity 已實作到哪。不要修改程式，除非我本回合要求修改。專案 `Timothy1007/shepherd` 的 `main` 是唯一正式來源。接著繼續我們的第一案件設計／實作。」

## 7. 測試與本機命令備忘
- 本輪**文件變更，不需 npm 或 Unity build**，所以不應宣稱跑過 PlayMode 或遊戲畫面。
- GitHub 最新提交 SHA：以本輪 GitHub create_file 工具最後返回的 commit 為準；不要將本文件硬寫一個會過時的 SHA。
- 新增故事的 QA 場景／遊玩入口目前**不存在**，測試場景仍是 `unity/Assets/Exploration/Scenes/AnomalyStreetDemo.unity`。
- 本地更新：`git pull origin main`。舊網頁專案需要時再用 `npm.cmd run build`／`npm.cmd run preview`；這兩條**不是 Unity 場景的啟動指令**。

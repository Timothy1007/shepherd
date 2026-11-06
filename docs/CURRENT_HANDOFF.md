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

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

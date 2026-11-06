# Case01 v0.2｜實作規格狀態告示
2026-11-06｜**下方 v0.1 只可供通用 QA 系統參考，不能按舊狀態圖直接實裝新版故事。**

已確認新增的需求方向：房屋顯現、室內 Graybox 調查、場景依線索變化、失序體於關鍵事件後顯現、返回事務所可回看遺留證物，以及跨案件線索可演進的資料結構。具體核解除方式、失序體分類、文案及各事件旗標仍待劇情審核。

Claude Code 開工順序：
1. 閱讀 `docs/CURRENT_HANDOFF.md`、`docs/story/MAIN_ARC.md`、`docs/story/FORESHADOWING_TRACKER.md`、Case01 STORY v0.2、LEVEL_DESIGN v0.2。
2. 可以先在**獨立 QA Scene**實作通用對話、物件互動、證物收藏記錄與跨場景旗標。
3. **不可**只依 v0.1 完成整章；室內故事、結局、核機制正式化前，不能將其視為完成品。
4. 不動既有 `AnomalyStreetDemo`、`CombatLab3D`、戰鬥 Core、正式主角佔位與全域 UI。

---

## 以下為 v0.1 歷史實作規格（僅可擇適用部分）

# Case 01｜Unity 實作交付規格 v0.1
2026-11-06｜**規格草案，尚未授權一次性完成整章；本輪只建文件、不動 Unity**

## 一、目標
製作一段可連續玩的序章：事務所 → 接委託 → Scene 切換至街區 → 三個線索 → 折返／失序顯現 → 一組既有戰鬥 → 鑰匙與門校正 → 返回事務所 → Demo 結束。

每段劇情必須能由第一人稱玩家動作推進，而不是純自動播放影片。維持現有 Graybox 風格，舊佔位主角仍作測試標準。

## 二、開工前必讀
- `docs/DEVELOPMENT_HANDOFF_2026-10-20.md`：**舊 V1 Web 交接歷史**，不得用其規則覆蓋 Unity v0.1。
- `docs/UNITY_BATTLE_V0.1_SPEC.md`：目前戰鬥規格。
- `docs/story/STORY_BIBLE.md`：固定世界觀與候選之分。
- 本資料夾的 STORY、LEVEL_DESIGN、DIALOGUES。
- `unity/Assets/Exploration/Scripts/AnomalyStreetDemo.cs`、`unity/Assets/Battle/UI/Arena/CombatArenaView.cs`、現有探索測試與場景建置器。
- `docs/CURRENT_HANDOFF.md`：最新狀態與後續順序。

## 三、推薦架構（名稱僅供參考；以現有程式命名為準）
| 系統 | 責任 | 不該負責 |
| --- | --- | --- |
| `DemoSession` | 跨場景維護當前案件、旗標、選項、檢查點 | 結算戰鬥數值 |
| `SceneTransitionService` | 控制淡出、載入 Office/Street、入口點 | 決定劇情通關條件 |
| `QuestFlow` | 依既定條件解鎖對話、交互物件和遭遇 | 直接改渲染器／手牌 |
| `InteractionRaycaster` | 從 FPS 準星找到可互動目標並顯示 E 提示 | 寫死案件 01 的邏輯 |
| `DialoguePresenter` | 從資料顯示通訊／NPC／調查／選項 | 硬編每句台詞於 MonoBehaviour |
| `ClueJournal` | 顯示玩家取得的線索和通訊重看 | 再決定是否取得線索 |
| `BattleAdapter` | 從既有 `BattleController`／`CombatArenaView` 接收勝敗、切換模式 | 複製 RoundResolver 或改 AP／攻防公式 |

資料驅動優先：對話節點 ID、觸發條件、下一節點和旗標變動可用 ScriptableObject 或 JSON；若選 JSON，需有驗證器與本地編輯易讀性。**避免**為一個 Demo 過度打造通用敘事框架。

## 四、最小持久狀態
```text
case_id = "case01"
stage = OFFICE_IDLE | OFFER_VISIBLE | CASE_ACCEPTED | STREET_ARRIVAL |
        CLUES_READY | ANOMALY_REVEALED | ENCOUNTER | BATTLE_WON |
        KEY_FOUND | CASE_RESOLVED | OFFICE_EPILOGUE | DEMO_COMPLETE
flags = { case_accepted, clue_map, clue_plaque, clue_witness,
          loop_seen, anomaly_revealed, battle_won, key_17, case_resolved }
```
同 Scene 每個互動事件須冪等：重按不能重複給線索、生成敵人、強制二次跳場景；跨 Scene 只要 Session 活著就記得事件。首版可只做同次啟動的持久狀態；退出程式再啟動的存檔屬後續項目，但重開或重試須提供可預期重置。

## 五、場景／資料分離
- 事務所獨立 Scene；街區用現有場景的安全複本或配置，不更動原 `AnomalyStreetDemo.unity` 的 QA 用途。
- 入口指定 `OfficeDemo` 或 `DemoEntry`；玩家在 Unity Editor 執行前需開啟入口 Scene，**按 Play 不會自動改成另一個 Scene**。Windows build 必須把 Demo 入口設為首場景。
- 門牌、線索、核、敵人與劇情觸發以資料引用與 prefab 身分管理，避免 Scene 名稱／物件名稱直接當唯一事件索引。
- 不依賴線上伺服器、不做連線任務、先不用 Addressables。
- 若日後要做 UI 切換與輸入鎖定，必須覆蓋游標鎖定／解鎖、E 按鍵交互、敵人點選與戰鬥 HUD 區域。

## 六、開發分段與成功條件
### Phase A：基礎 QA（先做）
- 獨立 QA Scene：一個簡單房間、一名佔位 NPC、一份文件、一個門。
- 完成互動提示、通訊字幕、非戰鬥對話選項、線索紀錄和跨 Scene session 的測試。
- 測試任意順序的三條線索；不接案件不能出發。
- 不需要完整第一章，也不修改正常遊戲場景。

### Phase B：串接案件
- 灰盒事務所 → 街區，三線索 → 折返 → 敵人顯現 → 既有戰鬥 → 核 → 事務所。
- 戰鬥保持 Unity v0.1 設定，沿用既有兩敵人情境，不在劇情中暗改難度。
- 有完整的勝利／失敗／重試與退出 UI。

### Phase C：打磨
- 補足教學提示、轉場與視覺事件、收尾、音效（有合法素材才加入）、畫面／操作 QA。
- 試玩看是否 15–25 分鐘、線索是否可自行理解、是否會卡關。

## 七、驗收清單
1. Play 從 Demo 入口啟動事務所，E 閱讀通知／接取任務。
2. 事務所門可以真正載入街區；任務旗標不丟失。
3. 未取得三線索不觸發異域；任意順序取得三線索有效。
4. 異域從空街顯現失序體，非預先站樁。
5. 戰鬥一律由正式 `BattleController`／`RoundResolver` 結算，預覽不改狀態。
6. 勝利後還要處理核，不能自動變成 `case_resolved`。
7. 返回事務所顯示已結案的內容；重試沒有重複敵人或卡死。
8. 舊 `CombatLab3D.unity`、`AnomalyStreetDemo.unity` 仍可正常運作。
9. Unity EditMode／PlayMode 既有測試全過；新增跨 Scene、對話狀態、事件冪等、重試測試。
10. 1920×1080／1280×720 顯示不截字。**若未真正執行 Unity，不得宣稱畫面或測試通過。**

## 八、變更控制
- **本設計回合只建立 docs**，不執行建置、Unity PlayMode、不改場景、不直接取代模型。
- 新劇情角色／核本質／失序通則不擅自寫成定案。
- 先讀現有實作再動手，避免另寫一套戰鬥、攝影機、輸入或 Scene 轉換系統。
- 後續實作時保留 QA 場景，修改後更新 `docs/CURRENT_HANDOFF.md` 和實作狀態。
- 主線資料與場景使用獨立 QA；若使用者指定只改某一層，不擴大修改。

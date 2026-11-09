# CASE01 Demo Phase 1（v0.7）｜缺陷清單與處理紀錄（Claude）

依 `CASE01_DEMO_PHASE1_VISUAL_CONSISTENCY_AND_QA_TASK_V0_7.md` §4：先在最新 main 重現，再修；沒有重現的列「未重現」。
截圖工具：`Case01V05Capture` 的 `p0` 組（`-captureTag before/after`）。所有驗證都是 batchmode 腳本＋截圖，**需真人目視驗收**。

| # | 位置／重現步驟 | 最新版仍存在？ | 根因 | 修正 | 自動化測試 | 真人待驗收 |
|---|---|---|---|---|---|---|
| P0-1 | 街區：站在 17 號對面看立面 | **是**（二樓有紫灰色門形線框） | v0.5 街區刻意加的「第三扇門痕跡」自發光線框，看起來像室內異常門外洩 | 移除；街上的異常只靠名冊少 17 號＋門牌偶爾閃 | `Street17Facade_ShowsNoInteriorPortalHint` | 白天／不同距離看立面 |
| P0-2 | 屋內二樓平台看開啟的第三扇門（S40） | **是**（膜邊緣破洞透出屋外街景與亮窗） | 第三扇門在外牆上，門後就是 `Case01Exterior` 街景；v0.5 彩色膜的流動邊界／破洞是 alpha | 所有失序門加「門後虛空」單面背板（只從來源側可見、無碰撞、與膜同步開關） | `PortalVoid_FollowsMembrane_IsOneSided_AndHasNoCollider`、`ThirdDoor_StillTraversable_ToMezzanine` | 近距／斜角、降低閃爍模式 |
| P0-3 | 屋內窗外是否看得到夾層（x33–47）／異常中庭（z23–59） | 未重現（測試角度：廚房北窗、東窗、臥室北窗都只看到街景） | 兩個「不存在的空間」和房屋在同一個世界座標，敞頂；理論上可能從窗或天空看到 | 預防：夾層／中庭／失序體放在渲染層 30、街景放 29；探索相機在屋內不畫 30、在異常空間不畫 29（只改渲染，碰撞與射線不變） | `AnomalySpaces_NotRenderedFromTheHouse_StreetNotRenderedInside`、`ManifestationAndEnemies_OnTheAnomalyLayer` | 在各房間轉一圈確認沒有缺漏 |
| P0-4 | 方案 A 玄關北牆 | **是**（玄關直通廚房的門） | v0.5 A 設計保留了玄關→廚房門 | 封成實牆；掛畫移到前門正前方。動線＝玄關→客廳→餐廳→廚房／樓梯；方案 A 路線測試改走新動線 | `LayoutA_EntranceDoesNotOpenIntoTheKitchen_DoorwaysClear`、住宅 A 完整流程 | 新動線的回訪距離是否可接受 |
| P0-6 | 正式 Demo 完整閉環（事務所→街區→17 號→線索／錯接／第三扇門→核→形成→戰鬥→核校正 v0.3→前門→街區→事務所結案） | 跑得完；發現一個輸入問題：Play 模式下 Demo 導演與屋內同時讀同一個輸入來源（實體鍵盤可重複讀所以不明顯，腳本輸入會被導演吃掉） | 導演在屋內時仍每影格 `InputSource.Read()` | 屋內由 `Case01HouseQA` 單獨讀輸入，導演改送空輸入 | `Case01DemoChainTests`（EditMode、真實屋內流程、快速勝利情境）、`Case01DemoPlayModeTests`（PlayMode 真場景：事務所→街區→屋內→前門→街區，各區一個 AudioListener、無 QA 字樣） | 真人完整玩一輪（含正式戰鬥） |
| P0-5 | 主要門洞是否被家具佔住 | 未重現（前門、客廳開口、餐廳寬開口、餐廳→廚房門、樓梯起步、書房門、臥室門） | — | — | 同上（膠囊檢查） | 真人走一圈 |

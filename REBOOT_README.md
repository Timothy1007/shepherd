# 《牧羊人：荒野之歌》Reboot

Reboot 是《牧羊人：荒野之歌》的新電子遊戲方向，正式開發分支為 `reboot`。舊版 `main` 是 Legacy 桌遊／既有 Prototype，不能作為 Reboot 的規則依據，也不在 Reboot 階段覆蓋或合併。

## 技術基準

- 目前正式 Prototype 引擎：Godot 4.7.x。
- Godot 專案位於 `godot/`，入口為 `godot/project.godot`。
- Unity 目前只保留為未來可能的正式移植方向，尚未作為 Reboot 實作基準。
- 規則與設計優先依據：`docs/REBOOT_GAME_DESIGN.md`。

## 當前狀態

目前已完成第一版視覺切片：荒野背景、行者狀態、遭遇焦點、敵方三格行動預覽、牌組資訊與七張手牌。手牌可點選查看卡面與說明；這些畫面是視覺 Prototype，尚未宣稱戰鬥規則已接通。

## 開發順序

1. 維持 Godot 視覺基線，先驗收場景、HUD、牌面尺寸與互動層級。
2. 建立與 UI 分離的純資料／規則核心：30 張初始牌組、7 手牌、3 AP、Player Action。
3. 實作成群、播種、積累、恩賜四個初始法則，並為每個法則建立可重現測試。
4. 接上基礎敵人、三格行動預知、理智與 Encounter 勝敗。
5. 把規則狀態接回 Godot UI，驗證「看得懂、選得到、結算順序清楚」。
6. 完成 15–20 分鐘 Vertical Slice：營火 → 探索 → 事件／敵人 → Encounter → 結果 → 回到探索。
7. 戰鬥循環成立後，再加入少量天賦節點與正式內容。

## 修改規則

每次修改前先讀 `docs/REBOOT_GAME_DESIGN.md` 與即將修改的檔案。已確定規則可以直接實作；Prototype 暫定規則要保留可調整性；待設計項目只能提出方案與測試，不能自行定案。每次修改都要回報實際變更、驗證方式與仍未完成項目。

## 開啟 Godot Prototype

在 Godot Project Manager 匯入 `godot/project.godot`，再執行主場景。Windows 本地開發使用 Godot 編輯器；目前不以 Legacy 的 npm 指令作為 Reboot 驗證方式。

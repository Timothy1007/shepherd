# 《牧羊人：荒野之歌》Reboot — Godot Prototype

這個資料夾是 Reboot 的 Godot 4.7.x 技術骨架，與既有 Web Prototype 暫時共存。

## 第一次在 Windows 開啟

1. 將 repository 的 `reboot` 分支拉到本機。
2. 開啟 Godot Project Manager。
3. 點「匯入」。
4. 選擇本資料夾內的 `project.godot`。
5. 匯入後開啟專案。
6. 按右上角執行或 F6/F5 測試。

第一版只驗證 Godot 專案能正常載入與執行；尚未宣稱戰鬥系統已移植。

## 開發原則

- Reboot 規格以 `../docs/REBOOT_GAME_DESIGN.md` 為準。
- Legacy `main` 不作為 Godot Reboot 的規則來源。
- Godot 版目前使用 GDScript。
- 既有 Web Prototype 暫不刪除，待 Godot 核心循環驗證後再決定退役範圍。

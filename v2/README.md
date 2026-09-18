# Shepherd PvP V2 isolated implementation

`v2/` 是《牧羊人：荒野之歌》PvP V2 的獨立可玩測試區。舊版仍保留在原本的 `src/`、`dist/`、`public/` 與既有測試中，V2 不覆寫舊版 runtime；美術、背景影片與音樂則直接沿用舊版 `public/assets/`。

## 安全邊界

- V2 遊戲邏輯、UI、測試與 preview 全部放在 `v2/`。
- 不修改舊版 `src/`、`dist/` 與既有 V1 tests。
- `public/` 只作為既有資產來源，不修改資產內容。
- V1 仍是回復／比較基準，直到 V2 完成獨立驗證。

## 目前可玩內容

這個測試版已可用 **真人 1 名 + AI ×3** 跑一場完整 7 輪爭局，包含：

- 第 1～6 輪公開使命；使命使用物資原始印刷數字累積。
- 第 7 輪最終使徒爭奪。
- 神蹟／災難後的自由物資橋接。
- 每輪重新隨機發牌，只繼承上一輪剩餘手牌的「張數」。
- 【整備】：無合法牌時棄全手、抽 2、消耗正常行動。
- 【死亡】：不進手牌、不占正常抽牌額度；揭示後完成當前效果，再進入鎖定順序的最後一圈；死亡輪不能整備。
- 使徒由本輪最後成功打出物資者取得，輪末收取已出牌區張數等量火種。
- 全桌共享【崩壞值】，跨輪累積；達臨界值時觸發未知審判，溢出保留。
- 已定稿審判的永久存在、同領域覆蓋與 I → II 升級池。
- 24 張 V2 神蹟與 18 張 V2 災難已進入可玩牌庫，並附已定稿崩壞值。
- 一般物資 1～4 崩壞 0、5～9 崩壞 1；恩典一律 0。
- R2、R5 後進入祝福競拍測試流程。
- 16 名人物可於開局選擇身份，AI 會自動操作基本 V2 牌局。
- 新 UI：即時崩壞條、審判列、公開使命進度、使徒獎池、牌面崩壞提示、戰局紀錄、競拍與終局視窗。
- 沿用舊版荒野背景影片、物資／神蹟／災難圖像與 BGM。

## V2 UI / UX 已重新接回舊版最後優化方向

V2 不再使用最初那套偏 Prototype 的獨立版面，而是在 `v2/` 內重新套用舊版最後確認過的互動與鏡頭語言：

- 高角度荒野戰場鏡頭與原有背景資產；拿掉厚重的假桌板層。
- P2／P3／P4 與中央牌堆位置回到最後一版桌面構圖。
- P1 手牌固定比例、圓角與尺寸；8～15 張時自動壓縮，不再把整手牌擠出畫面。
- 恢復「穩定 hover」：重疊手牌不會在相鄰卡之間瘋狂跳動。
- 非法牌不再整張變暗，保留可讀性；是否合法由操作結果與按鈕狀態溝通。
- 恢復拖曳／甩牌到中央的操作，同時保留點擊查看與按鈕出牌。
- 回合玩家以箭頭與高亮呈現；死亡輪會增加環境狀態提示。
- Overlay、Toast、戰局紀錄與選擇視窗的層級重新整理，避免互相蓋住。

實作檔案：

- `legacy-ui-bridge.css`：把最後版 UI 決策映射到 V2 DOM。
- `hand-stability-v2.js`：穩定重疊手牌 hover 與高張數壓縮。
- `drag-play-v2.js`：拖曳到中央後委派給既有 V2 出牌流程，不複製規則邏輯。

## 執行測試

在 repository 根目錄：

```bash
cd v2
npm test
```

目前新增的 playable smoke / registry / UI bridge tests 覆蓋：

- 四座位與使命輪初始化；
- 合法行動與跳過；
- 物資崩壞累積與使徒更新；
- AI 可推進行動；
- 祝福競拍後進下一輪；
- 24 神蹟、18 災難與 132 張基礎牌庫數量；
- 物資與恩典的崩壞規則；
- 最終 UI bridge 載入順序、穩定手牌與拖曳出牌 helper 有正確接上。

## 啟動瀏覽器測試版

```bash
cd v2
npm run preview
```

然後開啟：

```text
http://localhost:4174/v2/
```

按「開始爭局」後瀏覽器才會嘗試播放原有荒野 BGM，以符合瀏覽器自動播放限制。

## 崩壞值定稿

### 物資

- 一般物資 1～4：0
- 一般物資 5～9：1
- 恩典：0

### 神蹟

回轉歸向 2、行曠野之路 2、荊棘冠冕 3、於水中重生 3、如風吹來 1、所望之實底 1、行向水深之處 3、拆毀後重建 2、勝利歸於我們 3、恰如飛鳥經過 2、窄門與窄路 3、分杯之火 1、在黎明前叩門 1、杯滿盈溢 2、三股合成繩 3、越過長夜 1、替罪羊 3、空墳墓 2、拆毀堅固營壘 2、勝過死亡 2、焚而不毀荊棘 2、雨幕之下 1、第二次生命 2、劫後餘生 3。

### 災難

方舟之外 3、謊言與試探 2、告別舊時代 3、半朽蜜果 2、瞳中倒影 2、蟲災 2、灰與燼 3、積財寶在地上 2、瘟疫 4、染血銀幣 2、盜火 2、哈米吉多頓 4、破碎玻璃海 4、愛慾之種 3、焚城之火 3、虛謊之舌 2、三分之一的星辰 4、倒塌帳幕 3。

人物與祝福預設不增加崩壞，除非未來某個例外效果明確寫出。

## 測試版暫定值／已知簡化

以下不是 V2 最終定案，只為了讓目前版本可以完整跑局：

- 崩壞臨界值預設 **30**，開局可調整；正式值仍待牌流測試。
- 死亡牌深度暫以剩餘牌庫第 **10～18** 張間隨機插入；正式值需依牌流調整。
- 使命需求暫使用 7/7/7、11/5/5、4/5/12 三類 Prototype。
- 4 人資格暫以完成 3 次使命判定。
- 若第 7 輪使徒未取得資格，測試版暫以「使命完成數 → 火種」作次順位；正式終局次順位尚未定案。
- 16 名人物目前已可選擇身份，但**完整人物專屬技能尚未全部接入 playable engine**。
- 24 祝福已進入競拍池，但較複雜的主動／應對式祝福仍有簡化。
- 部分高互動神蹟／災難的真人多段決策與【應對】窗口，目前以自動／簡化流程先維持可玩性，後續要逐張補回完整互動。
- `沉淪 II` 與新增審判池仍未定稿，因此沒有擅自加入。

## 主要檔案

- `src/game/`：V2 母規則與契約層。
- `src/playable/cards.js`：可玩版 132 張基礎牌庫與崩壞值。
- `src/playable/game.js`：完整測試局狀態機、AI、死亡、使命、審判與競拍。
- `index.html` / `ui.css` / `app.js`：獨立 V2 瀏覽器 UI／UX。
- `legacy-ui-bridge.css` / `hand-stability-v2.js` / `drag-play-v2.js`：舊版最後 UI/UX 優化的 V2 對應層。
- `scripts/preview.js`：V2 本機 preview server。
- `tests/`：V2 核心、playable、registry 與 UI bridge 契約／smoke tests。

## 2026-09-18 presentation baseline reset

V2 browser presentation is now **rebased directly on the final V1 presentation stack**, instead of maintaining a separately recreated V2 UI.

- `v2/index.html` loads a copied snapshot of the finalized V1 presentation stack from `v2/v1-presentation/`, so V2 no longer depends on the old runtime presentation files at test time.
- V2-only UI additions live in `v2/v1-ui-adapter.css`.
- V2 card gestures live in `v2/v1-interactions.js`.
- `v2/app.js` is the adapter that binds V2 state/rules into the V1 DOM/presentation language.
- Obsolete standalone V2 UI files (`ui.css`, `legacy-ui-bridge.css`, `hand-stability-v2.js`, `drag-play-v2.js`) were removed to avoid two competing UI baselines.
- No manual **跳過** button.
- Restored V1 hand behavior: stable overlapping hover, short press enlarged preview, long press detail, drag/pull/throw to center.
- Center played pile shows the latest **three** played cards and is clickable to inspect the full current-round played history.
- The played pile is positioned lower on the battlefield center per the annotated playtest screenshot.
- The floating left battle-info panel and right operation-hint panel are removed so they no longer cover player information.

This is now the canonical V2 presentation direction: **V1 final UI first, V2 systems layered on top.**

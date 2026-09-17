# Shepherd PvP V2 isolated implementation

This directory is a clean, parallel implementation area for the agreed PvP V2 redesign.

## Safety boundary

- Nothing under `src/`, `dist/`, `public/`, or the existing V1 tests is modified by V2 work in this directory.
- V1 remains the recovery/playable baseline until V2 is independently ready.
- V2 code must not import mutable runtime state from V1.
- Root build/preview scripts are intentionally untouched.

## Current implementation scope

This V2 code pass establishes rules that are already agreed strongly enough to encode:

- seven-round structure; rounds 1–6 are mission rounds and round 7 is the final Apostle contest;
- public per-player mission progress using printed/original resource numbers;
- Death as a non-hand hidden timer that does not consume normal draw quota;
- locked Death-round final action order, one final normal action per player, and no Preparation during Death;
- Apostle tracking only on successful resource plays;
- shared corruption-state infrastructure with a configurable threshold;
- finalized per-card corruption values for normal resources, Grace, the 24 V2 miracles, and the 18 V2 disasters;
- judgment registry, domains, persistence, upgrade/replacement rules for the currently agreed judgment texts.

## Corruption values frozen in this pass

### Resources

- normal resource printed number 1–4: corruption 0;
- normal resource printed number 5–9: corruption 1;
- Grace resources: corruption 0.

### Miracles

- 回轉歸向 2
- 行曠野之路 2
- 荊棘冠冕 3
- 於水中重生 3
- 如風吹來 1
- 所望之實底 1
- 行向水深之處 3
- 拆毀後重建 2
- 勝利歸於我們 3
- 恰如飛鳥經過 2
- 窄門與窄路 3
- 分杯之火 1
- 在黎明前叩門 1
- 杯滿盈溢 2
- 三股合成繩 3
- 越過長夜 1
- 替罪羊 3
- 空墳墓 2
- 拆毀堅固營壘 2
- 勝過死亡 2
- 焚而不毀荊棘 2
- 雨幕之下 1
- 第二次生命 2
- 劫後餘生 3

### Disasters

- 方舟之外 3
- 謊言與試探 2
- 告別舊時代 3
- 半朽蜜果 2
- 瞳中倒影 2
- 蟲災 2
- 灰與燼 3
- 積財寶在地上 2
- 瘟疫 4
- 染血銀幣 2
- 盜火 2
- 哈米吉多頓 4
- 破碎玻璃海 4
- 愛慾之種 3
- 焚城之火 3
- 虛謊之舌 2
- 三分之一的星辰 4
- 倒塌帳幕 3

Character skills and Blessings do not have generic corruption values. They default to 0 unless a future exceptional effect explicitly says otherwise.

## Deliberately not frozen yet

The following remain configuration or TODO boundaries rather than guessed values:

- corruption threshold;
- Death insertion depth by player count;
- exact base hand-size / compensation formula;
- mission requirement numbers by player count;
- `沉淪 II`;
- the proposed five additional judgments that have not been finalized.

## Folder layout

- `src/game/constants.js` — shared V2 enums/constants.
- `src/game/state.js` — V2 game/player state constructors and invariants.
- `src/game/missions.js` — printed-number mission progress.
- `src/game/death.js` — Death draw semantics and locked final-circle order.
- `src/game/corruption.js` — shared corruption meter and threshold crossing.
- `src/game/card-corruption.js` — finalized corruption lookup for resources, miracles and disasters.
- `src/game/judgments.js` — judgment definitions and persistent domain/upgrade handling.
- `src/game/round.js` — round/death-round transitions and Apostle tracking.
- `src/game/index.js` — public exports for the isolated V2 core.

This folder is intentionally not wired into the existing browser UI yet.

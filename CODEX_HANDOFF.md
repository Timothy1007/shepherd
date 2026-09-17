# Codex handoff

## Baseline / safety

- Old V1 Web implementation remains untouched as the recovery/comparison baseline.
- **V2 runtime is isolated under `v2/`. Do not move V2 work back into old `src/`, `dist/`, or V1 tests until V2 is independently ready.**
- Existing `public/assets/` is reused read-only for V2 art, background video, card images and BGM.
- V2 design sources of truth remain:
  - `docs/PVP_CORE_RULES_V2.md`
  - `docs/SPECIAL_CARDS_MASTER.md`
  - `docs/CHARACTERS_V2.md`
  - `docs/BLESSINGS_V2.md`

## PvP V2 core

- 7 rounds total. R1–6 use public per-player missions; R7 is the final Apostle contest.
- Mission progress resets each round; completed-mission count persists.
- Resource mission progress always uses the **printed/original number**.
- Miracle/disaster play opens one free resource play for the next normal-action player.
- Physical hand identities do not persist across rounds; only leftover hand **count** contributes to the next fresh random hand.
- Preparation: with no legal card during a normal action, discard the whole hand and draw 2; it consumes the action and cannot be used during Death round.
- Death is a hidden round timer. Draw/show triggers it; look does not. Death never enters hand and does not consume the requested normal-card draw count.
- After Death is revealed, finish the current effect, lock the current-direction final circuit starting from the next seat, and give each player exactly one final normal action.
- Apostle is the last player to successfully play a resource. Round jackpot = number of cards in played area, no artificial cap.
- Fire is an in-match strategic resource, not the primary victory score.
- Blessing auction occurs after R2 and R5. Capacity remains 2.
- Ranked balance target remains a full 4-seat table; fewer humans may be filled by AI.

## Corruption / Judgment V2

- The whole table shares one corruption meter; corruption persists across rounds.
- Threshold is still a tunable parameter. Current playable default is **30 only as a Prototype**, not a frozen design value.
- Reaching threshold triggers an unknown Judgment, subtracts the threshold and preserves overflow.
- Judgments persist for the match.
- Different domains stack; same-domain Judgments replace each other. Tier-I can add its Tier-II successor to the future Judgment pool.
- Finalized current Judgments: 失序 I/II、蒙蔽 I/II、沉淪 I、爭戰 I/II、傾覆 I/II、分裂 I/II、揭露 I/II. `沉淪 II` is not finalized and must not be invented.
- Character skills and Blessings default to corruption 0 unless a future exceptional effect explicitly says otherwise.

### Frozen card corruption values

Resources:
- normal printed 1–4 = 0
- normal printed 5–9 = 1
- Grace = 0

Miracles:
- 回轉歸向 2, 行曠野之路 2, 荊棘冠冕 3, 於水中重生 3, 如風吹來 1, 所望之實底 1, 行向水深之處 3, 拆毀後重建 2, 勝利歸於我們 3, 恰如飛鳥經過 2, 窄門與窄路 3, 分杯之火 1, 在黎明前叩門 1, 杯滿盈溢 2, 三股合成繩 3, 越過長夜 1, 替罪羊 3, 空墳墓 2, 拆毀堅固營壘 2, 勝過死亡 2, 焚而不毀荊棘 2, 雨幕之下 1, 第二次生命 2, 劫後餘生 3.

Disasters:
- 方舟之外 3, 謊言與試探 2, 告別舊時代 3, 半朽蜜果 2, 瞳中倒影 2, 蟲災 2, 灰與燼 3, 積財寶在地上 2, 瘟疫 4, 染血銀幣 2, 盜火 2, 哈米吉多頓 4, 破碎玻璃海 4, 愛慾之種 3, 焚城之火 3, 虛謊之舌 2, 三分之一的星辰 4, 倒塌帳幕 3.

Canonical lookup also exists at `v2/src/game/card-corruption.js`.

## Playable V2 build now exists

A browser-testable vertical slice has been added entirely inside `v2/`:

- `v2/src/playable/cards.js`
  - 90 resources + 24 miracles + 18 disasters = 132-card base registry.
  - reuses old card image assets.
  - includes finalized per-card corruption values.
- `v2/src/playable/game.js`
  - 1 human + 3 AI four-seat game flow;
  - 7 rounds, missions, carry-over hand count, Death/final circuit, Apostle jackpot;
  - corruption/Judgment triggering and persistent Judgment domains;
  - special-card playable resolution layer;
  - R2/R5 Blessing auction;
  - basic AI turn selection;
  - temporary test-build final fallback if R7 Apostle is unqualified.
- `v2/index.html`, `v2/ui.css`, `v2/app.js`
  - independent browser presentation;
  - live corruption meter, Judgment chips, public mission bars, Apostle jackpot, card corruption badges, card-detail panel, history drawer, target/choice modals, auction UI and result screen;
  - reuses `public/assets/background.mp4` and `public/assets/audio/bgm-wilderness.mp3`.
- `v2/legacy-ui-bridge.css`, `v2/hand-stability-v2.js`, `v2/drag-play-v2.js`
  - restore the final V1 presentation decisions inside the isolated V2 DOM without importing V1 runtime code;
  - high-angle battlefield composition and removal of the synthetic oval table layer;
  - stable overlapped-hand hover with high-card-count compression;
  - illegal-card artwork remains readable instead of being dimmed;
  - drag / fling card to the central play area is restored while click-to-inspect still works;
  - current-turn arrow, Death-state visual accent, corrected overlay/toast hierarchy.
- `v2/scripts/preview.js`
  - local isolated preview server.
- `v2/tests/playable.test.js`, `v2/tests/registry.test.js`, `v2/tests/ui-bridge.test.js`
  - smoke / registry / presentation bridge contracts.

### How to test

From repository root:

```bash
cd v2
npm test
npm run preview
```

On PowerShell systems that block unsigned `npm.ps1`, use:

```powershell
npm.cmd test
npm.cmd run preview
```

Then open `http://localhost:4174/v2/`.

Earlier authored playable snapshot evidence was locally syntax-checked and the playable/registry test set passed **8/8** before the UI bridge restoration. The newly added UI bridge contract test has been committed but has not been executed by the GitHub connector; do not claim it passes until local/CI output confirms it.

## Character / Blessing direction

- The 16 character V2 texts are in `docs/CHARACTERS_V2.md`.
- The 24 Blessing V2 texts are in `docs/BLESSINGS_V2.md`.
- The playable build currently lets players choose one of the 16 character identities, but **full character-specific mechanics are not all wired yet**.
- The auction uses the V2 Blessing pool, but some complex active/reaction Blessing interactions are simplified in the current vertical slice.

## Prototype-only values / known simplifications

Do not silently treat these as final rules:

- corruption threshold default 30;
- Death insertion depth currently 10–18 cards into the remaining deck;
- mission prototypes 7/7/7, 11/5/5 and 4/5/12;
- 4-seat qualification currently uses 3 completed missions;
- if R7 Apostle is unqualified, current test build temporarily falls back to completed missions then fire; official secondary settlement is still TBD;
- several high-interaction Miracle/Disaster reactions and multi-step human decisions are currently automated/simplified to keep the first playable build moving;
- detailed character abilities are not yet complete in playable runtime;
- some Blessing actives/reactions are not yet complete;
- additional Judgment pool beyond the finalized set is still open.

## Next implementation priority

1. Have the user pull and playtest the restored optimized UI first; fix any visual/interaction regression before adding more systems.
2. Playtest the isolated build and fix game-flow / Death / corruption / Judgment blockers.
3. Replace simplified special-card decisions and response windows with the finalized card-specific interaction flows.
4. Wire the 16 character kits faction by faction, without adding generic character corruption.
5. Complete the 24 Blessing mechanics and auction edge cases.
6. Tune corruption threshold and Death depth from representative full-game data instead of intuition.
7. Only after the V2 test build is stable, consider integration/deployment changes outside `v2/`.

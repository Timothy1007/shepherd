# Codex handoff

## Baseline

- Current playable implementation remains the recovered Web baseline and existing special-card work.
- **Upcoming design source of truth:** `docs/PVP_CORE_RULES_V2.md`.
- **V2 special-card source:** `docs/SPECIAL_CARDS_MASTER.md`.
- **V2 character source:** `docs/CHARACTERS_V2.md`.
- **V2 blessing source:** `docs/BLESSINGS_V2.md`.
- `docs/RULES.md` still describes the V1 rules semantics currently reflected by much of the old code.
- **Isolated V2 implementation now lives under `v2/` and must remain separate from the old `src/`, `dist/`, `public/`, and V1 tests until V2 is independently ready.**

## PvP V2 direction

The core rewrite targets 2–4 player Classic/Ranked PvP and keeps seven rounds while redesigning the victory/resource loop:

- Rounds 1–6 use public per-player missions; mission progress resets each round while completed-mission count persists.
- Mission progress uses the **printed/original resource number**, not temporary number modifiers.
- Round 7 is the final Apostle contest rather than a normal mission round.
- Miracle/disaster play still opens one free-resource play for the next normal-action player.
- The Death card is a hidden round timer. When revealed, the current effect finishes, then every player receives exactly one final normal action; no preparation is allowed during the Death round.
- V2 preparation: if a player has no legal card during a normal action, they may discard the whole hand and draw 2; preparation consumes that action and may be used again on a later normal action before Death.
- Physical cards do not persist across rounds. Only the number of cards left in hand is carried forward as an additional random-draw count next round.
- The Apostle remains the last player to successfully play a resource.
- Apostle fire reward keeps the original jackpot identity: gain fire equal to the number of cards in the played area, with no artificial cap.
- Fire becomes primarily an in-match strategic resource rather than the main victory score.
- Fire Spirit is redesigned as a fixed blessing auction after rounds 2 and 5; number of blessings equals number of players, fire totals are public, each player targets one auction item at a time, and each player is intended to leave with one blessing.
- Blessing capacity remains 2.
- Blessings are now primarily long-term build pieces rather than disposable items. Most persist in the Blessing area and provide ongoing or once-per-round effects; only explicitly marked strong effects are consumed.
- For Blessings with a generic `once per round` active and no event trigger, the default timing is at the start of one of the player's normal actions of their choice that round, not necessarily their first action.
- Ranked points are an out-of-match system and must not be a direct conversion of leftover fire.

## Corruption / Judgment V2

- The whole table shares one corruption meter and corruption persists across rounds.
- The exact Judgment threshold is still configurable and must be tuned after card-flow testing rather than guessed.
- Reaching the threshold triggers a persistent Judgment and subtracts the threshold while preserving overflow.
- Judgments persist for the match. Different domains can coexist; same-domain Judgments replace each other unless they belong to the same upgrade chain.
- Character skills and Blessings do **not** receive generic corruption values; they default to 0 unless a future exceptional effect explicitly says otherwise.

### Finalized card corruption values

Resources:
- normal resource printed number 1–4: corruption 0;
- normal resource printed number 5–9: corruption 1;
- Grace resources: corruption 0.

Miracles:
- 回轉歸向 2, 行曠野之路 2, 荊棘冠冕 3, 於水中重生 3, 如風吹來 1, 所望之實底 1, 行向水深之處 3, 拆毀後重建 2, 勝利歸於我們 3, 恰如飛鳥經過 2, 窄門與窄路 3, 分杯之火 1, 在黎明前叩門 1, 杯滿盈溢 2, 三股合成繩 3, 越過長夜 1, 替罪羊 3, 空墳墓 2, 拆毀堅固營壘 2, 勝過死亡 2, 焚而不毀荊棘 2, 雨幕之下 1, 第二次生命 2, 劫後餘生 3.

Disasters:
- 方舟之外 3, 謊言與試探 2, 告別舊時代 3, 半朽蜜果 2, 瞳中倒影 2, 蟲災 2, 灰與燼 3, 積財寶在地上 2, 瘟疫 4, 染血銀幣 2, 盜火 2, 哈米吉多頓 4, 破碎玻璃海 4, 愛慾之種 3, 焚城之火 3, 虛謊之舌 2, 三分之一的星辰 4, 倒塌帳幕 3.

Implementation lives in `v2/src/game/card-corruption.js` and is exported through `v2/src/game/index.js`.

## Faction / character V2 direction

The 16 characters have completed a first-pass V2 redesign. Use `docs/CHARACTERS_V2.md` as the current character text source.

Faction identities:

- **富饒城邦**: value, trade, wealth distribution, investment and risk management.
- **榮光聖殿**: information, prediction, order and reducing decision error; may foresee Death but normally cannot move it.
- **彼岸之使**: redirects ownership/effects and can explicitly manipulate Death when character text grants an exception.
- **流火之民**: converts danger, low resources and self-imposed cost into burst turns.

Ranked balance should primarily assume a full 4-seat table. If fewer than four human players are present, high-level AI may fill the remaining ranked seats; 2–3 human-only tables may be treated as casual/custom modes. Rules should still remain usable at lower player counts where practical.

## Blessings V2 direction

The 24 existing Blessings have completed a first-pass V2 redesign in `docs/BLESSINGS_V2.md`.

Current Blessing structure:

- Most positive Blessings are persistent build pieces.
- Some use once-per-round active or event-triggered effects.
- A small number of very strong Blessings remain one-shot and are discarded after use.
- Negative Blessings do not count toward Blessing capacity and are not part of the normal Fire Spirit auction pool.
- Current first-pass watchlist includes 群星的迴響、駛向新生命、偶爾需要沉默、命運從未公平.

## Deprecated balance rule

The old heuristic **“draw 1 card ≈ gain 5 fire” is retired and must not be used for V2 balancing.**

V2 card evaluation must separately consider card flow, mission progress, Apostle control, fire economy, Death timing and long-term Blessing build value.

## Current implementation status

- Old V1 remains untouched as the recovery/playable baseline.
- `v2/` is a parallel implementation area and must not mutate V1 runtime state.
- Implemented in V2 so far: base state, missions, Death semantics, corruption meter, per-card corruption lookup, Judgment registry/domain handling, round/death-round transitions, Apostle tracking, and contract tests.
- `v2/tests/card-corruption.test.js` records the finalized corruption lookup contract.
- Tests have been added but have **not** been executed by the connector; do not claim they pass without CI or local output.

## Still open

- exact corruption threshold;
- Death insertion depth by player count;
- exact base hand-size / compensation formula;
- mission requirement numbers by player count;
- `沉淪 II`;
- additional Judgment pool beyond the finalized existing set;
- wiring the V2 special-card definitions and effects into the isolated V2 engine;
- V2 UI, AI, QA seeds and presentation.

## Next work

1. Move finalized V2 miracle/disaster definitions into the isolated V2 card registry and attach the finalized corruption values.
2. Implement resource-card corruption rule (1–4 = 0, 5–9 = 1, Grace = 0) at card creation/play boundaries.
3. Continue the V2 core implementation only inside `v2/`.
4. Tune the corruption threshold after representative card-flow simulations/tests exist.

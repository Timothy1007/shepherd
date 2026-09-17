# Codex handoff

## Baseline

- Current playable implementation remains the recovered Web baseline and existing special-card work.
- **Upcoming design source of truth:** `docs/PVP_CORE_RULES_V2.md`.
- **V2 special-card source:** `docs/SPECIAL_CARDS_MASTER.md`.
- **V2 character source:** `docs/CHARACTERS_V2.md`.
- `docs/RULES.md` still describes the V1 rules semantics currently reflected by much of the code; do not assume V2 systems are implemented yet.
- The V2 redesign must be agreed and balanced at the text/rules layer before replacing the core implementation.

## PvP V2 direction

The next core rewrite targets 2–4 player Classic/Ranked PvP and keeps seven rounds while redesigning the victory/resource loop:

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
- Ranked points are an out-of-match system and must not be a direct conversion of leftover fire.

## Faction / character V2 direction

The 16 characters have completed a first-pass V2 redesign. Use `docs/CHARACTERS_V2.md` as the current character text source.

Faction identities:

- **富饒城邦**: value, trade, wealth distribution, investment and risk management.
- **榮光聖殿**: information, prediction, order and reducing decision error; may foresee Death but normally cannot move it.
- **彼岸之使**: redirects ownership/effects and can explicitly manipulate Death when character text grants an exception.
- **流火之民**: converts danger, low resources and self-imposed cost into burst turns.

Ranked balance should primarily assume a full 4-seat table. If fewer than four human players are present, high-level AI may fill the remaining ranked seats; 2–3 human-only tables may be treated as casual/custom modes. Rules should still remain usable at lower player counts where practical.

## Deprecated balance rule

The old heuristic **“draw 1 card ≈ gain 5 fire” is retired and must not be used for V2 balancing.**

V2 card evaluation must separately consider card flow, mission progress, Apostle control, fire economy, and Death timing.

## Current implementation constraints

- `currentResource` and `currentApostle` are separate. Only a successful resource play updates the Apostle.
- `dist/` is generated. Make source changes in `src/` and rebuild.
- Existing implemented miracles/disasters/characters remain V1 code behavior until the V2 core rewrite. Documentation updates do not imply implementation parity.
- Do not continue implementing the remaining V1 miracles by default.
- Current V2 text review has completed first-pass integration for 24 miracles, 18 disasters and 16 characters.

## Next work

1. Redesign and rebalance Blessings around auction/build value and the high-flow fire economy.
2. Review Judgments and any remaining global systems against V2 mission/Death rules.
3. Run whole-system balance review across miracles, disasters, characters and blessings.
4. Only after rule/text consensus, rewrite the game core, character/card definitions, AI, tests, QA seeds and presentation for V2.

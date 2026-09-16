# Codex handoff

## Baseline

- Current playable implementation remains the recovered Web baseline and existing special-card work.
- **Upcoming design source of truth:** `docs/PVP_CORE_RULES_V2.md`.
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

## Deprecated balance rule

The old heuristic **“draw 1 card ≈ gain 5 fire” is retired and must not be used for V2 balancing.**

V2 card evaluation must separately consider card flow, mission progress, Apostle control, fire economy, and Death timing.

## Current implementation constraints

- `currentResource` and `currentApostle` are separate. Only a successful resource play updates the Apostle.
- `dist/` is generated. Make source changes in `src/` and rebuild.
- Existing implemented miracles/disasters are V1 implementations until their V2 text is approved.
- Do not continue implementing the remaining V1 miracles by default; first complete the V2 text/balance review for miracles, disasters, characters, then blessings.

## Next work

1. Review and rebalance all 24 miracles against V2.
2. Review and rebalance all 18 disasters.
3. Rebalance the 16 characters around faction identities and individual playstyles.
4. Redesign blessings around auction/build value and the high-flow fire economy.
5. Only after rule/text consensus, rewrite the game core, tests, QA seeds and presentation for V2.

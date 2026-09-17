# Shepherd PvP V2 isolated implementation

This directory is a clean, parallel implementation area for the agreed PvP V2 redesign.

## Safety boundary

- Nothing under `src/`, `dist/`, `public/`, or the existing V1 tests is modified by V2 work in this directory.
- V1 remains the recovery/playable baseline until V2 is independently ready.
- V2 code must not import mutable runtime state from V1.
- Root build/preview scripts are intentionally untouched.

## Current implementation scope

This first V2 code pass establishes only rules that are already agreed strongly enough to encode:

- seven-round structure; rounds 1–6 are mission rounds and round 7 is the final Apostle contest;
- public per-player mission progress using printed/original resource numbers;
- Death as a non-hand hidden timer that does not consume normal draw quota;
- locked Death-round final action order, one final normal action per player, and no Preparation during Death;
- Apostle tracking only on successful resource plays;
- shared corruption-state infrastructure with a configurable threshold (threshold and per-card values are deliberately not frozen yet);
- judgment registry, domains, persistence, upgrade/replacement rules for the currently agreed judgment texts.

## Deliberately not frozen yet

The following are represented as configuration or TODO boundaries rather than guessed values:

- corruption threshold;
- per-card corruption values;
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
- `src/game/judgments.js` — judgment definitions and persistent domain/upgrade handling.
- `src/game/round.js` — round/death-round transitions and Apostle tracking.
- `src/game/index.js` — public exports for the isolated V2 core.

This folder is intentionally not wired into the existing browser UI yet.
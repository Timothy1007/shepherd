# Codex handoff

## Baseline

- Public stable target: **Playable Alpha 0.1.1**.
- Current development source of truth: **Shepherd Recovery Alpha 0.1.1 baseline**.
- The original 15-commit history is not required; do not depend on the damaged bundle.

## Recovered behavior

- Four players (one human, three AI), seven rounds, 90 resource cards, seeded gameplay RNG.
- Resource dominance and same-type number rules, grace declaration, apostle reward, redraw with discard recycling, empty-hand/end-participation flow, losing-streak hands, fixed starting-player rotation, and tied winners.
- Immutable validated actions: `playResource`, `redraw`, `endParticipation`, `endEmptyHand`, and `settleRound` via `getNormalActions` / `executeNormalAction`.
- Restart-safe controller tokens and AI pacing of 1.4–2.4 seconds using non-gameplay randomness.

## Constraints

- `currentResource` and `currentApostle` are separate. Only a successful resource play updates the apostle.
- A round settles as soon as at most one player retains a normal action; nobody follows their own card.
- Redraw may recycle `discardPile`, never `playedArea`. All `playedArea` cards count toward the apostle reward.
- `lowTemperatureFire` and player `effects` are retained data fields but have no Recovery 1 behavior.
- `dist/` is generated. Make changes in `src/` and rebuild.

## Next work

Do not begin Alpha 0.2 until this recovery baseline is accepted. The complete 36-card production artwork set is still needed. Future special cards, pending choices, UI expansion, animation, fonts, and market background are not part of this baseline.

# 牧羊人：荒野之歌 — Recovery Alpha 0.1.1

This repository is the recovery source-of-truth baseline for the four-player, seven-round resource game. It intentionally rebuilds the playable Alpha 0.1.1 core without depending on the damaged historical bundle.

## Commands

```bash
npm test       # regression suite
npm run simulate
npm run build  # regenerate dist/ from src/ and public/
npm run preview
```

Open <http://localhost:4173> after starting the preview server. The browser game supports one human and three automatic AI players. AI delay uses `Math.random()` and remains separate from the reproducible game RNG.

## Layout

- `src/game/`: deterministic definitions, seeded RNG, state, rules, and immutable action API.
- `src/controller/`: human/AI orchestration, action tokens, and restart-safe timers.
- `src/presentation/`: minimal browser interface.
- `public/assets/`: presentation-only artwork location.
- `tests/`: core, controller, invariants, and multi-seed seven-round simulations.
- `scripts/`: build, preview, and simulation commands.
- `dist/`: generated publication output; never the only source.

## Recovery scope

The baseline contains only the official 90-card resource recipe: sheep, food, and money values 1–9 with three copies each, plus grace values 1–9 with one copy each. Card definitions and instances are separate, and every physical card has a unique `instanceId`.

Miracles, disasters, characters, blessings, judgments, shops, special-card resolution, and Alpha 0.2 are deliberately out of scope. No public deployment should be made before owner acceptance.

## Missing artwork

The former `dist/assets` contains only isolated preview/example faces, not the complete 36 production resource faces. Recovery therefore uses honest text cards. Supply the original sheep, food, money, and grace faces for values 1–9 to restore artwork without changing gameplay.

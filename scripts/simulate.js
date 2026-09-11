import { advanceToNextRound, createGame, executeNormalAction, getNormalActions } from '../src/game/game.js';

export function simulate(seed) {
  let state = createGame({ seed });
  let actions = 0;
  while (!state.gameOver && actions < 5000) {
    if (state.phase === 'roundEnd') {
      const transition = advanceToNextRound(state);
      if (!transition.ok) throw new Error(transition.reason);
      state = transition.state;
      continue;
    }
    const available = getNormalActions(state);
    if (!available.length) throw new Error(`No action in active game for seed ${seed}`);
    const result = executeNormalAction(state, available[0]);
    if (!result.ok) throw new Error(result.reason);
    state = result.state;
    actions += 1;
  }
  if (!state.gameOver) throw new Error(`Simulation limit reached for seed ${seed}`);
  return { state, actions };
}

if (process.argv[1] && import.meta.url === new URL(`file://${process.argv[1]}`).href) {
  for (const seed of ['recovery-1', 'recovery-2', 'recovery-3', 'recovery-4', 'recovery-5']) {
    const result = simulate(seed);
    console.log(seed, `round=${result.state.round}`, `actions=${result.actions}`, `winner=${result.state.winner.join(',')}`);
  }
}

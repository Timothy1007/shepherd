import { createGame, executeNormalAction, getNormalActions } from '../src/game/game.js';

export function simulate(seed) {
  let state = createGame({ seed });
  let actions = 0;
  while (!state.gameOver && actions < 5000) {
    const available = getNormalActions(state);
    if (!available.length) throw new Error(`No action in active game for seed ${seed}`);
    let result = null;
    for (const action of available) {
      const attempt = executeNormalAction(state, action);
      if (attempt.ok) { result = attempt; break; }
      if (attempt.reason !== 'Unsupported special card.') throw new Error(attempt.reason);
    }
    if (!result) {
      const current = state.players.find(p => p.playerId === state.currentPlayer);
      const unsupportedIds = new Set(available.filter(a => a.instanceId).map(a => a.instanceId));
      if (!current || !unsupportedIds.size) throw new Error('Unsupported special card.');
      const next = structuredClone(state);
      const player = next.players.find(p => p.playerId === next.currentPlayer);
      const discarded = player.hand.filter(c => unsupportedIds.has(c.instanceId));
      player.hand = player.hand.filter(c => !unsupportedIds.has(c.instanceId));
      next.discardPile.push(...discarded);
      state = next;
      continue;
    }
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

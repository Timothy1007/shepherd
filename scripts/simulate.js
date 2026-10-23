import { createGame, effectChangeAmount, executeNormalAction, getNormalActions } from '../src/game/game.js';

export function simulate(seed, { observe, preferReaction = false } = {}) {
  let state = createGame({ seed });
  let actions = 0;
  while (!state.gameOver && actions < 5000) {
    const available = getNormalActions(state);
    if (!available.length) throw new Error(`No action in active game for seed ${seed}`);
    let result = null;
    const candidates = preferReaction ? [...available].sort((a,b) => Number(b.choice === 'use') - Number(a.choice === 'use')) : available;
    for (const action of candidates) {
      const player=state.players.find(p=>p.playerId===action.playerId);
      const card=player?.hand.find(c=>c.instanceId===action.instanceId);
      const availableCards=player?.hand.filter(c=>c.instanceId!==action.instanceId)??[];
      const gifts=card?.definitionId==='miracle-11'?availableCards.slice(0,Math.min(availableCards.length,effectChangeAmount(state,3))).map(c=>({instanceId:c.instanceId,recipientPlayerId:state.players.find(p=>p.playerId!==player.playerId).playerId})):undefined;
      const attempt = executeNormalAction(state, gifts?{...action,gifts}:action);
      if (!attempt.ok) throw new Error(`Seed ${seed}, action ${JSON.stringify(action)}: ${attempt.reason}`);
      observe?.({ state, action, card, nextState: attempt.state });
      result = attempt; break;
    }
    if (!result) throw new Error(`No executable action for seed ${seed}: ${JSON.stringify(available)}`);
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

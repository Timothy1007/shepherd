import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, executeNormalAction, getNormalActions, listCardLocations, settleRound } from '../src/game/game.js';

function take(state, definitionId) {
  for (const location of [state.deck, state.discardPile, state.playedArea, ...state.players.map((player) => player.hand), ...state.players.map((player) => player.effects)]) {
    const index = location.findIndex((card) => card.definitionId === definitionId);
    if (index >= 0) return location.splice(index, 1)[0];
  }
  throw new Error(`Missing ${definitionId}`);
}

function prepareHuman(state, definitions) {
  state.deck.push(...state.players[0].hand);
  state.players[0].hand = definitions.map((id) => take(state, id));
  state.currentPlayer = 'player-1';
  return state.players[0];
}

function act(state, action) {
  const result = executeNormalAction(state, action);
  assert.equal(result.ok, true, result.reason);
  return result.state;
}

test('alpha first hand contains 荊棘冠冕 while preserving seven cards', () => {
  const state = createGame({ seed: 'crown-alpha' });
  assert.equal(state.players[0].hand.length, 7);
  assert.ok(state.players[0].hand.some((card) => card.definitionId === 'miracle-03'));
});

test('荊棘冠冕 enters effect zone instead of played area', () => {
  let state = createGame({ seed: 'crown-zone' });
  prepareHuman(state, ['miracle-03', 'sheep-4']);
  state = act(state, getNormalActions(state).find((candidate) => candidate.type === 'playMiracle'));
  assert.ok(state.players[0].effects.some((card) => card.definitionId === 'miracle-03'));
  assert.equal(state.playedArea.some((card) => card.definitionId === 'miracle-03'), false);
  assert.equal(state.currentResource, null);
});

test('荊棘冠冕 makes the next resource legal despite number and counter restrictions', () => {
  let state = createGame({ seed: 'crown-free-play' });
  prepareHuman(state, ['miracle-03', 'sheep-1', 'food-1']);
  state = act(state, getNormalActions(state).find((candidate) => candidate.type === 'playMiracle'));

  state.currentPlayer = 'player-1';
  state.currentResource = { type: 'sheep', number: 9, instanceId: 'test' };
  const actions = getNormalActions(state).filter((candidate) => candidate.type === 'playResource');
  assert.ok(actions.some((candidate) => candidate.instanceId.includes('sheep-1')));
  assert.ok(actions.some((candidate) => candidate.instanceId.includes('food-1')));
});

test('successful resource consumes 荊棘冠冕 into discard pile', () => {
  let state = createGame({ seed: 'crown-consume' });
  prepareHuman(state, ['miracle-03', 'food-1']);
  state = act(state, getNormalActions(state).find((candidate) => candidate.type === 'playMiracle'));
  state.currentPlayer = 'player-1';
  state.currentResource = { type: 'sheep', number: 9, instanceId: 'test' };
  state = act(state, getNormalActions(state).find((candidate) => candidate.type === 'playResource'));
  assert.equal(state.players[0].effects.some((card) => card.definitionId === 'miracle-03'), false);
  assert.ok(state.discardPile.some((card) => card.definitionId === 'miracle-03'));
});

test('playing another miracle does not consume 荊棘冠冕', () => {
  let state = createGame({ seed: 'crown-waits' });
  prepareHuman(state, ['miracle-03', 'miracle-01', 'food-1']);
  state = act(state, getNormalActions(state).find((candidate) => candidate.instanceId.includes('miracle-03')));
  state.currentPlayer = 'player-1';
  state = act(state, getNormalActions(state).find((candidate) => candidate.instanceId.includes('miracle-01')));
  assert.ok(state.players[0].effects.some((card) => card.definitionId === 'miracle-03'));
});

test('荊棘冠冕 and 行曠野之路 stack on the same successful resource', () => {
  let state = createGame({ seed: 'crown-stack' });
  prepareHuman(state, ['miracle-03', 'miracle-02', 'food-1']);
  state = act(state, getNormalActions(state).find((candidate) => candidate.instanceId.includes('miracle-03')));
  state.currentPlayer = 'player-1';
  state = act(state, getNormalActions(state).find((candidate) => candidate.instanceId.includes('miracle-02')));

  state.currentPlayer = 'player-1';
  state.currentResource = { type: 'sheep', number: 9, instanceId: 'test' };
  const resource = getNormalActions(state).find((candidate) => candidate.type === 'playResource');
  state = act(state, resource);
  assert.equal(state.currentResource.baseNumber, 1);
  assert.equal(state.currentResource.number, 4);
  assert.equal(state.currentResource.numberBonus, 3);
  assert.equal(state.players[0].effects.length, 0);
  assert.ok(state.discardPile.some((card) => card.definitionId === 'miracle-02'));
  assert.ok(state.discardPile.some((card) => card.definitionId === 'miracle-03'));
});

test('荊棘冠冕 keeps Grace declarations valid while ignoring the current resource', () => {
  let state = createGame({ seed: 'crown-grace' });
  prepareHuman(state, ['miracle-03', 'grace-1']);
  state = act(state, getNormalActions(state).find((candidate) => candidate.type === 'playMiracle'));
  state.currentPlayer = 'player-1';
  state.currentResource = { type: 'sheep', number: 9, instanceId: 'test' };
  const actions = getNormalActions(state).filter((candidate) => candidate.type === 'playResource');
  assert.deepEqual(new Set(actions.map((action) => action.declaredType)), new Set(['sheep', 'food', 'money']));
});

test('unresolved 荊棘冠冕 is safely recycled at the round boundary', () => {
  let state = createGame({ seed: 'crown-round-boundary' });
  const expected = Object.keys(state.cardRegistry).length;
  prepareHuman(state, ['miracle-03', 'sheep-1']);
  state = act(state, getNormalActions(state).find((candidate) => candidate.type === 'playMiracle'));
  assert.ok(state.players[0].effects.some((card) => card.definitionId === 'miracle-03'));

  state.players.forEach((player, index) => { player.hasNormalAction = index === 0; });
  const result = settleRound(state);
  assert.equal(result.ok, true, result.reason);
  state = result.state;
  assert.equal(state.round, 2);
  assert.equal(state.players.every((player) => player.effects.length === 0), true);
  assert.equal(listCardLocations(state).length, expected);
  assert.equal(new Set(listCardLocations(state)).size, expected);
});

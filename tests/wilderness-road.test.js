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

test('alpha first hand contains 行曠野之路 while preserving seven cards', () => {
  const state = createGame({ seed: 'wilderness-alpha' });
  assert.equal(state.players[0].hand.length, 7);
  assert.ok(state.players[0].hand.some((card) => card.definitionId === 'miracle-02'));
});

test('行曠野之路 enters effect zone instead of played area', () => {
  let state = createGame({ seed: 'wilderness-zone' });
  prepareHuman(state, ['miracle-02', 'sheep-4']);
  const action = getNormalActions(state).find((candidate) => candidate.instanceId === state.players[0].hand[0].instanceId);
  state = act(state, action);
  assert.equal(state.players[0].effects.length, 1);
  assert.equal(state.players[0].effects[0].definitionId, 'miracle-02');
  assert.equal(state.playedArea.some((card) => card.definitionId === 'miracle-02'), false);
  assert.equal(state.currentResource, null);
});

test('next successful resource gets +3 then Wilderness Road moves to discard', () => {
  let state = createGame({ seed: 'wilderness-trigger' });
  prepareHuman(state, ['miracle-02', 'sheep-8']);
  let action = getNormalActions(state).find((candidate) => candidate.type === 'playMiracle');
  state = act(state, action);

  state.currentPlayer = 'player-1';
  action = getNormalActions(state).find((candidate) => candidate.type === 'playResource');
  state = act(state, action);
  assert.equal(state.currentResource.number, 11);
  assert.equal(state.currentResource.baseNumber, 8);
  assert.equal(state.currentResource.numberBonus, 3);
  assert.equal(state.players[0].effects.length, 0);
  assert.ok(state.discardPile.some((card) => card.definitionId === 'miracle-02'));
});

test('effect-zone cards remain part of the card location invariant', () => {
  let state = createGame({ seed: 'wilderness-invariant' });
  const expected = Object.keys(state.cardRegistry).length;
  prepareHuman(state, ['miracle-02', 'food-3']);
  const action = getNormalActions(state).find((candidate) => candidate.type === 'playMiracle');
  state = act(state, action);
  assert.equal(listCardLocations(state).length, expected);
  assert.equal(new Set(listCardLocations(state)).size, expected);
});

test('an unresolved Wilderness Road cannot stall the next round', () => {
  let state = createGame({ seed: 'wilderness-round-boundary' });
  const expected = Object.keys(state.cardRegistry).length;
  prepareHuman(state, ['miracle-02', 'sheep-1']);
  state = act(state, getNormalActions(state).find((candidate) => candidate.type === 'playMiracle'));
  assert.equal(state.players[0].effects.length, 1);

  state.players.forEach((player, index) => { player.hasNormalAction = index === 0; });
  const result = settleRound(state);
  assert.equal(result.ok, true, result.reason);
  state = result.state;
  assert.equal(state.round, 2);
  assert.equal(state.phase, 'playing');
  assert.ok(state.currentPlayer);
  assert.equal(state.players.every((player) => player.effects.length === 0), true);
  assert.equal(listCardLocations(state).length, expected);
  assert.ok(getNormalActions(state).length > 0);

  state.players.forEach((player, index) => { player.hasNormalAction = index === 0; });
  const second = settleRound(state);
  assert.equal(second.ok, true, second.reason);
  assert.equal(second.state.round, 3);
  assert.equal(second.state.phase, 'playing');
  assert.ok(second.state.currentPlayer);
});

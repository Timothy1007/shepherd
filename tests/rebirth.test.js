import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, executeNormalAction, getNormalActions, listCardLocations } from '../src/game/game.js';

function take(state, definitionId) {
  for (const location of [state.deck, state.discardPile, state.playedArea, ...state.players.map(player => player.hand), ...state.players.map(player => player.effects)]) {
    const index = location.findIndex(card => card.definitionId === definitionId);
    if (index >= 0) return location.splice(index, 1)[0];
  }
  throw new Error(`Missing ${definitionId}`);
}

function setHand(state, ids) {
  const player = state.players[0];
  state.deck.push(...player.hand);
  player.hand = ids.map(id => take(state, id));
  state.currentPlayer = player.playerId;
  return player;
}

function play(state, definitionId) {
  const player = state.players[0];
  const card = player.hand.find(c => c.definitionId === definitionId);
  const action = getNormalActions(state).find(a => a.instanceId === card.instanceId);
  const result = executeNormalAction(state, action);
  assert.equal(result.ok, true, result.reason);
  return result.state;
}

test('rebirth QA guarantees 於水中重生 and 在黎明前叩門', () => {
  const state = createGame({ seed: 'rebirth-preview' });
  assert.equal(state.players[0].hand.length, 7);
  assert.ok(state.players[0].hand.some(card => card.definitionId === 'miracle-04'));
  assert.ok(state.players[0].hand.some(card => card.definitionId === 'miracle-13'));
});

test('於水中重生 discards the entire remaining hand then draws the same amount', () => {
  let state = createGame({ seed: 'rebirth-basic' });
  const player = setHand(state, ['miracle-04', 'sheep-1', 'food-2', 'money-3']);
  const oldIds = player.hand.filter(card => card.definitionId !== 'miracle-04').map(card => card.instanceId);
  const beforeDeck = state.deck.length;
  state = play(state, 'miracle-04');
  const human = state.players[0];
  assert.equal(human.hand.length, 3);
  assert.equal(oldIds.every(id => state.discardPile.some(card => card.instanceId === id)), true);
  assert.ok(state.playedArea.some(card => card.definitionId === 'miracle-04'));
  assert.equal(state.deck.length, beforeDeck - 3);
  const locations = listCardLocations(state);
  assert.equal(locations.length, Object.keys(state.cardRegistry).length);
  assert.equal(new Set(locations).size, locations.length);
});

test('於水中重生 is an effect draw and therefore triggers 在黎明前叩門', () => {
  let state = createGame({ seed: 'rebirth-dawn' });
  setHand(state, ['miracle-13', 'miracle-04', 'sheep-1', 'food-2', 'money-3']);
  state = play(state, 'miracle-13');
  state.currentPlayer = 'player-1';
  state = play(state, 'miracle-04');
  const human = state.players[0];
  assert.equal(human.hand.length, 4);
  assert.ok(state.pendingEffectDiscard);
  assert.equal(state.pendingEffectDiscard.playerId, 'player-1');
  assert.ok(human.effects.some(card => card.definitionId === 'miracle-13'));
});

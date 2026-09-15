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

function setDeckTop(state, ids) {
  const cards = ids.map(id => take(state, id));
  state.deck.unshift(...cards);
  return cards;
}

function deepAction(state) {
  state.currentPlayer = 'player-1';
  const card = state.players[0].hand.find(held => held.definitionId === 'miracle-07');
  return getNormalActions(state).find(action => action.instanceId === card.instanceId);
}

test('deep QA guarantees 行向水深之處 without fixing unrelated random cards', () => {
  const state = createGame({ seed: 'deep-preview' });
  assert.equal(state.players[0].hand.length, 7);
  assert.ok(state.players[0].hand.some(card => card.definitionId === 'miracle-07'));
});

test('行向水深之處 stops when 物資、神蹟、災難 have all appeared', () => {
  let state = createGame({ seed: 'deep-three-types' });
  const viewed = setDeckTop(state, ['sheep-1', 'food-2', 'miracle-01', 'disaster-01', 'money-9']);
  const action = deepAction(state);
  const selected = viewed[2];
  const bottomOrder = [viewed[3], viewed[1], viewed[0]];
  const beforeDeckLength = state.deck.length;
  const result = executeNormalAction(state, {
    ...action,
    selectedCardId: selected.instanceId,
    bottomOrderIds: bottomOrder.map(card => card.instanceId),
  });
  assert.equal(result.ok, true, result.reason);
  state = result.state;
  assert.ok(state.players[0].hand.some(card => card.instanceId === selected.instanceId));
  assert.deepEqual(state.deck.slice(-3).map(card => card.instanceId), bottomOrder.map(card => card.instanceId));
  assert.equal(state.deck.some(card => card.instanceId === viewed[4].instanceId), true);
  assert.equal(state.deck.length, beforeDeckLength - 1);
  assert.ok(state.playedArea.some(card => card.definitionId === 'miracle-07'));
  const locations = listCardLocations(state);
  assert.equal(locations.length, Object.keys(state.cardRegistry).length);
  assert.equal(new Set(locations).size, locations.length);
});

test('群羊、糧食、金錢、恩典全部只算同一種物資牌型', () => {
  let state = createGame({ seed: 'deep-five-cap' });
  const viewed = setDeckTop(state, ['sheep-1', 'food-2', 'money-3', 'grace-4', 'sheep-5', 'disaster-01']);
  const action = deepAction(state);
  const selected = viewed[4];
  const rest = viewed.slice(0,4).reverse();
  const result = executeNormalAction(state, {
    ...action,
    selectedCardId: selected.instanceId,
    bottomOrderIds: rest.map(card => card.instanceId),
  });
  assert.equal(result.ok, true, result.reason);
  state = result.state;
  assert.ok(state.players[0].hand.some(card => card.instanceId === selected.instanceId));
  assert.deepEqual(state.deck.slice(-4).map(card => card.instanceId), rest.map(card => card.instanceId));
  assert.equal(state.deck.some(card => card.instanceId === viewed[5].instanceId), true);
});

test('行向水深之處 rejects selecting a card outside the revealed set without mutating state', () => {
  const state = createGame({ seed: 'deep-invalid' });
  setDeckTop(state, ['sheep-1', 'miracle-01', 'disaster-01', 'food-9']);
  const action = deepAction(state);
  const outside = state.deck[3];
  const before = structuredClone(state);
  const result = executeNormalAction(state, {
    ...action,
    selectedCardId: outside.instanceId,
  });
  assert.equal(result.ok, false);
  assert.deepEqual(result.state, before);
});

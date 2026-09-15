import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, executeNormalAction, getNormalActions, listCardLocations } from '../src/game/game.js';

function take(state, definitionId) {
  for (const location of [state.deck, state.discardPile, state.playedArea, ...state.players.map((player) => player.hand), ...state.players.map((player) => player.effects)]) {
    const index = location.findIndex((card) => card.definitionId === definitionId);
    if (index >= 0) return location.splice(index, 1)[0];
  }
  throw new Error(`Missing ${definitionId}`);
}

function act(state, action) {
  const result = executeNormalAction(state, action);
  assert.equal(result.ok, true, result.reason);
  return result.state;
}

test('rain QA guarantees 雨幕之下 and an infected player without fixing unrelated cards', () => {
  const state = createGame({ seed: 'rain-preview' });
  assert.equal(state.players[0].hand.length, 7);
  assert.ok(state.players[0].hand.some((card) => card.definitionId === 'miracle-22'));
  assert.ok(state.players[1].effects.some((card) => card.definitionId === 'disaster-09'));
});

test('雨幕之下 can target any player', () => {
  const state = createGame({ seed: 'rain-targets' });
  state.currentPlayer = 'player-1';
  const card = state.players[0].hand.find((held) => held.definitionId === 'miracle-22');
  const actions = getNormalActions(state).filter((action) => action.instanceId === card.instanceId);
  assert.deepEqual(new Set(actions.map((action) => action.targetPlayerId)), new Set(['player-1', 'player-2', 'player-3', 'player-4']));
});

test('雨幕之下 removes a persistent disaster and rewards the cleansed player', () => {
  let state = createGame({ seed: 'rain-cleanse' });
  state.currentPlayer = 'player-1';
  const rain = state.players[0].hand.find((card) => card.definitionId === 'miracle-22');
  const plague = state.players[1].effects.find((card) => card.definitionId === 'disaster-09');
  const action = getNormalActions(state).find((candidate) => candidate.instanceId === rain.instanceId && candidate.targetPlayerId === 'player-2');
  state = act(state, { ...action, effectInstanceId: plague.instanceId });
  assert.equal(state.players[1].effects.some((card) => card.instanceId === plague.instanceId), false);
  assert.ok(state.discardPile.some((card) => card.instanceId === plague.instanceId));
  assert.equal(state.players[1].fire, 2);
  assert.ok(state.playedArea.some((card) => card.instanceId === rain.instanceId));
  assert.equal(state.currentResource, null);
  const locations = listCardLocations(state);
  assert.equal(locations.length, Object.keys(state.cardRegistry).length);
  assert.equal(new Set(locations).size, locations.length);
});

test('雨幕之下 may choose a player with no removable effect and then gives no fire', () => {
  let state = createGame({ seed: 'rain-empty' });
  state.currentPlayer = 'player-1';
  const rain = state.players[0].hand.find((card) => card.definitionId === 'miracle-22');
  const action = getNormalActions(state).find((candidate) => candidate.instanceId === rain.instanceId && candidate.targetPlayerId === 'player-3');
  state = act(state, action);
  assert.equal(state.players[2].fire, 0);
  assert.ok(state.playedArea.some((card) => card.instanceId === rain.instanceId));
});

test('雨幕之下 cannot cleanse a normal beneficial miracle effect', () => {
  const state = createGame({ seed: 'rain-invalid-effect' });
  state.currentPlayer = 'player-1';
  const wilderness = take(state, 'miracle-02');
  state.players[1].effects.push(wilderness);
  const rain = state.players[0].hand.find((card) => card.definitionId === 'miracle-22');
  const action = getNormalActions(state).find((candidate) => candidate.instanceId === rain.instanceId && candidate.targetPlayerId === 'player-2');
  const before = structuredClone(state);
  const result = executeNormalAction(state, { ...action, effectInstanceId: wilderness.instanceId });
  assert.equal(result.ok, false);
  assert.match(result.reason, /persistent disaster|negative effect/);
  assert.deepEqual(result.state, before);
});

test('灰與燼 reduces the 2 fire reward granted by 雨幕之下', () => {
  let state = createGame({ seed: 'rain-ashes' });
  state.currentPlayer = 'player-1';
  state.players[1].roundModifiers = { miracleFireReduction: 2 };
  const rain = state.players[0].hand.find((card) => card.definitionId === 'miracle-22');
  const plague = state.players[1].effects.find((card) => card.definitionId === 'disaster-09');
  const action = getNormalActions(state).find((candidate) => candidate.instanceId === rain.instanceId && candidate.targetPlayerId === 'player-2');
  state = act(state, { ...action, effectInstanceId: plague.instanceId });
  assert.equal(state.players[1].fire, 0);
  assert.equal(state.players[1].effects.some((card) => card.instanceId === plague.instanceId), false);
});

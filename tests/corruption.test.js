import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, executeNormalAction, getNormalActions, settleRound } from '../src/game/game.js';
import { DISASTER_CORRUPTION, getCardCorruption, MIRACLE_CORRUPTION } from '../src/game/corruption.js';
import { getDefinition } from '../src/game/cards.js';

function take(state, definitionId) {
  for (const location of [state.deck, ...state.players.map((player) => player.hand)]) {
    const index = location.findIndex((card) => card.definitionId === definitionId);
    if (index >= 0) return location.splice(index, 1)[0];
  }
  throw new Error(`Missing ${definitionId}`);
}

function putHumanHand(state, definitionId) {
  const human = state.players[0];
  state.deck.push(...human.hand);
  human.hand = [take(state, definitionId)];
  state.currentPlayer = human.playerId;
  return human;
}

function play(state, definitionId) {
  putHumanHand(state, definitionId);
  const action = getNormalActions(state).find((candidate) => candidate.instanceId === state.players[0].hand[0].instanceId);
  assert.ok(action, `Expected ${definitionId} to be playable`);
  const result = executeNormalAction(state, action);
  assert.equal(result.ok, true, result.reason);
  return result.state;
}

test('finalized corruption registries keep every miracle and disaster value', () => {
  assert.equal(Object.keys(MIRACLE_CORRUPTION).length, 24);
  assert.equal(Object.keys(DISASTER_CORRUPTION).length, 18);
  assert.equal(MIRACLE_CORRUPTION['劫後餘生'], 3);
  assert.equal(DISASTER_CORRUPTION['三分之一的星辰'], 4);
});

test('resource corruption uses printed number and Grace is always zero', () => {
  assert.equal(getCardCorruption(getDefinition({ definitionId: 'sheep-4' })), 0);
  assert.equal(getCardCorruption(getDefinition({ definitionId: 'food-5' })), 1);
  assert.equal(getCardCorruption(getDefinition({ definitionId: 'money-9' })), 1);
  assert.equal(getCardCorruption(getDefinition({ definitionId: 'grace-9' })), 0);
});

test('successful resource and special plays add their corruption once', () => {
  let state = play(createGame({ seed: 'corruption-resource' }), 'sheep-5');
  assert.equal(state.corruption, 1);

  state = play(createGame({ seed: 'corruption-miracle' }), 'miracle-01');
  assert.equal(state.corruption, 2);

  state = play(createGame({ seed: 'corruption-disaster' }), 'disaster-09');
  assert.equal(state.corruption, 4);
});

test('corruption is shared state and survives a round transition', () => {
  const state = createGame({ seed: 'corruption-persistence' });
  state.corruption = 7;
  state.players.forEach((player, index) => { player.hasNormalAction = index === 0; });
  const result = settleRound(state);
  assert.equal(result.ok, true, result.reason);
  assert.equal(result.state.corruption, 7);
});

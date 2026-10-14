import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, executeNormalAction, getNormalActions } from '../src/game/game.js';

function take(state, definitionId) {
  for (const location of [state.deck, ...state.players.map(player => player.hand), ...state.players.map(player => player.effects)]) {
    const index = location.findIndex(card => card.definitionId === definitionId);
    if (index >= 0) return location.splice(index, 1)[0];
  }
  throw new Error(`Missing ${definitionId}`);
}

function prepare(state, judgmentId, handIds) {
  const human = state.players[0];
  state.deck.push(...human.hand);
  human.hand = handIds.map(id => take(state, id));
  state.currentPlayer = human.playerId;
  state.currentResource = null;
  state.activeJudgments = { 戰局: judgmentId };
  return human;
}

test('爭戰 I lets each player discard one and draw one exactly once per round', () => {
  const state = createGame({ seed: 'war-tier-one' });
  const human = prepare(state, 'war-1', ['food-1', 'sheep-1']);
  const beforeDeck = state.deck.length;
  const action = getNormalActions(state).find(candidate => candidate.type === 'warDiscard');
  assert.deepEqual(action.discardInstanceIds.length, 1);
  const result = executeNormalAction(state, action);
  assert.equal(result.ok, true, result.reason);
  assert.equal(result.state.players[0].hand.length, 2);
  assert.equal(result.state.deck.length, beforeDeck - 1);
  assert.equal(result.state.discardPile.length, 1);
  assert.equal(result.state.players[0].lastWarRound, result.state.round);
  result.state.currentPlayer = 'player-1';
  assert.equal(getNormalActions(result.state).some(candidate => candidate.type === 'warDiscard'), false);
  assert.equal(human.hand.length, 2, 'input state stays immutable');
});

test('爭戰 II supports one or two discards and treats its draw as an effect draw', () => {
  const state = createGame({ seed: 'war-tier-two' });
  const human = prepare(state, 'war-2', ['food-1', 'sheep-1', 'money-1']);
  human.effects.push(take(state, 'miracle-13'));
  const action = getNormalActions(state).find(candidate => candidate.type === 'warDiscard' && candidate.discardInstanceIds.length === 2);
  assert.ok(action);
  const result = executeNormalAction(state, action);
  assert.equal(result.ok, true, result.reason);
  assert.equal(result.state.players[0].hand.length, 4, 'discard 2, draw 2, plus Dawn draw 1');
  assert.ok(result.state.pendingEffectDiscard, 'Dawn must resolve after the effect draw');
  const resolve = getNormalActions(result.state)[0];
  const resolved = executeNormalAction(result.state, resolve);
  assert.equal(resolved.ok, true, resolved.reason);
  assert.equal(resolved.state.players[0].hand.length, 3);
  assert.equal(resolved.state.players[0].effects.some(card => card.definitionId === 'miracle-13'), false);
});

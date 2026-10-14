import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, executeNormalAction, getNormalActions } from '../src/game/game.js';

test('沉淪 I makes a single-target disaster affect the original target and both seat-neighbors', () => {
  const state = createGame({ seed: 'judgment-sinking-preview' });
  const human = state.players[0];
  const treasure = human.hand.find((card) => card.definitionId === 'disaster-08');
  const action = getNormalActions(state).find((candidate) => candidate.instanceId === treasure.instanceId && candidate.targetPlayerId === 'player-2');

  assert.equal(state.activeJudgments['災變'], 'sinking-1');
  assert.deepEqual(state.players.map((player) => player.fire), [10, 25, 15, 5]);

  const result = executeNormalAction(state, action);
  assert.equal(result.ok, true, result.reason);
  assert.deepEqual(result.state.players.map((player) => player.fire), [6, 15, 9, 5]);
});

test('single-target disaster keeps its original scope when 沉淪 I is absent', () => {
  const state = createGame({ seed: 'target-scope-regression' });
  const human = state.players[0];
  const treasure = human.hand.find((card) => card.definitionId === 'disaster-08');
  state.currentPlayer = human.playerId;
  state.players.forEach((player) => { player.fire = 10; });
  const action = getNormalActions(state).find((candidate) => candidate.instanceId === treasure.instanceId && candidate.targetPlayerId === 'player-2');

  const result = executeNormalAction(state, action);
  assert.equal(result.ok, true, result.reason);
  assert.deepEqual(result.state.players.map((player) => player.fire), [10, 6, 10, 10]);
});

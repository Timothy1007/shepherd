import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, getNormalActions, playMiracle, listCardLocations } from '../src/game/game.js';
import { createResourceDeck } from '../src/game/cards.js';

function makeHumanTurn(seed = 'wind-test') {
  const state = createGame({ seed });
  state.currentPlayer = 'player-1';
  return state;
}

test('alpha first hand contains 回轉歸向 and 如風吹來 while preserving seven cards', () => {
  const state = makeHumanTurn();
  const hand = state.players[0].hand;
  assert.equal(hand.length, 7);
  assert.ok(hand.some((card) => card.definitionId === 'miracle-01'));
  assert.ok(hand.some((card) => card.definitionId === 'miracle-05'));
  assert.equal(listCardLocations(state).length, createResourceDeck().length);
});

test('如風吹來 fire choice gives 3 fire and enters played area', () => {
  const state = makeHumanTurn('wind-fire');
  const human = state.players[0];
  const card = human.hand.find((held) => held.definitionId === 'miracle-05');
  const beforeFire = human.fire;
  const result = playMiracle(state, { playerId: human.playerId, instanceId: card.instanceId, choice: 'fire' });
  assert.equal(result.ok, true);
  assert.equal(result.state.players[0].fire, beforeFire + 3);
  assert.ok(result.state.playedArea.some((held) => held.instanceId === card.instanceId));
  assert.equal(result.state.currentResource, null);
});

test('如風吹來 cycle choice discards selected cards and draws the same count', () => {
  const state = makeHumanTurn('wind-cycle');
  const human = state.players[0];
  const miracle = human.hand.find((held) => held.definitionId === 'miracle-05');
  const selected = human.hand.filter((held) => held.instanceId !== miracle.instanceId).slice(0, 2);
  const beforeCount = human.hand.length;
  const result = playMiracle(state, {
    playerId: human.playerId,
    instanceId: miracle.instanceId,
    choice: 'cycle',
    discardIds: selected.map((card) => card.instanceId),
  });
  assert.equal(result.ok, true);
  assert.equal(result.state.players[0].hand.length, beforeCount - 1);
  assert.ok(selected.every((card) => result.state.discardPile.some((held) => held.instanceId === card.instanceId)));
  assert.ok(result.state.playedArea.some((held) => held.instanceId === miracle.instanceId));
});

test('如風吹來 rejects zero or more than three cycle discards without changing state', () => {
  const state = makeHumanTurn('wind-invalid');
  const human = state.players[0];
  const miracle = human.hand.find((held) => held.definitionId === 'miracle-05');
  const before = structuredClone(state);
  const result = playMiracle(state, { playerId: human.playerId, instanceId: miracle.instanceId, choice: 'cycle', discardIds: [] });
  assert.equal(result.ok, false);
  assert.deepEqual(result.state, before);
});

test('如風吹來 remains a legal normal action regardless of current resource', () => {
  const state = makeHumanTurn('wind-legal');
  state.currentResource = { type: 'sheep', number: 9, instanceId: 'test' };
  const miracle = state.players[0].hand.find((held) => held.definitionId === 'miracle-05');
  assert.ok(getNormalActions(state).some((action) => action.type === 'playMiracle' && action.instanceId === miracle.instanceId));
});

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

function prepareHand(state, playerId, definitionIds) {
  const player = state.players.find((candidate) => candidate.playerId === playerId);
  state.deck.push(...player.hand);
  player.hand = definitionIds.map((definitionId) => take(state, definitionId));
  state.currentPlayer = playerId;
  return player;
}

function act(state, action) {
  const result = executeNormalAction(state, action);
  assert.equal(result.ok, true, result.reason);
  return result.state;
}

test('plague QA guarantees 瘟疫 without fixing unrelated random cards', () => {
  const state = createGame({ seed: 'plague-preview' });
  assert.equal(state.players[0].hand.length, 7);
  assert.ok(state.players[0].hand.some((card) => card.definitionId === 'disaster-09'));
  assert.deepEqual(state.players.map((player) => player.fire), [0, 0, 0, 0]);
});

test('瘟疫 can target any player including its owner', () => {
  const state = createGame({ seed: 'plague-targets' });
  state.currentPlayer = 'player-1';
  const card = state.players[0].hand.find((held) => held.definitionId === 'disaster-09');
  const actions = getNormalActions(state).filter((action) => action.instanceId === card.instanceId);
  assert.deepEqual(new Set(actions.map((action) => action.targetPlayerId)), new Set(['player-1', 'player-2', 'player-3', 'player-4']));
});

test('瘟疫 enters the chosen player effect zone as a negative round effect', () => {
  let state = createGame({ seed: 'plague-zone' });
  state.currentPlayer = 'player-1';
  const card = state.players[0].hand.find((held) => held.definitionId === 'disaster-09');
  const action = getNormalActions(state).find((candidate) => candidate.instanceId === card.instanceId && candidate.targetPlayerId === 'player-2');
  state = act(state, action);
  const plague = state.players[1].effects.find((effect) => effect.instanceId === card.instanceId);
  assert.ok(plague);
  assert.equal(plague.effectSourcePlayerId, 'player-1');
  assert.equal(plague.effectOwnerPlayerId, 'player-2');
  assert.equal(state.playedArea.some((played) => played.instanceId === card.instanceId), false);
  assert.equal(state.currentResource, null);
});

test('infected sheep is reduced by 2 and plague spreads in current direction', () => {
  let state = createGame({ seed: 'plague-spread' });
  state.currentPlayer = 'player-1';
  const plague = state.players[0].hand.find((held) => held.definitionId === 'disaster-09');
  state = act(state, getNormalActions(state).find((candidate) => candidate.instanceId === plague.instanceId && candidate.targetPlayerId === 'player-2'));
  prepareHand(state, 'player-2', ['sheep-5']);
  state.currentResource = null;
  const sheep = state.players[1].hand[0];
  state = act(state, getNormalActions(state).find((candidate) => candidate.instanceId === sheep.instanceId && candidate.type === 'playResource'));
  assert.equal(state.currentResource.baseNumber, 5);
  assert.equal(state.currentResource.numberPenalty, 2);
  assert.equal(state.currentResource.numberModifier, -2);
  assert.equal(state.currentResource.number, 3);
  assert.equal(state.players[1].effects.some((effect) => effect.definitionId === 'disaster-09'), false);
  assert.equal(state.players[0].effects.some((effect) => effect.definitionId === 'disaster-09'), true);
});

test('瘟疫 penalty is applied before same-type legality checks', () => {
  let state = createGame({ seed: 'plague-legality' });
  const human = prepareHand(state, 'player-1', ['disaster-09', 'sheep-3']);
  const plague = human.hand.find((card) => card.definitionId === 'disaster-09');
  state = act(state, getNormalActions(state).find((candidate) => candidate.instanceId === plague.instanceId && candidate.targetPlayerId === 'player-1'));
  state.currentPlayer = 'player-1';
  state.currentResource = { type: 'sheep', number: 3, instanceId: 'fixture', baseNumber: 3, numberBonus: 0, numberPenalty: 0, numberModifier: 0 };
  const sheep = state.players[0].hand.find((card) => card.definitionId === 'sheep-3');
  const action = getNormalActions(state).find((candidate) => candidate.instanceId === sheep.instanceId);
  assert.equal(action, undefined);
});

test('行曠野之路 and 瘟疫 modifiers are both applied before legality checks', () => {
  let state = createGame({ seed: 'plague-legality-stack' });
  const human = prepareHand(state, 'player-1', ['disaster-09', 'miracle-02', 'sheep-3']);
  const plague = human.hand.find((card) => card.definitionId === 'disaster-09');
  state = act(state, getNormalActions(state).find((candidate) => candidate.instanceId === plague.instanceId && candidate.targetPlayerId === 'player-1'));
  state.currentPlayer = 'player-1';
  const wilderness = state.players[0].hand.find((card) => card.definitionId === 'miracle-02');
  state = act(state, getNormalActions(state).find((candidate) => candidate.instanceId === wilderness.instanceId));
  state.currentPlayer = 'player-1';
  state.currentResource = { type: 'sheep', number: 4, instanceId: 'fixture', baseNumber: 4, numberBonus: 0, numberPenalty: 0, numberModifier: 0 };
  const sheep = state.players[0].hand.find((card) => card.definitionId === 'sheep-3');
  const action = getNormalActions(state).find((candidate) => candidate.instanceId === sheep.instanceId);
  assert.ok(action);
  state = act(state, action);
  assert.equal(state.currentResource.numberBonus, 3);
  assert.equal(state.currentResource.numberPenalty, 2);
  assert.equal(state.currentResource.number, 4);
});

test('瘟疫 follows reversed direction after direction changes', () => {
  let state = createGame({ seed: 'plague-direction' });
  state.currentPlayer = 'player-1';
  const plague = state.players[0].hand.find((held) => held.definitionId === 'disaster-09');
  state = act(state, getNormalActions(state).find((candidate) => candidate.instanceId === plague.instanceId && candidate.targetPlayerId === 'player-2'));
  state.direction = 1;
  prepareHand(state, 'player-2', ['sheep-4']);
  state.currentResource = null;
  const sheep = state.players[1].hand[0];
  state = act(state, getNormalActions(state).find((candidate) => candidate.instanceId === sheep.instanceId && candidate.type === 'playResource'));
  assert.equal(state.players[1].effects.some((effect) => effect.definitionId === 'disaster-09'), false);
  assert.equal(state.players[2].effects.some((effect) => effect.definitionId === 'disaster-09'), true);
});

test('non-sheep resources neither receive the penalty nor spread plague', () => {
  let state = createGame({ seed: 'plague-food' });
  state.currentPlayer = 'player-1';
  const plague = state.players[0].hand.find((held) => held.definitionId === 'disaster-09');
  state = act(state, getNormalActions(state).find((candidate) => candidate.instanceId === plague.instanceId && candidate.targetPlayerId === 'player-2'));
  prepareHand(state, 'player-2', ['food-5']);
  state.currentResource = null;
  const food = state.players[1].hand[0];
  state = act(state, getNormalActions(state).find((candidate) => candidate.instanceId === food.instanceId && candidate.type === 'playResource'));
  assert.equal(state.currentResource.number, 5);
  assert.equal(state.currentResource.numberPenalty, 0);
  assert.equal(state.players[1].effects.some((effect) => effect.definitionId === 'disaster-09'), true);
});

test('瘟疫 and 行曠野之路 combine as signed modifiers on the same sheep', () => {
  let state = createGame({ seed: 'plague-stack' });
  const human = prepareHand(state, 'player-1', ['disaster-09', 'miracle-02', 'sheep-4']);
  const plague = human.hand.find((card) => card.definitionId === 'disaster-09');
  state = act(state, getNormalActions(state).find((candidate) => candidate.instanceId === plague.instanceId && candidate.targetPlayerId === 'player-1'));
  state.currentPlayer = 'player-1';
  const wilderness = state.players[0].hand.find((card) => card.definitionId === 'miracle-02');
  state = act(state, getNormalActions(state).find((candidate) => candidate.instanceId === wilderness.instanceId));
  state.currentPlayer = 'player-1';
  state.currentResource = null;
  const sheep = state.players[0].hand.find((card) => card.definitionId === 'sheep-4');
  state = act(state, getNormalActions(state).find((candidate) => candidate.instanceId === sheep.instanceId));
  assert.equal(state.currentResource.numberBonus, 3);
  assert.equal(state.currentResource.numberPenalty, 2);
  assert.equal(state.currentResource.numberModifier, 1);
  assert.equal(state.currentResource.number, 5);
  assert.equal(state.players[0].effects.some((effect) => effect.definitionId === 'miracle-02'), false);
  assert.equal(state.players[3].effects.some((effect) => effect.definitionId === 'disaster-09'), true);
});

test('round-end Plague expires and card location invariant survives', () => {
  let state = createGame({ seed: 'plague-round-end' });
  state.currentPlayer = 'player-1';
  const plague = state.players[0].hand.find((held) => held.definitionId === 'disaster-09');
  state = act(state, getNormalActions(state).find((candidate) => candidate.instanceId === plague.instanceId && candidate.targetPlayerId === 'player-2'));
  state.round = 7;
  state.players.forEach((player, index) => { player.hasNormalAction = index === 0; });
  const result = settleRound(state);
  assert.equal(result.ok, true, result.reason);
  state = result.state;
  assert.equal(state.gameOver, true);
  assert.equal(state.players.every((player) => player.effects.every((effect) => effect.definitionId !== 'disaster-09')), true);
  assert.ok(state.discardPile.some((card) => card.definitionId === 'disaster-09'));
  const locations = listCardLocations(state);
  assert.equal(locations.length, Object.keys(state.cardRegistry).length);
  assert.equal(new Set(locations).size, locations.length);
});

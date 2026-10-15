import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, executeNormalAction, getNormalActions, MISSION_TEMPLATES, settleRound } from '../src/game/game.js';

const resourceTypes = ['sheep', 'food', 'money'];

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
}

function setWideMission(player) {
  player.mission = { sheep: 20, food: 20, money: 20 };
  player.missionProgress = { sheep: 0, food: 0, money: 0 };
}

test('rounds one through six issue a public mission from a balanced configurable template', () => {
  const state = createGame({ seed: 'mission-template' });
  for (const player of state.players) {
    assert.deepEqual(Object.keys(player.mission).sort(), [...resourceTypes].sort());
    const assigned = JSON.stringify(Object.values(player.mission).sort((a, b) => a - b));
    assert.ok(MISSION_TEMPLATES.some((template) => JSON.stringify([...template].sort((a, b) => a - b)) === assigned));
    assert.deepEqual(player.missionProgress, { sheep: 0, food: 0, money: 0 });
    assert.equal(player.completedMissionCount, 0);
  }
});

test('a resource contributes its printed number, not its modified effective number, to its mission', () => {
  let state = createGame({ seed: 'mission-printed-number' });
  prepareHuman(state, ['miracle-02', 'sheep-8']);
  setWideMission(state.players[0]);
  let action = getNormalActions(state).find((candidate) => candidate.type === 'playMiracle');
  let result = executeNormalAction(state, action);
  assert.equal(result.ok, true, result.reason);
  state = result.state;
  state.currentPlayer = 'player-1';
  action = getNormalActions(state).find((candidate) => candidate.type === 'playResource');
  result = executeNormalAction(state, action);
  assert.equal(result.ok, true, result.reason);
  assert.equal(result.state.currentResource.number, 11);
  assert.equal(result.state.players[0].missionProgress.sheep, 8);
});

test('Grace contributes its printed number to the declared resource mission', () => {
  const state = createGame({ seed: 'mission-grace' });
  prepareHuman(state, ['grace-1']);
  setWideMission(state.players[0]);
  const action = getNormalActions(state).find((candidate) => candidate.type === 'playResource' && candidate.declaredType === 'money');
  const result = executeNormalAction(state, action);
  assert.equal(result.ok, true, result.reason);
  assert.equal(result.state.players[0].missionProgress.money, 1);
  assert.equal(result.state.players[0].missionProgress.sheep, 0);
});

test('settling a completed mission records it once, preserves a round record, then resets progress for the next mission', () => {
  let state = createGame({ seed: 'mission-settlement' });
  const player = state.players[0];
  player.mission = { sheep: 1, food: 1, money: 1 };
  player.missionProgress = { sheep: 1, food: 1, money: 1 };
  state.players.forEach((candidate, index) => { candidate.hasNormalAction = index === 0; });
  let result = settleRound(state);
  assert.equal(result.ok, true, result.reason);
  state = result.state;
  assert.equal(state.round, 2);
  assert.equal(state.players[0].completedMissionCount, 1);
  assert.equal(state.lastRoundResult.missions[0].completed, true);
  assert.deepEqual(state.lastRoundResult.missions[0].progress, { sheep: 1, food: 1, money: 1 });
  assert.deepEqual(state.players[0].missionProgress, { sheep: 0, food: 0, money: 0 });
  assert.ok(state.players[0].mission);
});

test('round seven has no general mission and does not award another completion', () => {
  let state = createGame({ seed: 'mission-final-round' });
  state.round = 6;
  state.players[0].completedMissionCount = 2;
  state.players.forEach((player, index) => { player.hasNormalAction = index === 0; });
  const result = settleRound(state);
  assert.equal(result.ok, true, result.reason);
  state = result.state;
  assert.equal(state.round, 7);
  assert.equal(state.players[0].mission, null);
  assert.equal(state.players[0].completedMissionCount, 2);
});

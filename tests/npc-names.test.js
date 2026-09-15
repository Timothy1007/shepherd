import test from 'node:test';
import assert from 'node:assert/strict';
import { GameController } from '../src/controller/game-controller.js';
import { getNormalActions } from '../src/game/game.js';

function makeController() {
  return new GameController({
    render: () => {},
    setTimer: () => 1,
    clearTimer: () => {},
    delay: () => 0,
  });
}

test('AI opponents receive three unique world NPC names for the current round', () => {
  const controller = makeController();
  const state = controller.start('npc-name-preview');
  const names = state.players.filter((player) => player.type === 'ai').map((player) => player.displayName);
  assert.equal(names.length, 3);
  assert.equal(new Set(names).size, 3);
  assert.ok(names.every((name) => typeof name === 'string' && name.length >= 2));
  assert.equal(state.npcNameRound, state.round);
});

test('NPC names reroll when the round changes without changing gameplay RNG', () => {
  const controller = makeController();
  const state = controller.start('npc-name-rounds');
  const firstNames = state.players.filter((player) => player.type === 'ai').map((player) => player.displayName);
  const rngBefore = state.rngState;
  state.round += 1;
  state.npcNameRound = state.round - 1;
  controller.prepare();
  const secondNames = state.players.filter((player) => player.type === 'ai').map((player) => player.displayName);
  assert.equal(new Set(secondNames).size, 3);
  assert.notDeepEqual(secondNames, firstNames);
  assert.equal(state.rngState, rngBefore);
  assert.equal(state.npcNameRound, state.round);
});

test('AI action log uses the NPC name instead of Player seat labels', () => {
  const controller = makeController();
  const state = controller.start('npc-log-preview');
  const ai = state.players.find((player) => player.type === 'ai');
  state.currentPlayer = ai.playerId;
  const action = getNormalActions(state)[0];
  assert.ok(action);
  const result = controller.act(action, controller.snapshot());
  assert.equal(result.ok, true, result.reason);
  assert.ok(controller.aiLog.some((line) => line.includes(ai.displayName)));
  assert.equal(controller.aiLog.some((line) => line.includes(`P${ai.seat} `)), false);
});

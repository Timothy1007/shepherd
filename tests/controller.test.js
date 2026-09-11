import test from 'node:test';
import assert from 'node:assert/strict';
import { GameController } from '../src/controller/game-controller.js';

test('empty action is explicitly rejected', () => {
  const controller = new GameController();
  controller.start(1);
  const result = controller.act(null);
  assert.equal(result.ok, false);
});

test('expired action token cannot change state', () => {
  const controller = new GameController();
  controller.start(2);
  const token = controller.snapshot();
  controller.actionToken += 1;
  const before = controller.state;
  const result = controller.act({}, token);
  assert.equal(result.ok, false);
  assert.equal(controller.state, before);
});

test('restart invalidates an old AI timer', () => {
  const callbacks = [];
  const controller = new GameController({
    setTimer(callback) { callbacks.push(callback); return callbacks.length; },
    clearTimer() {},
    delay: () => 0,
  });
  controller.start(3);
  controller.state.currentPlayer = 'player-2';
  controller.prepare();
  const oldCallback = callbacks.at(-1);
  controller.restart(4);
  const expected = structuredClone(controller.state);
  oldCallback();
  assert.deepEqual(controller.state, expected);
});

test('roundEnd automatically advances to the next round', () => {
  const callbacks = [];
  const controller = new GameController({
    setTimer(callback) { callbacks.push(callback); return callbacks.length; },
    clearTimer() {},
    delay: () => 0,
  });
  controller.start(5);
  controller.state.phase = 'roundEnd';
  controller.state.currentPlayer = null;
  controller.state.lastRoundResult = { round: 1, apostle: 'player-2', reward: 4 };
  controller.prepare();
  const transition = callbacks.at(-1);
  assert.equal(typeof transition, 'function');
  transition();
  assert.equal(controller.state.phase, 'playing');
  assert.equal(controller.state.round, 2);
});

test('restart invalidates a pending round transition', () => {
  const callbacks = [];
  const controller = new GameController({
    setTimer(callback) { callbacks.push(callback); return callbacks.length; },
    clearTimer() {},
    delay: () => 0,
  });
  controller.start(6);
  controller.state.phase = 'roundEnd';
  controller.state.currentPlayer = null;
  controller.prepare();
  const oldTransition = callbacks.at(-1);
  controller.restart(7);
  const expected = structuredClone(controller.state);
  oldTransition();
  assert.deepEqual(controller.state, expected);
});

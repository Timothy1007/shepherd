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

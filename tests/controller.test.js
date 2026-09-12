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

test('AI turn schedules and executes from a prepared state', () => {
  const callbacks = [];
  const controller = new GameController({
    setTimer(callback) { callbacks.push(callback); return callbacks.length; },
    clearTimer() {},
    delay: () => 0,
  });
  controller.start(5);
  controller.state.currentPlayer = 'player-2';
  const beforeHand = controller.state.players[1].hand.length;
  controller.prepare();
  const callback = callbacks.at(-1);
  assert.equal(typeof callback, 'function');
  callback();
  assert.ok(controller.state.players[1].hand.length <= beforeHand);
  assert.notEqual(controller.state.currentPlayer, null);
});

test('AI redraw resolves immediately into play or withdrawal without timer gap', () => {
  const callbacks = [];
  const controller = new GameController({
    setTimer(callback) { callbacks.push(callback); return callbacks.length; },
    clearTimer() {},
    delay: () => 0,
  });
  controller.start(6);
  const ai = controller.state.players[1];
  controller.state.currentPlayer = ai.playerId;
  controller.state.currentResource = { type: 'sheep', number: 9 };
  ai.hasRedrawnThisRound = false;
  ai.hand = ai.hand.filter((card) => card.definitionId.startsWith('sheep-') && Number(card.definitionId.split('-')[1]) < 9).slice(0, 1);
  if (!ai.hand.length) {
    const source = controller.state.deck.findIndex((card) => card.definitionId === 'sheep-1');
    ai.hand = [controller.state.deck.splice(source, 1)[0]];
  }
  controller.prepare();
  const callback = callbacks.at(-1);
  assert.equal(typeof callback, 'function');
  callback();
  assert.ok(controller.state.currentPlayer !== ai.playerId || ai.hasRedrawnThisRound || controller.state.round > 1);
});

test('presentation render failure does not prevent AI scheduling', () => {
  const callbacks = [];
  const controller = new GameController({
    render() { throw new Error('presentation exploded'); },
    setTimer(callback) { callbacks.push(callback); return callbacks.length; },
    clearTimer() {},
    delay: () => 0,
  });
  controller.start('recovery-preview');
  assert.ok(controller.lastRenderError instanceof Error);
  assert.equal(typeof callbacks.at(-1), 'function');
  const before = controller.state.currentPlayer;
  callbacks.at(-1)();
  assert.notEqual(controller.state.currentPlayer, before);
});

test('restart still replaces game state when presentation render fails', () => {
  const controller = new GameController({
    render() { throw new Error('presentation exploded'); },
    setTimer() { return 1; },
    clearTimer() {},
    delay: () => 0,
  });
  controller.start(20);
  const beforeGeneration = controller.generation;
  const beforeState = controller.state;
  controller.restart(21);
  assert.equal(controller.generation, beforeGeneration + 1);
  assert.notEqual(controller.state, beforeState);
});

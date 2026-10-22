import test from 'node:test';
import assert from 'node:assert/strict';
import { GameController } from '../src/controller/game-controller.js';
import { getNormalActions } from '../src/game/game.js';

test('AI effect choices resolve without turn delays, then the next normal turn keeps its delay', () => {
  const queue = [];
  const controller = new GameController({ setTimer(fn, delay) { queue.push({ fn, delay }); return queue.length; }, clearTimer() {}, delay: () => 2500 });
  controller.start('fruit-test');
  const action = getNormalActions(controller.state).find(a => a.instanceId === 'disaster-04#qa' && a.targetPlayerId === 'player-4');
  assert.ok(action);
  assert.equal(controller.act(action).ok, true);
  assert.ok(controller.aiLog.some(line => line.includes('效果結算中')));
  let steps = 0;
  while (controller.state.pendingFruit && steps++ < 10) {
    const timer = queue.at(-1);
    assert.equal(timer.delay, 0);
    timer.fn();
  }
  assert.equal(controller.state.pendingFruit, null);
  assert.equal(controller.state.players[3].hand.length, 1);
  assert.equal(controller.state.players[3].fire, 10);
  if (controller.state.players.find(p => p.playerId === controller.state.currentPlayer)?.type === 'ai') assert.equal(queue.at(-1).delay, 2500);
});

test('human effect choices wait for the player', () => {
  const queue = [];
  const controller = new GameController({ setTimer(fn, delay) { queue.push({ fn, delay }); return queue.length; }, clearTimer() {}, delay: () => 2500 });
  controller.start('fruit-test');
  const ai = controller.state.players[3];
  ai.type = 'human';
  const action = getNormalActions(controller.state).find(a => a.instanceId === 'disaster-04#qa' && a.targetPlayerId === ai.playerId);
  const before = queue.length;
  assert.equal(controller.act(action).ok, true);
  assert.equal(controller.state.currentPlayer, ai.playerId);
  assert.equal(controller.state.pendingFruit.stage, 'discard');
  assert.equal(queue.length, before);
});

import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, executeNormalAction, getNormalActions } from '../src/game/game.js';

test('揭露 I 對重新整備的正常抽牌建立公開紀錄，並切換至揭露 II', () => {
  let state = createGame({ seed: 'judgment-revelation-preview' });
  assert.equal(state.activeJudgments['資訊'], 'revelation-1');
  const redraw = getNormalActions(state).find(action => action.type === 'redraw');
  assert.ok(redraw);
  state = executeNormalAction(state, redraw).state;
  assert.equal(state.round, 2);
  assert.equal(state.activeJudgments['資訊'], 'revelation-2');
  assert.equal(state.lastRevelation.playerId, 'player-1');
  assert.equal(state.lastRevelation.source, 'redraw');
  assert.equal(state.lastRevelation.cardIds.length, 7);
});

test('揭露 II 保留所有玩家的完整手牌供介面公開呈現', () => {
  let state = createGame({ seed: 'judgment-revelation-preview' });
  state = executeNormalAction(state, getNormalActions(state).find(action => action.type === 'redraw')).state;
  assert.equal(state.activeJudgments['資訊'], 'revelation-2');
  assert.deepEqual(state.players.map(player => player.hand.length), [2, 2, 2, 2]);
  assert.deepEqual(state.players[1].hand.map(card => card.definitionId), ['food-3', 'disaster-07']);
});

import test from 'node:test';
import assert from 'node:assert/strict';
import { silverTransferChoices, completeSilverTransfer, SILVER_TRANSFER_SOURCE } from '../src/game/bloodied-silver.js';

const fixture = () => ({ players: [
  { playerId: 'a', fire: 5, effects: [{ definitionId: 'disaster-10' }] },
  { playerId: 'b', fire: 3, effects: [{ definitionId: 'disaster-10' }] },
  { playerId: 'c', fire: 0, effects: [] },
] });

test('持有效果者獲得火種後，可指定其他玩家轉移1點', () => {
  const state = fixture(), event = { playerId: 'a', amount: 5, source: 'miracle' };
  assert.deepEqual(silverTransferChoices(state, event), ['b', 'c']);
  const result = completeSilverTransfer(state, event, 'b');
  assert.equal(result.ok, true);
  assert.equal(result.state.players[0].fire, 4);
  assert.equal(result.state.players[1].fire, 4);
  assert.equal(state.players[0].fire, 5);
});

test('銀幣轉移得到的火種不再觸發另一個銀幣', () => {
  const result = completeSilverTransfer(fixture(), { playerId: 'a', amount: 1, source: 'judgment' }, 'b');
  assert.equal(result.gainEvent.source, SILVER_TRANSFER_SOURCE);
  assert.deepEqual(silverTransferChoices(result.state, result.gainEvent), []);
});

test('不能選自己，無實際收益或效果已清除時不觸發', () => {
  const state = fixture();
  assert.equal(completeSilverTransfer(state, { playerId: 'a', amount: 1, source: 'miracle' }, 'a').ok, false);
  assert.deepEqual(silverTransferChoices(state, { playerId: 'a', amount: 0, source: 'miracle' }), []);
  state.players[0].effects = [];
  assert.deepEqual(silverTransferChoices(state, { playerId: 'a', amount: 3, source: 'miracle' }), []);
});

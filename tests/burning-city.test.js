import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, executeNormalAction, getNormalActions } from '../src/game/game.js';

function act(state, action) {
  const result = executeNormalAction(state, action);
  assert.equal(result.ok, true, result.reason);
  return result.state;
}

test('焚城之火可以指定任兩名其他玩家，不受手牌數量限制', () => {
  const state = createGame({ seed: 'burning-city-test' });
  const card = state.players[0].hand.find((held) => held.definitionId === 'disaster-15');
  const actions = getNormalActions(state).filter((action) => action.instanceId === card.instanceId);
  assert.equal(actions.length, 3);
  assert.deepEqual(new Set(actions.map((action) => action.targetPlayerIds.join(','))), new Set([
    'player-2,player-3', 'player-2,player-4', 'player-3,player-4',
  ]));
});

test('焚城之火讓被選玩家自行棄置效果，沒有可棄置效果則失去6火種', () => {
  let state = createGame({ seed: 'burning-city-test' });
  const city = state.players[0].hand.find((card) => card.definitionId === 'disaster-15');
  const play = getNormalActions(state).find((action) => action.instanceId === city.instanceId && action.targetPlayerIds.join(',') === 'player-2,player-3');
  state = act(state, play);

  assert.equal(state.playedArea.some((card) => card.instanceId === city.instanceId), true);
  assert.equal(state.pendingBurningCity?.targetPlayerIds.join(','), 'player-2,player-3');
  assert.equal(state.currentPlayer, 'player-2');

  const choices = getNormalActions(state);
  assert.equal(choices.length, 2);
  state = act(state, choices.find((action) => action.effectInstanceId === state.players[1].effects[1].instanceId));

  assert.equal(state.players[1].effects.length, 1);
  assert.equal(state.discardPile.some((card) => card.definitionId === 'miracle-13'), true);
  assert.equal(state.players[2].fire, 4);
  assert.equal(state.pendingBurningCity, null);
});

test('焚城之火拒絕包含自己或不足兩名的目標', () => {
  const state = createGame({ seed: 'burning-city-test' });
  const city = state.players[0].hand.find((card) => card.definitionId === 'disaster-15');
  const invalid = executeNormalAction(state, {
    type: 'playMiracle', playerId: 'player-1', instanceId: city.instanceId, targetPlayerIds: ['player-1', 'player-2'],
  });
  assert.equal(invalid.ok, false);
  assert.match(invalid.reason, /stale or illegal/i);
});

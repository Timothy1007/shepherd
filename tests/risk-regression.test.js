import test from 'node:test';
import assert from 'node:assert/strict';
import { simulate } from '../scripts/simulate.js';
import { createGame, executeNormalAction, getNormalActions, listCardLocations } from '../src/game/game.js';

// Verify every intermediate state, including suspended reactions and effect choices.
for (let i = 0; i < 12; i++) {
  test(`完整對局每步維持牌位置唯一：risk-${i}`, () => {
    const { state, actions } = simulate(`risk-${i}`, {
      preferReaction: i % 2 === 1,
      observe: ({ nextState, action }) => {
        const locations = listCardLocations(nextState);
        assert.equal(locations.length, new Set(locations).size, `${action.type}: duplicated card`);
      },
    });
    assert.equal(state.gameOver, true);
    assert.equal(state.round, 7);
    assert.ok(actions < 5000);
  });
}

test('星辰停止後，下一次因神蹟抽到死亡才進入死亡輪', () => {
  let state = createGame({ seed: 'stars-test' });
  const stars = getNormalActions(state).find(a => a.instanceId === 'disaster-17#1');
  assert.ok(stars);
  let result = executeNormalAction(state, stars);
  assert.equal(result.ok, true, result.reason);
  state = result.state;
  assert.equal(state.death.triggered, false);
  assert.equal(state.deck[0].definitionId, 'death');
  const owner = state.players.find(p => p.playerId === state.currentPlayer);
  let card;
  for (const zone of [state.deck, state.discardPile, ...state.players.map(p => p.hand)]) {
    const i = zone.findIndex(c => c.definitionId === 'miracle-01');
    if (i >= 0) { card = zone.splice(i, 1)[0]; break; }
  }
  assert.ok(card);
  owner.hand.push(card);
  const action = getNormalActions(state).find(a => a.instanceId === card.instanceId);
  assert.ok(action);
  result = executeNormalAction(state, action);
  assert.equal(result.ok, true, result.reason);
  assert.equal(result.state.death.triggered, true);
  assert.equal(result.state.death.revealerId, owner.playerId);
  assert.equal(result.state.phase, 'deathRound');
});

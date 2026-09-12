import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, settleRound } from '../src/game/game.js';

test('seventh round awards the final apostle before computing final ranking', () => {
  const state = createGame({ seed: 'final-settlement' });
  state.round = 7;
  state.currentApostle = 'player-1';
  state.players[0].fire = 8;
  state.players[1].fire = 10;
  state.players[2].fire = 7;
  state.players[3].fire = 6;
  state.playedArea = state.deck.splice(0, 3);
  state.players.forEach((player, index) => { player.hasNormalAction = index === 0; });

  const result = settleRound(state);
  assert.equal(result.ok, true);
  const next = result.state;

  assert.equal(next.lastRoundResult.round, 7);
  assert.equal(next.lastRoundResult.apostle, 'player-1');
  assert.equal(next.lastRoundResult.reward, 3);
  assert.equal(next.players[0].fire, 11);
  assert.deepEqual(next.winner, ['player-1']);
  assert.equal(next.phase, 'gameOver');
  assert.equal(next.gameOver, true);
});

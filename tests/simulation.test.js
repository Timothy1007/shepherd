import test from 'node:test';
import assert from 'node:assert/strict';
import { simulate } from '../scripts/simulate.js';
import { listCardLocations } from '../src/game/game.js';
import { createResourceDeck } from '../src/game/cards.js';

const expectedCardCount = createResourceDeck().length;

for (const seed of ['alpha', 'bravo', 'charlie', 'delta', 'echo', 'recovery-preview']) {
  test(`complete seven-round simulation: ${seed}`, () => {
    const { state, actions } = simulate(seed);
    assert.equal(state.gameOver, true);
    assert.equal(state.round, 7);
    assert.ok(state.winner.length >= 1);
    assert.ok(actions < 5000);
    assert.equal(listCardLocations(state).length, expectedCardCount);
  });
}

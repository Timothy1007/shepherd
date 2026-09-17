import test from 'node:test';
import assert from 'node:assert/strict';

import {
  CARD_TYPES,
  DIRECTIONS,
  ROUND_PHASES,
  activateJudgment,
  addCorruption,
  addResourceMissionProgress,
  beginRound,
  buildLockedDeathOrder,
  consumeJudgmentThreshold,
  createBaseJudgmentDeck,
  createGameState,
  drawNormalCards,
  getActiveJudgments,
  markDeathTriggered,
  recordSuccessfulResourcePlay,
} from '../src/game/index.js';

test('mission progress uses printed resource number', () => {
  const state = createGameState({ playerIds: ['A', 'B'] });
  addResourceMissionProgress(state, 'A', {
    type: CARD_TYPES.RESOURCE,
    resourceType: 'sheep',
    printedNumber: 5,
    effectiveNumber: 9,
  });
  assert.equal(state.players.A.missionProgress.sheep, 5);
});

test('Death does not consume normal draw quota', () => {
  const deck = [
    { id: 'n1', type: CARD_TYPES.RESOURCE },
    { id: 'death', type: CARD_TYPES.DEATH },
    { id: 'n2', type: CARD_TYPES.MIRACLE },
  ];
  const result = drawNormalCards(deck, 2);
  assert.deepEqual(result.drawn.map((card) => card.id), ['n1', 'n2']);
  assert.equal(result.deathTriggered, true);
});

test('Death round order starts from next seat and locks current direction', () => {
  assert.deepEqual(
    buildLockedDeathOrder(['A', 'B', 'C', 'D'], 'B', DIRECTIONS.CLOCKWISE),
    ['C', 'D', 'A', 'B'],
  );
  assert.deepEqual(
    buildLockedDeathOrder(['A', 'B', 'C', 'D'], 'B', DIRECTIONS.COUNTER_CLOCKWISE),
    ['A', 'D', 'C', 'B'],
  );
});

test('only successful resource play updates Apostle', () => {
  const state = createGameState({ playerIds: ['A', 'B'] });
  beginRound(state, 1);
  recordSuccessfulResourcePlay(state, 'B', { id: 's5', type: CARD_TYPES.RESOURCE, resourceType: 'sheep', printedNumber: 5 });
  assert.equal(state.currentApostle, 'B');
});

test('Death trigger moves state to pending before the locked final circle', () => {
  const state = createGameState({ playerIds: ['A', 'B', 'C', 'D'] });
  beginRound(state, 2);
  markDeathTriggered(state, 'C');
  assert.equal(state.phase, ROUND_PHASES.DEATH_PENDING);
  assert.deepEqual(state.death.finalOrder, ['D', 'A', 'B', 'C']);
});

test('corruption threshold remains configurable and preserves overflow', () => {
  const state = createGameState({ playerIds: ['A', 'B'], corruptionThreshold: 20 });
  addCorruption(state, 23, 'test-card');
  assert.equal(consumeJudgmentThreshold(state), true);
  assert.equal(state.corruption.value, 3);
});

test('judgments persist by domain and same-domain judgment replaces previous', () => {
  const state = createGameState({ playerIds: ['A', 'B'] });
  state.judgments.deck = createBaseJudgmentDeck();
  activateJudgment(state, 'blindness-1');
  activateJudgment(state, 'revelation-1');
  const active = getActiveJudgments(state);
  assert.equal(active.length, 1);
  assert.equal(active[0].id, 'revelation-1');
});

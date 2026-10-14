import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, executeNormalAction, getNormalActions } from '../src/game/game.js';
import { getDefinition } from '../src/game/cards.js';

function takeCard(state, definitionId) {
  const index = state.deck.findIndex((card) => card.definitionId === definitionId);
  assert.notEqual(index, -1, `${definitionId} must be available for the fixture`);
  return state.deck.splice(index, 1)[0];
}

function blindnessPendingFixture(definitionId, { targetHands = false } = {}) {
  const state = createGame({ seed: `blindness-choice-${definitionId}` });
  state.deck.push(...state.players.flatMap((player) => player.hand));
  for (const player of state.players) player.hand = [];
  const human = state.players[0];
  human.hand.push(takeCard(state, definitionId), takeCard(state, 'sheep-1'));
  if (targetHands) state.players.slice(1).forEach((player, index) => player.hand.push(takeCard(state, `food-${index + 1}`)));
  state.activeJudgments = { 資訊: 'blindness-1' };
  state.currentPlayer = human.playerId;
  const pile = getNormalActions(state).find((action) => action.type === 'playBlindnessPile' && action.kind === getDefinition(human.hand[0]).kind);
  return executeNormalAction(state, pile).state;
}

test('蒙蔽 I offers one directly playable covered pile for each special-card type', () => {
  const state = createGame({ seed: 'judgment-blindness-preview' });
  const actions = getNormalActions(state);

  assert.equal(state.activeJudgments['資訊'], 'blindness-1');
  assert.deepEqual(actions.filter((action) => action.type === 'playBlindnessPile').map((action) => action.kind).sort(), ['disaster', 'miracle']);
  assert.equal(actions.some((action) => action.type === 'playMiracle'), false);
});

test('蒙蔽 II combines miracles and disasters into one unknown-function action', () => {
  let state = createGame({ seed: 'judgment-blindness-preview' });
  const firstChoice = getNormalActions(state).find((action) => action.type === 'playBlindnessPile' && action.kind === 'miracle');
  state = executeNormalAction(state, firstChoice).state;
  const revealed = getNormalActions(state).find((action) => action.type === 'playMiracle');
  state = executeNormalAction(state, revealed).state;

  assert.equal(state.round, 2);
  assert.equal(state.activeJudgments['資訊'], 'blindness-2');
  assert.deepEqual(getNormalActions(state).filter((action) => action.type === 'playBlindnessPile').map((action) => action.kind), ['unknown']);
});

test('蒙蔽 I 將新抽到的功能牌自動歸回對應的覆蓋牌堆', () => {
  let state = createGame({ seed: 'judgment-blindness-preview' });
  const firstChoice = getNormalActions(state).find((action) => action.type === 'playBlindnessPile' && action.kind === 'miracle');
  state.qaSeed = 'blindness-draw-inspection';
  state = executeNormalAction(state, firstChoice).state;
  const revealed = getNormalActions(state).find((action) => action.type === 'playMiracle');
  state = executeNormalAction(state, revealed).state;
  const human = state.players[0];
  assert.equal(human.hand.filter((card) => getDefinition(card).kind === 'miracle').length, 1);
  assert.equal(human.hand.filter((card) => getDefinition(card).kind === 'disaster').length, 3);
  assert.ok(human.hand.some((card) => getDefinition(card).definitionId === 'disaster-09'));

  state.currentPlayer = human.playerId;
  human.hasNormalAction = true;
  assert.ok(getNormalActions(state).some((action) => action.type === 'playBlindnessPile' && action.kind === 'disaster'));
});

test('蒙蔽翻出的目標型功能牌保留原本的選擇行動，不會被預設值直接結算', () => {
  const cases = [
    { id: 'disaster-08', targetHands: false, field: 'targetPlayerId', count: 3 },
    { id: 'miracle-10', targetHands: true, field: 'targetPlayerId', count: 3 },
    { id: 'disaster-02', targetHands: true, field: 'targetPlayerIds', count: 3 },
    { id: 'disaster-09', targetHands: false, field: 'targetPlayerId', count: 4 },
    { id: 'miracle-22', targetHands: false, field: 'targetPlayerId', count: 4 },
  ];

  for (const scenario of cases) {
    const state = blindnessPendingFixture(scenario.id, scenario);
    const actions = getNormalActions(state).filter((action) => action.type === 'playMiracle');
    assert.equal(actions.length, scenario.count, scenario.id);
    assert.ok(actions.every((action) => action[scenario.field]), scenario.id);
    assert.ok(state.players[0].hand.some((card) => card.definitionId === scenario.id), `${scenario.id} remains pending until its choice is resolved`);
  }
});

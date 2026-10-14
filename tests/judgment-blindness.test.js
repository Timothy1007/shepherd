import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, executeNormalAction, getNormalActions } from '../src/game/game.js';
import { getDefinition } from '../src/game/cards.js';

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

  assert.equal(state.round, 2);
  assert.equal(state.activeJudgments['資訊'], 'blindness-2');
  assert.deepEqual(getNormalActions(state).filter((action) => action.type === 'playBlindnessPile').map((action) => action.kind), ['unknown']);
});

test('蒙蔽 I 將新抽到的功能牌自動歸回對應的覆蓋牌堆', () => {
  let state = createGame({ seed: 'judgment-blindness-preview' });
  const firstChoice = getNormalActions(state).find((action) => action.type === 'playBlindnessPile' && action.kind === 'miracle');
  state.qaSeed = 'blindness-draw-inspection';
  state = executeNormalAction(state, firstChoice).state;
  const human = state.players[0];
  assert.ok(human.hand.some((card) => getDefinition(card).definitionId === 'disaster-09'));

  state.currentPlayer = human.playerId;
  human.hasNormalAction = true;
  assert.ok(getNormalActions(state).some((action) => action.type === 'playBlindnessPile' && action.kind === 'disaster'));
});

import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, executeNormalAction, getNormalActions } from '../src/game/game.js';
import { getDefinition } from '../src/game/cards.js';

test('蒙蔽 I only offers a separate random choice for each available special-card type', () => {
  const state = createGame({ seed: 'judgment-blindness-preview' });
  const actions = getNormalActions(state);

  assert.equal(state.activeJudgments['資訊'], 'blindness-1');
  assert.deepEqual(actions.filter((action) => action.type === 'blindnessChooseKind').map((action) => action.kind).sort(), ['disaster', 'miracle']);
  assert.equal(actions.some((action) => action.type === 'playMiracle'), false);

  const chosen = executeNormalAction(state, actions.find((action) => action.kind === 'miracle'));
  assert.equal(chosen.ok, true, chosen.reason);
  assert.equal(chosen.state.pendingBlindness.kind, 'miracle');
  assert.equal(getDefinition(chosen.state.players[0].hand.find((card) => card.instanceId === chosen.state.pendingBlindness.instanceId)).kind, 'miracle');
});

test('蒙蔽 II combines miracles and disasters into one unknown-function action', () => {
  let state = createGame({ seed: 'judgment-blindness-preview' });
  const firstChoice = getNormalActions(state).find((action) => action.type === 'blindnessChooseKind' && action.kind === 'miracle');
  state = executeNormalAction(state, firstChoice).state;
  const revealed = getNormalActions(state).find((action) => action.type === 'playMiracle');
  state = executeNormalAction(state, revealed).state;

  assert.equal(state.round, 2);
  assert.equal(state.activeJudgments['資訊'], 'blindness-2');
  assert.deepEqual(getNormalActions(state).filter((action) => action.type === 'blindnessChooseKind').map((action) => action.kind), ['unknown']);
});

test('蒙蔽 I 將新抽到的功能牌自動歸回對應的覆蓋牌堆', () => {
  let state = createGame({ seed: 'judgment-blindness-preview' });
  const firstChoice = getNormalActions(state).find((action) => action.type === 'blindnessChooseKind' && action.kind === 'miracle');
  state = executeNormalAction(state, firstChoice).state;
  assert.equal(getDefinition(state.players[0].hand.find((card) => card.instanceId === state.pendingBlindness.instanceId)).definitionId, 'miracle-01');

  // 保持在第一階，觀察《如風吹來》抽出的《瘟疫》是否回到災難覆蓋牌堆。
  state.qaSeed = 'blindness-draw-inspection';
  state = executeNormalAction(state, getNormalActions(state).find((action) => action.type === 'playMiracle')).state;
  const human = state.players[0];
  assert.ok(human.hand.some((card) => getDefinition(card).definitionId === 'disaster-09'));

  state.currentPlayer = human.playerId;
  human.hasNormalAction = true;
  assert.ok(getNormalActions(state).some((action) => action.type === 'blindnessChooseKind' && action.kind === 'disaster'));
});

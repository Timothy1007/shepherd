import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, executeNormalAction, getNormalActions, listCardLocations } from '../src/game/game.js';

function take(state, definitionId) {
  for (const location of [state.deck, state.discardPile, state.playedArea, ...state.players.map(player => player.hand), ...state.players.map(player => player.effects)]) {
    const index = location.findIndex(card => card.definitionId === definitionId);
    if (index >= 0) return location.splice(index, 1)[0];
  }
  throw new Error(`Missing ${definitionId}`);
}

function playFromHuman(state, definitionId, extra = {}) {
  state.currentPlayer = 'player-1';
  const card = state.players[0].hand.find(held => held.definitionId === definitionId);
  const action = getNormalActions(state).find(candidate => candidate.instanceId === card.instanceId);
  const result = executeNormalAction(state, { ...action, ...extra });
  assert.equal(result.ok, true, result.reason);
  return result.state;
}

test('dawn QA guarantees 在黎明前叩門 and a draw-effect trigger card', () => {
  const state = createGame({ seed: 'dawn-preview' });
  assert.equal(state.players[0].hand.length, 7);
  assert.ok(state.players[0].hand.some(card => card.definitionId === 'miracle-13'));
  assert.ok(state.players[0].hand.some(card => card.definitionId === 'miracle-01'));
});

test('在黎明前叩門 enters effect zone, adds one effect draw, then requires one discard and removes itself', () => {
  let state = createGame({ seed: 'dawn-trigger' });
  state = playFromHuman(state, 'miracle-13');
  const dawn = state.players[0].effects.find(card => card.definitionId === 'miracle-13');
  assert.ok(dawn);
  const before = state.players[0].hand.length;

  state = playFromHuman(state, 'miracle-01');
  assert.ok(state.pendingEffectDiscard);
  assert.equal(state.pendingEffectDiscard.playerId, 'player-1');
  assert.equal(state.players[0].hand.length, before - 1 + 2);
  assert.ok(state.players[0].effects.some(card => card.definitionId === 'miracle-13'));

  const discardAction = getNormalActions(state)[0];
  assert.equal(discardAction.type, 'resolveEffectDiscard');
  const result = executeNormalAction(state, discardAction);
  assert.equal(result.ok, true, result.reason);
  state = result.state;
  assert.equal(state.pendingEffectDiscard, null);
  assert.equal(state.players[0].hand.length, before);
  assert.equal(state.players[0].effects.some(card => card.definitionId === 'miracle-13'), false);
  assert.ok(state.discardPile.some(card => card.instanceId === dawn.instanceId));
  const locations = listCardLocations(state);
  assert.equal(locations.length, Object.keys(state.cardRegistry).length);
  assert.equal(new Set(locations).size, locations.length);
});

test('redraw is not an effect draw and does not consume 在黎明前叩門', () => {
  let state = createGame({ seed: 'dawn-redraw' });
  state = playFromHuman(state, 'miracle-13');
  const player = state.players[0];
  state.deck.push(...player.hand);
  player.hand = [take(state, 'sheep-1')];
  state.currentPlayer = 'player-1';
  state.currentResource = { type: 'sheep', number: 9, instanceId: 'qa-current', baseNumber: 9, numberBonus: 0, numberPenalty: 0, numberModifier: 0 };
  const redraw = getNormalActions(state).find(action => action.type === 'redraw');
  assert.ok(redraw);
  const result = executeNormalAction(state, redraw);
  assert.equal(result.ok, true, result.reason);
  state = result.state;
  assert.equal(state.pendingEffectDiscard, null);
  assert.ok(state.players[0].effects.some(card => card.definitionId === 'miracle-13'));
  assert.equal(state.players[0].hand.length, 1);
});

test('adding a card to hand through 所望之實底 is not treated as drawing', () => {
  let state = createGame({ seed: 'dawn-insight' });
  state = playFromHuman(state, 'miracle-13');
  const human = state.players[0];
  if (!human.hand.some(card => card.definitionId === 'miracle-06')) human.hand.push(take(state, 'miracle-06'));
  state.currentPlayer = 'player-1';
  const hope = human.hand.find(card => card.definitionId === 'miracle-06');
  const top = state.deck.slice(0,3);
  const action = getNormalActions(state).find(candidate => candidate.instanceId === hope.instanceId);
  const result = executeNormalAction(state, { ...action, selectedCardId: top[0].instanceId, bottomOrderIds: top.slice(1).map(card => card.instanceId) });
  assert.equal(result.ok, true, result.reason);
  state = result.state;
  assert.equal(state.pendingEffectDiscard, null);
  assert.ok(state.players[0].effects.some(card => card.definitionId === 'miracle-13'));
});

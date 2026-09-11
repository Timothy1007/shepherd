import test from 'node:test';
import assert from 'node:assert/strict';
import { advanceToNextRound, createGame, executeNormalAction, getNormalActions, listCardLocations, settleRound } from '../src/game/game.js';
import { DEFINITION_BY_ID } from '../src/game/cards.js';

function take(state, definitionId, source = state.deck) {
  let card;
  for (const location of [source, state.deck, state.discardPile, state.playedArea, ...state.players.map((player) => player.hand)]) {
    const index = location.findIndex((candidate) => candidate.definitionId === definitionId);
    if (index >= 0) { card = location.splice(index, 1)[0]; break; }
  }
  if (!card) throw new Error(`Missing ${definitionId}`);
  return card;
}

function putHand(state, playerIndex, definitions) {
  state.deck.push(...state.players[playerIndex].hand);
  state.players[playerIndex].hand = definitions.map((definition) => take(state, definition));
  state.currentPlayer = state.players[playerIndex].playerId;
  return state.players[playerIndex];
}

function act(state, action) {
  const result = executeNormalAction(state, action);
  assert.equal(result.ok, true, result.reason);
  return result.state;
}

function advanceRound(state) {
  assert.equal(state.phase, 'roundEnd');
  const result = advanceToNextRound(state);
  assert.equal(result.ok, true, result.reason);
  return result.state;
}

test('new game deals seven cards to every player and preserves 90 locations', () => {
  const state = createGame({ seed: 1 });
  assert.deepEqual(state.players.map((player) => player.hand.length), [7, 7, 7, 7]);
  assert.equal(listCardLocations(state).length, 90);
  assert.equal(new Set(listCardLocations(state)).size, 90);
});

test('same seed reproduces rolls, deck, hands, and RNG state', () => {
  assert.deepEqual(createGame({ seed: 'repeatable' }), createGame({ seed: 'repeatable' }));
});

test('starting roll ties reroll only tied contenders', () => {
  let found;
  for (let seed = 1; seed < 5000; seed += 1) {
    const state = createGame({ seed });
    if (state.startingRolls.length > 1) { found = state; break; }
  }
  assert.ok(found);
  const first = found.startingRolls[0];
  const highest = Math.max(...first.map((roll) => roll.value));
  const tied = first.filter((roll) => roll.value === highest).map((roll) => roll.playerId).sort();
  assert.deepEqual(found.startingRolls[1].map((roll) => roll.playerId).sort(), tied);
});

test('successful resource play updates resource and apostle without mutating input', () => {
  const original = createGame({ seed: 2 });
  putHand(original, 0, ['money-1']);
  const before = structuredClone(original);
  const action = getNormalActions(original)[0];
  const result = executeNormalAction(original, action);
  assert.equal(result.ok, true);
  assert.deepEqual(original, before);
  assert.equal(result.state.currentResource.type, 'money');
  assert.equal(result.state.currentApostle, 'player-1');
});

test('grace declaration belongs to played instance and not its definition', () => {
  let state = createGame({ seed: 3 });
  putHand(state, 0, ['grace-4']);
  const action = getNormalActions(state).find((candidate) => candidate.declaredType === 'food');
  state = act(state, action);
  assert.equal(state.playedArea[0].declaredType, 'food');
  assert.equal(DEFINITION_BY_ID.get('grace-4').type, 'grace');
});

test('illegal or stale action returns original state', () => {
  const state = createGame({ seed: 4 });
  const result = executeNormalAction(state, { type: 'playResource', playerId: state.currentPlayer, instanceId: 'not-real', declaredType: 'sheep' });
  assert.equal(result.ok, false);
  assert.equal(result.state, state);
});

test('redraw discards the hand, draws the same count, and remains the same turn', () => {
  const state = createGame({ seed: 5 });
  const player = putHand(state, 0, ['sheep-1', 'sheep-2']);
  state.currentResource = { type: 'sheep', number: 9 };
  const oldIds = player.hand.map((card) => card.instanceId);
  const next = act(state, getNormalActions(state)[0]);
  assert.equal(next.currentPlayer, 'player-1');
  assert.equal(next.players[0].hasRedrawnThisRound, true);
  assert.equal(next.players[0].hand.length, 2);
  assert.ok(oldIds.every((id) => next.discardPile.some((card) => card.instanceId === id)));
});

test('redraw recycles discard when deck is insufficient and never playedArea', () => {
  const state = createGame({ seed: 6 });
  const player = putHand(state, 0, ['sheep-1', 'sheep-2', 'sheep-3']);
  state.currentResource = { type: 'sheep', number: 9 };
  const played = take(state, 'money-9');
  state.playedArea.push(played);
  state.discardPile.push(...state.deck.splice(1));
  const playedId = played.instanceId;
  const next = act(state, getNormalActions(state)[0]);
  assert.equal(next.players[0].hand.length, 3);
  assert.ok(next.playedArea.some((card) => card.instanceId === playedId));
  assert.ok(!next.players[0].hand.some((card) => card.instanceId === playedId));
});

test('redraw with a legal result requires an immediate play on the same turn', () => {
  const state = createGame({ seed: 7 });
  putHand(state, 0, ['sheep-1']);
  state.currentResource = { type: 'sheep', number: 9 };
  state.deck = [take(state, 'money-1'), ...state.deck];
  const redrawn = act(state, getNormalActions(state)[0]);
  assert.equal(redrawn.currentPlayer, 'player-1');
  assert.deepEqual([...new Set(getNormalActions(redrawn).map((action) => action.type))], ['playResource']);
});

test('redraw with no legal result exposes endParticipation', () => {
  const state = createGame({ seed: 8 });
  putHand(state, 0, ['sheep-1']);
  state.currentResource = { type: 'sheep', number: 9 };
  state.deck = [take(state, 'sheep-2'), ...state.deck];
  const redrawn = act(state, getNormalActions(state)[0]);
  assert.equal(getNormalActions(redrawn)[0].type, 'endParticipation');
});

test('endParticipation permanently removes normal action for the round', () => {
  let state = createGame({ seed: 9 });
  putHand(state, 0, ['sheep-1']);
  state.currentResource = { type: 'sheep', number: 9 };
  state.players[0].hasRedrawnThisRound = true;
  state = act(state, getNormalActions(state)[0]);
  assert.equal(state.players[0].hasNormalAction, false);
});

test('empty hand exposes endEmptyHand without redraw', () => {
  let state = createGame({ seed: 10 });
  putHand(state, 0, []);
  assert.deepEqual(getNormalActions(state).map((action) => action.type), ['endEmptyHand']);
  state = act(state, getNormalActions(state)[0]);
  assert.equal(state.players[0].hasNormalAction, false);
});

test('ending with one eligible player enters roundEnd before starting next round', () => {
  let state = createGame({ seed: 11 });
  putHand(state, 0, []);
  state.players[2].hasNormalAction = false;
  state.players[3].hasNormalAction = false;
  state = act(state, getNormalActions(state)[0]);
  assert.equal(state.round, 1);
  assert.equal(state.phase, 'roundEnd');
  assert.equal(state.currentPlayer, null);
  state = advanceRound(state);
  assert.equal(state.round, 2);
  assert.equal(state.phase, 'playing');
});

test('redraw and ineligibility do not change the apostle', () => {
  let state = createGame({ seed: 12 });
  state.currentApostle = 'player-4';
  putHand(state, 0, ['sheep-1']);
  state.currentResource = { type: 'sheep', number: 9 };
  state = act(state, getNormalActions(state)[0]);
  assert.equal(state.currentApostle, 'player-4');
});

test('round reward counts every playedArea card', () => {
  const state = createGame({ seed: 13 });
  state.currentApostle = 'player-2';
  state.playedArea.push(...state.deck.splice(0, 4));
  state.players[1].hasNormalAction = true;
  state.players[0].hasNormalAction = false;
  state.players[2].hasNormalAction = false;
  state.players[3].hasNormalAction = false;
  const result = settleRound(state);
  assert.equal(result.ok, true);
  assert.equal(result.state.players[1].fire, 4);
  assert.equal(result.state.lastRoundResult.reward, 4);
  assert.equal(result.state.phase, 'roundEnd');
});

test('losing streak increments, resets for apostle, and caps at three', () => {
  const state = createGame({ seed: 14 });
  state.currentApostle = 'player-1';
  state.players[0].losingStreak = 2;
  state.players[1].losingStreak = 3;
  state.players.forEach((player, index) => { player.hasNormalAction = index === 0; });
  const next = settleRound(state).state;
  assert.equal(next.players[0].losingStreak, 0);
  assert.equal(next.players[1].losingStreak, 3);
  assert.equal(next.players[1].startingHandBonus, 3);
});

test('next round deals seven through ten cards from losing streak after roundEnd', () => {
  const state = createGame({ seed: 15 });
  state.players.forEach((player, index) => {
    player.losingStreak = index;
    player.startingHandBonus = index;
    player.hasNormalAction = index === 0;
  });
  const ended = settleRound(state).state;
  const next = advanceRound(ended);
  assert.deepEqual(next.players.map((player) => player.hand.length), [8, 9, 10, 10]);
});

test('starting player rotates counterclockwise independently after roundEnd', () => {
  const state = createGame({ seed: 16 });
  const previousIndex = state.players.findIndex((player) => player.playerId === state.startingPlayer);
  state.players.forEach((player, index) => { player.hasNormalAction = index === 0; });
  const ended = settleRound(state).state;
  const next = advanceRound(ended);
  const expected = state.players[(previousIndex - 1 + 4) % 4].playerId;
  assert.equal(next.startingPlayer, expected);
  assert.equal(next.direction, -1);
});

test('seventh round ends without creating round eight and permits tied winners', () => {
  const state = createGame({ seed: 17 });
  state.round = 7;
  state.players[0].fire = 8;
  state.players[1].fire = 8;
  state.players.forEach((player, index) => { player.hasNormalAction = index === 0; });
  const next = settleRound(state).state;
  assert.equal(next.round, 7);
  assert.equal(next.phase, 'gameOver');
  assert.equal(next.gameOver, true);
  assert.deepEqual(next.winner, ['player-1', 'player-2']);
});

test('card location invariant is preserved through actions and round transitions', () => {
  let state = createGame({ seed: 18 });
  for (let index = 0; index < 30 && !state.gameOver; index += 1) {
    if (state.phase === 'roundEnd') state = advanceRound(state);
    else state = act(state, getNormalActions(state)[0]);
    const locations = listCardLocations(state);
    assert.equal(locations.length, 90);
    assert.equal(new Set(locations).size, 90);
    assert.ok(locations.every((id) => state.cardRegistry[id]));
  }
});

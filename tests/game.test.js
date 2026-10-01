import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, executeNormalAction, getNormalActions, listCardLocations, settleRound } from '../src/game/game.js';
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

function playJudgmentThresholdResource(state, definitionId = 'sheep-5') {
  const player = putHand(state, 0, [definitionId]);
  state.currentPlayer = player.playerId;
  state.currentResource = null;
  const action = getNormalActions(state).find((candidate) => candidate.type === 'playResource');
  assert.ok(action, 'expected a legal resource action');
  return act(state, action);
}

test('corruption threshold reveals one unknown judgment and preserves overflow', () => {
  const state = createGame({ seed: 'judgment-threshold' });
  state.corruption = 29;
  state.judgmentDeck = ['war-1'];
  const next = playJudgmentThresholdResource(state);
  assert.equal(next.corruptionThreshold, 30);
  assert.equal(next.corruption, 0);
  assert.equal(next.judgmentHistory.length, 1);
  assert.equal(next.lastJudgment.judgmentId, 'war-1');
  assert.equal(next.activeJudgments['戰局'], 'war-1');
  assert.ok(next.judgmentDeck.includes('war-2'));
});

test('new judgment in the same domain replaces the previous active judgment', () => {
  let state = createGame({ seed: 'judgment-replacement' });
  state.corruption = 29;
  state.judgmentDeck = ['blindness-1'];
  state = playJudgmentThresholdResource(state, 'food-5');
  assert.equal(state.activeJudgments['資訊'], 'blindness-1');
  state.corruption = 29;
  state.judgmentDeck = ['revelation-1'];
  state = playJudgmentThresholdResource(state, 'money-5');
  assert.equal(state.activeJudgments['資訊'], 'revelation-1');
  assert.equal(state.lastJudgment.replacedJudgmentId, 'blindness-1');
  assert.equal(state.judgmentHistory.length, 2);
});

test('失序 I reverses only the resource dominance relation', () => {
  let state = createGame({ seed: 'disorder-effect' });
  const player = putHand(state, 0, ['food-2']);
  state.currentPlayer = player.playerId;
  state.currentResource = { type: 'sheep', number: 6, instanceId: 'fixture' };
  assert.equal(getNormalActions(state).some((action) => action.type === 'playResource'), false);
  state.activeJudgments = { 秩序: 'disorder-1' };
  const action = getNormalActions(state).find((candidate) => candidate.type === 'playResource');
  assert.ok(action);
  state = act(state, action);
  assert.equal(state.currentResource.type, 'food');
  assert.equal(state.currentResource.number, 2);
});

test('失序 II also reverses same-resource number comparison', () => {
  let state = createGame({ seed: 'disorder-two-effect' });
  const player = putHand(state, 0, ['sheep-4']);
  state.currentPlayer = player.playerId;
  state.currentResource = { type: 'sheep', number: 6, instanceId: 'fixture' };
  state.activeJudgments = { 秩序: 'disorder-1' };
  assert.equal(getNormalActions(state).some((action) => action.type === 'playResource'), false);
  state.activeJudgments = { 秩序: 'disorder-2' };
  const action = getNormalActions(state).find((candidate) => candidate.type === 'playResource');
  assert.ok(action);
  state = act(state, action);
  assert.equal(state.currentResource.type, 'sheep');
  assert.equal(state.currentResource.number, 4);
});

test('judgment disorder QA starts with a visible active judgment and a legal reversed counter', () => {
  const state = createGame({ seed: 'judgment-disorder-preview' });
  assert.equal(state.currentPlayer, 'player-1');
  assert.equal(state.activeJudgments['秩序'], 'disorder-1');
  assert.equal(state.currentResource.type, 'sheep');
  assert.equal(state.currentResource.number, 6);
  const foodTwo = state.players[0].hand.find((card) => card.definitionId === 'food-2');
  assert.ok(foodTwo);
  assert.ok(getNormalActions(state).some((action) => action.instanceId === foodTwo.instanceId && action.type === 'playResource'));
  assert.equal(new Set(listCardLocations(state)).size, Object.keys(state.cardRegistry).length);
  assert.equal(listCardLocations(state).length, Object.keys(state.cardRegistry).length);
});

test('judgment disorder II QA guarantees a lower same-type resource as its proof card', () => {
  const state = createGame({ seed: 'judgment-disorder-2-preview' });
  assert.equal(state.activeJudgments['秩序'], 'disorder-2');
  const sheepFour = state.players[0].hand.find((card) => card.definitionId === 'sheep-4');
  assert.ok(sheepFour);
  assert.ok(getNormalActions(state).some((action) => action.instanceId === sheepFour.instanceId && action.type === 'playResource'));
});

test('傾覆 I caps an existing fire-loss effect at three, while 傾覆 II fixes it at one', () => {
  for (const [judgmentId, expectedLoss] of [['overturn-1', 3], ['overturn-2', 1]]) {
    let state = createGame({ seed: `overturn-${judgmentId}` });
    const player = putHand(state, 0, ['disaster-08']);
    state.players[1].fire = 30;
    state.activeJudgments = { 異變: judgmentId };
    const action = getNormalActions(state).find((candidate) => candidate.type === 'playMiracle' && candidate.targetPlayerId === 'player-2');
    assert.ok(action);
    state = act(state, action);
    assert.equal(state.players[1].fire, 30 - expectedLoss);
  }
});

test('傾覆 QA supplies distinguishable tier-I and tier-II manual test cards', () => {
  let state = createGame({ seed: 'judgment-overturn-preview' });
  assert.equal(state.round, 1);
  assert.equal(state.activeJudgments['異變'], 'overturn-1');
  const treasure = state.players[0].hand.find((card) => card.definitionId === 'disaster-08');
  const rebirth = state.players[0].hand.find((card) => card.definitionId === 'miracle-04');
  assert.ok(treasure);
  assert.ok(rebirth);
  assert.equal(state.players[1].fire, 25);
  let action = getNormalActions(state).find((candidate) => candidate.instanceId === treasure.instanceId && candidate.targetPlayerId === 'player-2');
  state = act(state, action);
  assert.equal(state.players[1].fire, 22, 'tier I must visibly cap a 10-fire loss at 3');

  state.players.forEach((player) => { player.hasNormalAction = false; });
  const roundTwo = settleRound(state);
  assert.equal(roundTwo.ok, true, roundTwo.reason);
  state = roundTwo.state;
  assert.equal(state.round, 2);
  assert.equal(state.corruption, 0);
  assert.equal(state.activeJudgments['異變'], 'overturn-2');
  assert.equal(state.lastJudgment.replacedJudgmentId, 'overturn-1');
  const wind = state.players[0].hand.find((card) => card.definitionId === 'miracle-05');
  const ashes = state.players[0].hand.find((card) => card.definitionId === 'disaster-07');
  const ark = state.players[0].hand.find((card) => card.definitionId === 'disaster-01');
  assert.ok(wind);
  assert.ok(ashes);
  assert.ok(ark);
  const discardIds = state.players[0].hand.filter((card) => card.instanceId !== wind.instanceId).slice(0, 3).map((card) => card.instanceId);
  action = getNormalActions(state).find((candidate) => candidate.instanceId === wind.instanceId);
  state = act(state, { ...action, choice: 'cycle', discardIds });
  assert.equal(discardIds.filter((id) => state.discardPile.some((card) => card.instanceId === id)).length, 1, 'tier II only permits one discard/draw change');
  assert.equal(new Set(listCardLocations(state)).size, Object.keys(state.cardRegistry).length);
  assert.equal(listCardLocations(state).length, Object.keys(state.cardRegistry).length);
});

test('分裂 QA makes tier I exclude the fire gainer and tier II permit the fire gainer', () => {
  let state = createGame({ seed: 'judgment-division-preview' });
  assert.equal(state.round, 1);
  assert.equal(state.activeJudgments['資源'], 'division-1');
  assert.deepEqual(state.players.map((player) => player.fire), [0, 1, 1, 4]);
  let action = getNormalActions(state).find((candidate) => candidate.type === 'divisionQaGain');
  assert.ok(action);
  state = act(state, action);
  assert.equal(state.players[0].fire, 5);
  assert.deepEqual(getNormalActions(state).map((candidate) => candidate.recipientPlayerId), ['player-2', 'player-3']);
  action = getNormalActions(state).find((candidate) => candidate.recipientPlayerId === 'player-2');
  state = act(state, action);
  assert.equal(state.players[1].fire, 3);

  state.players.forEach((player) => { player.hasNormalAction = false; });
  const roundTwo = settleRound(state);
  assert.equal(roundTwo.ok, true, roundTwo.reason);
  state = roundTwo.state;
  assert.equal(state.round, 2);
  assert.equal(state.activeJudgments['資源'], 'division-2');
  assert.deepEqual(state.players.map((player) => player.fire), [0, 5, 5, 5]);
  action = getNormalActions(state).find((candidate) => candidate.type === 'divisionQaGain');
  state = act(state, action);
  assert.deepEqual(getNormalActions(state).map((candidate) => candidate.recipientPlayerId), ['player-1', 'player-2', 'player-3', 'player-4']);
  action = getNormalActions(state).find((candidate) => candidate.recipientPlayerId === 'player-1');
  state = act(state, action);
  assert.equal(state.players[0].fire, 8, 'tier II permits the original fire gainer to receive the extra 3 fire');
});

test('傾覆 II limits multi-card and multi-fire effects across every implemented V1 resolver', () => {
  for (const [cardId, expectedHandLoss] of [['disaster-01', 1]]) {
    let state = createGame({ seed: `overturn-${cardId}` });
    const player = putHand(state, 0, [cardId, 'sheep-1', 'food-1', 'money-1']);
    state.activeJudgments = { 異變: 'overturn-2' };
    const before = player.hand.length;
    const action = getNormalActions(state).find((candidate) => candidate.instanceId === player.hand.find((card) => card.definitionId === cardId).instanceId);
    state = act(state, action);
    assert.equal(before - state.players[0].hand.length, 1 + expectedHandLoss, `${cardId} should only discard one additional hand card`);
  }
  let ashes = createGame({ seed: 'overturn-ashes' });
  const ashesPlayer = putHand(ashes, 0, ['disaster-07']);
  ashes.players.forEach((player) => { player.fire = 3; });
  ashes.activeJudgments = { 異變: 'overturn-2' };
  ashes = act(ashes, getNormalActions(ashes).find((candidate) => candidate.instanceId === ashesPlayer.hand[0].instanceId));
  assert.deepEqual(ashes.players.map((player) => player.fire), [2, 2, 2, 2]);
});

test('傾覆 limits rebirth, bird exchange, rain cover, and victory fire gains', () => {
  let rebirth = createGame({ seed: 'overturn-rebirth' });
  const rebirthPlayer = putHand(rebirth, 0, ['miracle-04', 'sheep-1', 'food-1', 'money-1', 'sheep-2', 'food-2', 'money-2']);
  const originalHand = rebirthPlayer.hand.slice(1).map((card) => card.instanceId);
  rebirth.activeJudgments = { 異變: 'overturn-1' };
  rebirth = act(rebirth, getNormalActions(rebirth).find((candidate) => candidate.instanceId === rebirthPlayer.hand[0].instanceId));
  assert.equal(originalHand.filter((id) => rebirth.discardPile.some((card) => card.instanceId === id)).length, 3);

  for (const [judgmentId, expected] of [['overturn-1', 3], ['overturn-2', 1]]) {
    let bird = createGame({ seed: `overturn-bird-${judgmentId}` });
    const birdPlayer = putHand(bird, 0, ['miracle-10', 'sheep-1', 'food-1']);
    putHand(bird, 1, ['money-1']);
    bird.currentPlayer = birdPlayer.playerId;
    bird.activeJudgments = { 異變: judgmentId };
    const birdAction = getNormalActions(bird).find((candidate) => candidate.instanceId === birdPlayer.hand.find((card) => card.definitionId === 'miracle-10').instanceId && candidate.targetPlayerId === 'player-2');
    bird = act(bird, birdAction);
    assert.equal(bird.players[0].fire, expected);
    assert.equal(bird.players[1].fire, expected);

    let rain = createGame({ seed: `overturn-rain-${judgmentId}` });
    const rainPlayer = putHand(rain, 0, ['miracle-22']);
    const plague = take(rain, 'disaster-09');
    rain.players[1].effects.push({ ...plague, effectOwnerPlayerId: 'player-2' });
    rain.activeJudgments = { 異變: judgmentId };
    const rainAction = getNormalActions(rain).find((candidate) => candidate.instanceId === rainPlayer.hand[0].instanceId && candidate.targetPlayerId === 'player-2');
    rain = act(rain, rainAction);
    assert.equal(rain.players[1].fire, judgmentId === 'overturn-1' ? 2 : 1);

    let victory = createGame({ seed: `overturn-victory-${judgmentId}` });
    const victoryPlayer = putHand(victory, 0, ['miracle-09']);
    victory.activeJudgments = { 異變: judgmentId };
    victory.currentApostle = 'player-1';
    const victoryAction = getNormalActions(victory).find((candidate) => candidate.instanceId === victoryPlayer.hand[0].instanceId && candidate.targetPlayerId === 'player-2');
    victory = act(victory, victoryAction);
    victory.players.forEach((player) => { player.hasNormalAction = false; });
    victory = settleRound(victory).state;
    assert.equal(victory.lastRoundResult.reward, 0);
    assert.equal(victory.players[0].fire, expected);
    assert.equal(victory.players[1].fire, expected);
  }
});

test('new game deals seven cards to every player, guarantees alpha miracles to human, and preserves every registered card', () => {
  const state = createGame({ seed: 1 });
  const expectedTotal = Object.keys(state.cardRegistry).length;
  assert.deepEqual(state.players.map((player) => player.hand.length), [7, 7, 7, 7]);
  assert.ok(state.players[0].hand.some((card) => card.definitionId === 'miracle-01'));
  assert.ok(state.players[0].hand.some((card) => card.definitionId === 'miracle-05'));
  assert.equal(listCardLocations(state).length, expectedTotal);
  assert.equal(new Set(listCardLocations(state)).size, expectedTotal);
});

test('same seed reproduces rolls, deck, hands, and RNG state', () => {
  assert.deepEqual(createGame({ seed: 'repeatable' }), createGame({ seed: 'repeatable' }));
});

test('回轉歸向 reverses direction, draws one, enters played area, preserves apostle, and frees next resource', () => {
  let state = createGame({ seed: 21 });
  putHand(state, 0, ['miracle-01', 'sheep-1']);
  state.currentResource = { type: 'food', number: 9, instanceId: 'test' };
  state.currentApostle = 'player-4';
  state.direction = -1;
  const beforeHand = state.players[0].hand.length;
  const action = getNormalActions(state).find((candidate) => candidate.type === 'playMiracle');
  assert.ok(action);
  state = act(state, action);
  assert.equal(state.direction, 1);
  assert.equal(state.players[0].hand.length, beforeHand);
  assert.equal(state.playedArea.at(-1).definitionId, 'miracle-01');
  assert.equal(state.currentResource, null);
  assert.equal(state.currentApostle, 'player-4');
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

test('redraw discards the hand, draws the same count, and stays on turn when redraw is playable', () => {
  const state = createGame({ seed: 5 });
  const player = putHand(state, 0, ['sheep-1', 'sheep-2']);
  state.currentResource = { type: 'sheep', number: 9 };
  const oldIds = player.hand.map((card) => card.instanceId);
  state.deck = [take(state, 'money-1'), ...state.deck];
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

test('redraw with no legal result automatically ends participation and advances', () => {
  const state = createGame({ seed: 8 });
  putHand(state, 0, ['sheep-1']);
  state.currentResource = { type: 'sheep', number: 9 };
  state.deck = [take(state, 'sheep-2'), ...state.deck];
  const redrawn = act(state, getNormalActions(state)[0]);
  assert.equal(redrawn.players[0].hasRedrawnThisRound, true);
  assert.equal(redrawn.players[0].hasNormalAction, false);
  assert.notEqual(redrawn.currentPlayer, 'player-1');
});

test('endParticipation remains a defensive fallback for malformed post-redraw states', () => {
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

test('ending with one eligible player immediately settles and starts next round', () => {
  let state = createGame({ seed: 11 });
  putHand(state, 0, []);
  state.players[2].hasNormalAction = false;
  state.players[3].hasNormalAction = false;
  state = act(state, getNormalActions(state)[0]);
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
});

test('losing streak increments, resets for apostle, and caps at two', () => {
  const state = createGame({ seed: 14 });
  state.currentApostle = 'player-1';
  state.players[0].losingStreak = 2;
  state.players[1].losingStreak = 3;
  state.players.forEach((player, index) => { player.hasNormalAction = index === 0; });
  const next = settleRound(state).state;
  assert.equal(next.players[0].losingStreak, 0);
  assert.equal(next.players[1].losingStreak, 2);
  assert.equal(next.players[1].startingHandBonus, 2);
});

test('next round deals at most nine cards from losing streak', () => {
  const state = createGame({ seed: 15 });
  state.players.forEach((player, index) => {
    player.losingStreak = index;
    player.startingHandBonus = index;
    player.hasNormalAction = index === 0;
  });
  const next = settleRound(state).state;
  assert.deepEqual(next.players.map((player) => player.hand.length), [8, 9, 9, 9]);
});

test('starting player rotates counterclockwise independently each round', () => {
  const state = createGame({ seed: 16 });
  const previousIndex = state.players.findIndex((player) => player.playerId === state.startingPlayer);
  state.players.forEach((player, index) => { player.hasNormalAction = index === 0; });
  const next = settleRound(state).state;
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

test('card location invariant is preserved through actions', () => {
  let state = createGame({ seed: 18 });
  const expectedTotal = Object.keys(state.cardRegistry).length;
  for (let index = 0; index < 30 && !state.gameOver; index += 1) {
    state = act(state, getNormalActions(state)[0]);
    const locations = listCardLocations(state);
    assert.equal(locations.length, expectedTotal);
    assert.equal(new Set(locations).size, expectedTotal);
    assert.ok(locations.every((id) => state.cardRegistry[id]));
  }
});

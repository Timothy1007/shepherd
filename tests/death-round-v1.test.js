import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, executeNormalAction, getNormalActions, listCardLocations, settleRound } from '../src/game/game.js';

function takeFromDeck(state, definitionId) {
  const index = state.deck.findIndex((card) => card.definitionId === definitionId);
  assert.notEqual(index, -1, `Missing ${definitionId}`);
  return state.deck.splice(index, 1)[0];
}

function act(state, action) {
  const result = executeNormalAction(state, action);
  assert.equal(result.ok, true, result.reason);
  return result.state;
}

test('整備翻到死亡仍補足兩張正常牌，並進入鎖定的死亡輪', () => {
  const state = createGame({ seed: 'death-v1-preparation' });
  const human = state.players[0];
  state.currentPlayer = human.playerId;
  state.currentResource = { type: 'sheep', number: 9 };
  state.deck.push(...human.hand);
  human.hand = [takeFromDeck(state, 'sheep-1')];
  const death = takeFromDeck(state, 'death');
  const first = takeFromDeck(state, 'money-1');
  const second = takeFromDeck(state, 'food-1');
  state.deck.unshift(death, first, second);

  const next = act(state, getNormalActions(state)[0]);

  assert.equal(next.phase, 'deathRound');
  assert.deepEqual(next.death.finalOrder, ['player-4', 'player-3', 'player-2', 'player-1']);
  assert.equal(next.currentPlayer, 'player-4');
  assert.deepEqual(next.players[0].hand.map((card) => card.definitionId), ['money-1', 'food-1']);
  assert.equal(next.deck.some((card) => card.definitionId === 'death'), false);
  assert.equal(listCardLocations(next).length, Object.keys(next.cardRegistry).length);
});

test('死亡輪不能整備；四名玩家各結束一次後立即換輪', () => {
  let state = createGame({ seed: 'death-v1-skip' });
  state.phase = 'deathRound';
  state.death = { triggered: true, revealerId: 'player-1', finalOrder: ['player-4', 'player-3', 'player-2', 'player-1'], remaining: ['player-4', 'player-3', 'player-2', 'player-1'] };
  state.currentPlayer = 'player-4';
  state.currentResource = { type: 'sheep', number: 9 };
  for (const player of state.players) player.hand = [];

  for (let count = 0; count < 4; count += 1) {
    const actions = getNormalActions(state);
    assert.deepEqual(actions.map((action) => action.type), ['deathSkip']);
    state = act(state, actions[0]);
  }

  assert.equal(state.round, 2);
  assert.equal(state.phase, 'playing');
});

test('未翻到的死亡牌不會被洗進下一輪手牌', () => {
  const state = createGame({ seed: 'death-v1-next-round' });
  state.players.forEach((player, index) => { player.hasNormalAction = index === 0; });

  const next = settleRound(state).state;

  assert.equal(next.players.flatMap((player) => player.hand).some((card) => card.definitionId === 'death'), false);
  assert.equal(next.deck.filter((card) => card.definitionId === 'death').length, 1);
});

test('死亡固定從牌庫底開始，且每次正常行動向頂部推進四張', () => {
  const state = createGame({ seed: 'death-v1-clock' });
  const human = state.players[0];
  state.currentPlayer = human.playerId;
  state.deck.push(...human.hand);
  state.deck.push(takeFromDeck(state, 'death'));
  human.hand = [takeFromDeck(state, 'money-1')];
  const before = state.deck.findIndex((card) => card.definitionId === 'death');
  assert.equal(before, state.deck.length - 1);

  const next = act(state, getNormalActions(state).find((action) => action.type === 'playResource'));

  assert.equal(next.deck.findIndex((card) => card.definitionId === 'death'), before - 4);
  assert.equal(next.phase, 'playing');
});

test('死亡時鐘到達牌庫頂時，當前行動結束後進入死亡輪', () => {
  const state = createGame({ seed: 'death-v1-clock-trigger' });
  const human = state.players[0];
  state.currentPlayer = human.playerId;
  state.deck.push(...human.hand);
  human.hand = [takeFromDeck(state, 'money-1')];
  const death = takeFromDeck(state, 'death');
  state.deck.splice(3, 0, death);

  const next = act(state, getNormalActions(state).find((action) => action.type === 'playResource'));

  assert.equal(next.phase, 'deathRound');
  assert.equal(next.currentPlayer, 'player-4');
  assert.equal(next.deck.some((card) => card.definitionId === 'death'), false);
});

test('整備抽兩張後，死亡時鐘額外再推進四張', () => {
  const state = createGame({ seed: 'death-v1-preparation-clock' });
  const human = state.players[0];
  state.currentPlayer = human.playerId;
  state.currentResource = { type: 'sheep', number: 9 };
  state.deck.push(...human.hand);
  state.deck.push(takeFromDeck(state, 'death'));
  human.hand = [takeFromDeck(state, 'sheep-1')];
  const before = state.deck.findIndex((card) => card.definitionId === 'death');

  const next = act(state, getNormalActions(state)[0]);

  assert.equal(next.deck.findIndex((card) => card.definitionId === 'death'), before - 6);
});

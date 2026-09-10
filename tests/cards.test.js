import test from 'node:test';
import assert from 'node:assert/strict';
import { CARD_DEFINITIONS, createResourceDeck, isLegalResourcePlay } from '../src/game/cards.js';

const card = (definitionId) => ({ instanceId: `${definitionId}#test`, definitionId });

test('resource recipe has 36 definitions and 90 instances', () => {
  assert.equal(CARD_DEFINITIONS.length, 36);
  assert.equal(createResourceDeck().length, 90);
});

test('every card instanceId is unique', () => {
  const ids = createResourceDeck().map(({ instanceId }) => instanceId);
  assert.equal(new Set(ids).size, 90);
});

test('resource counts match the official recipe', () => {
  const counts = Object.fromEntries(['sheep', 'food', 'money', 'grace'].map((type) => [type, 0]));
  for (const instance of createResourceDeck()) counts[instance.definitionId.split('-')[0]] += 1;
  assert.deepEqual(counts, { sheep: 27, food: 27, money: 27, grace: 9 });
});

test('same type accepts equal number', () => assert.equal(isLegalResourcePlay(card('sheep-5'), { type: 'sheep', number: 5 }, 'sheep'), true));
test('same type accepts higher number', () => assert.equal(isLegalResourcePlay(card('sheep-6'), { type: 'sheep', number: 5 }, 'sheep'), true));
test('same type rejects lower number', () => assert.equal(isLegalResourcePlay(card('sheep-4'), { type: 'sheep', number: 5 }, 'sheep'), false));
test('counter type ignores number', () => assert.equal(isLegalResourcePlay(card('money-1'), { type: 'sheep', number: 9 }, 'money'), true));
test('non-counter type is illegal', () => assert.equal(isLegalResourcePlay(card('food-9'), { type: 'sheep', number: 1 }, 'food'), false));
test('a free resource accepts any resource', () => assert.equal(isLegalResourcePlay(card('food-1'), null, 'food'), true));

test('grace checks its own number using the declared resource type', () => {
  assert.equal(isLegalResourcePlay(card('grace-5'), { type: 'food', number: 5 }, 'food'), true);
  assert.equal(isLegalResourcePlay(card('grace-4'), { type: 'food', number: 5 }, 'food'), false);
  assert.equal(isLegalResourcePlay(card('grace-1'), { type: 'food', number: 9 }, 'sheep'), true);
  assert.equal(isLegalResourcePlay(card('grace-9'), null, undefined), false);
});

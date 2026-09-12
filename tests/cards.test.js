import test from 'node:test';
import assert from 'node:assert/strict';
import { CARD_DEFINITIONS, createResourceDeck, isLegalResourcePlay } from '../src/game/cards.js';

const card = (definitionId) => ({ instanceId: `${definitionId}#test`, definitionId });

test('alpha recipe has 36 resource definitions plus three miracles and 93 instances', () => {
  assert.equal(CARD_DEFINITIONS.filter((definition) => definition.kind === 'resource').length, 36);
  assert.equal(CARD_DEFINITIONS.filter((definition) => definition.kind === 'miracle').length, 3);
  assert.equal(CARD_DEFINITIONS.length, 39);
  assert.equal(createResourceDeck().length, 93);
});

test('every resource definition points to its production artwork path', () => {
  for (const definition of CARD_DEFINITIONS.filter((definition) => definition.kind === 'resource')) {
    assert.equal(definition.image, `assets/cards/${definition.type}-${definition.number}.png`);
  }
});

test('implemented miracles point to their uploaded artwork', () => {
  const first = CARD_DEFINITIONS.find((definition) => definition.definitionId === 'miracle-01');
  assert.equal(first.name, '回轉歸向');
  assert.equal(first.image, 'assets/miracles/miracle-01.png');
  assert.deepEqual(first.tags, ['新生']);

  const wilderness = CARD_DEFINITIONS.find((definition) => definition.definitionId === 'miracle-02');
  assert.equal(wilderness.name, '行曠野之路');
  assert.equal(wilderness.image, 'assets/miracles/miracle-02.png');
  assert.deepEqual(wilderness.tags, ['新生']);

  const wind = CARD_DEFINITIONS.find((definition) => definition.definitionId === 'miracle-05');
  assert.equal(wind.name, '如風吹來');
  assert.equal(wind.image, 'assets/miracles/miracle-05.png');
  assert.deepEqual(wind.tags, ['流轉', '火種']);
});

test('every card instanceId is unique', () => {
  const ids = createResourceDeck().map(({ instanceId }) => instanceId);
  assert.equal(new Set(ids).size, ids.length);
});

test('resource counts match the official recipe', () => {
  const counts = Object.fromEntries(['sheep', 'food', 'money', 'grace'].map((type) => [type, 0]));
  for (const instance of createResourceDeck().filter((instance) => !instance.definitionId.startsWith('miracle-'))) {
    counts[instance.definitionId.split('-')[0]] += 1;
  }
  assert.deepEqual(counts, { sheep: 27, food: 27, money: 27, grace: 9 });
});

test('same type accepts equal number', () => assert.equal(isLegalResourcePlay(card('sheep-5'), { type: 'sheep', number: 5 }, 'sheep'), true));
test('same type accepts higher number', () => assert.equal(isLegalResourcePlay(card('sheep-6'), { type: 'sheep', number: 5 }, 'sheep'), true));
test('same type rejects lower number', () => assert.equal(isLegalResourcePlay(card('sheep-4'), { type: 'sheep', number: 5 }, 'sheep'), false));
test('counter type ignores number', () => assert.equal(isLegalResourcePlay(card('money-1'), { type: 'sheep', number: 9 }, 'money'), true));
test('non-counter type is illegal', () => assert.equal(isLegalResourcePlay(card('food-9'), { type: 'sheep', number: 1 }, 'food'), false));
test('a free resource accepts any resource', () => assert.equal(isLegalResourcePlay(card('food-1'), null, 'food'), true));
test('miracles are not legal resource plays', () => assert.equal(isLegalResourcePlay(card('miracle-01'), null, undefined), false));

test('grace checks its own number using the declared resource type', () => {
  assert.equal(isLegalResourcePlay(card('grace-5'), { type: 'food', number: 5 }, 'food'), true);
  assert.equal(isLegalResourcePlay(card('grace-4'), { type: 'food', number: 5 }, 'food'), false);
  assert.equal(isLegalResourcePlay(card('grace-1'), { type: 'food', number: 9 }, 'sheep'), true);
  assert.equal(isLegalResourcePlay(card('grace-9'), null, undefined), false);
});

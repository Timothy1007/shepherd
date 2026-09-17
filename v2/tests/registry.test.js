import test from 'node:test';
import assert from 'node:assert/strict';
import { createFullDeck, MIRACLES, DISASTERS } from '../src/playable/cards.js';

test('V2 playable registry contains all finalized special cards', () => {
  assert.equal(MIRACLES.length, 24);
  assert.equal(DISASTERS.length, 18);
});

test('V2 playable deck contains expected base copies', () => {
  const deck = createFullDeck();
  const resources = deck.filter((card) => card.type === 'resource');
  const specials = deck.filter((card) => card.type !== 'resource');
  assert.equal(resources.length, 90);
  assert.equal(specials.length, 42);
  assert.equal(deck.length, 132);
});

test('resource corruption follows the frozen V2 rule', () => {
  const deck = createFullDeck();
  for (const card of deck.filter((c) => c.type === 'resource')) {
    if (card.resourceType === 'grace') assert.equal(card.corruption, 0);
    else assert.equal(card.corruption, card.printedNumber <= 4 ? 0 : 1);
  }
});

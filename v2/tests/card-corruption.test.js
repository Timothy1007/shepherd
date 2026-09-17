import test from 'node:test';
import assert from 'node:assert/strict';

import {
  DISASTER_CORRUPTION,
  MIRACLE_CORRUPTION,
  getCardCorruption,
  getResourceCorruption,
} from '../src/game/index.js';

test('normal resources 1-4 are corruption 0 and 5-9 are corruption 1', () => {
  for (let n = 1; n <= 4; n += 1) {
    assert.equal(getResourceCorruption({ resourceType: 'sheep', printedNumber: n }), 0);
  }
  for (let n = 5; n <= 9; n += 1) {
    assert.equal(getResourceCorruption({ resourceType: 'money', printedNumber: n }), 1);
  }
});

test('Grace resources always have corruption 0', () => {
  assert.equal(getResourceCorruption({ resourceType: 'grace', printedNumber: 1 }), 0);
  assert.equal(getResourceCorruption({ resourceType: 'grace', printedNumber: 9 }), 0);
});

test('all 24 V2 miracles have finalized corruption values', () => {
  assert.equal(Object.keys(MIRACLE_CORRUPTION).length, 24);
  assert.equal(MIRACLE_CORRUPTION['荊棘冠冕'], 3);
  assert.equal(MIRACLE_CORRUPTION['如風吹來'], 1);
  assert.equal(MIRACLE_CORRUPTION['劫後餘生'], 3);
});

test('all 18 V2 disasters have finalized corruption values', () => {
  assert.equal(Object.keys(DISASTER_CORRUPTION).length, 18);
  assert.equal(DISASTER_CORRUPTION['瘟疫'], 4);
  assert.equal(DISASTER_CORRUPTION['哈米吉多頓'], 4);
  assert.equal(DISASTER_CORRUPTION['盜火'], 2);
});

test('card corruption lookup returns finalized values', () => {
  assert.equal(getCardCorruption({ type: 'resource', resourceType: 'food', printedNumber: 7 }), 1);
  assert.equal(getCardCorruption({ type: 'resource', resourceType: 'grace', printedNumber: 9 }), 0);
  assert.equal(getCardCorruption({ type: 'miracle', name: '回轉歸向' }), 2);
  assert.equal(getCardCorruption({ type: 'disaster', name: '三分之一的星辰' }), 4);
});

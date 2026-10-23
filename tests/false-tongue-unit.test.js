import test from 'node:test';
import assert from 'node:assert/strict';
import {falseTongueOptions, applyFalseTongue, openFalseTongue, falseTongueActions, resolveFalseTongue} from '../src/game/false-tongue.js';

test('神蹟效果可以選擇減少抽牌、火種或使命其中一項', () => {
  const effect = {sourceKind:'miracle', draw:2, fire:6, mission:3};
  assert.deepEqual(falseTongueOptions(effect), ['draw','fire','mission']);
  assert.deepEqual(applyFalseTongue(effect,'draw'), {...effect,draw:1});
  assert.deepEqual(applyFalseTongue(effect,'fire'), {...effect,fire:1});
  assert.deepEqual(applyFalseTongue(effect,'mission'), {...effect,mission:1});
  assert.deepEqual(effect, {sourceKind:'miracle',draw:2,fire:6,mission:3});
});

test('應對者可跳過，或由受影響的玩家選擇受到影響的項目', () => {
  const effect={sourceKind:'miracle',playerId:'p1',draw:2,fire:5};
  const window=openFalseTongue(effect,'p2','disaster-16#1');
  assert.deepEqual(falseTongueActions(window).map(a=>a.choice),['pass','use']);
  assert.deepEqual(resolveFalseTongue(window,{playerId:'p2',choice:'pass'}),{stage:'done',effect,consumeCard:false});
  const choosing=resolveFalseTongue(window,{playerId:'p2',choice:'use'});
  assert.deepEqual(falseTongueActions(choosing),[{playerId:'p1',choice:'draw'},{playerId:'p1',choice:'fire'}]);
  assert.throws(()=>resolveFalseTongue(choosing,{playerId:'p2',choice:'fire'}),RangeError);
  assert.deepEqual(resolveFalseTongue(choosing,{playerId:'p1',choice:'fire'}),{stage:'done',effect:{...effect,fire:0},consumeCard:true});
});

test('只有一種效果時使用後直接套用；無合法效果不開啟應對', () => {
  const window=openFalseTongue({sourceKind:'miracle',playerId:'p1',draw:2},'p2','card');
  assert.deepEqual(resolveFalseTongue(window,{playerId:'p2',choice:'use'}),{stage:'done',effect:{sourceKind:'miracle',playerId:'p1',draw:1},consumeCard:true});
  assert.equal(openFalseTongue({sourceKind:'disaster',playerId:'p1',draw:2},'p2','card'),null);
});

test('數量不降到零以下，其他來源不能應對', () => {
  const effect = {sourceKind:'miracle',draw:1,fire:2,mission:1};
  for (const kind of ['draw','fire','mission']) assert.equal(applyFalseTongue(effect,kind)[kind],0);
  assert.deepEqual(falseTongueOptions({...effect,sourceKind:'disaster'}), []);
  assert.throws(() => applyFalseTongue(effect,'unknown'), RangeError);
  assert.deepEqual(falseTongueOptions({sourceKind:'miracle',draw:0,fire:0,mission:0}), []);
});

import test from 'node:test';
import assert from 'node:assert/strict';
import { thresholdCrossings, canOvercomeDeath, applyOvercomeDeath, THRESHOLD_FIRE, THRESHOLD_HAND } from '../src/game/threshold-reaction.js';

test('Overcome Death triggers only when fire crosses from above zero to zero',()=>{
  assert.deepEqual(thresholdCrossings({fire:3,handCount:4},{fire:0,handCount:4}),[THRESHOLD_FIRE]);
  assert.equal(canOvercomeDeath({fire:0,handCount:4},{fire:0,handCount:4}),false);
  assert.equal(canOvercomeDeath({fire:3,handCount:4},{fire:1,handCount:4}),false);
});

test('Overcome Death triggers when hand crosses from above zero to zero',()=>{
  assert.deepEqual(thresholdCrossings({fire:5,handCount:3},{fire:5,handCount:0}),[THRESHOLD_HAND]);
});

test('when both values reach zero player must choose only one to preserve',()=>{
  const before={fire:2,handCount:2};
  const after={fire:0,handCount:0,hand:[]};
  assert.deepEqual(thresholdCrossings(before,after),[THRESHOLD_FIRE,THRESHOLD_HAND]);
  const fire=applyOvercomeDeath({before,after,choice:THRESHOLD_FIRE});
  assert.equal(fire.ok,true);assert.equal(fire.value.fire,1);assert.equal(fire.value.hand.length,0);
  const keep={instanceId:'sheep-1#test',definitionId:'sheep-1'};
  const hand=applyOvercomeDeath({before,after,choice:THRESHOLD_HAND,restoreCard:keep});
  assert.equal(hand.ok,true);assert.equal(hand.value.fire,0);assert.deepEqual(hand.value.hand,[keep]);
});

test('hand preservation requires an actual card to remain',()=>{
  const r=applyOvercomeDeath({before:{fire:1,handCount:1},after:{fire:1,handCount:0,hand:[]},choice:THRESHOLD_HAND});
  assert.equal(r.ok,false);
});

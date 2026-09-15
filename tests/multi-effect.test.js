import test from 'node:test';
import assert from 'node:assert/strict';
import { CARD_DEFINITIONS } from '../src/game/cards.js';
import { affectedEffects,canUseStronghold,strongholdChoices,resolveAshesForPlayer,applyMiracleFireGain,clearRoundMultiEffectModifiers } from '../src/game/multi-effect.js';

const ashes={instanceId:'disaster-07#test',definitionId:'disaster-07'};

test('灰與燼 exposes two independently blockable player effects',()=>{
  assert.equal(affectedEffects(ashes,'player-1').length,2);
  assert.equal(canUseStronghold(ashes,'player-1'),true);
  assert.deepEqual(strongholdChoices(ashes,'player-1').map(c=>c.effectId),['lose-fire','miracle-fire-reduction']);
});

test('拆毀堅固營壘 may block 灰與燼 fire loss only',()=>{
  const player={fire:8};
  const result=resolveAshesForPlayer({player,blockedEffectId:'lose-fire'});
  assert.equal(player.fire,8);
  assert.equal(result.lostFire,0);
  assert.equal(player.roundModifiers.miracleFireReduction,2);
});

test('拆毀堅固營壘 may block 灰與燼 miracle-fire reduction only',()=>{
  const player={fire:8};
  const result=resolveAshesForPlayer({player,blockedEffectId:'miracle-fire-reduction'});
  assert.equal(player.fire,5);
  assert.equal(result.lostFire,3);
  assert.equal(player.roundModifiers,undefined);
});

test('灰與燼 reduces miracle fire gain by two, minimum zero, until round cleanup',()=>{
  const player={fire:4,roundModifiers:{miracleFireReduction:2}};
  assert.equal(applyMiracleFireGain(player,3),1);
  assert.equal(player.fire,5);
  assert.equal(applyMiracleFireGain(player,1),0);
  assert.equal(player.fire,5);
  clearRoundMultiEffectModifiers(player);
  assert.equal(applyMiracleFireGain(player,3),3);
  assert.equal(player.fire,8);
});

test('multi-effect registration remains data-driven rather than card-name driven',()=>{
  const definition=CARD_DEFINITIONS.find(d=>d.definitionId==='disaster-07');
  assert.equal(definition.effects.length,2);
  assert.ok(definition.effects.every(effect=>effect.effectId&&effect.text));
});

import test from 'node:test';
import assert from 'node:assert/strict';
import {CARD_DEFINITIONS} from '../src/game/cards.js';
import {affectedEffects,canUseStronghold,strongholdChoices,resolveAshesForPlayer,applyMiracleFireGain,clearRoundMultiEffectModifiers} from '../src/game/multi-effect.js';
const ashes={instanceId:'disaster-07#test',definitionId:'disaster-07'};
test('灰與燼 exposes two independently blockable player effects',()=>{assert.equal(affectedEffects(ashes,'player-1').length,2);assert.equal(canUseStronghold(ashes,'player-1'),true);assert.deepEqual(strongholdChoices(ashes,'player-1').map(choice=>choice.effectId),['lose-fire','miracle-fire-reduction']);});
test('拆毀堅固營壘 can block either official 灰與燼 effect',()=>{let player={fire:8};let result=resolveAshesForPlayer({player,blockedEffectId:'lose-fire'});assert.equal(player.fire,8);assert.equal(result.lostFire,0);assert.equal(player.roundModifiers.miracleFireReduction,2);player={fire:8};result=resolveAshesForPlayer({player,blockedEffectId:'miracle-fire-reduction'});assert.equal(player.fire,4);assert.equal(result.lostFire,4);assert.equal(player.roundModifiers,undefined);});
test('灰與燼 reduces miracle fire gain by two until cleanup',()=>{const player={fire:4,roundModifiers:{miracleFireReduction:2}};assert.equal(applyMiracleFireGain(player,3),1);clearRoundMultiEffectModifiers(player);assert.equal(applyMiracleFireGain(player,3),3);});
test('multi-effect registration remains data-driven',()=>{const definition=CARD_DEFINITIONS.find(card=>card.definitionId==='disaster-07');assert.equal(definition.effects.length,2);assert.match(definition.text,/失去4點火種/);});

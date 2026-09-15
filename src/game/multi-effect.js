import { getDefinition } from './cards.js';

export const STRONGHOLD_ID='miracle-19';

export function affectedEffects(card,playerId){
  const definition=getDefinition(card);
  if(definition.kind!=='disaster'||!Array.isArray(definition.effects))return[];
  return definition.effects.filter(effect=>effect.scope==='each-player'||effect.targetPlayerId===playerId);
}

export function canUseStronghold(card,playerId){
  return affectedEffects(card,playerId).length>=2;
}

export function strongholdChoices(card,playerId){
  return affectedEffects(card,playerId).map(effect=>({effectId:effect.effectId,text:effect.text}));
}

export function resolveAshesForPlayer({player,blockedEffectId=null}){
  const result={lostFire:0,miracleFireReduction:0,blockedEffectId};
  if(blockedEffectId!=='lose-fire'){
    const before=player.fire;
    player.fire=Math.max(0,player.fire-3);
    result.lostFire=before-player.fire;
  }
  if(blockedEffectId!=='miracle-fire-reduction'){
    player.roundModifiers??={};
    player.roundModifiers.miracleFireReduction=2;
    result.miracleFireReduction=2;
  }
  return result;
}

export function applyMiracleFireGain(player,amount){
  const reduction=Math.max(0,player.roundModifiers?.miracleFireReduction??0);
  const gained=Math.max(0,amount-reduction);
  player.fire+=gained;
  return gained;
}

export function clearRoundMultiEffectModifiers(player){
  if(player.roundModifiers)delete player.roundModifiers.miracleFireReduction;
}

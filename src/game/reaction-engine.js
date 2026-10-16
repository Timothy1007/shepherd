import { getDefinition } from './cards.js';
import { canUseStronghold, strongholdChoices } from './multi-effect.js';

export const REACTION_IDS=Object.freeze({
  immunity:'miracle-16',
  scapegoat:'miracle-17',
  emptyTomb:'miracle-18',
  stronghold:'miracle-19',
  overcomeDeath:'miracle-20'
});

function playerById(state,playerId){return state.players.find(player=>player.playerId===playerId)??null;}

export function disasterForAction(state,action){
  if(action?.type!=='playMiracle')return null;
  const actor=playerById(state,action.playerId),card=actor?.hand.find(held=>held.instanceId===action.instanceId);
  if(!card)return null;
  const definition=getDefinition(card);
  return definition.kind==='disaster'?{actor,card,definition}:null;
}

export function disasterTargetIds(state,action){
  const disaster=disasterForAction(state,action);
  if(!disaster)return[];
  if(['disaster-01','disaster-07'].includes(disaster.definition.definitionId))return state.players.map(player=>player.playerId);
  if(disaster.definition.definitionId==='disaster-02')return [...new Set(action.targetPlayerIds??[])];
  return action.targetPlayerId?[action.targetPlayerId]:[];
}

export function reactionOptionsFor(state,action,responderId,{previewState=null}={}){
  const disaster=disasterForAction(state,action),responder=playerById(state,responderId);
  if(!disaster||!responder)return[];
  const targetIds=disasterTargetIds(state,action),handIds=new Set(responder.hand.map(card=>card.definitionId)),options=[];
  if(handIds.has(REACTION_IDS.scapegoat))for(const targetPlayerId of targetIds.filter(id=>id!==responderId))options.push({reactionId:REACTION_IDS.scapegoat,kind:'redirect',targetPlayerId});
  if(targetIds.includes(responderId)&&handIds.has(REACTION_IDS.immunity))options.push({reactionId:REACTION_IDS.immunity,kind:'immunity'});
  if(targetIds.includes(responderId)&&handIds.has(REACTION_IDS.stronghold)&&canUseStronghold(disaster.card,responderId))options.push({reactionId:REACTION_IDS.stronghold,kind:'blockEffect',choices:strongholdChoices(disaster.card,responderId)});
  if(previewState&&targetIds.includes(responderId)){
    const emptyTombOutcome=reactionOutcome(state,previewState,responderId,handIds.has(REACTION_IDS.emptyTomb)?responder.hand.find(card=>card.definitionId===REACTION_IDS.emptyTomb)?.instanceId:null);
    if(handIds.has(REACTION_IDS.emptyTomb)&&emptyTombOutcome.lostFire>0)options.push({reactionId:REACTION_IDS.emptyTomb,kind:'afterFireLoss',drawCount:Math.min(2,Math.floor(emptyTombOutcome.lostFire/3))});
    if(handIds.has(REACTION_IDS.overcomeDeath)&&(emptyTombOutcome.crossedFireToZero||emptyTombOutcome.crossedHandToZero))options.push({reactionId:REACTION_IDS.overcomeDeath,kind:'preserveThreshold',fire:emptyTombOutcome.crossedFireToZero,hand:emptyTombOutcome.crossedHandToZero});
  }
  return options;
}

export function reactionWindowFor(state,action){
  const disaster=disasterForAction(state,action);
  if(!disaster)return null;
  const targetIds=disasterTargetIds(state,action);
  return {disaster,targetIds,optionsByPlayer:Object.fromEntries(state.players.map(player=>[player.playerId,reactionOptionsFor(state,action,player.playerId)]))};
}

export function reactionOutcome(beforeState,afterState,playerId,reactionInstanceId=null){
  const before=playerById(beforeState,playerId),after=playerById(afterState,playerId);
  if(!before||!after)return{lostFire:0,lostCardIds:[],crossedFireToZero:false,crossedHandToZero:false};
  const beforeHand=before.hand.filter(card=>card.instanceId!==reactionInstanceId),afterIds=new Set(after.hand.map(card=>card.instanceId));
  return {lostFire:Math.max(0,before.fire-after.fire),lostCardIds:beforeHand.filter(card=>!afterIds.has(card.instanceId)).map(card=>card.instanceId),crossedFireToZero:before.fire>0&&after.fire===0,crossedHandToZero:beforeHand.length>0&&after.hand.length===0};
}

export function consumeScapegoatProtection(beforeState,afterState,playerId){
  const before=playerById(beforeState,playerId),after=playerById(afterState,playerId),effect=(after?.effects??[]).find(card=>card.definitionId===REACTION_IDS.scapegoat)??(before?.effects??[]).find(card=>card.definitionId===REACTION_IDS.scapegoat);
  if(!before||!after||!effect)return{used:false,kind:null};
  const outcome=reactionOutcome(beforeState,afterState,playerId,effect.instanceId);
  if(outcome.lostFire){after.fire+=Math.min(4,outcome.lostFire);after.effects=after.effects.filter(card=>card.instanceId!==effect.instanceId);afterState.discardPile.push(structuredClone(effect));return{used:true,kind:'fire'};}
  const restoreId=outcome.lostCardIds[0];
  if(!restoreId)return{used:false,kind:null};
  const discardIndex=afterState.discardPile.findIndex(card=>card.instanceId===restoreId);
  if(discardIndex<0)return{used:false,kind:null};
  after.hand.push(afterState.discardPile.splice(discardIndex,1)[0]);after.effects=after.effects.filter(card=>card.instanceId!==effect.instanceId);afterState.discardPile.push(structuredClone(effect));
  return{used:true,kind:'hand'};
}

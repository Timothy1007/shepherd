import { GameController } from '../controller/game-controller.js';
import { getDefinition, RESOURCE_TYPES } from '../game/cards.js';
import { getNormalActions } from '../game/game.js';
import { appendChoiceCard, renderCardRow } from './choice-card-row.js';

const SCAPEGOAT_ID='miracle-17';
const originalAct=GameController.prototype.act,originalRunAiTurn=GameController.prototype.runAiTurn,originalPrepare=GameController.prototype.prepare;
let pending=null,settling=null;
const names={sheep:'群羊',food:'糧食',money:'金錢'};
const resources=player=>player.hand.filter(card=>{const d=getDefinition(card);return d.kind==='resource'&&RESOURCE_TYPES.includes(d.type);});
const hasScapegoat=player=>player.effects.some(card=>card.definitionId===SCAPEGOAT_ID);
const effectCount=(state,count)=>{const judgment=state?.activeJudgments?.['異變'];return judgment==='overturn-2'?(count?1:0):judgment==='overturn-1'?Math.min(count,3):count;};
const overlayVisible=()=>{const overlay=document.querySelector('#ark-choice-overlay');return !!overlay&&!overlay.hidden;};
const actorIsHuman=()=>pending?.controller.state.players.find(player=>player.playerId===pending.action.playerId)?.type==='human';
function autoChoice(state,player){const cards=resources(player);if(!cards.length)return{keepType:null,discardIds:[]};const counts=Object.fromEntries(RESOURCE_TYPES.map(type=>[type,cards.filter(card=>getDefinition(card).type===type).length]));const keepType=[...RESOURCE_TYPES].sort((a,b)=>counts[b]-counts[a])[0],eligible=cards.filter(card=>getDefinition(card).type!==keepType);return{keepType,discardIds:eligible.slice(0,effectCount(state,Math.min(2,eligible.length))).map(card=>card.instanceId)};}
const allChoices=(controller,humanChoice)=>Object.fromEntries(controller.state.players.map(player=>[player.playerId,player.type==='human'?humanChoice:autoChoice(controller.state,player)]));
function ensureUi(){let overlay=document.querySelector('#ark-choice-overlay');if(!overlay){overlay=document.createElement('section');overlay.id='ark-choice-overlay';document.body.append(overlay);}return overlay;}
function optionTile(host,label,handler,selected=false){const b=document.createElement('button');b.type='button';b.className='choice-row-card ark-option-card';b.textContent=label;b.style.cssText=`height:auto;min-height:104px;padding:15px 13px;aspect-ratio:auto;color:#f5eee0;background:${selected?'#d7bd7a33':'linear-gradient(155deg,#26332de8,#17211d)'};border:1px solid ${selected?'#ffe190':'#d8c79588'};line-height:1.5;font:700 15px/1.5 Inter,system-ui,"Noto Sans TC",sans-serif`;b.onclick=handler;host.append(b);return b;}
function burnChosenCards(ids=[]){
  return Promise.all([...new Set(ids)].filter(Boolean).map(id=>new Promise(resolve=>{
    const card=[...document.querySelectorAll('#hand .card[data-card-id]')].find(item=>item.dataset.cardId===id);
    if(!card){resolve();return;}
    const rect=card.getBoundingClientRect();
    if(!rect.width||!rect.height){resolve();return;}
    const frame=document.createElement('div'),copy=card.cloneNode(true),flame=document.createElement('div');
    frame.setAttribute('aria-hidden','true');
    frame.style.cssText=`position:fixed;left:${rect.left}px;top:${rect.top}px;width:${rect.width}px;height:${rect.height}px;z-index:39000;pointer-events:none;overflow:visible;`;
    copy.removeAttribute('data-card-id');copy.style.cssText='position:absolute;inset:0;width:100%;height:100%;margin:0;padding:0;border:0;box-sizing:border-box;visibility:visible;filter:none;box-shadow:none;';
    const image=copy.querySelector('img');if(image)image.style.cssText='display:block;width:100%;height:100%;object-fit:contain';
    flame.style.cssText='position:absolute;left:0;top:-20px;width:100%;height:20px;pointer-events:none;';
    for(let i=0;i<7;i++){const tongue=document.createElement('span');tongue.style.cssText=`position:absolute;left:${i*100/7}%;bottom:0;width:20%;height:${i%2?22:16}px;border-radius:70% 25% 55% 40%;background:radial-gradient(ellipse at 50% 85%,#fff8c2 0%,#ffb329 28%,#ed4a0b 60%,transparent 76%);`;flame.append(tongue);tongue.animate([{transform:'scale(.85,.75) rotate(-9deg)'},{transform:'scale(1.1,1.25) rotate(8deg)'},{transform:'scale(.9,.9) rotate(-5deg)'}],{duration:200+i*23,delay:-i*40,iterations:Infinity,easing:'ease-in-out'});}
    frame.append(copy,flame);document.body.append(frame);card.style.visibility='hidden';
    try{const duration=1100;flame.animate([{transform:'translateY(0)'},{transform:`translateY(${rect.height}px)`}],{duration,easing:'linear',fill:'both'});const animation=copy.animate([{clipPath:'inset(0 0 0 0)'},{clipPath:'inset(100% 0 0 0)'}],{duration,easing:'linear',fill:'both'});animation.onfinish=animation.oncancel=()=>{frame.remove();resolve();};}catch(error){frame.remove();resolve();}
  })));
}
function consumeShield(state,humanId,restoreId){const human=state.players.find(player=>player.playerId===humanId),effect=human?.effects.find(card=>card.definitionId===SCAPEGOAT_ID);if(!human||!effect)return;if(restoreId){const i=state.discardPile.findIndex(card=>card.instanceId===restoreId);if(i>=0)human.hand.push(state.discardPile.splice(i,1)[0]);}human.effects=human.effects.filter(card=>card.instanceId!==effect.instanceId);state.discardPile.push(effect);}
function finish(humanChoice){
  const active=pending;
  if(!active)return;
  const controller=active.controller;
  const human=controller.state.players.find(player=>player.type==='human');
  const shielded=hasScapegoat(human);
  let coreChoice=humanChoice,restoreId=null;
  if(shielded&&humanChoice.keepType){
    const eligible=resources(human).filter(card=>getDefinition(card).type!==humanChoice.keepType);
    const normalRequired=effectCount(controller.state,Math.min(2,eligible.length));
    if(normalRequired>humanChoice.discardIds.length){
      restoreId=eligible.find(card=>!humanChoice.discardIds.includes(card.instanceId))?.instanceId??null;
      if(restoreId)coreChoice={...humanChoice,discardIds:[...humanChoice.discardIds,restoreId]};
    }
  }
  const action={...active.action,groupChoices:allChoices(controller,coreChoice)};
  const token=active.token,humanId=human.playerId;
  const visibleBurnIds=humanChoice.discardIds;
  pending=null;
  settling={controller};
  const overlay=document.querySelector('#ark-choice-overlay');
  if(overlay)overlay.hidden=true;
  controller.cancelTimer();
  // Do not mutate state until the chosen physical cards have visibly burned.
  // This prevents a re-render or turn progression from replacing them first.
  const burn=burnChosenCards(visibleBurnIds);
  Promise.resolve(burn).catch(()=>{}).then(()=>{
    if(settling?.controller!==controller)return;
    const result=originalAct.call(controller,action,token);
    if(result?.ok&&shielded&&humanChoice.discardIds.length<coreChoice.discardIds.length){
      consumeShield(result.state,humanId,restoreId);
      controller.state=result.state;
      controller.safeRender(getNormalActions(controller.state));
    }
    if(controller.state?.qaSeed==='choice-batch-preview')controller.state.choiceBatchFrozen=true;
    controller.cancelTimer();
    if(settling?.controller!==controller)return;
    settling=null;
    controller.prepare();
  });
  return{ok:true,pendingChoice:true,state:controller.state};
}
function showDiscard(keepType){if(!pending)return;const human=pending.controller.state.players.find(player=>player.type==='human'),eligible=resources(human).filter(card=>getDefinition(card).type!==keepType),normalRequired=effectCount(pending.controller.state,Math.min(2,eligible.length)),shielded=hasScapegoat(human)&&normalRequired>0,required=Math.max(0,normalRequired-(shielded?1:0));if(required===0)return finish({keepType,discardIds:[]});const overlay=ensureUi(),picked=[];const paint=()=>{const host=renderCardRow(overlay,'方舟之外',`保留「${names[keepType]}」，從其他種類物資棄置 ${required} 張。${shielded?'《替罪羊》使你少棄置 1 張。':''}`);for(const card of eligible){const d=getDefinition(card),button=appendChoiceCard(host,card,`${names[d.type]} ${d.number}`,()=>{const index=picked.indexOf(card.instanceId);if(index>=0)picked.splice(index,1);else if(picked.length<required)picked.push(card.instanceId);else{picked.shift();picked.push(card.instanceId);}paint();});button.classList.toggle('selected',picked.includes(card.instanceId));}optionTile(host,`確認棄置 ${picked.length} / ${required} 張`,()=>{if(picked.length===required)finish({keepType,discardIds:picked});},picked.length===required);};paint();}
function showChoice(){if(!pending)return;const human=pending.controller.state.players.find(player=>player.type==='human'),cards=resources(human),types=[...new Set(cards.map(card=>getDefinition(card).type))],overlay=ensureUi();if(!cards.length)return finish({keepType:null,discardIds:[]});const host=renderCardRow(overlay,'方舟之外',types.length===1?`你只有「${names[types[0]]}」一般物資，點選後確認保留。`:'選擇 1 種保留的物資；其他種類物資需棄置 2 張，不足則全棄。');for(const type of types){const count=cards.filter(card=>getDefinition(card).type===type).length;optionTile(host,`保留 ${names[type]}\n${count} 張`,()=>showDiscard(type));}}
function begin(controller,action,token){controller.cancelTimer();pending={controller,action,token};const overlay=ensureUi();overlay.dataset.decisionCardId=action.instanceId;showChoice();return{ok:false,pendingChoice:true,state:controller.state};}
GameController.prototype.act=function(action,token=this.snapshot()){if(action?.type==='playMiracle'&&!action.groupChoices){const actor=this.state?.players.find(player=>player.playerId===action.playerId),card=actor?.hand.find(item=>item.instanceId===action.instanceId);if(card&&getDefinition(card).definitionId==='disaster-01')return begin(this,action,token);}return originalAct.call(this,action,token);};
GameController.prototype.prepare=function(){if(settling?.controller===this){this.cancelTimer();this.safeRender(getNormalActions(this.state));return;}if(pending?.controller===this&&overlayVisible()){this.cancelTimer();this.safeRender(getNormalActions(this.state));return;}if(pending?.controller===this&&!overlayVisible())pending=null;return originalPrepare.call(this);};
GameController.prototype.runAiTurn=function(generation,playerId){if(settling?.controller===this){this.cancelTimer();return;}if(pending?.controller===this&&overlayVisible()){this.cancelTimer();return;}if(pending?.controller===this&&!overlayVisible())pending=null;if(generation!==this.generation||!this.state||this.state.gameOver)return;const actor=this.state.players.find(player=>player.playerId===this.state.currentPlayer);if(actor?.type==='ai'&&actor.playerId===playerId){const arkAction=getNormalActions(this.state).find(action=>action?.type==='playMiracle'&&actor.hand.find(card=>card.instanceId===action.instanceId)&&getDefinition(actor.hand.find(card=>card.instanceId===action.instanceId)).definitionId==='disaster-01');if(arkAction)return begin(this,arkAction,this.snapshot());}return originalRunAiTurn.call(this,generation,playerId);};

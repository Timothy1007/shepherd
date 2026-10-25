import { GameController } from '../controller/game-controller.js';
import { getNormalActions } from '../game/game.js';
import { getDefinition } from '../game/cards.js';
import { appendChoiceCard, renderCardRow } from './choice-card-row.js';

const originalAct=GameController.prototype.act;
const originalPrepare=GameController.prototype.prepare;
let panel=null,revealPanel=null;
const cardLabel=card=>{const d=getDefinition(card);return d.name??`${({sheep:'群羊',food:'糧食',money:'金錢',grace:'恩典'})[d.type]??d.type} ${d.number}`;};
function optionTile(host,label,handler,selected=false){const b=document.createElement('button');b.type='button';b.className='choice-row-card rope-option-card';b.textContent=label;b.style.cssText=`height:auto;min-height:104px;padding:15px 13px;aspect-ratio:auto;color:#f5eee0;background:${selected?'#d7bd7a33':'linear-gradient(155deg,#26332de8,#17211d)'};border:1px solid ${selected?'#ffe190':'#d8c79588'};line-height:1.5;font:700 15px/1.5 Inter,system-ui,"Noto Sans TC",sans-serif`;b.onclick=handler;host.append(b);return b;}
function ensurePanel(id){let node=document.querySelector(`#${id}`);if(!node){node=document.createElement('section');node.id=id;document.body.append(node);}return node;}
function logReveal(controller,state){const view=state.lastRope,labels={same:'牌型皆相同',different:'牌型皆不同',mixed:'牌型部分相同，無額外效果'};controller.logAi(`↳ 三股展示：${view.definitionIds.map((id,i)=>`${state.players.find(p=>p.playerId===view.playerIds[i])?.displayName??view.playerIds[i]} ${cardLabel({definitionId:id})}`).join('；')}；${labels[view.mode]}`);}
function openPlayChoice(controller,action,token,actor,rope){
 panel=ensurePanel('rope-choice');panel.dataset.decisionCardId=rope.instanceId;
 const candidates=getNormalActions(controller.state).filter(candidate=>candidate.instanceId===rope.instanceId);
 let ownId=actor.hand.find(card=>card.instanceId!==rope.instanceId)?.instanceId??null;
 const paint=()=>{const host=renderCardRow(panel,'三股合成的繩子',action.glassRandom?'玻璃海已隨機選定玩家。選擇你要展示的牌。':'先選擇你要展示的牌，再選擇兩名參與展示的玩家。');for(const card of actor.hand.filter(card=>card.instanceId!==rope.instanceId)){const button=appendChoiceCard(host,card,cardLabel(card),()=>{ownId=card.instanceId;paint();});button.classList.toggle('selected',card.instanceId===ownId);}for(const candidate of candidates){const names=candidate.targetPlayerIds.map(id=>controller.state.players.find(player=>player.playerId===id)).map(player=>player.displayName??`玩家 ${player.seat}`).join('、');optionTile(host,action.glassRandom?`確認展示\n隨機目標：${names}`:`與 ${names} 一同展示`,()=>{if(!ownId)return;panel.hidden=true;const result=originalAct.call(controller,{...candidate,selectedOwnCardId:ownId,ropeChoiceComplete:true},token);if(result.ok&&result.state.lastRope)logReveal(controller,result.state);});}
 };
 paint();return{ok:false,pendingChoice:true,state:controller.state};
}
GameController.prototype.act=function(action,token=this.snapshot()){
 const actor=this.state?.players.find(player=>player.playerId===action?.playerId),rope=actor?.hand.find(card=>card.instanceId===action?.instanceId);
 if(action?.noEffect||action?.type!=='playMiracle'||rope?.definitionId!=='miracle-15'||actor.type!=='human'||action.ropeChoiceComplete)return originalAct.call(this,action,token);
 return openPlayChoice(this,action,token,actor,rope);
};
GameController.prototype.prepare=function(){
 const result=originalPrepare.call(this),pending=this.state?.pendingRope,human=this.state?.players.find(player=>player.type==='human');
 if(!pending?.waitingPlayerId||pending.waitingPlayerId!==human?.playerId){if(revealPanel)revealPanel.hidden=true;return result;}
 revealPanel=ensurePanel('rope-reveal-choice');const host=renderCardRow(revealPanel,'三股合成的繩子','選擇 1 張手牌展示；其他參與者也會各展示 1 張，之後同時結算。');
 for(const action of getNormalActions(this.state)){const card=human.hand.find(item=>item.instanceId===action.discardInstanceId);if(!card)continue;appendChoiceCard(host,card,cardLabel(card),()=>{revealPanel.hidden=true;const outcome=this.act(action,this.snapshot());if(outcome.ok&&outcome.state.lastRope)logReveal(this,outcome.state);});}
 return result;
};

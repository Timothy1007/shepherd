import { renderPlayers, renderOptions } from './player-choice-row.js';
import {GameController} from '../controller/game-controller.js';
import {getNormalActions} from '../game/game.js';
const originalAct=GameController.prototype.act;
let panel;
GameController.prototype.act=function(action,token=this.snapshot()){
 const actor=this.state?.players.find(p=>p.playerId===action?.playerId),card=actor?.hand.find(c=>c.instanceId===action?.instanceId);
 if(action?.type!=='playMiracle'||card?.definitionId!=='miracle-14'||actor.type!=='human'||action.overflowChoiceComplete)return originalAct.call(this,action,token);
 if(!panel){panel=document.createElement('div');panel.id='overflow-choice';panel.hidden=true;document.body.append(panel);}
 const choices=getNormalActions(this.state).filter(x=>x.instanceId===card.instanceId), candidateFor=id=>choices.find(c=>c.targetPlayerId===id);
 const showEffects=candidate=>{if(!candidate)return;const target=this.state.players.find(p=>p.playerId===candidate.targetPlayerId),name=target?.displayName??`玩家 ${target?.seat}`;
   const choose=choice=>{panel.hidden=true;originalAct.call(this,{...candidate,choice,overflowChoiceComplete:true},token);};
   renderOptions(panel,'杯滿盈溢',`對象：${name}。選擇你獲得的效果。`,[
     {label:'我抽 1 張牌，對方使命 +3',onSelect:()=>choose('draw')},
     {label:'我使命 +3，對方抽 1 張牌',disabled:!['sheep','food','money'].some(t=>(actor.missionProgress?.[t]??0)<(actor.mission?.[t]??0)),onSelect:()=>choose('mission')},
   ]);
 };
 panel.dataset.decisionCardId=action.instanceId;
 if(action.glassRandom)showEffects(candidateFor(action.targetPlayerId)??action);
 else {const targets=choices.map(c=>this.state.players.find(p=>p.playerId===c.targetPlayerId)).filter(Boolean);renderPlayers(panel,'杯滿盈溢','選擇 1 名其他玩家。',targets,target=>showEffects(candidateFor(target.playerId)),target=>`火種 ${target.fire} · 手牌 ${target.hand.length}`);}
 return{ok:false,pendingChoice:true,state:this.state};
};

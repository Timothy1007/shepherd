import { renderPlayers } from './player-choice-row.js';
import { GameController } from '../controller/game-controller.js';
import { getDefinition } from '../game/cards.js';

const previousAct=GameController.prototype.act;
let pending=null;

function playerTitle(player){return player?.type==='human'?'你':(player?.displayName??`Player ${player?.seat??'?'}`);}
function ensureUi(){if(document.querySelector('#our-victory-overlay'))return;const style=document.createElement('style');style.textContent=`#our-victory-overlay[hidden]{display:none!important}#our-victory-overlay{position:fixed;inset:0;z-index:31500;pointer-events:none;color:#eee8d8}`;document.head.append(style);const overlay=document.createElement('div');overlay.id='our-victory-overlay';overlay.hidden=true;document.body.append(overlay);}
function close(){const overlay=document.querySelector('#our-victory-overlay');if(overlay)overlay.hidden=true;pending=null;}
function open(controller,action,token){ensureUi();const actor=controller.state.players.find(p=>p.playerId===action.playerId),targets=controller.state.players.filter(p=>p.playerId!==actor?.playerId),overlay=document.querySelector('#our-victory-overlay');pending={controller,action,token};overlay.dataset.decisionCardId=action.instanceId;renderPlayers(overlay,'勝利歸於我們','選擇 1 名其他玩家與你建立本輪共鳴。',targets,target=>{const current=pending;if(!current)return;close();previousAct.call(current.controller,{...current.action,targetPlayerId:target.playerId},current.token);},target=>`火種 ${target.fire} · 手牌 ${target.hand.length}`);}

GameController.prototype.act=function ourVictoryChoiceAct(action,token=this.snapshot()){
  if(action?.type==='playMiracle'){
    const player=this.state?.players.find(p=>p.playerId===action.playerId),card=player?.hand.find(c=>c.instanceId===action.instanceId);
    if(player?.type==='human'&&card&&!action.glassRandom&&getDefinition(card).definitionId==='miracle-09'){
      open(this,action,token);
      return{ok:false,pendingChoice:true,state:this.state};
    }
  }
  return previousAct.call(this,action,token);
};

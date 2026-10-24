import { renderPlayers } from './player-choice-row.js';
import {GameController} from '../controller/game-controller.js';
import {getNormalActions} from '../game/game.js';
const originalAct=GameController.prototype.act;
let panel;
GameController.prototype.act=function(action,token=this.snapshot()){
 const actor=this.state?.players.find(p=>p.playerId===action?.playerId),card=actor?.hand.find(c=>c.instanceId===action?.instanceId);
 if(action?.noEffect||action?.type!=='playMiracle'||card?.definitionId!=='miracle-12'||actor.type!=='human'||action.cupChoiceComplete||action.glassRandom)return originalAct.call(this,action,token);
 if(!panel){panel=document.createElement('div');panel.id='cup-choice';panel.hidden=true;document.body.append(panel);}
 const choices=getNormalActions(this.state).filter(x=>x.instanceId===card.instanceId), targets=choices.map(c=>this.state.players.find(p=>p.playerId===c.targetPlayerId)).filter(Boolean);
 panel.dataset.decisionCardId=action.instanceId;
 renderPlayers(panel,'分杯之火','選擇 1 名其他玩家獲得 3 點火種。',targets,target=>{const choice=choices.find(c=>c.targetPlayerId===target.playerId);if(!choice)return;panel.hidden=true;originalAct.call(this,{...choice,cupChoiceComplete:true},token);},target=>`火種 ${target.fire} · 手牌 ${target.hand.length}`);
 return{ok:false,pendingChoice:true,state:this.state};
};

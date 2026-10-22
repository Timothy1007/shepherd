import {GameController} from '../controller/game-controller.js';
import {getNormalActions} from '../game/game.js';
const originalAct=GameController.prototype.act;
let panel;
GameController.prototype.act=function(action,token=this.snapshot()){
 const actor=this.state?.players.find(p=>p.playerId===action?.playerId),card=actor?.hand.find(c=>c.instanceId===action?.instanceId);
 if(action?.type!=='playMiracle'||card?.definitionId!=='miracle-12'||actor.type!=='human'||action.cupChoiceComplete)return originalAct.call(this,action,token);
 if(!panel){panel=document.createElement('section');panel.id='cup-choice';panel.style.cssText='position:fixed;z-index:42000;right:20px;bottom:24%;width:min(420px,90vw);max-height:60vh;overflow:auto;padding:16px;border:1px solid #d7bd7a;border-radius:14px;background:#17221f;color:#fff;box-shadow:0 5px 24px #0009';document.body.append(panel);}
 panel.replaceChildren();const title=document.createElement('h3');title.textContent='分杯之火：選擇其他玩家';panel.append(title);const info=document.createElement('p');info.textContent='支付 6 點火種，所選玩家獲得 3 點火種，然後你抽 1 張牌。';panel.append(info);
 for(const choice of getNormalActions(this.state).filter(x=>x.instanceId===card.instanceId)){const target=this.state.players.find(p=>p.playerId===choice.targetPlayerId),button=document.createElement('button');button.textContent=target.displayName??`玩家 ${target.seat}`;button.style.cssText='margin:4px;padding:8px;cursor:pointer';button.onclick=()=>{panel.hidden=true;originalAct.call(this,{...choice,cupChoiceComplete:true},token);};panel.append(button);}
 const cancel=document.createElement('button');cancel.textContent='取消';cancel.onclick=()=>{panel.hidden=true;};panel.append(cancel);panel.hidden=false;return{ok:false,pendingChoice:true,state:this.state};
};

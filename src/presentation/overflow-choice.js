import {GameController} from '../controller/game-controller.js';
import {getNormalActions} from '../game/game.js';
const originalAct=GameController.prototype.act;
let panel;
GameController.prototype.act=function(action,token=this.snapshot()){
 const actor=this.state?.players.find(p=>p.playerId===action?.playerId),card=actor?.hand.find(c=>c.instanceId===action?.instanceId);
 if(action?.type!=='playMiracle'||card?.definitionId!=='miracle-14'||actor.type!=='human'||action.overflowChoiceComplete)return originalAct.call(this,action,token);
 if(!panel){panel=document.createElement('section');panel.id='overflow-choice';panel.style.cssText='position:fixed;z-index:42000;right:20px;bottom:24%;width:min(420px,90vw);max-height:60vh;overflow:auto;padding:16px;border:1px solid #d7bd7a;border-radius:14px;background:#17221f;color:#fff;box-shadow:0 5px 24px #0009';document.body.append(panel);}
 panel.replaceChildren();const title=document.createElement('h3');title.textContent=action.glassRandom?'杯滿盈溢：選擇自己的效果':'杯滿盈溢：選擇玩家及自己的效果';panel.append(title);
 const info=document.createElement('p');info.textContent='你和對方各獲得不同效果：一方抽 1 張牌，另一方使一種尚未完成的使命進度 +3。';panel.append(info);
 for(const candidate of getNormalActions(this.state).filter(x=>x.instanceId===card.instanceId)){const target=this.state.players.find(p=>p.playerId===candidate.targetPlayerId);for(const [choice,label] of [['draw','我抽牌，對方使命 +3'],['mission','我使命 +3，對方抽牌']]){const button=document.createElement('button');button.textContent=action.glassRandom?`${label}（隨機目標：${target.displayName??`玩家 ${target.seat}`}）`:`${target.displayName??`玩家 ${target.seat}`}：${label}`;button.style.cssText='display:block;margin:4px;padding:8px;cursor:pointer';if(choice==='mission'&&!['sheep','food','money'].some(t=>(actor.missionProgress?.[t]??0)<(actor.mission?.[t]??0)))button.disabled=true;button.onclick=()=>{panel.hidden=true;originalAct.call(this,{...candidate,choice,overflowChoiceComplete:true},token);};panel.append(button);}}
 const cancel=document.createElement('button');cancel.textContent='取消';cancel.onclick=()=>{panel.hidden=true;};panel.append(cancel);panel.hidden=false;return{ok:false,pendingChoice:true,state:this.state};
};

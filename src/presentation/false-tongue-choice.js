import {GameController} from '../controller/game-controller.js';
import {getNormalActions} from '../game/game.js';
const previousPrepare=GameController.prototype.prepare;
let panel;
GameController.prototype.prepare=function(){
  const result=previousPrepare.call(this),window=this.state?.pendingFalseTongue?.window;
  const actorId=window?.stage==='offer'?window.responderPlayerId:window?.effect.playerId;
  const actor=this.state?.players.find(player=>player.playerId===actorId);
  if(!window||actor?.type!=='human'){if(panel)panel.hidden=true;return result;}
  if(!panel){panel=document.createElement('section');panel.id='false-tongue-choice';panel.style.cssText='position:fixed;right:20px;bottom:24%;z-index:42000;width:min(430px,90vw);padding:16px;border:1px solid #d7bd7a;border-radius:14px;background:#17221f;color:white';document.body.append(panel);}
  panel.replaceChildren();const heading=document.createElement('h3');heading.textContent='虛謊之舌';panel.append(heading);
  const message=document.createElement('p');message.textContent=window.stage==='offer'?'有玩家即將因神蹟獲得效果，要使用應對嗎？':'選擇一項效果減少。';panel.append(message);
  const labels={pass:'不使用',use:'使用應對',draw:'抽牌減少 1 張',fire:'火種減少 5 點',mission:'使命進度減少 2'};
  for(const action of getNormalActions(this.state)){const button=document.createElement('button');button.type='button';button.textContent=labels[action.choice]??action.choice;button.onclick=()=>{panel.hidden=true;this.act(action,this.snapshot());};panel.append(button);}
  panel.hidden=false;return result;
};

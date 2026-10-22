import {GameController} from '../controller/game-controller.js';
import {getDefinition} from '../game/cards.js';
const previous=GameController.prototype.safeRender;
let panel;
function renderChoice(controller, actions){
 const state=controller.state, pending=state?.pendingAftermath, human=state?.players.find(p=>p.type==='human');
 const available=(actions??[]).filter(a=>a.type==='resolveAftermath'&&a.playerId===human?.playerId);
 if(!pending||!available.length){if(panel)panel.hidden=true;return;}
 if(!panel){panel=document.createElement('section');panel.id='aftermath-choice';panel.style.cssText='position:fixed;z-index:42000;right:20px;bottom:24%;width:min(420px,90vw);max-height:65vh;overflow:auto;padding:16px;border:1px solid #d7bd7a;border-radius:14px;background:#17221f;color:#fff;box-shadow:0 5px 24px #0009';document.body.append(panel);}
 panel.replaceChildren();const title=document.createElement('h3');title.textContent='劫後餘生：選擇一項';panel.append(title);
 const info=document.createElement('p');info.textContent='支付 4 火種抽 1 張，或棄置 1 張手牌獲得 3 火種；也可以不選。';panel.append(info);
 for(const action of available){const button=document.createElement('button');button.style.cssText='margin:4px;padding:8px;border-radius:6px;cursor:pointer';if(action.choice==='pay')button.textContent='支付 4 火種，抽 1 張';else if(action.choice==='skip')button.textContent='不選擇';else{const card=human.hand.find(c=>c.instanceId===action.discardInstanceId),d=getDefinition(card);button.textContent=`棄置 ${d.name??d.type+' '+d.number}，獲得 3 火種`;}button.onclick=()=>controller.act(action,controller.snapshot());panel.append(button);}
 panel.hidden=false;
}
GameController.prototype.safeRender=function(available){const result=previous.call(this,available);try{renderChoice(this,available);}catch(error){console.error('Aftermath UI failed',error);if(panel)panel.hidden=true;}return result;};

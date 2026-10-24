import { GameController } from '../controller/game-controller.js';
import { getNormalActions } from '../game/game.js';
import { renderCardRow } from './choice-card-row.js';
import { addChoiceMotion } from './player-choice-row.js';

const originalPrepare=GameController.prototype.prepare;
let shownFor=null;
const missions={sheep:{label:'群羊',icon:'🐑'},food:{label:'糧食',icon:'🌾'},money:{label:'金錢',icon:'💰'}};

function show(controller){
  const pending=controller.state?.pendingMissionReward,entry=pending?.entries[pending.index],human=controller.state?.players.find(player=>player.type==='human');
  let overlay=document.querySelector('#mission-reward-overlay');
  if(!entry||entry.playerId!==human?.playerId){if(overlay)overlay.hidden=true;shownFor=null;return;}
  const options=getNormalActions(controller.state).filter(action=>action.type==='resolveDivisionRecipient'&&missions[action.recipientPlayerId]);
  if(!options.length){if(overlay)overlay.hidden=true;shownFor=null;return;}
  const key=`${pending.index}:${entry.playerId}:${entry.amount}`;
  if(shownFor===key&&overlay&&!overlay.hidden)return;
  if(!overlay){overlay=document.createElement('div');overlay.id='mission-reward-overlay';overlay.hidden=true;document.body.append(overlay);}
  shownFor=key;
  const host=renderCardRow(overlay,'選擇要推進的使命',`選擇一種尚未完成的使命，進度增加 ${entry.amount}。`);
  for(const action of options){
    const type=action.recipientPlayerId,info=missions[type],current=human.missionProgress?.[type]??0,required=human.mission?.[type]??0;
    const button=document.createElement('button');button.type='button';button.className='choice-row-card';
    button.style.cssText='display:flex;flex-direction:column;align-items:center;justify-content:center;gap:12px;height:auto;min-height:150px;aspect-ratio:1;color:#f5eee0;background:#26332dea;border:1px solid #d8c79588;box-shadow:0 8px 24px #0009';
    const icon=document.createElement('span');icon.textContent=info.icon;icon.style.cssText='font-size:42px;line-height:1';icon.setAttribute('aria-hidden','true');
    const title=document.createElement('strong');title.textContent=info.label;
    const progress=document.createElement('span');progress.textContent=`${current}／${required}　+${entry.amount}`;
    button.setAttribute('aria-label',`${info.label}使命，${current}／${required}，增加${entry.amount}`);
    button.append(icon,title,progress);addChoiceMotion(button);button.onclick=()=>{overlay.hidden=true;shownFor=null;controller.act(action,controller.snapshot());};host.append(button);
  }
}

GameController.prototype.prepare=function missionRewardPrepare(){
  const result=originalPrepare.call(this);
  queueMicrotask(()=>show(this));
  return result;
};

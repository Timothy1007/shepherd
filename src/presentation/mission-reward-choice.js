import { GameController } from '../controller/game-controller.js';
import { getNormalActions } from '../game/game.js';

const originalPrepare=GameController.prototype.prepare;
let shownFor=null;
const labels={sheep:'群羊',food:'糧食',money:'金錢'};

function ensureOverlay(){
  if(document.querySelector('#mission-reward-overlay'))return;
  const overlay=document.createElement('div');
  overlay.id='mission-reward-overlay';
  overlay.hidden=true;
  overlay.innerHTML='<article><span>神蹟 · 使命</span><h2>選擇要推進的使命</h2><p></p><div></div></article>';
  document.body.append(overlay);
  const style=document.createElement('style');
  style.textContent='#mission-reward-overlay[hidden]{display:none!important}#mission-reward-overlay{position:fixed;right:20px;bottom:24%;z-index:31800;display:block}#mission-reward-overlay article{width:min(520px,92vw);padding:24px;border:1px solid #d7bd7a88;border-radius:18px;background:linear-gradient(145deg,#171b18,#0c100e);color:#eee8d8;box-shadow:0 28px 80px #000b}#mission-reward-overlay h2{margin:6px 0}#mission-reward-overlay p{opacity:.8}#mission-reward-overlay div{display:grid;grid-template-columns:repeat(3,1fr);gap:10px}#mission-reward-overlay button{padding:15px 8px;border:1px solid #d7bd7a66;border-radius:12px;background:#ffffff0d;color:inherit;cursor:pointer;font-weight:700}#mission-reward-overlay button:hover{background:#d7bd7a22;border-color:#d7bd7a}';
  document.head.append(style);
}

function show(controller){
  const pending=controller.state?.pendingMissionReward,entry=pending?.entries[pending.index],human=controller.state?.players.find(player=>player.type==='human');
  if(!entry||entry.playerId!==human?.playerId)return;
  const key=`${pending.index}:${entry.playerId}:${entry.amount}`;
  if(shownFor===key&&!document.querySelector('#mission-reward-overlay')?.hidden)return;
  ensureOverlay();shownFor=key;
  const overlay=document.querySelector('#mission-reward-overlay'),options=getNormalActions(controller.state);
  overlay.querySelector('p').textContent=`選擇 1 種尚未完成的使命物資，使其進度 +${entry.amount}。`;
  const host=overlay.querySelector('div');host.replaceChildren();
  for(const action of options){const button=document.createElement('button');button.textContent=`${labels[action.recipientPlayerId]} +${entry.amount}`;button.onclick=()=>{overlay.hidden=true;shownFor=null;controller.act(action,controller.snapshot());};host.append(button);}
  overlay.hidden=false;
}

GameController.prototype.prepare=function missionRewardPrepare(){
  const result=originalPrepare.call(this);
  queueMicrotask(()=>show(this));
  return result;
};

import { GameController } from '../controller/game-controller.js';
import { getDefinition } from '../game/cards.js';

const previousAct=GameController.prototype.act;
let pending=null;

function playerTitle(player){return player?.type==='human'?'你':(player?.displayName??`Player ${player?.seat??'?'}`);}
function ensureUi(){if(document.querySelector('#our-victory-overlay'))return;const style=document.createElement('style');style.textContent=`#our-victory-overlay[hidden]{display:none!important}#our-victory-overlay{position:fixed;inset:0;z-index:31500;display:grid;place-items:center;background:#050807c7;backdrop-filter:blur(8px);padding:12px}.our-victory-panel{width:min(650px,94vw);padding:22px;border:1px solid #d7bd7a55;border-radius:20px;background:linear-gradient(145deg,#171b18,#0c100e);box-shadow:0 28px 80px #000b;color:#eee8d8}.our-victory-panel h2{margin:4px 0 6px;font-size:24px}.our-victory-panel p{opacity:.82;line-height:1.6}.our-victory-options{display:grid;grid-template-columns:repeat(3,1fr);gap:10px;margin:16px 0}.our-victory-option{padding:14px 10px;border:1px solid #d7bd7a55;border-radius:14px;background:#ffffff08;color:inherit;cursor:pointer}.our-victory-option:hover{background:#d7bd7a22;border-color:#d7bd7a}.our-victory-option strong{display:block;font-size:16px;margin-bottom:5px}.our-victory-cancel{padding:10px 16px;border-radius:10px;border:1px solid #d7bd7a55;background:#ffffff0d;color:inherit;cursor:pointer}@media(max-width:650px){.our-victory-options{grid-template-columns:1fr}}`;document.head.append(style);const overlay=document.createElement('div');overlay.id='our-victory-overlay';overlay.hidden=true;document.body.append(overlay);}
function close(){const overlay=document.querySelector('#our-victory-overlay');if(overlay)overlay.hidden=true;pending=null;}
function open(controller,action,token){ensureUi();const actor=controller.state.players.find(p=>p.playerId===action.playerId),targets=controller.state.players.filter(p=>p.playerId!==actor?.playerId),overlay=document.querySelector('#our-victory-overlay');pending={controller,action,token};overlay.innerHTML=`<article class="our-victory-panel"><span class="eyebrow">神蹟 · 共鳴｜火種</span><h2>勝利歸於我們</h2><p>選擇 1 名其他玩家與你建立本輪共鳴。輪末若你或該玩家成為使徒，你們各獲得 4 點火種。</p><div class="our-victory-options"></div><button class="our-victory-cancel" type="button">取消，放回手牌</button></article>`;const host=overlay.querySelector('.our-victory-options');for(const target of targets){const button=document.createElement('button');button.className='our-victory-option';button.type='button';button.innerHTML=`<strong>${playerTitle(target)}</strong><span>🔥 ${target.fire}　手牌 ${target.hand.length}</span>`;button.onclick=()=>{const current=pending;if(!current)return;close();previousAct.call(current.controller,{...current.action,targetPlayerId:target.playerId},current.token);};host.append(button);}overlay.querySelector('.our-victory-cancel').onclick=close;overlay.hidden=false;}

GameController.prototype.act=function ourVictoryChoiceAct(action,token=this.snapshot()){
  if(action?.type==='playMiracle'){
    const player=this.state?.players.find(p=>p.playerId===action.playerId),card=player?.hand.find(c=>c.instanceId===action.instanceId);
    if(player?.type==='human'&&card&&getDefinition(card).definitionId==='miracle-09'){
      open(this,action,token);
      return{ok:false,pendingChoice:true,state:this.state};
    }
  }
  return previousAct.call(this,action,token);
};

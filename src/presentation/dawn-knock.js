import { GameController } from '../controller/game-controller.js';
import { getDefinition } from '../game/cards.js';

const previousSafeRender = GameController.prototype.safeRender;
let overlay = null;
let battlefieldReturn = null;

function title(card){const d=getDefinition(card);if(d.kind==='resource'){const names={sheep:'群羊',food:'糧食',money:'金錢',grace:'恩典'};return `${names[d.type]??d.type} ${d.number}`;}return d.name;}

function ensureUi(){
  if(overlay)return;
  const style=document.createElement('style');
  style.textContent=`#dawn-knock-overlay[hidden]{display:none!important}#dawn-knock-overlay{position:fixed;inset:0;z-index:42500;display:grid;place-items:center;padding:18px;background:#050807c2;backdrop-filter:blur(9px)}.dawn-panel{width:min(900px,94vw);max-height:90vh;overflow:auto;padding:26px;border:1px solid #d7bd7a55;border-radius:22px;background:linear-gradient(145deg,#171d1af5,#0b100ef7);box-shadow:0 30px 90px #000c;color:#eee8d8}.dawn-panel h2{margin:5px 0 8px}.dawn-panel p{opacity:.76;line-height:1.55}.dawn-cards{display:flex;justify-content:center;gap:12px;flex-wrap:wrap;margin:20px 0}.dawn-card{width:118px;padding:7px;border:1px solid #d7bd7a44;border-radius:12px;background:#ffffff08;color:inherit;cursor:pointer;transition:.15s}.dawn-card:hover{transform:translateY(-6px);border-color:#d7bd7a}.dawn-card img{display:block;width:100%;border-radius:8px}.dawn-card span{display:block;margin-top:6px;font-size:12px}.dawn-actions{display:flex;justify-content:center;gap:10px}.dawn-actions button,#dawn-return{padding:10px 16px;border:1px solid #d7bd7a55;border-radius:10px;background:#ffffff0d;color:#eee8d8;cursor:pointer}#dawn-return{position:fixed;right:24px;bottom:24px;z-index:42600;background:#171d1af2}`;
  document.head.append(style);
  overlay=document.createElement('div');overlay.id='dawn-knock-overlay';overlay.hidden=true;document.body.append(overlay);
}

function hideBattlefieldReturn(){battlefieldReturn?.remove();battlefieldReturn=null;}
function close(){if(overlay)overlay.hidden=true;hideBattlefieldReturn();}
function viewBattlefield(){if(!overlay)return;overlay.hidden=true;hideBattlefieldReturn();battlefieldReturn=document.createElement('button');battlefieldReturn.id='dawn-return';battlefieldReturn.textContent='返回《在黎明前叩門》結算';battlefieldReturn.onclick=()=>{battlefieldReturn.remove();battlefieldReturn=null;overlay.hidden=false;};document.body.append(battlefieldReturn);}

function show(controller,available){
  const pending=controller.state?.pendingEffectDiscard;
  const player=controller.state?.players?.find(p=>p.playerId===pending?.playerId);
  if(!pending||player?.type!=='human'){close();return;}
  ensureUi();
  overlay.innerHTML=`<article class="dawn-panel" role="dialog" aria-modal="true"><span>神蹟 · 洞察｜新生</span><h2>在黎明前叩門</h2><p>這次效果抽牌已額外 +1。現在從你的手牌選擇 1 張棄置；完成後《在黎明前叩門》也會離開效果區。</p><div class="dawn-cards"></div><div class="dawn-actions"><button id="dawn-battlefield">查看戰場</button></div></article>`;
  const host=overlay.querySelector('.dawn-cards');
  for(const action of available.filter(a=>a.type==='resolveEffectDiscard'&&a.playerId===player.playerId)){
    const card=player.hand.find(c=>c.instanceId===action.discardInstanceId);if(!card)continue;const d=getDefinition(card),button=document.createElement('button');button.className='dawn-card';button.innerHTML=`<img src="${d.image}" alt="${title(card)}"><span>${title(card)}</span>`;button.onclick=()=>{close();controller.act(action,controller.snapshot());};host.append(button);
  }
  overlay.querySelector('#dawn-battlefield').onclick=viewBattlefield;
  overlay.hidden=false;
}

GameController.prototype.safeRender=function patchedDawnSafeRender(available){const result=previousSafeRender.call(this,available);try{show(this,available??[]);}catch(error){console.error('[Shepherd] dawn-knock UI failed; gameplay is unaffected.',error);close();}return result;};

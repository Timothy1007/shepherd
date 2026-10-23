import { GameController } from '../controller/game-controller.js';
import { getDefinition } from '../game/cards.js';
import { renderCardRow, appendChoiceCard } from './choice-card-row.js';

const previousSafeRender = GameController.prototype.safeRender;
let overlay = null;

function title(card){const d=getDefinition(card);if(d.kind==='resource'){const names={sheep:'群羊',food:'糧食',money:'金錢',grace:'恩典'};return `${names[d.type]??d.type} ${d.number}`;}return d.name;}

function ensureUi(){
  if(overlay)return;
  overlay=document.createElement('div');overlay.id='dawn-knock-overlay';overlay.hidden=true;document.body.append(overlay);
}
function close(){if(overlay)overlay.hidden=true;}

function show(controller,available){
  const pending=controller.state?.pendingEffectDiscard;
  const player=controller.state?.players?.find(p=>p.playerId===pending?.playerId);
  if(!pending||player?.type!=='human'){close();return;}
  ensureUi();
  const drawInfo=Number.isInteger(pending.drawnCount)?`本次實際抽到 ${pending.drawnCount} 張牌。`:'效果抽牌已完成。';
  const host=renderCardRow(overlay,'在黎明前叩門',`${drawInfo}請選擇 1 張手牌並確認棄置；完成後此牌離開效果區。`);
  let selectedAction=null;
  const controls=document.createElement('div');
  controls.style.cssText='position:absolute;left:50%;top:calc(50% + min(112px,15vh) + 28px);transform:translateX(-50%);pointer-events:auto;';
  const confirm=document.createElement('button');confirm.type='button';confirm.textContent='確認棄置';confirm.disabled=true;
  confirm.style.cssText='padding:10px 16px;border:1px solid #e1cf9b88;border-radius:10px;background:#14201be8;color:#fff;cursor:pointer;font:inherit;';
  confirm.onclick=()=>{if(!selectedAction)return;const action=selectedAction;close();controller.act(action,controller.snapshot());};
  controls.append(confirm);overlay.querySelector('.choice-row-panel').append(controls);
  const buttons=[];
  for(const action of available.filter(a=>a.type==='resolveEffectDiscard'&&a.playerId===player.playerId)){
    const card=player.hand.find(c=>c.instanceId===action.discardInstanceId);if(!card)continue;
    const button=appendChoiceCard(host,card,title(card),()=>{selectedAction=action;for(const item of buttons){item.classList.toggle('selected',item===button);item.setAttribute('aria-pressed',String(item===button));}confirm.disabled=false;});
    button.setAttribute('aria-pressed','false');buttons.push(button);
  }
  overlay.hidden=false;
}

GameController.prototype.safeRender=function patchedDawnSafeRender(available){const result=previousSafeRender.call(this,available);try{show(this,available??[]);}catch(error){console.error('[Shepherd] dawn-knock UI failed; gameplay is unaffected.',error);close();}return result;};

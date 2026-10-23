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
  const host=renderCardRow(overlay,'在黎明前叩門','效果抽牌額外 +1；選擇 1 張手牌棄置。');
  for(const action of available.filter(a=>a.type==='resolveEffectDiscard'&&a.playerId===player.playerId)){
    const card=player.hand.find(c=>c.instanceId===action.discardInstanceId);if(!card)continue;
    appendChoiceCard(host,card,title(card),()=>{close();controller.act(action,controller.snapshot());});
  }
  overlay.hidden=false;
}

GameController.prototype.safeRender=function patchedDawnSafeRender(available){const result=previousSafeRender.call(this,available);try{show(this,available??[]);}catch(error){console.error('[Shepherd] dawn-knock UI failed; gameplay is unaffected.',error);close();}return result;};

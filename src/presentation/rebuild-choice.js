import { GameController } from '../controller/game-controller.js';
import { getDefinition } from '../game/cards.js';
import { renderCardRow, appendChoiceCard } from './choice-card-row.js';

const previousSafeRender = GameController.prototype.safeRender;
const OVERLAY_ID = 'rebuild-choice-overlay';
const RETURN_ID = 'rebuild-choice-return';

function title(card){const d=getDefinition(card);return d.name??`${{sheep:'群羊',food:'糧食',money:'金錢',grace:'恩典'}[d.type]??d.type} ${d.number}`;}
function renderPending(controller,available){const pending=controller.state?.pendingRebuild,human=controller.state?.players.find(p=>p.type==='human');document.querySelector(`#${RETURN_ID}`)?.remove();if(!pending||pending.playerId!==human?.playerId){document.querySelector(`#${OVERLAY_ID}`)?.remove();return;}let o=document.querySelector(`#${OVERLAY_ID}`);if(!o){o=document.createElement('div');o.id=OVERLAY_ID;document.body.append(o);}const stage=pending.stage;let heading='',note='';if(stage==='discardInitial'){heading='先棄置 1 張手牌';note='《拆毀後重建》已成功打出，現在必須完成效果。棄置後，查看棄牌區最上方至多 5 張牌。';}else if(stage==='chooseDiscard'){heading='從棄牌區重建';note='選擇剛才查看的其中 1 張牌加入手牌。若選到神蹟或災難，之後還要再棄置 1 張手牌。';}else{heading='再棄置 1 張手牌';note='你取回的是神蹟或災難，因此必須再棄置 1 張手牌完成結算。';}
const host=renderCardRow(o,'拆毀後重建',`${heading}：${note}`);
for(const action of available){let card=null;if(stage==='chooseDiscard'){card=controller.state.discardPile.find(c=>c.instanceId===action.selectedCardId);}else{card=human.hand.find(c=>c.instanceId===action.discardInstanceId);}if(!card)continue;appendChoiceCard(host,card,title(card),()=>controller.act(action,controller.snapshot()));}
}

GameController.prototype.safeRender=function patchedRebuildSafeRender(available){const result=previousSafeRender.call(this,available);try{renderPending(this,available??[]);}catch(error){console.error('[Shepherd] rebuild-choice render failed; gameplay will continue.',error);}return result;};

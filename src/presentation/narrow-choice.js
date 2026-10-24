import { GameController } from '../controller/game-controller.js';
import { getDefinition } from '../game/cards.js';
import { effectChangeAmount } from '../game/game.js';
import { renderCardRow, appendChoiceCard } from './choice-card-row.js';
import { renderPlayers } from './player-choice-row.js';

const originalAct=GameController.prototype.act;
const originalStart=GameController.prototype.start;
const resourceNames={sheep:'群羊',food:'糧食',money:'金錢',grace:'恩典'};
let panel;
function cardName(card){const d=getDefinition(card);return d.name??`${resourceNames[d.type]??d.type} ${d.number}`;}
function show(controller,action,token,player,required){
  if(!panel){panel=document.createElement('section');panel.id='narrow-choice';panel.hidden=true;document.body.append(panel);}
  panel.dataset.decisionCardId=action.instanceId;
  const cards=player.hand.filter(card=>card.instanceId!==action.instanceId);
  const targets=controller.state.players.filter(candidate=>candidate.playerId!==player.playerId);
  const gifts=new Map();
  const playerName=target=>target.displayName??`玩家 ${target.seat}`;
  function showCards(){
    const host=renderCardRow(panel,'窄門與窄路',`選擇 ${required} 張手牌並分配給其他玩家；可以給同一人。`);
    for(const card of cards){
      const button=appendChoiceCard(host,card,cardName(card),()=>showRecipients(card));
      const selected=gifts.get(card.instanceId);
      button.classList.toggle('selected',Boolean(selected));
      if(selected){const badge=document.createElement('span');badge.textContent=`給 ${playerName(selected)}`;badge.style.cssText='position:absolute;top:calc(100% + 26px);left:-20px;right:-20px;color:#f4d995;font-size:13px;white-space:nowrap;text-shadow:0 2px 5px #000';button.append(badge);}
    }
    const confirm=document.createElement('button');confirm.type='button';confirm.textContent=`確認分配 ${gifts.size}／${required}`;confirm.disabled=gifts.size!==required;
    confirm.style.cssText='position:absolute;left:50%;top:calc(50% + min(170px,24vh));transform:translateX(-50%);pointer-events:auto;padding:9px 18px;border:1px solid #dfca92;border-radius:9px;color:#fff;background:#24342be8';
    confirm.onclick=()=>{const assigned=[...gifts].map(([instanceId,target])=>({instanceId,recipientPlayerId:target.playerId}));panel.hidden=true;originalAct.call(controller,{...action,gifts:assigned},token);};
    panel.querySelector('.choice-row-panel').append(confirm);
  }
  function showRecipients(card){
    renderPlayers(panel,'窄門與窄路',`將「${cardName(card)}」交給誰？`,targets,target=>{
      if(!gifts.has(card.instanceId)&&gifts.size>=required)gifts.delete(gifts.keys().next().value);
      gifts.set(card.instanceId,target);
      showCards();
    },target=>`🔥 ${target.fire}　手牌 ${target.hand.length}`);
  }
  showCards();
}
GameController.prototype.act=function(action,token=this.snapshot()){
  const player=this.state?.players.find(p=>p.playerId===action?.playerId),card=player?.hand.find(c=>c.instanceId===action?.instanceId);
  if(action?.type==='playMiracle'&&card?.definitionId==='miracle-11'&&player.type==='human'&&!action.gifts&&player.hand.length>1){
    const required=Math.min(player.hand.length-1,effectChangeAmount(this.state,3));
    if(!required)return originalAct.call(this,{...action,gifts:[]},token);
    show(this,action,token,player,required);
    return {ok:false,pendingChoice:true,state:this.state};
  }
  return originalAct.call(this,action,token);
};
GameController.prototype.start=function(...args){if(panel)panel.hidden=true;return originalStart.apply(this,args);};

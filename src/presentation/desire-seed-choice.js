import { GameController } from '../controller/game-controller.js';
import { getNormalActions } from '../game/game.js';
import { getDefinition } from '../game/cards.js';
import { renderCardRow, appendChoiceCard } from './choice-card-row.js';
import { renderPlayers } from './player-choice-row.js';

const previousPrepare=GameController.prototype.prepare;
const resourceNames={sheep:'群羊',food:'糧食',money:'金錢',grace:'恩典'};
let panel,activeKey=null;
function cardName(card){const d=getDefinition(card);return d.name??`${resourceNames[d.type]??d.type} ${d.number}`;}
function show(controller,event,player){
  if(!panel){panel=document.createElement('section');panel.id='desire-seed-choice';panel.hidden=true;document.body.append(panel);}
  const actions=getNormalActions(controller.state).filter(a=>a.type==='resolveDesireSeed');
  const cardIds=[...new Set(actions.map(a=>a.discardInstanceId))];
  const cards=cardIds.map(id=>player.hand.find(card=>card.instanceId===id)).filter(Boolean);
  let selectedTarget=null;
  function showCards(){
    selectedTarget=null;
    const host=renderCardRow(panel,'愛慾之種','你同時取得多張牌；選擇其中一張轉交給其他玩家。');
    for(const card of cards)appendChoiceCard(host,card,cardName(card),()=>showTargets(card));
  }
  function showTargets(card){
    const targetIds=new Set(actions.filter(a=>a.discardInstanceId===card.instanceId).map(a=>a.recipientPlayerId));
    const targets=controller.state.players.filter(candidate=>targetIds.has(candidate.playerId));
    const host=renderPlayers(panel,'愛慾之種',`將「${cardName(card)}」與此效果交給誰？`,targets,target=>{selectedTarget=target;showTargets(card);},target=>`🔥 ${target.fire}　手牌 ${target.hand.length}`);
    [...host.children].forEach((button,index)=>button.classList.toggle('selected',targets[index]?.playerId===selectedTarget?.playerId));
    const cardButton=appendChoiceCard(host,card,cardName(card),showCards);cardButton.title='點擊以更換要轉交的牌';
    const confirm=document.createElement('button');confirm.type='button';confirm.textContent=selectedTarget?`確認交給 ${selectedTarget.displayName??`玩家 ${selectedTarget.seat}`}`:'選擇接收者';confirm.disabled=!selectedTarget;
    confirm.style.cssText='position:absolute;left:50%;top:calc(50% + min(170px,24vh));transform:translateX(-50%);pointer-events:auto;padding:9px 18px;border:1px solid #dfca92;border-radius:9px;color:#fff;background:#24342be8';
    confirm.onclick=()=>{
      const action=getNormalActions(controller.state).find(a=>a.type==='resolveDesireSeed'&&a.discardInstanceId===card.instanceId&&a.recipientPlayerId===selectedTarget?.playerId);
      if(!action)return;
      panel.hidden=true;activeKey=null;controller.act(action,controller.snapshot());
    };
    panel.querySelector('.choice-row-panel').append(confirm);
  }
  showCards();
}
GameController.prototype.prepare=function(){
  const result=previousPrepare.call(this),event=this.state?.pendingDesireSeeds?.[0],player=this.state?.players.find(p=>p.playerId===event?.playerId);
  if(!event||player?.type!=='human'){if(panel)panel.hidden=true;activeKey=null;return result;}
  const key=[this.generation,event.playerId,...event.cardIds,...event.effectIds].join('|');
  if(activeKey!==key||!panel||panel.hidden){activeKey=key;show(this,event,player);}
  return result;
};

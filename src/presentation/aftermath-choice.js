import {GameController} from '../controller/game-controller.js';
import {getDefinition} from '../game/cards.js';
import {renderCardRow,appendChoiceCard} from './choice-card-row.js';
import {renderOptions} from './player-choice-row.js';
const previous=GameController.prototype.safeRender;
let panel,shownKey=null;
function label(card){const d=getDefinition(card);return d.name??`${{sheep:'群羊',food:'糧食',money:'金錢',grace:'恩典'}[d.type]??d.type} ${d.number}`;}
function renderChoice(controller,actions){
 const state=controller.state,pending=state?.pendingAftermath,human=state?.players.find(p=>p.type==='human');
 const available=(actions??[]).filter(a=>a.type==='resolveAftermath'&&a.playerId===human?.playerId);
 if(!pending||!available.length){if(panel)panel.hidden=true;shownKey=null;return;}
 const key=JSON.stringify(available);
 if(panel&&!panel.hidden&&shownKey===key)return;
 if(!panel){panel=document.createElement('section');panel.id='aftermath-choice';panel.hidden=true;document.body.append(panel);}
 shownKey=key;
 const discard=available.filter(action=>action.choice==='discard');
 const resolve=action=>{panel.hidden=true;shownKey=null;controller.act(action,controller.snapshot());};
 const options=[];
 const pay=available.find(action=>action.choice==='pay');if(pay)options.push({label:'支付 4 火種，抽 1 張牌',onSelect:()=>resolve(pay)});
 if(discard.length)options.push({label:'棄置 1 張手牌，獲得 3 火種',onSelect:()=>{
   const host=renderCardRow(panel,'劫後餘生','選擇 1 張手牌棄置，獲得 3 火種。');
   for(const action of discard){const card=human.hand.find(c=>c.instanceId===action.discardInstanceId);if(card)appendChoiceCard(host,card,label(card),()=>resolve(action));}
 }});
 const skip=available.find(action=>action.choice==='skip');if(skip)options.push({label:'不選擇',onSelect:()=>resolve(skip)});
 renderOptions(panel,'劫後餘生','選擇一項效果。',options);
}
GameController.prototype.safeRender=function(available){const result=previous.call(this,available);try{renderChoice(this,available);}catch(error){console.error('Aftermath UI failed',error);if(panel)panel.hidden=true;shownKey=null;}return result;};

import { GameController } from '../controller/game-controller.js';
import { getDefinition } from '../game/cards.js';
import { renderCardRow, appendChoiceCard } from './choice-card-row.js';

const CARD_IDS=new Set(['miracle-06','miracle-07']);
const previousAct=GameController.prototype.act;
let active=null;
const log=(c,t)=>typeof c.logAi==='function'&&c.logAi(t);

function ensureUi(){if(document.querySelector('#hope-insight-overlay'))return;const o=document.createElement('div');o.id='hope-insight-overlay';o.hidden=true;document.body.append(o);}
function cleanupBattlefieldView(){document.querySelector('#insight-battlefield-lock')?.remove();document.querySelector('#insight-return')?.remove();}
function close(){const o=document.querySelector('#hope-insight-overlay');if(o)o.hidden=true;cleanupBattlefieldView();active=null;}
function title(card){const d=getDefinition(card);return d.name??`${d.type} ${d.number}`;}
function cardType(card){return getDefinition(card).kind;}
function viewedCards(controller,definitionId){const seen=new Set(),cards=[],limit=definitionId==='miracle-06'?3:5;for(const card of controller.state.deck.slice(0,limit)){if(card.kind==='death'||card.definitionId==='death'){if(definitionId==='miracle-07')cards.push(card);break;}cards.push(card);if(definitionId==='miracle-07'){seen.add(cardType(card));if(seen.size>=3)break;}}return cards;}
function mode(definitionId){return definitionId==='miracle-07'?{name:'行向水深之處',tag:'洞察｜新生',note:'依序展示牌庫頂，直到出現3種不同牌型（物資／神蹟／災難）或共5張。選1張加入手牌，其餘以隨機順序置於牌庫底。'}:{name:'所望之實底',tag:'洞察',note:'查看牌庫頂3張牌。選擇其中1張加入手牌，其餘牌以隨機順序置於牌庫底。'};}
function commit(selectedId,bottomOrderIds){const p=active;if(!p)return;const cards=viewedCards(p.controller,p.definitionId),selected=cards.find(c=>c.instanceId===selectedId),m=mode(p.definitionId);close();log(p.controller,`↳ 你使用〈${m.name}〉：選擇〈${selected?title(selected):'未知牌'}〉加入手牌，其餘 ${bottomOrderIds.length} 張置於牌庫底`);previousAct.call(p.controller,{...p.action,selectedCardId:selectedId,bottomOrderIds},p.token);}
function showChoose(){
  ensureUi();const p=active;if(!p)return;
  const {controller,definitionId}=p,o=document.querySelector('#hope-insight-overlay'),cards=viewedCards(controller,definitionId),m=mode(definitionId);
  active.view=showChoose;cleanupBattlefieldView();
  const host=renderCardRow(o,m.name,m.note);
  for(const card of cards)appendChoiceCard(host,card,title(card),()=>commit(card.instanceId,cards.filter(c=>c.instanceId!==card.instanceId).map(c=>c.instanceId)));
}

GameController.prototype.act=function(action,token=this.snapshot()){if(action?.type==='playMiracle'){const actor=this.state?.players.find(p=>p.playerId===action.playerId),card=actor?.hand.find(c=>c.instanceId===action.instanceId);if(actor?.type==='human'&&CARD_IDS.has(card?.definitionId)&&!action.selectedCardId){const viewed=viewedCards(this,card.definitionId);if(!viewed.length||viewed.some(c=>c.kind==='death'||c.definitionId==='death'))return previousAct.call(this,action,token);active={controller:this,action,token,definitionId:card.definitionId,view:null};showChoose();return{ok:false,pendingChoice:true,state:this.state};}}return previousAct.call(this,action,token);};

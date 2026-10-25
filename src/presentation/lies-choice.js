import { renderCardRow, appendChoiceCard } from './choice-card-row.js';
import { GameController } from '../controller/game-controller.js';
import { getDefinition } from '../game/cards.js';
import { getNormalActions } from '../game/game.js';

const DISASTER_ID='disaster-02';
const originalRunAiTurn=GameController.prototype.runAiTurn;
let pending=null;
const humanOf=c=>c.state?.players.find(p=>p.type==='human');
const playerLabel=(s,id)=>{const p=s.players.find(x=>x.playerId===id);return p?`P${p.seat}${p.type==='human'?'（你）':''}`:id;};
const cardLabel=card=>{const d=getDefinition(card);if(d.kind==='resource'){const names={sheep:'群羊',food:'糧食',money:'金錢',grace:'恩典'};return`${names[d.type]??d.type} ${d.number}`;}return`〈${d.name}〉`;};
const log=(c,t)=>typeof c.logAi==='function'&&c.logAi(t);
function actionCard(c,a){return c.state.players.find(p=>p.playerId===a?.playerId)?.hand.find(x=>x.instanceId===a?.instanceId);}
function isLies(c,a){const card=actionCard(c,a);return !!card&&getDefinition(card).definitionId===DISASTER_ID;}
function aiRevealCard(player){return [...player.hand].sort((a,b)=>{const da=getDefinition(a),db=getDefinition(b);const va=da.kind==='resource'?(da.number??0):10,vb=db.kind==='resource'?(db.number??0):10;return va-vb;})[0];}
function aiTakeIndex(cards){let best=0,bestValue=-Infinity;cards.forEach((card,i)=>{const d=getDefinition(card),v=d.kind==='resource'?(d.number??0):(d.kind==='miracle'?11:10);if(v>bestValue){bestValue=v;best=i;}});return best;}
function ensureUi(){if(document.querySelector('#lies-choice-overlay'))return;const style=document.createElement('style');style.textContent=`#lies-choice-overlay[hidden]{display:none!important}#lies-choice-overlay{position:fixed;inset:0;z-index:36500;pointer-events:none;color:#eee8d8}`;document.head.append(style);const o=document.createElement('div');o.id='lies-choice-overlay';o.hidden=true;document.body.append(o);}
function close(){const o=document.querySelector('#lies-choice-overlay');if(o)o.hidden=true;document.querySelector('#lies-return')?.remove();pending=null;}
function resolveWithHumanCard(humanCard){if(!pending)return;const {controller:c,action,targets}=pending;const cards=targets.map(p=>p.type==='human'?humanCard:aiRevealCard(p));if(cards.some(x=>!x)){close();return originalRunAiTurn.call(c,c.generation,action.playerId);}const chosenIndex=aiTakeIndex(cards),chosen=cards[chosenIndex],chosenOwner=targets[chosenIndex];log(c,`↳ ${targets.map((p,i)=>`${playerLabel(c.state,p.playerId)} 展示 ${cardLabel(cards[i])}`).join('；')}`);log(c,`↳ ${playerLabel(c.state,action.playerId)} 選擇取得 ${playerLabel(c.state,chosenOwner.playerId)} 的 ${cardLabel(chosen)}`);const finalAction={...action,targetPlayerIds:targets.map(p=>p.playerId),targetCardIds:cards.map(x=>x.instanceId),chosenCardId:chosen.instanceId};close();const result=c.act(finalAction,c.snapshot());if(result?.ok)log(c,`↳ 結果：${cardLabel(chosen)} 已移至 ${playerLabel(c.state,action.playerId)} 手牌`);else log(c,`↳ 〈謊言與試探〉未能完成：${result?.reason??'未知原因'}`);}
function showHumanReveal(c,action,targets){ensureUi();const h=humanOf(c),o=document.querySelector('#lies-choice-overlay');c.cancelTimer();pending={controller:c,action,targets};log(c,`${playerLabel(c.state,action.playerId)} 使用災難〈謊言與試探〉，目標：${targets.map(p=>playerLabel(c.state,p.playerId)).join('、')}`);const host=renderCardRow(o,'謊言與試探','你是目標之一，選擇 1 張手牌展示。');for(const card of h.hand)appendChoiceCard(host,card,cardLabel(card),()=>resolveWithHumanCard(card));}
GameController.prototype.runAiTurn=function(g,pid){if(pending?.controller===this){this.cancelTimer();return;}if(g!==this.generation||!this.state||this.state.gameOver)return;const actor=this.state.players.find(p=>p.playerId===this.state.currentPlayer);if(actor?.type==='ai'&&actor.playerId===pid){const available=getNormalActions(this.state),a=available.find(x=>isLies(this,x));if(a){const targets=(a.targetPlayerIds??[]).map(id=>this.state.players.find(p=>p.playerId===id)).filter(Boolean),h=humanOf(this);if(targets.some(p=>p.playerId===h?.playerId)&&h.hand.length){showHumanReveal(this,a,targets);return;}}}return originalRunAiTurn.call(this,g,pid);};

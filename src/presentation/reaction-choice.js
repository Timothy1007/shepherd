import { GameController } from '../controller/game-controller.js';
import { executeNormalAction, getNormalActions } from '../game/game.js';
import { getDefinition } from '../game/cards.js';

const REACTION_ID='miracle-16';
const originalSafeRender=GameController.prototype.safeRender;
const originalRunAiTurn=GameController.prototype.runAiTurn;
const originalPrepare=GameController.prototype.prepare;
const originalStart=GameController.prototype.start;
let pending=null;
const isDisasterAction=(controller,action)=>{if(action?.type!=='playMiracle')return false;const actor=controller.state?.players.find(p=>p.playerId===action.playerId),card=actor?.hand.find(c=>c.instanceId===action.instanceId);return !!card&&getDefinition(card).kind==='disaster';};
const humanOf=c=>c.state?.players.find(p=>p.type==='human');
const reactionCard=p=>p?.hand.find(c=>c.definitionId===REACTION_ID);
function affectsPlayer(action,definitionId,playerId){if(definitionId==='disaster-01')return true;if(definitionId==='disaster-02')return (action.targetPlayerIds??[]).includes(playerId);return action.targetPlayerId===playerId;}
function disasterInfo(controller,action){const actor=controller.state.players.find(p=>p.playerId===action.playerId),card=actor?.hand.find(c=>c.instanceId===action.instanceId);return card?{actor,card,definition:getDefinition(card)}:null;}
function purgeCard(state,instanceId){state.deck=state.deck.filter(c=>c.instanceId!==instanceId);state.discardPile=state.discardPile.filter(c=>c.instanceId!==instanceId);state.removedForRound=state.removedForRound.filter(c=>c.instanceId!==instanceId);state.playedArea=state.playedArea.filter(c=>c.instanceId!==instanceId);for(const p of state.players){p.hand=p.hand.filter(c=>c.instanceId!==instanceId);p.effects=p.effects.filter(c=>c.instanceId!==instanceId);}}
function snapshotPlayer(player,reaction){return{fire:player.fire,lowTemperatureFire:player.lowTemperatureFire,hand:structuredClone(player.hand.filter(c=>c.instanceId!==reaction.instanceId)),effects:structuredClone(player.effects)};}
function restoreImmunePlayer(state,playerId,snapshot,reaction){purgeCard(state,reaction.instanceId);const player=state.players.find(p=>p.playerId===playerId);player.fire=snapshot.fire;player.lowTemperatureFire=snapshot.lowTemperatureFire;player.hand=structuredClone(snapshot.hand);player.effects=structuredClone(snapshot.effects);state.playedArea.push(structuredClone(reaction));}
function executeWithHumanImmunity(controller,action){const human=humanOf(controller),reaction=reactionCard(human);if(!reaction)return{ok:false,reason:'越過長夜 is no longer in hand.',state:controller.state};const snapshot=snapshotPlayer(human,reaction),result=executeNormalAction(controller.state,action);if(!result.ok)return result;restoreImmunePlayer(result.state,human.playerId,snapshot,reaction);controller.state=result.state;controller.actionToken+=1;pending=null;hide();controller.prepare();return{ok:true,state:controller.state};}
function ensureUi(){if(document.querySelector('#reaction-overlay'))return;const style=document.createElement('style');style.textContent=`#reaction-overlay[hidden]{display:none!important}#reaction-overlay{position:fixed;inset:0;z-index:35000;display:grid;place-items:center;background:#050807d8;backdrop-filter:blur(9px);padding:14px}.reaction-panel{width:min(560px,94vw);padding:22px;box-sizing:border-box;border:1px solid #d7bd7a66;border-radius:20px;background:linear-gradient(145deg,#18201d,#0b100e);box-shadow:0 30px 90px #000c;color:#eee8d8}.reaction-panel h2{margin:5px 0 8px}.reaction-panel p{line-height:1.55;opacity:.85}.reaction-card{display:flex;gap:16px;align-items:center;margin:16px 0;padding:12px;border:1px solid #d7bd7a44;border-radius:14px;background:#ffffff08}.reaction-card img{width:100px;max-height:145px;object-fit:contain;border-radius:7px}.reaction-actions{display:flex;gap:10px;justify-content:center}.reaction-actions button{padding:11px 18px;border-radius:11px;border:1px solid #d7bd7a66;background:#ffffff0d;color:inherit;cursor:pointer}.reaction-actions .use{background:#d7bd7a26;border-color:#d7bd7a}`;document.head.append(style);const o=document.createElement('div');o.id='reaction-overlay';o.hidden=true;document.body.append(o);}
function hide(){const o=document.querySelector('#reaction-overlay');if(o)o.hidden=true;}
function show(controller,action,token,generation,playerId){ensureUi();const info=disasterInfo(controller,action),human=humanOf(controller),reaction=reactionCard(human),o=document.querySelector('#reaction-overlay');if(!info||!reaction)return false;controller.cancelTimer();pending={controller,action,token};o.innerHTML=`<article class="reaction-panel"><span>【應對】時機</span><h2>${info.definition.name} 即將影響你</h2><p>你持有《越過長夜》。現在可以打出它，使你免疫這一次災難效果。原本出牌者的行動順序不會被你搶走；遊戲會停在這裡等你決定。</p><div class="reaction-card"><img src="${getDefinition(reaction).image}" alt="越過長夜"><div><strong>越過長夜</strong><p>你免疫該次災難效果。</p></div></div><div class="reaction-actions"><button class="use" id="reaction-use">打出《越過長夜》</button><button id="reaction-pass">不應對</button></div></article>`;o.querySelector('#reaction-use').onclick=()=>executeWithHumanImmunity(controller,action);o.querySelector('#reaction-pass').onclick=()=>{pending=null;hide();originalRunAiTurn.call(controller,generation,playerId);};o.hidden=false;return true;}
function guaranteePreviewReaction(controller){
  if(controller.state?.qaSeed!=='recovery-preview'||controller.state.round!==1)return;
  const state=controller.state,human=humanOf(controller);
  const existing=human.hand.filter(c=>c.definitionId===REACTION_ID);
  if(existing.length){
    const keep=existing[0];
    human.hand=human.hand.filter(c=>c.definitionId!==REACTION_ID||c.instanceId===keep.instanceId);
    return;
  }
  let incoming=null,source=null;
  const deckIndex=state.deck.findIndex(c=>c.definitionId===REACTION_ID);
  if(deckIndex>=0){incoming=state.deck.splice(deckIndex,1)[0];source=state.deck;}
  if(!incoming){for(const player of state.players.filter(p=>p.type==='ai')){const index=player.hand.findIndex(c=>c.definitionId===REACTION_ID);if(index>=0){incoming=player.hand.splice(index,1)[0];source=player.hand;break;}}}
  if(!incoming)return;
  const replaceIndex=human.hand.findIndex(c=>getDefinition(c).kind!=='disaster');
  if(replaceIndex>=0){const replaced=human.hand.splice(replaceIndex,1,incoming)[0];if(replaced)source.push(replaced);}else{const replaced=human.hand.pop();human.hand.push(incoming);if(replaced)source.push(replaced);}
}
GameController.prototype.start=function(seed){originalStart.call(this,seed);guaranteePreviewReaction(this);this.safeRender(getNormalActions(this.state));return this.state;};
GameController.prototype.safeRender=function(available=getNormalActions(this.state)){const filtered=available.filter(a=>{if(a.type!=='playMiracle')return true;const actor=this.state?.players.find(p=>p.playerId===a.playerId),card=actor?.hand.find(c=>c.instanceId===a.instanceId);return card?.definitionId!==REACTION_ID;});return originalSafeRender.call(this,filtered);};
GameController.prototype.prepare=function(){if(pending?.controller===this){this.cancelTimer();this.safeRender(getNormalActions(this.state));return;}return originalPrepare.call(this);};
GameController.prototype.runAiTurn=function(generation,playerId){if(pending?.controller===this){this.cancelTimer();return;}if(generation!==this.generation||!this.state||this.state.gameOver)return;const actor=this.state.players.find(p=>p.playerId===this.state.currentPlayer);if(actor?.type==='ai'&&actor.playerId===playerId){const actions=getNormalActions(this.state),action=actions.find(a=>{if(a.type!=='playMiracle')return false;const card=actor.hand.find(c=>c.instanceId===a.instanceId);return card?.definitionId!==REACTION_ID;})??actions.find(a=>a.type!=='playMiracle');if(action&&isDisasterAction(this,action)){const info=disasterInfo(this,action),human=humanOf(this);if(reactionCard(human)&&affectsPlayer(action,info.definition.definitionId,human.playerId)){show(this,action,this.snapshot(),generation,playerId);return;}}if(actions[0]?.type==='playMiracle'&&actor.hand.find(c=>c.instanceId===actions[0].instanceId)?.definitionId===REACTION_ID&&action){const result=executeNormalAction(this.state,action);if(result.ok){this.state=result.state;this.actionToken+=1;this.prepare();}return;}}
return originalRunAiTurn.call(this,generation,playerId);};
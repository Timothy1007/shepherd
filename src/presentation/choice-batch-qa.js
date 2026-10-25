import { GameController } from '../controller/game-controller.js';
import { getNormalActions } from '../game/game.js';
const route=new URLSearchParams(globalThis.location?.search??'').get('qa');
const SEED='choice-batch-preview';
const stages=Object.freeze([
  {title:'第 1 項｜三股合成的繩子',note:'先選自己的展示牌，再選兩名玩家；卡面應在中央透明卡列。',ids:['miracle-15','sheep-1','disaster-01']},
  {title:'第 2 項｜方舟之外',note:'選擇保留一種物資，再從其餘種類點選要棄置的牌。',ids:['disaster-01','sheep-1','food-1','money-1','sheep-2','food-2']}
]);
function take(state,id){const i=state.deck.findIndex(card=>card.definitionId===id);return i<0?null:state.deck.splice(i,1)[0];}
function reset(state){state.deck.push(...state.players.flatMap(p=>p.hand),...state.players.flatMap(p=>p.effects),...state.playedArea,...state.discardPile,...state.removedForRound);state.playedArea=[];state.discardPile=[];state.removedForRound=[];for(const p of state.players){p.hand=[];p.effects=[];p.fire=10;p.hasNormalAction=p.type==='human';}state.phase='playing';state.gameOver=false;state.currentResource=null;state.currentApostle=null;state.pendingRope=null;state.activeJudgments={};}
function configure(controller,stage){const state=controller.state,fixture=stages[stage-1];if(!fixture)return;globalThis.__shepherdSuppressHandArrivals=stage!==1;if(stage===1)globalThis.__shepherdRestartHandArrivals?.();reset(state);const human=state.players.find(p=>p.type==='human');for(const id of fixture.ids){const card=take(state,id);if(card)human.hand.push(card);}if(stage===1){for(const [i,id] of [[1,'food-1'],[2,'money-1']]){const card=take(state,id);if(card)state.players[i].hand.push(card);}}else{for(let i=1;i<state.players.length;i++){for(const id of ['sheep-3','food-3','money-3']){const card=take(state,id);if(card)state.players[i].hand.push(card);}}}state.currentPlayer=human.playerId;state.startingPlayer=human.playerId;state.choiceBatchStage=stage;controller.actionToken++;controller.logAi(`選擇介面測試 ${stage}/2：${fixture.note}`);controller.safeRender(getNormalActions(state));}
function ui(controller){if(document.querySelector('#choice-batch-qa'))return;const button=document.createElement('button');button.id='choice-batch-qa';button.type='button';button.style.cssText='position:fixed;right:18px;bottom:18px;z-index:45000;padding:10px 13px;border:1px solid #f0c56f99;border-radius:9px;background:#151a16e8;color:#fff6d8;box-shadow:0 8px 24px #0008;font:800 12px/1.2 Inter,system-ui,"Noto Sans TC",sans-serif;cursor:pointer';const paint=()=>{const stage=controller.state?.choiceBatchStage??1;button.textContent=stage>=2?'已到第 2 項測試':'下一項：方舟之外';button.disabled=stage>=2;button.style.opacity=stage>=2?'.55':'1';};button.onclick=()=>{configure(controller,2);paint();};document.body.append(button);paint();}
const previousStart=GameController.prototype.start;
GameController.prototype.start=function(seed){const result=previousStart.call(this,seed);if(seed===SEED){configure(this,1);ui(this);}return result;};
if(route==='choice-batch'&&globalThis.__shepherdController){configure(globalThis.__shepherdController,1);ui(globalThis.__shepherdController);}

// The batch is a visual test, not a continuing match. Once the Ark result is
// visible, keep that exact post-effect state on screen for inspection.
const batchPrepare=GameController.prototype.prepare;
GameController.prototype.prepare=function(){
 if(this.state?.qaSeed===SEED&&this.state.choiceBatchFrozen){this.cancelTimer();this.safeRender(getNormalActions(this.state));return;}
 return batchPrepare.call(this);
};

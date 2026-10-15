import { GameController } from '../controller/game-controller.js';
import { getNormalActions } from '../game/game.js';

const SEED='reaction-five-preview';
const SCENARIOS=Object.freeze([
  { title:'第 1 輪｜越過長夜', note:'你自己打出《灰與燼》，你同時也是受影響者；應跳出《越過長夜》。', cards:['disaster-07','miracle-16'], fire:10 },
  { title:'第 2 輪｜替罪羊', note:'你自己打出《積財寶在地上》並指定任一 NPC；應跳出《替罪羊》。', cards:['disaster-08','miracle-17'], fire:10, targetFire:10 },
  { title:'第 3 輪｜空墳墓', note:'你自己打出《灰與燼》；應跳出《空墳墓》，正常失去 3 火種後抽 1 張。', cards:['disaster-07','miracle-18'], fire:6 },
  { title:'第 4 輪｜拆毀堅固營壘', note:'你自己打出《灰與燼》；應跳出《拆毀堅固營壘》，可選擇擋掉其中一項效果。', cards:['disaster-07','miracle-19'], fire:6 },
  { title:'第 5 輪｜勝過死亡', note:'你自己打出《灰與燼》；火種 3 將降為 0，應跳出《勝過死亡》。', cards:['disaster-07','miracle-20'], fire:3 },
]);

function take(state,definitionId){const index=state.deck.findIndex(card=>card.definitionId===definitionId);return index<0?null:state.deck.splice(index,1)[0];}
function resetCards(state){state.deck.push(...state.players.flatMap(player=>player.hand),...state.players.flatMap(player=>player.effects),...state.playedArea,...state.discardPile,...state.removedForRound);state.playedArea=[];state.discardPile=[];state.removedForRound=[];for(const player of state.players){player.hand=[];player.effects=[];}}
function configure(controller,stage){const state=controller.state,scenario=SCENARIOS[stage-1];if(!scenario)return;resetCards(state);state.round=stage;state.phase='playing';state.gameOver=false;state.winner=[];state.currentResource=null;state.currentApostle=null;state.pendingEffectDiscard=null;state.pendingRebuild=null;state.pendingDivision=null;state.corruption=0;state.activeJudgments={};state.judgmentHistory=[];state.lastJudgment=null;for(const player of state.players){player.fire=10;player.lowTemperatureFire=0;player.hasNormalAction=player.type==='human';player.hasRedrawnThisRound=false;player.mission=null;player.missionProgress={sheep:0,food:0,money:0};}
  const human=state.players.find(player=>player.type==='human');
  human.fire=scenario.fire;
  if(scenario.targetFire!=null)state.players.find(player=>player.type==='ai').fire=scenario.targetFire;
  for(const definitionId of scenario.cards){const card=take(state,definitionId);if(!card)throw new Error(`應對 QA 缺少 ${definitionId}`);human.hand.push(card);}
  state.currentPlayer=human.playerId;state.startingPlayer=human.playerId;state.reactionQaStage=stage;controller.actionToken++;controller.logAi(`應對測試 ${stage}/5：${scenario.note}`);controller.safeRender(getNormalActions(state));
}
function setupUi(controller){if(document.querySelector('#reaction-five-qa'))return;const button=document.createElement('button');button.id='reaction-five-qa';button.type='button';button.style.cssText='position:fixed;right:18px;bottom:18px;z-index:45000;padding:10px 13px;border:1px solid #f0c56f99;border-radius:9px;background:#151a16e8;color:#fff6d8;box-shadow:0 8px 24px #0008;font:800 12px/1.2 Inter,system-ui,"Noto Sans TC",sans-serif;cursor:pointer';const paint=()=>{const stage=controller.state?.reactionQaStage??1;button.textContent=stage>=5?'已到第 5 輪測試':'完成本輪，下一項測試';button.disabled=stage>=5;button.style.opacity=stage>=5?'.55':'1';};button.onclick=()=>{const next=(controller.state?.reactionQaStage??1)+1;if(next<=SCENARIOS.length){configure(controller,next);paint();}};document.body.append(button);paint();}

const previousStart=GameController.prototype.start;
const previousRunAiTurn=GameController.prototype.runAiTurn;
GameController.prototype.start=function(seed){const result=previousStart.call(this,seed);if(seed===SEED){configure(this,1);setupUi(this);}return result;};
GameController.prototype.runAiTurn=function(generation,playerId){if(this.state?.qaSeed===SEED)return;return previousRunAiTurn.call(this,generation,playerId);};

if(new URLSearchParams(globalThis.location?.search??'').get('qa')==='reaction-five'&&globalThis.__shepherdController){configure(globalThis.__shepherdController,1);setupUi(globalThis.__shepherdController);}

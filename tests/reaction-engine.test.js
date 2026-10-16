import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, getNormalActions } from '../src/game/game.js';
import { consumeScapegoatProtection, disasterTargetIds, reactionOptionsFor, reactionOutcome, reactionWindowFor } from '../src/game/reaction-engine.js';

function take(state,definitionId){for(const player of state.players){const index=player.hand.findIndex(card=>card.definitionId===definitionId);if(index>=0)return player.hand.splice(index,1)[0];}const index=state.deck.findIndex(card=>card.definitionId===definitionId);assert.notEqual(index,-1,`Missing ${definitionId}`);return state.deck.splice(index,1)[0];}
function give(state,playerId,definitionId){const player=state.players.find(candidate=>candidate.playerId===playerId);const card=take(state,definitionId);player.hand.push(card);return card;}
function actionFor(state,playerId,card){state.currentPlayer=playerId;return getNormalActions(state).find(action=>action.instanceId===card.instanceId);}

test('通用應對視窗正確列出群體災難受影響者與可用應對',()=>{
  const state=createGame({seed:'reaction-window-ashes'}),ashes=give(state,'player-1','disaster-07');
  give(state,'player-2','miracle-16');give(state,'player-3','miracle-19');
  const action=actionFor(state,'player-1',ashes),window=reactionWindowFor(state,action);
  assert.deepEqual(window.targetIds,['player-1','player-2','player-3','player-4']);
  assert.deepEqual(window.optionsByPlayer['player-2'].map(option=>option.reactionId),['miracle-16']);
  assert.deepEqual(window.optionsByPlayer['player-3'][0].choices.map(choice=>choice.effectId),['lose-fire','miracle-fire-reduction']);
});

test('其他玩家打出單體災難時，受影響的玩家仍會取得可用應對',()=>{
  const state=createGame({seed:'reaction-window-opponent'}),treasure=give(state,'player-2','disaster-08');
  give(state,'player-1','miracle-16');
  const action=actionFor(state,'player-2',treasure);
  action.targetPlayerId='player-1';
  assert.deepEqual(reactionOptionsFor(state,action,'player-1'),[{reactionId:'miracle-16',kind:'immunity'}]);
});

test('通用應對視窗為多目標災難提供替罪羊可轉移的原目標',()=>{
  const state=createGame({seed:'reaction-window-lies'}),lies=give(state,'player-1','disaster-02');
  give(state,'player-4','miracle-17');
  const action=actionFor(state,'player-1',lies),targets=action.targetPlayerIds;
  const options=reactionOptionsFor(state,action,'player-4');
  assert.deepEqual(disasterTargetIds(state,action),targets);
  assert.deepEqual(options,[{reactionId:'miracle-17',kind:'redirect',targetPlayerId:targets[0]},{reactionId:'miracle-17',kind:'redirect',targetPlayerId:targets[1]}]);
});

test('通用應對結果正確辨識火種與手牌歸零，並排除打出的應對牌',()=>{
  const before={players:[{playerId:'player-1',fire:3,hand:[{instanceId:'reaction',definitionId:'miracle-20'},{instanceId:'sheep',definitionId:'sheep-1'}]}]};
  const after={players:[{playerId:'player-1',fire:0,hand:[]}]};
  assert.deepEqual(reactionOutcome(before,after,'player-1','reaction'),{lostFire:3,lostCardIds:['sheep'],crossedFireToZero:true,crossedHandToZero:true});
});

test('替罪羊殘留保護優先減少火種損失，否則復原一張被棄的手牌',()=>{
  const effect={instanceId:'scape',definitionId:'miracle-17'},lost={instanceId:'food',definitionId:'food-1'};
  const fireBefore={players:[{playerId:'player-1',fire:8,hand:[],effects:[effect]}]},fireAfter={players:[{playerId:'player-1',fire:1,hand:[],effects:[effect]}],discardPile:[]};
  assert.deepEqual(consumeScapegoatProtection(fireBefore,fireAfter,'player-1'),{used:true,kind:'fire'});assert.equal(fireAfter.players[0].fire,5);
  const handBefore={players:[{playerId:'player-1',fire:3,hand:[lost],effects:[effect]}]},handAfter={players:[{playerId:'player-1',fire:3,hand:[],effects:[effect]}],discardPile:[lost]};
  assert.deepEqual(consumeScapegoatProtection(handBefore,handAfter,'player-1'),{used:true,kind:'hand'});assert.deepEqual(handAfter.players[0].hand,[lost]);
});

test('通用應對視窗以災難預覽判定空墳墓與勝過死亡',()=>{
  const state=createGame({seed:'reaction-window-threshold'}),ashes=give(state,'player-1','disaster-07');
  const responder=state.players.find(player=>player.playerId==='player-2');responder.fire=3;give(state,'player-2','miracle-18');give(state,'player-2','miracle-20');
  const action=actionFor(state,'player-1',ashes),preview=structuredClone(state);preview.players.find(player=>player.playerId==='player-2').fire=0;
  const options=reactionOptionsFor(state,action,'player-2',{previewState:preview});
  assert.deepEqual(options.filter(option=>['miracle-18','miracle-20'].includes(option.reactionId)),[{reactionId:'miracle-18',kind:'afterFireLoss',drawCount:1},{reactionId:'miracle-20',kind:'preserveThreshold',fire:true,hand:false}]);
});

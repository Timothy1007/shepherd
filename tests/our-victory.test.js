import test from 'node:test';
import assert from 'node:assert/strict';
import {createGame,executeNormalAction,getNormalActions,settleRound} from '../src/game/game.js';

function setup(){const state=createGame({seed:'victory-mission'});state.currentPlayer='player-1';const card=state.players[0].hand.find(held=>held.definitionId==='miracle-09');const action=getNormalActions(state).find(candidate=>candidate.instanceId===card.instanceId&&candidate.targetPlayerId==='player-2');return executeNormalAction(state,action).state;}
function finishWithMissionChoices(state,apostleId){for(const player of state.players)player.hasNormalAction=false;state.currentApostle=apostleId;let result=settleRound(state);while(result.state.pendingMissionReward){const action=getNormalActions(result.state)[0];result=executeNormalAction(result.state,action);}return result;}

test('勝利歸於我們 enters the owner effect zone and links the chosen player',()=>{const state=setup();const effect=state.players[0].effects.find(card=>card.definitionId==='miracle-09');assert.equal(effect.targetPlayerId,'player-2');});
test('linked apostle result gives both linked players a chosen unfinished mission +2',()=>{for(const apostleId of ['player-1','player-2']){const state=setup();const before=state.players.slice(0,2).map(player=>({...player.missionProgress}));const result=finishWithMissionChoices(state,apostleId);assert.equal(result.ok,true,result.reason);for(let index=0;index<2;index++)assert.equal(Object.values(result.state.lastRoundResult.missions[index].progress).reduce((sum,value)=>sum+value,0),Object.values(before[index]).reduce((sum,value)=>sum+value,0)+2);}});
test('unrelated apostle does not give mission progress',()=>{const state=setup(),before=state.players.slice(0,2).map(player=>({...player.missionProgress}));const result=finishWithMissionChoices(state,'player-3');assert.deepEqual(result.state.lastRoundResult.missions.slice(0,2).map(entry=>entry.progress),before);});

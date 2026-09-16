import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, executeNormalAction, getNormalActions, settleRound, listCardLocations } from '../src/game/game.js';

function setup(targetPlayerId='player-2'){
  const state=createGame({seed:'victory-test'});
  const human=state.players[0];
  state.currentPlayer=human.playerId;
  const card=human.hand.find(c=>c.definitionId==='miracle-09');
  assert.ok(card,'victory QA should guarantee 勝利歸於我們');
  const actions=getNormalActions(state).filter(a=>a.type==='playMiracle'&&a.instanceId===card.instanceId);
  assert.deepEqual(actions.map(a=>a.targetPlayerId).sort(),['player-2','player-3','player-4']);
  const action=actions.find(a=>a.targetPlayerId===targetPlayerId);
  const played=executeNormalAction(state,action);
  assert.equal(played.ok,true,played.reason);
  const effect=played.state.players[0].effects.find(c=>c.definitionId==='miracle-09');
  assert.ok(effect);
  assert.equal(effect.targetPlayerId,targetPlayerId);
  assert.equal(played.state.playedArea.some(c=>c.definitionId==='miracle-09'),false);
  const locations=listCardLocations(played.state);
  assert.equal(locations.length,Object.keys(played.state.cardRegistry).length);
  assert.equal(new Set(locations).size,locations.length);
  return played.state;
}

function finish(state,apostleId){
  for(const player of state.players)player.hasNormalAction=false;
  state.currentApostle=apostleId;
  return settleRound(state);
}

test('勝利歸於我們 can target any other player and enters the owner effect zone',()=>{
  const state=setup('player-4');
  assert.equal(state.players[0].effects.find(c=>c.definitionId==='miracle-09').targetPlayerId,'player-4');
});

test('勝利歸於我們 gives both linked players 4 fire when either becomes apostle',()=>{
  for(const apostleId of ['player-1','player-2']){
    const state=setup('player-2');
    const before=state.players.map(p=>p.fire);
    const result=finish(state,apostleId);
    assert.equal(result.ok,true,result.reason);
    assert.equal(result.state.players[0].fire,before[0]+4);
    assert.equal(result.state.players[1].fire,before[1]+4);
  }
});

test('勝利歸於我們 does not award fire when an unrelated player becomes apostle',()=>{
  const state=setup('player-2');
  const before=state.players.map(p=>p.fire);
  const result=finish(state,'player-3');
  assert.equal(result.ok,true,result.reason);
  assert.equal(result.state.players[0].fire,before[0]);
  assert.equal(result.state.players[1].fire,before[1]);
});

test('灰與燼 reduces each 勝利歸於我們 miracle fire gain independently',()=>{
  const state=setup('player-2');
  state.players[0].roundModifiers={miracleFireReduction:2};
  state.players[1].roundModifiers={miracleFireReduction:2};
  const before=state.players.map(p=>p.fire);
  const result=finish(state,'player-1');
  assert.equal(result.ok,true,result.reason);
  assert.equal(result.state.players[0].fire,before[0]+2);
  assert.equal(result.state.players[1].fire,before[1]+2);
});

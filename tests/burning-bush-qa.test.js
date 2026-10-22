import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, executeNormalAction, getNormalActions } from '../src/game/game.js';

function activate(seed) {
  let state=createGame({seed});
  const player=state.players[0];
  assert.deepEqual(player.hand.map(card=>card.definitionId),['miracle-21']);
  const action=getNormalActions(state).find(item=>item.instanceId===player.hand[0].instanceId);
  const result=executeNormalAction(state,action);
  assert.equal(result.ok,true,result.reason);
  assert.equal(result.state.currentPlayer,'player-4');
  assert.ok(result.state.players[0].effects.some(card=>card.definitionId==='miracle-21'));
  return result.state;
}

test('QA seed grants card and attacker; treasure causes actual loss and capped return',()=>{
  let state=activate('burning-bush-treasure');
  const attack=getNormalActions(state).find(a=>a.targetPlayerId==='player-1');
  assert.ok(attack);
  const result=executeNormalAction(state,attack);assert.equal(result.ok,true,result.reason);
  state=result.state;
  assert.equal(state.players[0].fire,19);
  assert.deepEqual(state.lastBurningBush,{playerId:'player-1',lost:10,gain:4});
  assert.ok(state.discardPile.some(card=>card.definitionId==='miracle-21'));
  assert.equal(state.players[0].effects.some(card=>card.definitionId==='miracle-21'),false);
});

test('QA ashes first loses fire and its miracle reduction limits the refund',()=>{
  let state=activate('burning-bush-ashes');
  const action=getNormalActions(state)[0];
  const result=executeNormalAction(state,action);assert.equal(result.ok,true,result.reason);
  state=result.state;
  assert.equal(state.players[0].fire,21);
  assert.deepEqual(state.lastBurningBush,{playerId:'player-1',lost:4,gain:0});
  assert.equal(state.players[0].roundModifiers.miracleFireReduction,2);
});

test('normal game never contains the QA-only miracle',()=>{
  const state=createGame({seed:'ordinary-game'});
  assert.equal(Object.values(state.cardRegistry).includes('miracle-21'),false);
});

test('zero actual loss keeps the effect for a later disaster',()=>{
  let state=activate('burning-bush-treasure');
  state.players[0].fire=4;
  const attack=getNormalActions(state).find(a=>a.targetPlayerId==='player-1');
  const result=executeNormalAction(state,attack);assert.equal(result.ok,true,result.reason);
  state=result.state;
  assert.equal(state.players[0].fire,4);
  assert.equal(state.lastBurningBush,undefined);
  assert.ok(state.players[0].effects.some(card=>card.definitionId==='miracle-21'));
});

test('blocking the fire-loss part of ashes does not trigger the effect',()=>{
  let state=activate('burning-bush-ashes');
  const action={...getNormalActions(state)[0],blockedEffects:{'player-1':'lose-fire'}};
  const result=executeNormalAction(state,action);assert.equal(result.ok,true,result.reason);
  state=result.state;
  assert.equal(state.players[0].fire,25);
  assert.ok(state.players[0].effects.some(card=>card.definitionId==='miracle-21'));
});

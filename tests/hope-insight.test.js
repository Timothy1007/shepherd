import test from 'node:test';
import assert from 'node:assert/strict';
import {createGame,getNormalActions,executeNormalAction,listCardLocations} from '../src/game/game.js';

test('所望之實底 lets the player keep one of top three and order the rest on deck bottom',()=>{
  const state=createGame({seed:'insight-core'});
  state.currentPlayer='player-1';
  const player=state.players[0];
  const card=player.hand.find(c=>c.definitionId==='miracle-06');
  assert.ok(card);
  const action=getNormalActions(state).find(a=>a.instanceId===card.instanceId);
  assert.ok(action);
  const top=state.deck.slice(0,3);
  assert.equal(top.length,3);
  const selected=top[1];
  const rest=[top[2],top[0]];
  const beforeHand=player.hand.length;
  const result=executeNormalAction(state,{...action,selectedCardId:selected.instanceId,bottomOrderIds:rest.map(c=>c.instanceId)});
  assert.equal(result.ok,true,result.reason);
  const next=result.state.players[0];
  assert.equal(next.hand.length,beforeHand);
  assert.ok(next.hand.some(c=>c.instanceId===selected.instanceId));
  assert.deepEqual(result.state.deck.slice(-2).map(c=>c.instanceId),rest.map(c=>c.instanceId));
  assert.ok(result.state.playedArea.some(c=>c.instanceId===card.instanceId));
  const locations=listCardLocations(result.state);
  assert.equal(new Set(locations).size,locations.length);
  assert.equal(locations.length,Object.keys(result.state.cardRegistry).length);
});

test('所望之實底 rejects cards outside viewed top three',()=>{
  const state=createGame({seed:'insight-invalid'});
  state.currentPlayer='player-1';
  const card=state.players[0].hand.find(c=>c.definitionId==='miracle-06');
  const action=getNormalActions(state).find(a=>a.instanceId===card.instanceId);
  const outsider=state.deck[4];
  const result=executeNormalAction(state,{...action,selectedCardId:outsider.instanceId});
  assert.equal(result.ok,false);
  assert.equal(result.state,state);
});

test('AI/default resolution of 所望之實底 keeps the first viewed card and preserves remaining order',()=>{
  const state=createGame({seed:'insight-ai'});
  const source=state.players[0].hand.find(c=>c.definitionId==='miracle-06');
  state.players[0].hand=state.players[0].hand.filter(c=>c.instanceId!==source.instanceId);
  state.players[1].hand.unshift(source);
  state.currentPlayer='player-2';
  const top=state.deck.slice(0,3);
  const action=getNormalActions(state).find(a=>a.instanceId===source.instanceId);
  const result=executeNormalAction(state,action);
  assert.equal(result.ok,true,result.reason);
  assert.ok(result.state.players[1].hand.some(c=>c.instanceId===top[0].instanceId));
  assert.deepEqual(result.state.deck.slice(-2).map(c=>c.instanceId),top.slice(1).map(c=>c.instanceId));
});

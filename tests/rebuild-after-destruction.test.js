import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, executeNormalAction, getNormalActions, listCardLocations } from '../src/game/game.js';
import { getDefinition } from '../src/game/cards.js';

function act(state, action){const result=executeNormalAction(state,action);assert.equal(result.ok,true,result.reason);return result.state;}
function human(state){return state.players.find(player=>player.type==='human');}
function rebuildPlay(state){state.currentPlayer=human(state).playerId;const card=human(state).hand.find(c=>c.definitionId==='miracle-08');return getNormalActions(state).find(a=>a.instanceId===card.instanceId);}

test('rebuild QA guarantees 拆毀後重建 and a four-card discard fixture',()=>{
  const state=createGame({seed:'rebuild-preview'});
  assert.ok(human(state).hand.some(c=>c.definitionId==='miracle-08'));
  assert.equal(state.discardPile.length,4);
});

test('拆毀後重建 discards one, views top five, and may recover a resource without extra discard',()=>{
  let state=createGame({seed:'rebuild-resource'});
  const beforeTotal=Object.keys(state.cardRegistry).length;
  state=act(state,rebuildPlay(state));
  assert.equal(state.pendingRebuild.stage,'discardInitial');
  const firstDiscard=getNormalActions(state)[0];
  const discardedId=firstDiscard.discardInstanceId;
  state=act(state,firstDiscard);
  assert.equal(state.pendingRebuild.stage,'chooseDiscard');
  assert.equal(state.pendingRebuild.viewedIds.length,5);
  assert.ok(state.pendingRebuild.viewedIds.includes(discardedId));
  const resourceChoice=getNormalActions(state).find(a=>getDefinition(state.discardPile.find(c=>c.instanceId===a.selectedCardId)).kind==='resource');
  const recoveredId=resourceChoice.selectedCardId;
  state=act(state,resourceChoice);
  assert.equal(state.pendingRebuild,null);
  assert.ok(human(state).hand.some(c=>c.instanceId===recoveredId));
  assert.ok(state.playedArea.some(c=>c.definitionId==='miracle-08'));
  const locations=listCardLocations(state);
  assert.equal(locations.length,beforeTotal);
  assert.equal(new Set(locations).size,locations.length);
});

test('recovering a miracle or disaster requires one additional discard',()=>{
  let state=createGame({seed:'rebuild-special'});
  state=act(state,rebuildPlay(state));
  state=act(state,getNormalActions(state)[0]);
  const specialChoice=getNormalActions(state).find(a=>{const card=state.discardPile.find(c=>c.instanceId===a.selectedCardId);return ['miracle','disaster'].includes(getDefinition(card).kind);});
  assert.ok(specialChoice);
  const recoveredId=specialChoice.selectedCardId;
  state=act(state,specialChoice);
  assert.equal(state.pendingRebuild.stage,'discardExtra');
  assert.ok(human(state).hand.some(c=>c.instanceId===recoveredId));
  const extra=getNormalActions(state)[0];
  const extraDiscardId=extra.discardInstanceId;
  state=act(state,extra);
  assert.equal(state.pendingRebuild,null);
  assert.ok(state.discardPile.some(c=>c.instanceId===extraDiscardId));
});

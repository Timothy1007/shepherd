import test from 'node:test';
import assert from 'node:assert/strict';
import {createGame, executeNormalAction, getNormalActions} from '../src/game/game.js';
import {reactionOutcome} from '../src/game/reaction-engine.js';

function activate() {
  const state = createGame({seed:'burning-bush-treasure'});
  const bush = state.players[0].hand.find(card => card.definitionId === 'miracle-21');
  const action = getNormalActions(state).find(item => item.instanceId === bush.instanceId);
  const result = executeNormalAction(state, action);
  assert.equal(result.ok, true, result.reason);
  return result.state;
}

test('空墳墓應依返還前實際失去的火種計算', () => {
  const state = activate();
  state.players[0].fire = 15;
  const attack = getNormalActions(state).find(item => item.targetPlayerId === 'player-1');
  const result = executeNormalAction(state, attack);
  assert.equal(result.ok, true, result.reason);
  assert.equal(result.state.lastBurningBush.lost, 6);
  assert.equal(reactionOutcome(state, result.state, 'player-1').lostFire, 6);
});

import {reactionOptionsFor, consumeScapegoatProtection} from '../src/game/reaction-engine.js';
import {finishDeferredBurningBush} from '../src/game/game.js';

for (const [scenario,reactionId,target] of [['immunity','miracle-16','player-1'],['scapegoat','miracle-17','player-2'],['empty','miracle-18','player-1'],['stronghold','miracle-19',null],['overcome','miracle-20',null]]) {
  test(`應對 QA ${scenario}：真人持牌且存在合法應對時機`, () => {
    let state=createGame({seed:`burning-bush-reaction-${scenario}`});
    assert.deepEqual(state.players[0].hand.map(c=>c.definitionId),['miracle-21',reactionId]);
    const bush=state.players[0].hand.find(c=>c.definitionId==='miracle-21');
    const placed=executeNormalAction(state,getNormalActions(state).find(a=>a.instanceId===bush.instanceId));
    assert.equal(placed.ok,true,placed.reason);state=placed.state;
    const attack=getNormalActions(state).find(a=>target===null||a.targetPlayerId===target);
    assert.ok(attack);
    const preview=executeNormalAction(state,{...attack,deferBurningBush:true});
    assert.equal(preview.ok,true,preview.reason);
    assert.ok(reactionOptionsFor(state,attack,'player-1',{previewState:preview.state}).some(o=>o.reactionId===reactionId));
  });
}

test('延後結算：免疫保留荊棘，不得複製或返還',()=>{
  const state=activate(),attack=getNormalActions(state).find(a=>a.targetPlayerId==='player-1');
  const result=executeNormalAction(state,{...attack,deferBurningBush:true});assert.equal(result.ok,true);
  result.state.players[0].fire=state.players[0].fire;
  finishDeferredBurningBush(result.state,{'player-1':0});
  assert.equal(result.state.players[0].effects.filter(c=>c.definitionId==='miracle-21').length,1);
  assert.equal(result.state.discardPile.filter(c=>c.definitionId==='miracle-21').length,0);
});

test('延後結算：替罪羊抵減後，荊棘依剩餘實際損失返還',()=>{
  const state=activate(),h=state.players[0];
  const shield=state.deck.find(c=>c.definitionId==='miracle-17');
  assert.ok(shield);state.deck=state.deck.filter(c=>c.instanceId!==shield.instanceId);h.effects.push(shield);
  const attack=getNormalActions(state).find(a=>a.targetPlayerId==='player-1');
  const result=executeNormalAction(state,{...attack,deferBurningBush:true});assert.equal(result.ok,true);
  assert.equal(consumeScapegoatProtection(state,result.state,'player-1').used,true);
  finishDeferredBurningBush(result.state,{'player-1':state.players[0].fire-result.state.players[0].fire});
  assert.deepEqual(result.state.lastBurningBush,{playerId:'player-1',lost:6,gain:3});
  assert.equal(result.state.players[0].fire,22);
});

test('替罪羊抵減測試局：先持有保護效果，再打出荊棘',()=>{
  const state=createGame({seed:'burning-bush-reaction-shield'});
  assert.deepEqual(state.players[0].hand.map(c=>c.definitionId),['miracle-21']);
  assert.ok(state.players[0].effects.some(c=>c.definitionId==='miracle-17'));
  const result=executeNormalAction(state,getNormalActions(state).find(a=>a.instanceId===state.players[0].hand[0].instanceId));
  assert.equal(result.ok,true,result.reason);
  assert.equal(result.state.currentPlayer,'player-4');
});

import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, executeNormalAction, getNormalActions } from '../src/game/game.js';

function activate(seed) {
  let state=createGame({seed});
  const player=state.players[0];
  assert.deepEqual(player.hand.map(card=>card.definitionId),['miracle-21']);
  const blind=getNormalActions(state).find(item=>item.type==='playBlindnessPile');
  if(blind){
    const revealed=executeNormalAction(state,blind);
    assert.equal(revealed.ok,true,revealed.reason);
    state=revealed.state;
    assert.equal(state.pendingBlindness.instanceId,player.hand[0].instanceId);
  }
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

function attackHuman(state,target='player-1') {
  const blind=getNormalActions(state).find(item=>item.type==='playBlindnessPile');
  if(blind){
    const revealed=executeNormalAction(state,blind);
    assert.equal(revealed.ok,true,revealed.reason);
    state=revealed.state;
  }
  const attack=getNormalActions(state).find(item=>item.targetPlayerId===target);
  assert.ok(attack,'QA disaster action must target the chosen player');
  const result=executeNormalAction(state,attack);
  assert.equal(result.ok,true,result.reason);
  return result.state;
}

for(const [judgment,lost,gain,remaining] of [
  ['overturn-1',3,1,23], ['overturn-2',1,0,24],
  ['division-1',10,4,19], ['division-2',10,4,19],
  ['blindness-1',10,4,19], ['blindness-2',10,4,19],
  ['war-1',10,4,19], ['war-2',10,4,19],
  ['revelation-1',10,4,19], ['revelation-2',10,4,19],
  ['disorder-1',10,4,19], ['disorder-2',10,4,19],
]) test(`荊棘與 ${judgment}：合法出牌、失火與返還`,()=>{
  const state=attackHuman(activate(`burning-bush-treasure--${judgment}`));
  assert.deepEqual(state.lastBurningBush,{playerId:'player-1',lost,gain});
  assert.equal(state.players[0].fire,remaining);
  if(judgment.startsWith('division'))assert.equal(state.pendingDivision,null);
});

test('沉淪 I：攻擊相鄰玩家時，荊棘持有者受擴散並依實際損失觸發',()=>{
  const initial=activate('burning-bush-treasure--sinking-1');
  assert.equal(getNormalActions(initial)[0].targetPlayerId,'player-2');
  const state=attackHuman(initial,'player-2');
  assert.equal(state.players[0].fire,19);
  assert.deepEqual(state.lastBurningBush,{playerId:'player-1',lost:10,gain:4});
  assert.equal(state.players[1].fire,15);
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

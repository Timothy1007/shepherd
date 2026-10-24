import test from 'node:test';
import assert from 'node:assert/strict';
import { GameController } from '../src/controller/game-controller.js';
import { createGame, getNormalActions, executeNormalAction } from '../src/game/game.js';

function situation(single){
  let state=createGame({seed:'armageddon-test'});
  if(single){const hand=state.players[0].hand;const index=hand.findIndex(c=>c.definitionId==='food-8');state.deck.push(hand.splice(index,1)[0]);}
  const action=getNormalActions(state).find(a=>a.instanceId==='disaster-12#1');
  const result=executeNormalAction(state,action);
  assert.equal(result.ok,true);
  return result.state;
}
function controllerFor(state){const controller=new GameController({render:()=>{},setTimer:()=>1,clearTimer:()=>{}});controller.state=state;return controller;}
test('forced discard with a single legal card resolves automatically',async()=>{
  const controller=controllerFor(situation(true));
  assert.equal(getNormalActions(controller.state).length,1);
  controller.prepare();await Promise.resolve();
  assert.equal(controller.state.players[0].hand.some(c=>c.definitionId==='sheep-8'),false);
  assert.ok(controller.state.discardPile.some(c=>c.definitionId==='sheep-8'));
});
test('tied discard candidates remain a player choice',async()=>{
  const controller=controllerFor(situation(false));
  assert.equal(getNormalActions(controller.state).length,2);
  controller.prepare();await Promise.resolve();
  assert.equal(controller.state.players[0].hand.some(c=>c.definitionId==='sheep-8'),true);
  assert.equal(controller.state.players[0].hand.some(c=>c.definitionId==='food-8'),true);
});

test('burns only after a hand card actually enters discard',async()=>{
  const fs=await import('node:fs');const vm=await import('node:vm');
  const src=fs.readFileSync(new URL('../src/presentation/app.js',import.meta.url),'utf8');
  const code=src.slice(src.indexOf('let previousBurnSnapshot=null;'),src.indexOf('function renderHand(state)'));
  let burns=0;const card={dataset:{cardId:'card-1'}};
  const context={document:{hidden:false,querySelectorAll:()=>[card]},console};
  vm.createContext(context);vm.runInContext(code,context);
  vm.runInContext('burnDiscardCard=()=>{globalThis.burns=(globalThis.burns??0)+1}',context);
  const state=(hand,discard)=>({round:1,players:[{hand:hand.map(instanceId=>({instanceId}))}],discardPile:discard.map(instanceId=>({instanceId}))});
  context.renderDiscardBurns(state(['card-1'],[]));
  context.renderDiscardBurns(state(['card-1'],[]));
  assert.equal(context.burns,undefined,'protected card stays in hand');
  context.renderDiscardBurns(state([],['card-1']));
  assert.equal(context.burns,1);
  context.renderDiscardBurns(state([],['card-1']));
  assert.equal(context.burns,1,'rerender does not repeat the burn');
});

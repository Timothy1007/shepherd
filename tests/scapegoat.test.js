import test from 'node:test';
import assert from 'node:assert/strict';
import { GameController } from '../src/controller/game-controller.js';
import { DEFINITION_BY_ID } from '../src/game/cards.js';
import { getNormalActions, listCardLocations } from '../src/game/game.js';
import '../src/presentation/reaction-choice.js';

const noTimer=()=>1;
const noClear=()=>{};

test('替罪羊 is registered as a reaction miracle',()=>{
  const card=DEFINITION_BY_ID.get('miracle-17');
  assert.equal(card?.name,'替罪羊');
  assert.equal(card?.kind,'miracle');
  assert.equal(card?.reaction,true);
  assert.deepEqual(card?.tags,['應對','共鳴']);
});

test('recovery preview guarantees exactly one 替罪羊 to human and 積財寶在地上 to AI',()=>{
  const controller=new GameController({render:()=>{},setTimer:noTimer,clearTimer:noClear,delay:()=>999999});
  const state=controller.start('recovery-preview');
  const human=state.players.find(p=>p.type==='human');
  const ais=state.players.filter(p=>p.type==='ai');
  assert.equal(human.fire,10);
  assert.equal(human.hand.filter(c=>c.definitionId==='miracle-17').length,1);
  assert.equal(human.hand.filter(c=>c.definitionId==='miracle-16').length,0);
  assert.ok(ais.some(p=>p.hand.some(c=>c.definitionId==='disaster-08')));
  const locations=listCardLocations(state);
  assert.equal(locations.length,Object.keys(state.cardRegistry).length);
  assert.equal(new Set(locations).size,locations.length);
});

test('替罪羊 remains legal as a normal miracle and resolves as one action outside reaction timing',()=>{
  const controller=new GameController({render:()=>{},setTimer:noTimer,clearTimer:noClear,delay:()=>999999});
  controller.start('recovery-preview');
  const human=controller.state.players.find(p=>p.type==='human');
  controller.state.currentPlayer=human.playerId;
  controller.state.players.forEach(p=>{p.hasNormalAction=true;});
  controller.state.currentResource={type:'sheep',number:9,instanceId:'qa-current'};
  const action=getNormalActions(controller.state).find(a=>a.type==='playMiracle'&&human.hand.find(c=>c.instanceId===a.instanceId)?.definitionId==='miracle-17');
  assert.ok(action);
  const result=controller.act(action,controller.snapshot());
  assert.equal(result.ok,true,result.reason);
  assert.equal(controller.state.currentResource,null);
  assert.equal(controller.state.players.find(p=>p.playerId===human.playerId).hand.some(c=>c.definitionId==='miracle-17'),false);
  assert.equal(controller.state.playedArea.some(c=>c.definitionId==='miracle-17'),true);
});
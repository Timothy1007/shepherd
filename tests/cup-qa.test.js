import test from 'node:test';
import assert from 'node:assert/strict';
import {createGame,getNormalActions,executeNormalAction} from '../src/game/game.js';
function play(s){const a=getNormalActions(s).find(a=>a.instanceId==='miracle-12#qa'&&a.targetPlayerId==='player-2');assert.ok(a);const r=executeNormalAction(s,a);assert.equal(r.ok,true,r.reason);return r.state;}
function effect(s,owner,id){for(const zone of [s.deck,...s.players.map(p=>p.hand)]){const i=zone.findIndex(c=>c.definitionId===id);if(i>=0){owner.effects.push(zone.splice(i,1)[0]);return;}}throw Error('missing '+id);}
test('支付六點，另一名玩家獲得三點，自己抽一張',()=>{let s=createGame({seed:'cup-test'});s=play(s);assert.equal(s.players[0].fire,0);assert.equal(s.players[1].fire,3);assert.equal(s.players[0].hand.length,1);});
test('不足六點不可支付，測試牌不在正式牌組',()=>{const s=createGame({seed:'cup-test'});s.players[0].fire=5;assert.equal(getNormalActions(s).some(a=>a.instanceId==='miracle-12#qa'),false);assert.equal(Object.values(createGame({seed:'normal'}).cardRegistry).includes('miracle-12'),false);});
test('傾覆 II 不改變支付代價，目標獲一點',()=>{let s=createGame({seed:'cup-test'});s.activeJudgments={'異變':'overturn-2'};s=play(s);assert.equal(s.players[0].fire,0);assert.equal(s.players[1].fire,1);assert.equal(s.players[0].hand.length,1);});
test('灰與燼影響目標獲得的神蹟火種',()=>{let s=createGame({seed:'cup-test'});s.players[1].roundModifiers={miracleFireReduction:2};s=play(s);assert.equal(s.players[1].fire,1);});
test('目標的第二次生命與黎明先結算，再由出牌者抽牌',()=>{let s=createGame({seed:'cup-test'});effect(s,s.players[1],'miracle-23');effect(s,s.players[1],'miracle-13');s=play(s);assert.equal(s.currentPlayer,'player-2');assert.equal(s.players[0].hand.length,0);assert.equal(getNormalActions(s)[0].type,'resolveEffectDiscard');const r=executeNormalAction(s,getNormalActions(s)[0]);assert.equal(r.ok,true,r.reason);assert.equal(r.state.players[0].hand.length,1);});

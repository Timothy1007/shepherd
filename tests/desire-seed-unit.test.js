import test from 'node:test';
import assert from 'node:assert/strict';
import {desireSeedEvent, desireSeedChoices, completeDesireSeed} from '../src/game/desire-seed.js';
const card = instanceId => ({instanceId});
function fixture(){return {players:[{playerId:'a',hand:[card('x'),card('y'),card('old')],effects:[{instanceId:'d',definitionId:'disaster-14'}]},{playerId:'b',hand:[],effects:[]},{playerId:'c',hand:[],effects:[]}]};}
test('一次效果加入兩張，僅能轉交其中一張給其他人',()=>{const s=fixture(),event=desireSeedEvent(s.players[0],[card('x'),card('y')],'effect'),choices=desireSeedChoices(s,event);assert.equal(choices.length,4);assert.ok(choices.every(c=>c.cardId!=='old'&&c.recipientPlayerId!=='a'));assert.equal(completeDesireSeed(s,event,choices[0]),true);assert.equal(s.players[0].effects.length,0);assert.equal(s.players[1].hand.length,1);assert.equal(s.players[1].effects[0].instanceId,'d');});
test('整備及單張不觸發',()=>{const s=fixture();assert.equal(desireSeedEvent(s.players[0],[card('x'),card('y')],'preparation'),null);assert.equal(desireSeedEvent(s.players[0],[card('x')],'effect'),null);});
test('效果已被移除時不可轉交',()=>{const s=fixture(),event=desireSeedEvent(s.players[0],[card('x'),card('y')],'effect');s.players[0].effects=[];assert.deepEqual(desireSeedChoices(s,event),[]);});

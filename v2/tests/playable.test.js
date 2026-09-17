import test from 'node:test';
import assert from 'node:assert/strict';
import { createPlayableGame, legalActions, playCard, runAiTurn, resolveAuction } from '../src/playable/game.js';

test('playable state starts four seats and mission round',()=>{
 const s=createPlayableGame({seed:'qa',threshold:30});
 assert.equal(s.players.length,4);assert.equal(s.round,1);assert.equal(s.phase,'actions');
 assert.ok(s.players.every(p=>p.mission));
});

test('human eventually has at least pass action',()=>{
 const s=createPlayableGame({seed:'qa2'});s.currentPlayer='p1';
 assert.ok(legalActions(s,'p1').some(a=>a.type==='pass'));
});

test('resource corruption is applied when resource is played',()=>{
 const s=createPlayableGame({seed:'qa3',threshold:99});s.currentPlayer='p1';s.currentResource=null;s.freeResource=true;
 const p=s.players[0];p.hand=[{id:'x',type:'resource',resourceType:'sheep',printedNumber:7,name:'群羊 7',corruption:1}];
 playCard(s,'p1','x',{declared:'sheep'});assert.equal(s.corruption,1);assert.equal(s.currentApostle,'p1');
});

test('AI can advance a turn without throwing',()=>{
 const s=createPlayableGame({seed:'qa4'});s.currentPlayer='p2';runAiTurn(s);assert.ok(s.currentPlayer!==null);
});

test('auction resolves and advances round',()=>{
 const s=createPlayableGame({seed:'qa5'});s.round=2;s.phase='auction';s.auction={options:[{id:'newbirth',name:'破土與新生',text:'x'},{id:'branch',name:'枝椏',text:'x'},{id:'money+',name:'安眠',text:'x'},{id:'food+',name:'豐盛',text:'x'}]};
 resolveAuction(s,{blessingId:'newbirth',bid:1});assert.equal(s.round,3);assert.equal(s.phase,'actions');
});

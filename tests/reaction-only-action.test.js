import test from 'node:test';
import assert from 'node:assert/strict';
import {createGame,getNormalActions,executeNormalAction} from '../src/game/game.js';

for (const definitionId of ['miracle-19','disaster-16']) for (const judgment of [null,'blindness-1','blindness-2']) test(`${definitionId} 可依規則作為一般牌打出（${judgment??'無審判'}）`,()=>{
 let s=createGame({seed:'reaction-only-check'}),p=s.players[0];
 for(const zone of [s.deck,...s.players.map(x=>x.hand)]){const i=zone.findIndex(c=>c.definitionId===definitionId);if(i>=0){const card=zone.splice(i,1)[0];s.deck.push(...p.hand.splice(0));p.hand.push(card);break;}}
 const card=p.hand.find(c=>c.definitionId===definitionId);assert.ok(card);s.currentPlayer=p.playerId;
 if(judgment){s.activeJudgments={'資訊':judgment};const pile=getNormalActions(s).find(a=>a.type==='playBlindnessPile');assert.ok(pile);const r=executeNormalAction(s,pile);assert.equal(r.ok,true,r.reason);s=r.state;assert.equal(s.pendingBlindness.instanceId,card.instanceId);}
 const action=getNormalActions(s).find(a=>a.instanceId===card.instanceId);assert.ok(action);const r=executeNormalAction(s,action);assert.equal(r.ok,true,r.reason);assert.ok(r.state.playedArea.some(c=>c.instanceId===card.instanceId));
});

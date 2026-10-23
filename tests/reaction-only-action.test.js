import test from 'node:test';
import assert from 'node:assert/strict';
import {createGame,getNormalActions,executeNormalAction} from '../src/game/game.js';

for (const definitionId of ['miracle-16','miracle-17','miracle-18','miracle-19','miracle-20','disaster-16']) for (const judgment of [null,'blindness-1','blindness-2']) test(`${definitionId} 可依規則作為一般牌打出（${judgment??'無審判'}）`,()=>{
 let s=createGame({seed:'reaction-only-check'}),p=s.players[0];
 for(const zone of [s.deck,...s.players.map(x=>x.hand)]){const i=zone.findIndex(c=>c.definitionId===definitionId);if(i>=0){const card=zone.splice(i,1)[0];s.deck.push(...p.hand.splice(0));p.hand.push(card);break;}}
 const card=p.hand.find(c=>c.definitionId===definitionId);assert.ok(card);s.currentPlayer=p.playerId;
 if(judgment){s.activeJudgments={'資訊':judgment};const pile=getNormalActions(s).find(a=>a.type==='playBlindnessPile');assert.ok(pile);const r=executeNormalAction(s,pile);assert.equal(r.ok,true,r.reason);s=r.state;assert.equal(s.pendingBlindness.instanceId,card.instanceId);}
 const action=getNormalActions(s).find(a=>a.instanceId===card.instanceId);assert.ok(action);const r=executeNormalAction(s,action);assert.equal(r.ok,true,r.reason);assert.ok(r.state.playedArea.some(c=>c.instanceId===card.instanceId));
});

for(const definitionId of ['miracle-10','miracle-15','miracle-12']) test(`${definitionId} 條件不足時仍可出牌，效果不成立，下一位自由出物資`,()=>{
 let s=createGame({seed:'ordinary-special-test'}),p=s.players[0];
 s.deck.push(...p.hand.splice(0));let card;for(const z of [s.deck,...s.players.slice(1).map(x=>x.hand)]){const i=z.findIndex(c=>c.definitionId===definitionId);if(i>=0){card=z.splice(i,1)[0];break;}}assert.ok(card);p.hand.push(card);p.fire=0;s.currentPlayer=p.playerId;s.currentResource={type:'sheep',number:9};
 const action=getNormalActions(s).find(a=>a.instanceId===card.instanceId);assert.ok(action);assert.equal(action.noEffect,true);const result=executeNormalAction(s,action);assert.equal(result.ok,true,result.reason);assert.ok(result.state.playedArea.some(c=>c.instanceId===card.instanceId));assert.equal(result.state.currentResource,null);assert.notEqual(result.state.currentPlayer,p.playerId);
});

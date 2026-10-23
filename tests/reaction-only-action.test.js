import test from 'node:test';
import assert from 'node:assert/strict';
import {createGame,getNormalActions,executeNormalAction} from '../src/game/game.js';

for (const judgment of [null,'blindness-1','blindness-2']) test(`拆毀堅固營壘只能應對，不能作為普通牌（${judgment??'無審判'}）`,()=>{
 const s=createGame({seed:'reaction-only-check'}),p=s.players[0];
 for(const zone of [s.deck,...s.players.map(x=>x.hand)]){const i=zone.findIndex(c=>c.definitionId==='miracle-19');if(i>=0){p.hand.push(zone.splice(i,1)[0]);break;}}
 const card=p.hand.find(c=>c.definitionId==='miracle-19');assert.ok(card);s.currentPlayer=p.playerId;
 if(judgment)s.activeJudgments={'資訊':judgment};
 const actions=getNormalActions(s);assert.ok(!actions.some(a=>a.instanceId===card.instanceId));
 const forged=executeNormalAction(s,{type:'playMiracle',playerId:p.playerId,instanceId:card.instanceId});assert.equal(forged.ok,false);
 if(judgment){p.hand=[card];const only=getNormalActions(s);assert.ok(!only.some(a=>a.type==='playBlindnessPile'));}
});

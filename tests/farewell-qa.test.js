import test from 'node:test';
import assert from 'node:assert/strict';
import {createGame,getNormalActions,executeNormalAction,listCardLocations} from '../src/game/game.js';
import {reactionOptionsFor} from '../src/game/reaction-engine.js';
import {getJudgmentDefinition} from '../src/game/cards.js';
function play(s,extra={}){const a=getNormalActions(s).find(x=>x.instanceId==='disaster-03#qa');assert.ok(a);const r=executeNormalAction(s,{...a,...extra});assert.equal(r.ok,true,r.reason);return r.state;}
test('QA 按出牌方向同時傳牌且每張牌位置唯一',()=>{const s=createGame({seed:'farewell-test'}),before=s.players.map(p=>p.hand.filter(c=>c.definitionId!=='disaster-03').map(c=>c.instanceId));const r=play(s);for(const [i,p] of r.players.entries()){const from=(i-s.direction+4)%4;assert.deepEqual(p.hand.map(c=>c.instanceId),before[from]);}const ids=listCardLocations(r);assert.equal(new Set(ids).size,ids.length);});
test('反方向與空手玩家跳過',()=>{const s=createGame({seed:'farewell-test'});s.direction=1;s.deck.push(...s.players[1].hand.splice(0));const r=play(s);assert.deepEqual(r.lastFarewell.map(t=>[t.fromPlayerId,t.toPlayerId]),[['player-1','player-3'],['player-3','player-4'],['player-4','player-1']]);});
test('免疫玩家排除，不被收牌或給牌',()=>{const s=createGame({seed:'farewell-test'}),held=s.players[1].hand[0].instanceId,r=play(s,{immunePlayerIds:['player-2']});assert.equal(r.players[1].hand[0].instanceId,held);assert.ok(r.lastFarewell.every(t=>t.fromPlayerId!=='player-2'&&t.toPlayerId!=='player-2'));});
test('不足兩名不傳牌',()=>{const s=createGame({seed:'farewell-test'});for(const p of s.players.slice(1))s.deck.push(...p.hand.splice(0));const held=s.players[0].hand.find(c=>c.definitionId==='sheep-1').instanceId,r=play(s);assert.equal(r.lastFarewell.length,0);assert.ok(r.players[0].hand.some(c=>c.instanceId===held));});
test('隨機可重現，且不觸發抽牌效果',()=>{const s=createGame({seed:'farewell-test'}),r1=play(s),r2=play(s);assert.deepEqual(r1.lastFarewell,r2.lastFarewell);assert.equal(r1.pendingEffectDiscard,null);});
test('免疫應對可用；空墳墓與拆毀不適用',()=>{const s=createGame({seed:'farewell-immunity-test'}),a=getNormalActions(s).find(x=>x.instanceId==='disaster-03#qa'),options=reactionOptionsFor(s,a,'player-1');assert.ok(options.some(o=>o.kind==='immunity'));assert.ok(!options.some(o=>['afterFireLoss','blockEffect'].includes(o.kind)));});
for(const judgment of ['overturn-1','overturn-2','division-1','division-2','sinking-1','war-1','war-2','revelation-1','revelation-2','disorder-1','disorder-2'])test(`${judgment} 不改變每人傳一張`,()=>{const s=createGame({seed:'farewell-test'});s.activeJudgments={[getJudgmentDefinition(judgment).domain]:judgment};assert.equal(play(s).lastFarewell.length,4);});
test('未加入正式牌組',()=>{const s=createGame({seed:'normal'});assert.equal(Object.values(s.cardRegistry).filter(id=>id==='disaster-03').length,0);});

for(const judgment of ['blindness-1','blindness-2'])test(`${judgment} 可翻出並結算`,()=>{const s=createGame({seed:'farewell-test'});s.activeJudgments={'資訊':judgment};const a=getNormalActions(s).find(x=>x.type==='playBlindnessPile');const r=executeNormalAction(s,a);assert.equal(r.ok,true,r.reason);assert.equal(play(r.state).lastFarewell.length,4);});

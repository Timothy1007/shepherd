import test from 'node:test';
import assert from 'node:assert/strict';
import {createGame,getNormalActions,executeNormalAction,listCardLocations} from '../src/game/game.js';
import {reactionOptionsFor} from '../src/game/reaction-engine.js';
function act(s,a){const r=executeNormalAction(s,a);assert.equal(r.ok,true,r.reason);return r.state;}
function play(s,extra={}){const a=getNormalActions(s).find(x=>x.instanceId==='disaster-18#qa');assert.ok(a);return act(s,{...a,...extra});}
test('只棄效果區神蹟，有棄置者獲得5點火種',()=>{let s=createGame({seed:'tabernacle-test'});const before=s.players.map(p=>p.fire),effects=s.players.slice(0,2).map(p=>p.effects[0].instanceId);s=play(s);assert.deepEqual(s.players.map((p,i)=>p.fire-before[i]),[5,5,0,0]);for(const id of effects)assert.ok(s.discardPile.some(c=>c.instanceId===id));assert.ok(!s.pendingTabernacle);const ids=listCardLocations(s);assert.equal(new Set(ids).size,ids.length);});
test('免疫玩家不棄神蹟或獲得火種',()=>{let s=createGame({seed:'tabernacle-test'}),id=s.players[0].effects[0].instanceId,before=s.players[0].fire;s=play(s,{immunePlayerIds:['player-1']});assert.ok(s.players[0].effects.some(c=>c.instanceId===id));assert.equal(s.players[0].fire,before);assert.ok(!s.discardPile.some(c=>c.instanceId===id));});
test('灰與燼只減少神蹟獲得，不減少災難給的火種',()=>{let s=createGame({seed:'tabernacle-test'}),p=s.players[0],before=p.fire;p.roundModifiers={miracleFireReduction:2};s=play(s);assert.equal(s.players[0].fire-before,5);});
for(const j of ['overturn-1','overturn-2'])test(`${j} 限制每次獲得火種`,()=>{let s=createGame({seed:'tabernacle-test'}),before=s.players[0].fire;s.activeJudgments={'異變':j};s=play(s);assert.equal(s.players[0].fire-before,j==='overturn-1'?3:1);});
for(const j of ['division-1','division-2'])test(`${j} 逐個結算受影響者且不遺失後續效果`,()=>{let s=createGame({seed:'tabernacle-test'});s.activeJudgments={'資源':j};s=play(s);let count=0;while(s.pendingDivision&&count++<5)s=act(s,getNormalActions(s).find(a=>a.type==='resolveDivisionRecipient'));assert.equal(count,2);assert.equal(s.pendingTabernacle,null);assert.ok(s.players.slice(0,2).every(p=>!p.effects.length));});
for(const j of ['blindness-1','blindness-2'])test(`${j} 能翻出並結算`,()=>{let s=createGame({seed:'tabernacle-test'});s.activeJudgments={'資訊':j};s=act(s,getNormalActions(s).find(a=>a.type==='playBlindnessPile'));s=play(s);assert.equal(s.players[0].effects.length,0);});
test('群體災難不能由受影響者使用替罪羊，且測試牌不進正式牌組',()=>{let s=createGame({seed:'tabernacle-test'});const a=getNormalActions(s).find(x=>x.instanceId==='disaster-18#qa');assert.ok(!reactionOptionsFor(s,a,'player-1').some(x=>x.kind==='redirect'));assert.equal(Object.values(createGame({seed:'normal'}).cardRegistry).filter(id=>id==='disaster-18').length,0);});

test('分裂觸發前所有玩家神蹟均已棄置',()=>{let s=createGame({seed:'tabernacle-test'});s.activeJudgments={'資源':'division-1'};s=play(s);assert.ok(s.pendingDivision);assert.ok(s.players.slice(0,2).every(p=>!p.effects.length));});

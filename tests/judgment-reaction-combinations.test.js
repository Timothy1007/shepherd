import test from 'node:test';
import assert from 'node:assert/strict';
import {createGame,getNormalActions,executeNormalAction} from '../src/game/game.js';

function act(s,a){assert.ok(a, 'expected legal action');const r=executeNormalAction(s,a);assert.equal(r.ok,true,r.reason);return r.state;}
function take(s,id){for(const zone of [s.deck,...s.players.map(p=>p.hand)]){const i=zone.findIndex(c=>c.definitionId===id);if(i>=0)return zone.splice(i,1)[0];}throw Error(`Missing ${id}`);}
function fruit(s,target='player-3'){return getNormalActions(s).find(a=>a.instanceId==='disaster-04#qa'&&a.targetPlayerId===target);}
function finish(s){for(let i=0;i<40&&s.pendingFruit;i++){const a=getNormalActions(s)[0];s=act(s,a);}assert.equal(s.pendingFruit,null);return s;}

for(const [judgment,expectedFire,expectedHand] of [['overturn-1',[10,7,7,10],1],['overturn-2',[10,9,9,9],2]])test(`半朽蜜果：沉淪與${judgment}同時生效`,()=>{
 let s=createGame({seed:'fruit-test'});s.activeJudgments={'災變':'sinking-1','異變':judgment};
 s=finish(act(s,fruit(s)));assert.deepEqual(s.players.map(p=>p.fire),expectedFire);assert.equal(s.players[3].hand.length,expectedHand);
});

test('蜜果失火 → 空墳墓 → 黎明加抽 → 虛謊減抽，結算後仍需棄牌',()=>{
 let s=createGame({seed:'fruit-test'}),target=s.players[1];s.deck.push(...target.hand.splice(0));
 target.hand.push(take(s,'miracle-18'));target.effects.push(take(s,'miracle-13'));s.players[0].hand.push(take(s,'disaster-16'));
 s=act(s,fruit(s,'player-2'));assert.equal(s.pendingFruit.stage,'reaction');
 s=act(s,getNormalActions(s).find(a=>a.type==='resolveFruitReaction'&&a.choice==='use'));
 assert.equal(s.pendingFalseTongue?.kind,'draw');
 s=act(s,getNormalActions(s).find(a=>a.type==='resolveFalseTongue'&&a.choice==='use'));
 assert.equal(s.players[1].fire,4);assert.equal(s.players[1].hand.length,2);assert.equal(s.pendingEffectDiscard?.playerId,'player-2');
 s=act(s,getNormalActions(s).find(a=>a.type==='resolveEffectDiscard'));assert.equal(s.players[1].hand.length,1);assert.equal(s.pendingFruit,null);
});

for(const [judgment,fire] of [['overturn-1',15],['overturn-2',12]])test(`第二次生命：${judgment}、分裂及揭露同時生效`,()=>{
 let s=createGame({seed:'second-life-resource'});
 const first=getNormalActions(s).find(a=>a.instanceId===s.players[0].hand.find(c=>c.definitionId==='miracle-23').instanceId);
 s=act(s,first);s.activeJudgments={'異變':judgment,'資源':'division-1','資訊':'revelation-2'};
 const others=s.players.slice(1).map(p=>p.fire);
 const wind=getNormalActions(s).find(a=>a.instanceId===s.players[0].hand.find(c=>c.definitionId==='miracle-05').instanceId);
 s=act(s,{...wind,choice:'fire'});
 assert.equal(s.players[0].fire,fire);assert.equal(s.lastSecondLife.resource,true);
 assert.deepEqual(s.players.slice(1).map(p=>p.fire),others);assert.ok(!s.pendingDivision);
});

for(const [judgment,fire,overcome] of [['overturn-1',1,true],['overturn-2',2,false]]) for(const direction of [1,-1]) test(`焚城：${judgment}、方向${direction}下依實際失火計算`,()=>{
 let s=createGame({seed:'burning-city-test'});
 for(const p of s.players){s.deck.push(...p.hand.splice(0),...p.effects.splice(0));p.fire=10;}
 const city=take(s,'disaster-15');s.players[0].hand.push(city);s.players[1].hand.push(take(s,'miracle-18'),take(s,'miracle-20'));s.players[1].fire=3;
 s.currentPlayer='player-1';s.direction=direction;s.activeJudgments={'異變':judgment,'資源':'division-1','資訊':'revelation-2'};
 const attack=getNormalActions(s).find(a=>a.instanceId===city.instanceId&&a.targetPlayerIds.join(',')==='player-2,player-3');s=act(s,attack);
 s=act(s,getNormalActions(s).find(a=>a.type==='resolveBurningCityTomb'&&a.choice==='use'));
 if(overcome)s=act(s,getNormalActions(s).find(a=>a.type==='resolveBurningCityOvercome'&&a.choice==='use'));
 assert.equal(s.players[1].fire,fire);assert.equal(s.players[1].hand.some(c=>c.definitionId==='miracle-18'),false);
 assert.equal(s.players[1].hand.some(c=>c.definitionId==='miracle-20'),!overcome);
 assert.equal(s.pendingBurningCity,null);assert.equal(s.currentPlayer,direction===1?'player-2':'player-4');
});

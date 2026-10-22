import test from 'node:test';
import assert from 'node:assert/strict';
import {createGame,getNormalActions,executeNormalAction} from '../src/game/game.js';
function play(s,gifts){const a=getNormalActions(s).find(a=>a.instanceId==='miracle-11#qa');const r=executeNormalAction(s,{...a,gifts});assert.equal(r.ok,true,r.reason);return r.state;}
function gifts(s,recipients=['player-2','player-3','player-4']){return s.players[0].hand.filter(c=>c.definitionId!=='miracle-11').map((c,i)=>({instanceId:c.instanceId,recipientPlayerId:recipients[i]}));}
test('正確分配三張牌，抽兩張並得到五點',()=>{let s=createGame({seed:'narrow-test'}),g=gifts(s);s=play(s,g);for(const x of g)assert.ok(s.players.find(p=>p.playerId===x.recipientPlayerId).hand.some(c=>c.instanceId===x.instanceId));assert.equal(s.players[0].hand.length,2);assert.equal(s.players[0].fire,5);});
test('可將三張牌分配給同一玩家',()=>{let s=createGame({seed:'narrow-test'}),g=gifts(s,['player-2','player-2','player-2']);s=play(s,g);assert.equal(s.players[1].hand.length,3);});
test('禁止重複分配或將牌給自己',()=>{let s=createGame({seed:'narrow-test'}),a=getNormalActions(s).find(a=>a.instanceId==='miracle-11#qa'),g=gifts(s);g[1]=g[0];assert.equal(executeNormalAction(s,{...a,gifts:g}).ok,false);g=gifts(s);g[0].recipientPlayerId='player-1';assert.equal(executeNormalAction(s,{...a,gifts:g}).ok,false);});
test('未加入正式牌組',()=>assert.equal(Object.values(createGame({seed:'normal'}).cardRegistry).includes('miracle-11'),false));
function effect(s,id){for(const zone of [s.deck,...s.players.map(p=>p.hand)]){const i=zone.findIndex(c=>c.definitionId===id);if(i>=0){s.players[0].effects.push(zone.splice(i,1)[0]);return;}}throw Error('missing '+id);}
test('黎明多抽一張並先棄牌，完成後才獲得火種',()=>{let s=createGame({seed:'narrow-test'});effect(s,'miracle-13');s=play(s,gifts(s));assert.equal(s.players[0].hand.length,3);assert.equal(s.players[0].fire,0);assert.equal(getNormalActions(s)[0].type,'resolveEffectDiscard');const r=executeNormalAction(s,getNormalActions(s)[0]);assert.equal(r.ok,true,r.reason);assert.equal(r.state.players[0].fire,5);});
test('分裂 I：獲得 5 點火種後選擇額外獲得者',()=>{let s=createGame({seed:'narrow-test'});s.activeJudgments={'資源':'division-1'};s=play(s,gifts(s));assert.equal(s.players[0].fire,5);assert.equal(s.pendingDivision?.sourcePlayerId,'player-1');});
test('灰與燼：神蹟獲得 5 點時減少 2 點',()=>{let s=createGame({seed:'narrow-test'});s.players[0].roundModifiers={miracleFireReduction:2};s=play(s,gifts(s));assert.equal(s.players[0].fire,3);});
test('死亡牌出現時仍完成火種結算再進死亡輪',()=>{let s=createGame({seed:'narrow-test'}),i=s.deck.findIndex(c=>c.definitionId==='death');s.deck.unshift(s.deck.splice(i,1)[0]);s=play(s,gifts(s));assert.equal(s.players[0].fire,5);assert.equal(s.phase,'deathRound');});
test('與第二次生命互動：獲得火種會觸發抽牌',()=>{let s=createGame({seed:'narrow-test'});effect(s,'miracle-23');s=play(s,gifts(s));assert.ok(s.lastSecondLife);assert.equal(s.players[0].effects.some(c=>c.definitionId==='miracle-23'),false);});

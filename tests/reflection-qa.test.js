import test from 'node:test';
import assert from 'node:assert/strict';
import {createGame,getNormalActions,executeNormalAction} from '../src/game/game.js';
import {canUseStronghold} from '../src/game/multi-effect.js';
const act=(s,a)=>{const r=executeNormalAction(s,a);assert.equal(r.ok,true,r.reason);return r.state;};
const play=s=>getNormalActions(s).find(a=>a.instanceId==='disaster-05#qa');
function finish(s){let n=0;while(s.pendingReflection&&n++<20){const a=getNormalActions(s);assert.ok(a.length);s=act(s,a[0]);}assert.ok(n<20);return s;}
function give(s,player,id){let i=s.deck.findIndex(c=>c.definitionId===id),c=i>=0?s.deck.splice(i,1)[0]:null;if(!c)for(const p of s.players){i=p.hand.findIndex(x=>x.definitionId===id);if(i>=0){c=p.hand.splice(i,1)[0];break;}}assert.ok(c);player.hand.push(c);return c;}
test('測試局固定手牌為 3、3、2，打出後三位並列最高或次高者各棄一張',()=>{let s=createGame({seed:'reflection-test'});s=act(s,play(s));assert.deepEqual(s.pendingReflection.playerIds,['player-2','player-3','player-4']);s=finish(s);assert.deepEqual(s.players.map(p=>p.hand.length),[0,2,2,1]);});
test('四人同手牌數時四人均受影響，真人自行選牌',()=>{let s=createGame({seed:'reflection-test'});for(const p of s.players.slice(1))s.deck.push(...p.hand.splice(0));for(const p of s.players)while(p.hand.length<(p.playerId==='player-1'?3:2))p.hand.push(s.deck.shift());s=act(s,play(s));assert.deepEqual(s.pendingReflection.playerIds,['player-1','player-2','player-3','player-4']);assert.equal(s.currentPlayer,'player-1');assert.equal(getNormalActions(s).length,2);});
test('越過長夜免疫只影響本人，其他玩家仍棄牌',()=>{let s=createGame({seed:'reflection-test'}),p=s.players[1];s.deck.push(p.hand.pop());give(s,p,'miracle-16');s=act(s,play(s));assert.equal(s.pendingReflection.stage,'immunity');s=act(s,getNormalActions(s).find(a=>a.choice==='use'));s=finish(s);assert.deepEqual(s.players.map(p=>p.hand.length),[0,2,2,1]);assert.ok(s.playedArea.some(c=>c.definitionId==='miracle-16'));});
test('替罪羊的既有效果抵擋一張棄牌',()=>{let s=createGame({seed:'reflection-test'}),p=s.players[1],card=give(s,p,'miracle-17');p.effects.push(p.hand.pop());s=finish(act(s,play(s)));assert.equal(s.players[1].hand.length,3);assert.ok(s.discardPile.some(c=>c.instanceId===card.instanceId));});
test('空手玩家不會收到棄牌選項，全部空手時結算完成',()=>{let s=createGame({seed:'reflection-test'});for(const p of s.players.slice(1))s.deck.push(...p.hand.splice(0));s=act(s,play(s));assert.equal(s.pendingReflection,null);});
test('效果只有棄牌，不符合拆毀堅固營壘',()=>assert.equal(canUseStronghold({definitionId:'disaster-05'},'player-1'),false));
test('正式牌組含一張瞳中倒影',()=>assert.equal(Object.values(createGame({seed:'normal'}).cardRegistry).filter(id=>id==='disaster-05').length,1));

test('未受影響者可用替罪羊轉移其中一名目標，且實際棄牌後進入效果區',()=>{let s=createGame({seed:'reflection-test'}),p=s.players[0];give(s,p,'miracle-17');give(s,p,'sheep-4');give(s,s.players[1],'food-4');s=act(s,play(s));assert.equal(s.pendingReflection.stage,'redirect');s=act(s,getNormalActions(s).find(a=>a.choice==='use'&&a.targetPlayerId==='player-2'));assert.equal(s.pendingReflection.stage,'discard');s=act(s,getNormalActions(s)[0]);s=finish(s);assert.equal(s.players[1].hand.length,4);assert.equal(s.players[0].hand.length,0);assert.ok(s.players[0].effects.some(c=>c.definitionId==='miracle-17'));});

import test from 'node:test';
import assert from 'node:assert/strict';
import {createGame,getNormalActions,executeNormalAction} from '../src/game/game.js';
function act(s,a){const r=executeNormalAction(s,a);assert.equal(r.ok,true,r.reason);return r.state;}
function fixture(){const s=createGame({seed:'overflow-test'});for(const player of s.players.slice(1))s.deck.push(...player.hand.splice(0));return s;}
function play(s,choice){const card=s.players[0].hand.find(card=>card.definitionId==='miracle-14');assert.ok(card);const a=getNormalActions(s).find(a=>a.instanceId===card.instanceId&&a.targetPlayerId==='player-2');assert.ok(a);return act(s,{...a,choice});}
function reward(s){const a=getNormalActions(s)[0];assert.equal(a.type,'resolveDivisionRecipient');return act(s,a);}
function effect(s,owner,id){for(const z of [s.deck,...s.players.map(p=>p.hand)]){const i=z.findIndex(c=>c.definitionId===id);if(i>=0){owner.effects.push(z.splice(i,1)[0]);return;}}throw Error('missing '+id);}
test('自己抽牌，對方選擇使命增加三點',()=>{let s=fixture();s=play(s,'draw');assert.equal(s.players[0].hand.length,1);assert.equal(s.pendingMissionReward.entries[0].playerId,'player-2');assert.equal(s.currentPlayer,'player-2');const type=getNormalActions(s)[0].recipientPlayerId;s=reward(s);assert.equal(s.players[1].missionProgress[type],3);assert.equal(s.pendingOverflow,null);});
test('自己選擇使命後，對方抽牌',()=>{let s=fixture();s=play(s,'mission');assert.equal(s.pendingMissionReward.entries[0].playerId,'player-1');assert.equal(s.currentPlayer,'player-1');const type=getNormalActions(s)[0].recipientPlayerId;s=reward(s);assert.equal(s.players[0].missionProgress[type],3);assert.equal(s.players[1].hand.length,1);});
test('黎明棄牌先完成再處理下一項效果',()=>{let s=fixture();effect(s,s.players[0],'miracle-13');s=play(s,'draw');assert.equal(s.currentPlayer,'player-1');assert.equal(s.pendingMissionReward,null);assert.equal(getNormalActions(s)[0].type,'resolveEffectDiscard');s=act(s,getNormalActions(s)[0]);assert.equal(s.pendingMissionReward.entries[0].playerId,'player-2');});
test('對方黎明抽牌後由對方棄牌',()=>{let s=fixture();effect(s,s.players[1],'miracle-13');s=play(s,'mission');s=reward(s);assert.equal(s.currentPlayer,'player-2');assert.equal(getNormalActions(s)[0].type,'resolveEffectDiscard');s=act(s,getNormalActions(s)[0]);assert.equal(s.pendingOverflow,null);});
test('傾覆 II 將使命進度變動改為一點',()=>{let s=fixture();s.activeJudgments={'異變':'overturn-2'};s=play(s,'mission');const type=getNormalActions(s)[0].recipientPlayerId;s=reward(s);assert.equal(s.players[0].missionProgress[type],1);});
test('沒有未完成的使命時繼續結算，不會卡住',()=>{let s=fixture();s.players[1].missionProgress={...s.players[1].mission};s=play(s,'draw');assert.equal(s.players[0].hand.length,1);assert.equal(s.pendingOverflow,null);});
test('抽到死亡牌時先完成另一項使命選擇',()=>{let s=fixture();const i=s.deck.findIndex(c=>c.definitionId==='death');assert.ok(i>=0);s.deck.unshift(s.deck.splice(i,1)[0]);s=play(s,'draw');assert.equal(s.phase,'deathPending');assert.ok(s.pendingMissionReward);s=reward(s);assert.equal(s.pendingOverflow,null);assert.equal(s.phase,'deathRound');});

test('正式牌庫含一張杯滿盈溢',()=>{const s=createGame({seed:'ordinary'});assert.equal(Object.values(s.cardRegistry).filter(id=>id==='miracle-14').length,1);});

test('杯滿盈溢 QA 只限制真人手牌，AI 保留正常起手牌',()=>{const s=createGame({seed:'overflow-test'});assert.equal(s.players[0].hand.length,1);assert.deepEqual(s.players.slice(1).map(player=>player.hand.length),[7,7,7]);});

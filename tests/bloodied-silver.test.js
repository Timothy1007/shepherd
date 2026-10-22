import test from 'node:test';
import assert from 'node:assert/strict';
import { silverTransferChoices, completeSilverTransfer, SILVER_TRANSFER_SOURCE } from '../src/game/bloodied-silver.js';

const fixture = () => ({ players: [
  { playerId: 'a', fire: 5, effects: [{ definitionId: 'disaster-10' }] },
  { playerId: 'b', fire: 3, effects: [{ definitionId: 'disaster-10' }] },
  { playerId: 'c', fire: 0, effects: [] },
] });

test('持有效果者獲得火種後，可指定其他玩家轉移1點', () => {
  const state = fixture(), event = { playerId: 'a', amount: 5, source: 'miracle' };
  assert.deepEqual(silverTransferChoices(state, event), ['b', 'c']);
  const result = completeSilverTransfer(state, event, 'b');
  assert.equal(result.ok, true);
  assert.equal(result.state.players[0].fire, 4);
  assert.equal(result.state.players[1].fire, 4);
  assert.equal(state.players[0].fire, 5);
});

test('銀幣轉移得到的火種不再觸發另一個銀幣', () => {
  const result = completeSilverTransfer(fixture(), { playerId: 'a', amount: 1, source: 'judgment' }, 'b');
  assert.equal(result.gainEvent.source, SILVER_TRANSFER_SOURCE);
  assert.deepEqual(silverTransferChoices(result.state, result.gainEvent), []);
});

test('不能選自己，無實際收益或效果已清除時不觸發', () => {
  const state = fixture();
  assert.equal(completeSilverTransfer(state, { playerId: 'a', amount: 1, source: 'miracle' }, 'a').ok, false);
  assert.deepEqual(silverTransferChoices(state, { playerId: 'a', amount: 0, source: 'miracle' }), []);
  state.players[0].effects = [];
  assert.deepEqual(silverTransferChoices(state, { playerId: 'a', amount: 3, source: 'miracle' }), []);
});

test('獨立 QA 可以指定並放置一張染血銀幣', async () => {
  const { createGame, executeNormalAction, getNormalActions } = await import('../src/game/game.js');
  const state = createGame({ seed: 'bloodied-silver-test' });
  const card = state.players[0].hand.find(c => c.definitionId === 'disaster-10');
  assert.ok(card);
  const action = getNormalActions(state).find(a => a.instanceId === card.instanceId && a.targetPlayerId === 'player-2');
  const result = executeNormalAction(state, action);
  assert.equal(result.ok, true, result.reason);
  assert.equal(result.state.players[1].effects.filter(c => c.definitionId === 'disaster-10').length, 1);
  assert.ok(!result.state.playedArea.some(c => c.instanceId === card.instanceId));
});

test('沉淪下每個受影響玩家各有獨立效果，虛擬效果不產生額外實體牌', async () => {
  const { createGame, executeNormalAction, getNormalActions } = await import('../src/game/game.js');
  const { removeEffectByInstanceId } = await import('../src/game/effect-zone-rules.js');
  const state = createGame({ seed: 'bloodied-silver-test' });
  state.activeJudgments['災變'] = 'sinking-1';
  const card = state.players[0].hand.find(c => c.definitionId === 'disaster-10');
  const action = getNormalActions(state).find(a => a.instanceId === card.instanceId && a.targetPlayerId === 'player-2');
  const result = executeNormalAction(state, action);
  assert.equal(result.ok, true, result.reason);
  assert.equal(result.state.players.filter(p => p.effects.some(c => c.definitionId === 'disaster-10')).length, 3);
  const virtualOwner = result.state.players.find(p => p.effects.some(c => c.virtualSilverEffect));
  const marker = virtualOwner.effects.find(c => c.virtualSilverEffect);
  removeEffectByInstanceId(result.state, virtualOwner, marker.instanceId);
  assert.ok(!result.state.discardPile.some(c => c.instanceId === marker.instanceId));
  assert.equal(result.state.players.filter(p => p.effects.some(c => c.definitionId === 'disaster-10')).length, 2);
});

test('正式行動中獲得火種後選擇對象，銀幣轉移不連鎖', async () => {
  const { createGame, executeNormalAction, getNormalActions } = await import('../src/game/game.js');
  let state = createGame({seed:'bloodied-silver-test'});
  const actor = state.players[0]; actor.effects.push({instanceId:'silver-a',definitionId:'disaster-10'});
  state.players[1].effects.push({instanceId:'silver-b',definitionId:'disaster-10'});
  actor.hand=[{instanceId:'wind',definitionId:'miracle-05'}];
  const action=getNormalActions(state).find(a=>a.instanceId==='wind');
  let result=executeNormalAction(state,action); assert.equal(result.ok,true,result.reason);state=result.state;
  assert.equal(state.players[0].fire,14);assert.equal(state.currentPlayer,'player-1');
  const choices=getNormalActions(state);assert.deepEqual(choices.map(a=>a.recipientPlayerId),['player-2','player-3','player-4']);
  result=executeNormalAction(state,choices[0]);assert.equal(result.ok,true,result.reason);state=result.state;
  assert.equal(state.players[0].fire,13);assert.equal(state.players[1].fire,11);assert.ok(!state.pendingSilverGains?.length);
});

test('使徒獎勵也觸發銀幣並在選擇後結束本輪', async () => {
  const { createGame, executeNormalAction, getNormalActions, settleRound } = await import('../src/game/game.js');
  let state=createGame({seed:'bloodied-silver-test'});state.players[0].effects.push({instanceId:'silver-a',definitionId:'disaster-10'});state.currentApostle='player-1';state.playedArea.push({instanceId:'reward-card',definitionId:'miracle-05'});state.players.forEach(p=>p.hasNormalAction=false);
  const result=settleRound(state);assert.equal(result.ok,true,result.reason);state=result.state;assert.equal(state.players[0].fire,11);assert.equal(state.round,1);
  const finish=executeNormalAction(state,getNormalActions(state)[0]);assert.equal(finish.ok,true,finish.reason);state=finish.state;assert.equal(state.players[0].fire,10);assert.equal(state.players[1].fire,11);assert.equal(state.round,2);assert.equal(state.lastRoundResult.reward,1);
});

test('傾覆限制實際獲得後再轉移一點', async () => {
 const {createGame,getNormalActions,executeNormalAction}=await import('../src/game/game.js');let s=createGame({seed:'bloodied-silver-test'});s.activeJudgments={'異變':'overturn-2'};s.players[0].effects.push({instanceId:'silver',definitionId:'disaster-10'});s.players[0].hand=[{instanceId:'wind',definitionId:'miracle-05'}];let r=executeNormalAction(s,getNormalActions(s).find(a=>a.instanceId==='wind'));assert.equal(r.ok,true,r.reason);s=r.state;assert.equal(s.players[0].fire,11);r=executeNormalAction(s,getNormalActions(s)[0]);assert.equal(r.ok,true,r.reason);assert.equal(r.state.players[0].fire,10);assert.equal(r.state.players[1].fire,11);
});

test('雨幕解除銀幣後，其後獲得火種不再轉移', async () => {
 const {createGame,getNormalActions,executeNormalAction}=await import('../src/game/game.js');let s=createGame({seed:'bloodied-silver-test'});s.players[1].effects.push({instanceId:'silver',definitionId:'disaster-10'});s.players[0].hand=[{instanceId:'rain',definitionId:'miracle-22'}];const action=getNormalActions(s).find(a=>a.instanceId==='rain'&&a.targetPlayerId==='player-2');assert.ok(action);const r=executeNormalAction(s,{...action,effectInstanceId:'silver'});assert.equal(r.ok,true,r.reason);assert.equal(r.state.players[1].fire,12);assert.ok(!r.state.pendingSilverGains?.length);assert.ok(!r.state.players[1].effects.some(c=>c.definitionId==='disaster-10'));
});

test('越過長夜能免疫銀幣，銀幣不進入目標效果區', async () => {
 const {createGame,getNormalActions,executeNormalAction}=await import('../src/game/game.js');let s=createGame({seed:'bloodied-silver-test'});s.players[1].hand.push({instanceId:'immunity',definitionId:'miracle-16'});const card=s.players[0].hand[0];let r=executeNormalAction(s,getNormalActions(s).find(a=>a.instanceId===card.instanceId&&a.targetPlayerId==='player-2'));assert.equal(r.ok,true,r.reason);s=r.state;assert.equal(s.currentPlayer,'player-2');r=executeNormalAction(s,getNormalActions(s).find(a=>a.choice==='use'));assert.equal(r.ok,true,r.reason);assert.ok(!r.state.players[1].effects.some(c=>c.definitionId==='disaster-10'));assert.ok(r.state.playedArea.some(c=>c.instanceId===card.instanceId));
});

test('分裂額外火種會觸發銀幣', async () => {
 const {createGame,getNormalActions,executeNormalAction}=await import('../src/game/game.js');let s=createGame({seed:'judgment-division-preview'});s.players[1].effects.push({instanceId:'silver',definitionId:'disaster-10'});let r=executeNormalAction(s,getNormalActions(s).find(a=>a.type==='divisionQaGain'));assert.equal(r.ok,true,r.reason);s=r.state;const choice=getNormalActions(s).find(a=>a.type==='resolveDivisionRecipient'&&a.recipientPlayerId==='player-2');assert.ok(choice);r=executeNormalAction(s,choice);assert.equal(r.ok,true,r.reason);s=r.state;assert.equal(s.currentPlayer,'player-2');assert.equal(getNormalActions(s)[0].type,'resolveSilverTransfer');r=executeNormalAction(s,getNormalActions(s)[0]);assert.equal(r.ok,true,r.reason);assert.ok(!r.state.pendingSilverGains?.length);
});

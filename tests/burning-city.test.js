import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, executeNormalAction, getNormalActions } from '../src/game/game.js';

function act(state, action) {
  const result = executeNormalAction(state, action);
  assert.equal(result.ok, true, result.reason);
  return result.state;
}

test('焚城之火可以指定任兩名其他玩家，不受手牌數量限制', () => {
  const state = createGame({ seed: 'burning-city-test' });
  const card = state.players[0].hand.find((held) => held.definitionId === 'disaster-15');
  const actions = getNormalActions(state).filter((action) => action.instanceId === card.instanceId);
  assert.equal(actions.length, 3);
  assert.deepEqual(new Set(actions.map((action) => action.targetPlayerIds.join(','))), new Set([
    'player-2,player-3', 'player-2,player-4', 'player-3,player-4',
  ]));
});

test('焚城之火讓被選玩家自行棄置效果，沒有可棄置效果則失去6火種', () => {
  let state = createGame({ seed: 'burning-city-test' });
  const city = state.players[0].hand.find((card) => card.definitionId === 'disaster-15');
  const play = getNormalActions(state).find((action) => action.instanceId === city.instanceId && action.targetPlayerIds.join(',') === 'player-2,player-3');
  state = act(state, play);

  assert.equal(state.playedArea.some((card) => card.instanceId === city.instanceId), true);
  assert.equal(state.pendingBurningCity?.targetPlayerIds.join(','), 'player-2,player-3');
  assert.equal(state.currentPlayer, 'player-2');

  const choices = getNormalActions(state);
  assert.equal(choices.length, 2);
  state = act(state, choices.find((action) => action.effectInstanceId === state.players[1].effects[1].instanceId));

  assert.equal(state.players[1].effects.length, 1);
  assert.equal(state.discardPile.some((card) => card.definitionId === 'miracle-13'), true);
  assert.equal(state.players[2].fire, 4);
  assert.equal(state.pendingBurningCity, null);
});

test('焚城之火拒絕包含自己或不足兩名的目標', () => {
  const state = createGame({ seed: 'burning-city-test' });
  const city = state.players[0].hand.find((card) => card.definitionId === 'disaster-15');
  const invalid = executeNormalAction(state, {
    type: 'playMiracle', playerId: 'player-1', instanceId: city.instanceId, targetPlayerIds: ['player-1', 'player-2'],
  });
  assert.equal(invalid.ok, false);
  assert.match(invalid.reason, /stale or illegal/i);
});

function cleanCity() {
  const state=createGame({seed:'burning-city-test'});
  for(const p of state.players){p.hand=p.hand.filter(c=>c.definitionId==='disaster-15');p.effects=[];p.fire=10;}
  state.activeJudgments={};
  return state;
}
function put(p,id){p.hand.push({definitionId:id,instanceId:`${id}#fixture-${p.playerId}`});}
function playCity(s){const city=s.players[0].hand.find(c=>c.definitionId==='disaster-15');return act(s,getNormalActions(s).find(a=>a.instanceId===city.instanceId&&a.targetPlayerIds.join(',')==='player-2,player-3'));}
function choose(s,type,choice,targetPlayerId){return act(s,getNormalActions(s).find(a=>a.type===type&&a.choice===choice&&(!targetPlayerId||a.targetPlayerId===targetPlayerId)));}

test('空手仍可被選為目標，蟲災並非增益',()=>{let s=cleanCity();s.players[1].effects.push({definitionId:'disaster-06',instanceId:'insect#fixture'});assert.equal(getNormalActions(s).filter(a=>a.instanceId===s.players[0].hand[0].instanceId).length,3);s=playCity(s);assert.equal(s.players[1].fire,4);assert.equal(s.players[1].effects.length,1);});
for(const [judgment,fire] of [['overturn-1',7],['overturn-2',9]])test(`焚城失火遵守 ${judgment}`,()=>{let s=cleanCity();s.activeJudgments['異變']=judgment;s=playCity(s);assert.equal(s.players[1].fire,fire);assert.equal(s.players[2].fire,fire);});
test('越過長夜只免疫自身',()=>{let s=cleanCity();put(s.players[1],'miracle-16');s=playCity(s);s=choose(s,'resolveBurningCityImmunity','use');assert.equal(s.players[1].fire,10);assert.equal(s.players[2].fire,4);});
test('替罪羊轉移一名未受影響玩家，失火才進入效果區',()=>{let s=cleanCity();put(s.players[3],'miracle-17');put(s.players[3],'miracle-16');s=playCity(s);s=choose(s,'resolveBurningCityRedirect','use','player-2');assert.equal(s.players[1].fire,10);assert.equal(s.players[3].fire,4);assert.equal(s.players[3].effects.some(c=>c.definitionId==='miracle-17'),true);assert.equal(s.players[3].hand.some(c=>c.definitionId==='miracle-16'),true);});
test('轉移後棄置效果區神蹟不算棄手牌，替罪羊留在已出牌區',()=>{let s=cleanCity();put(s.players[3],'miracle-17');s.players[3].effects.push({definitionId:'miracle-02',instanceId:'effect#fixture'});s=playCity(s);s=choose(s,'resolveBurningCityRedirect','use','player-2');assert.equal(s.players[3].fire,10);assert.equal(s.players[3].effects.length,0);assert.equal(s.playedArea.some(c=>c.definitionId==='miracle-17'),true);});
test('空墳墓按實際損失抽兩張',()=>{let s=cleanCity();put(s.players[1],'miracle-18');s=playCity(s);s=choose(s,'resolveBurningCityTomb','use');assert.equal(s.players[1].fire,4);assert.equal(s.players[1].hand.length,2);});
test('勝過死亡降低實際損失，空墳墓按減少後的損失計算',()=>{let s=cleanCity();s.players[1].fire=3;put(s.players[1],'miracle-18');put(s.players[1],'miracle-20');s=playCity(s);s=choose(s,'resolveBurningCityTomb','use');s=choose(s,'resolveBurningCityOvercome','use');assert.equal(s.players[1].fire,1);assert.equal(s.players[1].hand.length,0);});

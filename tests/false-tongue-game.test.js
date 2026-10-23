import test from 'node:test';
import assert from 'node:assert/strict';
import {createGame,getNormalActions,executeNormalAction,requestExternalMiracleDraw} from '../src/game/game.js';

function setup(){const s=createGame({seed:'tongue-test'});for(const p of s.players){s.deck.push(...p.hand.splice(0),...p.effects.splice(0));}const i=s.deck.findIndex(c=>c.definitionId==='miracle-01');s.players[0].hand.push(s.deck.splice(i,1)[0]);s.players[1].hand.push({instanceId:'disaster-16#qa',definitionId:'disaster-16'});s.currentPlayer=s.players[0].playerId;s.currentResource=null;return s;}

test('回轉歸向結算前暫停，使用虛謊之舌後少抽一張並恢復流程',()=>{
 let s=setup();const action=getNormalActions(s).find(a=>a.instanceId==='miracle-01#1');let r=executeNormalAction(s,action);assert.equal(r.ok,true,r.reason);s=r.state;
 assert.equal(s.currentPlayer,'player-2');assert.ok(s.playedArea.some(c=>c.definitionId==='miracle-01'));
 const response=getNormalActions(s).find(a=>a.choice==='use');r=executeNormalAction(s,response);assert.equal(r.ok,true,r.reason);
 assert.equal(r.state.players[0].hand.length,0);assert.ok(r.state.playedArea.some(c=>c.definitionId==='disaster-16'));assert.equal(r.state.pendingFalseTongue,null);
});

test('放棄應對時保留手牌，正常抽牌',()=>{
 let s=setup();let r=executeNormalAction(s,getNormalActions(s).find(a=>a.instanceId==='miracle-01#1'));assert.equal(r.ok,true,r.reason);
 r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.choice==='pass'));assert.equal(r.ok,true,r.reason);
 assert.equal(r.state.players[0].hand.length,1);assert.ok(r.state.players[1].hand.some(c=>c.definitionId==='disaster-16'));
});

test('外部行動不能偽裝為已完成應對',()=>{
 const s=setup(),action=getNormalActions(s).find(a=>a.instanceId==='miracle-01#1');
 const r=executeNormalAction(s,{...action,falseTongueResolved:true});assert.equal(r.ok,true,r.reason);
 assert.ok(r.state.pendingFalseTongue);assert.ok(r.state.playedArea.some(c=>c.definitionId==='miracle-01'));
});

test('黎明加抽後被應對，仍抽一張並要求棄牌',()=>{
 const s=setup(),i=s.deck.findIndex(c=>c.definitionId==='miracle-13');s.players[0].effects.push(s.deck.splice(i,1)[0]);
 let r=executeNormalAction(s,getNormalActions(s).find(a=>a.instanceId==='miracle-01#1'));assert.equal(r.ok,true,r.reason);
 r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.choice==='use'));assert.equal(r.ok,true,r.reason);
 assert.equal(r.state.players[0].hand.length,1);assert.ok(r.state.pendingEffectDiscard);
});

test('於水中重生仍棄置原有手牌，但少抽一張',()=>{
 const s=setup(),p=s.players[0];s.deck.push(...p.hand.splice(0));
 for(const id of ['miracle-04','sheep-1','food-1']){const i=s.deck.findIndex(c=>c.definitionId===id);p.hand.push(s.deck.splice(i,1)[0]);}
 let r=executeNormalAction(s,getNormalActions(s).find(a=>a.instanceId==='miracle-04#1'));assert.equal(r.ok,true,r.reason);
 r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.choice==='use'));assert.equal(r.ok,true,r.reason);assert.equal(r.state.players[0].hand.length,1);
});

test('如風吹來的火種選項被應對後歸零',()=>{
 const s=setup(),p=s.players[0];s.deck.push(...p.hand.splice(0));const i=s.deck.findIndex(c=>c.definitionId==='miracle-05');p.hand.push(s.deck.splice(i,1)[0]);
 let r=executeNormalAction(s,{...getNormalActions(s).find(a=>a.instanceId==='miracle-05#1'),choice:'fire'});assert.equal(r.ok,true,r.reason);
 r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.choice==='use'));assert.equal(r.ok,true,r.reason);assert.equal(r.state.players[0].fire,0);
});

test('杯滿盈溢的使命獎勵可應對，接回使命選擇及另一名玩家的抽牌',()=>{
 const s=setup(),p=s.players[0];s.deck.push(...p.hand.splice(0));const i=s.deck.findIndex(c=>c.definitionId==='miracle-14');p.hand.push(s.deck.splice(i,1)[0]);
 p.mission={sheep:9,food:9,money:9};p.missionProgress={sheep:0,food:0,money:0};
 let r=executeNormalAction(s,{...getNormalActions(s).find(a=>a.instanceId==='miracle-14#1'&&a.targetPlayerId==='player-2'),choice:'mission'});assert.equal(r.ok,true,r.reason);
 assert.equal(r.state.currentPlayer,'player-2');assert.ok(r.state.pendingFalseTongue);
 r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.choice==='use'));assert.equal(r.ok,true,r.reason);
 assert.equal(r.state.currentPlayer,'player-1');const choice=getNormalActions(r.state).find(a=>a.type==='resolveDivisionRecipient'&&a.recipientPlayerId==='sheep');assert.ok(choice);
 r=executeNormalAction(r.state,choice);assert.equal(r.ok,true,r.reason);assert.equal(r.state.players[0].missionProgress.sheep,1);
 assert.equal(r.state.players[1].hand.length,1);
});

test('杯滿盈溢的抽牌可應對並繼續另一名玩家的使命獎勵',()=>{
 const s=setup(),p=s.players[0];s.deck.push(...p.hand.splice(0));const i=s.deck.findIndex(c=>c.definitionId==='miracle-14');p.hand.push(s.deck.splice(i,1)[0]);
 const target=s.players[1];target.mission={sheep:9,food:9,money:9};target.missionProgress={sheep:0,food:0,money:0};
 let r=executeNormalAction(s,{...getNormalActions(s).find(a=>a.instanceId==='miracle-14#1'&&a.targetPlayerId==='player-2'),choice:'draw'});assert.equal(r.ok,true,r.reason);
 assert.ok(r.state.pendingFalseTongue);r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.choice==='use'));assert.equal(r.ok,true,r.reason);
 assert.equal(r.state.players[0].hand.length,0);const choice=getNormalActions(r.state).find(a=>a.type==='resolveDivisionRecipient'&&a.recipientPlayerId==='sheep');assert.ok(choice);
 r=executeNormalAction(r.state,choice);assert.equal(r.ok,true,r.reason);assert.equal(r.state.players[1].missionProgress.sheep,3);
});

for(const choice of ['draw','fire'])test(`窄門同時抽牌和獲得火種，由受影響者選擇減少${choice}`,()=>{
 const s=setup(),p=s.players[0];s.deck.push(...p.hand.splice(0));
 for(const id of ['miracle-11','sheep-3']){const i=s.deck.findIndex(c=>c.definitionId===id);p.hand.push(s.deck.splice(i,1)[0]);}
 const action=getNormalActions(s).find(a=>a.instanceId==='miracle-11#1');
 let r=executeNormalAction(s,{...action,gifts:[{instanceId:p.hand.find(c=>c.definitionId==='sheep-3').instanceId,recipientPlayerId:'player-3'}]});assert.equal(r.ok,true,r.reason);
 r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.choice==='use'));assert.equal(r.ok,true,r.reason);
 assert.equal(r.state.currentPlayer,'player-1');r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.choice===choice));assert.equal(r.ok,true,r.reason);
 assert.equal(r.state.players[0].hand.length,choice==='draw'?1:2);assert.equal(r.state.players[0].fire,choice==='fire'?0:5);
});

test('分杯之火先支付代價，受影響的火種減少後仍接回出牌者的抽牌',()=>{
 const s=setup(),p=s.players[0];s.deck.push(...p.hand.splice(0));const i=s.deck.findIndex(c=>c.definitionId==='miracle-12');p.hand.push(s.deck.splice(i,1)[0]);p.fire=6;
 let r=executeNormalAction(s,getNormalActions(s).find(a=>a.instanceId==='miracle-12#1'&&a.targetPlayerId==='player-2'));assert.equal(r.ok,true,r.reason);
 assert.equal(r.state.players[0].fire,0);assert.ok(r.state.pendingFalseTongue);
 r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.choice==='use'));assert.equal(r.ok,true,r.reason);
 assert.equal(r.state.players[1].fire,0);assert.equal(r.state.players[0].hand.length,1);assert.equal(r.state.pendingCup,null);
});

test('劫後餘生支付火種後的抽牌可應對，並繼續下一位玩家',()=>{
 const s=setup(),p=s.players[0];s.deck.push(...p.hand.splice(0));const i=s.deck.findIndex(c=>c.definitionId==='miracle-24');p.hand.push(s.deck.splice(i,1)[0]);p.fire=5;
 let r=executeNormalAction(s,getNormalActions(s).find(a=>a.instanceId==='miracle-24#1'));assert.equal(r.ok,true,r.reason);
 r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.type==='resolveAftermath'&&a.choice==='pay'));assert.equal(r.ok,true,r.reason);assert.ok(r.state.pendingFalseTongue);
 r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.choice==='use'));assert.equal(r.ok,true,r.reason);
 assert.equal(r.state.players[0].hand.length,0);assert.equal(r.state.players[0].fire,3);assert.ok(r.state.pendingAftermath);assert.notEqual(r.state.currentPlayer,'player-1');
});

test('劫後餘生先放過抽牌，仍可應對首名完成者的額外火種',()=>{
 const s=setup(),p=s.players[0];s.deck.push(...p.hand.splice(0));const i=s.deck.findIndex(c=>c.definitionId==='miracle-24');p.hand.push(s.deck.splice(i,1)[0]);p.fire=5;
 let r=executeNormalAction(s,getNormalActions(s).find(a=>a.instanceId==='miracle-24#1'));assert.equal(r.ok,true,r.reason);
 r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.type==='resolveAftermath'&&a.choice==='pay'));assert.equal(r.ok,true,r.reason);
 r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.choice==='pass'));assert.equal(r.ok,true,r.reason);assert.ok(r.state.pendingFalseTongue);
 r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.choice==='use'));assert.equal(r.ok,true,r.reason);assert.equal(r.state.players[0].fire,1);assert.equal(r.state.players[0].hand.length,1);
});

test('第二次生命觸發抽牌時可應對，抽牌減為零後仍繼續回合',()=>{
 const s=setup(),p=s.players[0];s.deck.push(...p.hand.splice(0));
 for(const id of ['miracle-05','miracle-23']){const i=s.deck.findIndex(c=>c.definitionId===id),card=s.deck.splice(i,1)[0];if(id==='miracle-05')p.hand.push(card);else p.effects.push(card);}
 let r=executeNormalAction(s,{...getNormalActions(s).find(a=>a.instanceId==='miracle-05#1'),choice:'fire'});assert.equal(r.ok,true,r.reason);
 r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.choice==='pass'));assert.equal(r.ok,true,r.reason);assert.equal(r.state.players[0].fire,4);assert.equal(r.state.pendingFalseTongue?.kind,'secondLife');
 r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.choice==='use'));assert.equal(r.ok,true,r.reason);assert.equal(r.state.players[0].hand.length,0);
 assert.ok(!r.state.players[0].effects.some(c=>c.definitionId==='miracle-23'));assert.equal(r.state.pendingFalseTongue,null);
});

test('第二次生命抽到物資後，可對後續額外火種應對',()=>{
 const s=setup(),p=s.players[0];s.deck.push(...p.hand.splice(0));
 for(const id of ['miracle-05','miracle-23']){const i=s.deck.findIndex(c=>c.definitionId===id),card=s.deck.splice(i,1)[0];if(id==='miracle-05')p.hand.push(card);else p.effects.push(card);}
 const i=s.deck.findIndex(c=>c.definitionId==='sheep-1');s.deck.unshift(s.deck.splice(i,1)[0]);
 let r=executeNormalAction(s,{...getNormalActions(s).find(a=>a.instanceId==='miracle-05#1'),choice:'fire'});
 r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.choice==='pass'));
 r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.choice==='pass'));assert.equal(r.ok,true,r.reason);assert.equal(r.state.pendingFalseTongue?.kind,'fire');
 r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.choice==='use'));assert.equal(r.ok,true,r.reason);assert.equal(r.state.players[0].fire,4);assert.equal(r.state.players[0].hand.length,1);
});

test('半朽蜜果引發空墳墓抽牌時可應對，並完成災難結算',()=>{
 let s=createGame({seed:'fruit-test'}),target=s.players[1];s.deck.push(...target.hand.splice(0));
 let i=s.deck.findIndex(c=>c.definitionId==='miracle-18');const owner=s.players.find(p=>p.hand.some(c=>c.definitionId==='miracle-18'));
 const tomb=i>=0?s.deck.splice(i,1)[0]:owner.hand.splice(owner.hand.findIndex(c=>c.definitionId==='miracle-18'),1)[0];target.hand.push(tomb);
 s.players[0].hand.push({instanceId:'disaster-16#qa',definitionId:'disaster-16'});
 let r=executeNormalAction(s,getNormalActions(s).find(a=>a.instanceId==='disaster-04#qa'&&a.targetPlayerId==='player-2'));assert.equal(r.ok,true,r.reason);
 r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.type==='resolveFruitReaction'&&a.choice==='use'));assert.equal(r.ok,true,r.reason);assert.equal(r.state.pendingFalseTongue?.kind,'draw');
 r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.type==='resolveFalseTongue'&&a.choice==='use'));assert.equal(r.ok,true,r.reason);assert.equal(r.state.players[1].hand.length,1);assert.equal(r.state.pendingFruit,null);
});

test('荊棘受到災難後返還的火種可應對，仍保留實際損失紀錄',()=>{
 const s=createGame({seed:'fruit-test'}),target=s.players[1],owner=s.players.find(p=>p.hand.some(c=>c.definitionId==='miracle-21')),i=s.deck.findIndex(c=>c.definitionId==='miracle-21');
 const thorn=i>=0?s.deck.splice(i,1)[0]:owner.hand.splice(owner.hand.findIndex(c=>c.definitionId==='miracle-21'),1)[0];target.effects.push(thorn);
 s.players[0].hand.push({instanceId:'disaster-16#qa',definitionId:'disaster-16'});
 let r=executeNormalAction(s,getNormalActions(s).find(a=>a.instanceId==='disaster-04#qa'&&a.targetPlayerId==='player-2'));assert.equal(r.ok,true,r.reason);assert.equal(r.state.pendingFalseTongue?.kind,'fire');
 r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.choice==='use'));assert.equal(r.ok,true,r.reason);assert.equal(r.state.players[1].fire,4);
 assert.equal(r.state.lastBurningBush.lost,6);assert.equal(r.state.lastBurningBush.gain,0);assert.equal(r.state.pendingFruit,null);
});

test('既有應對介面的後續抽牌可以暫停並接回原本的下一位玩家',()=>{
 const s=setup();s.currentPlayer='player-3';const drawn=requestExternalMiracleDraw(s,'player-1',2,'player-4','empty-tomb');assert.equal(drawn,null);
 assert.equal(s.currentPlayer,'player-2');const r=executeNormalAction(s,getNormalActions(s).find(a=>a.choice==='use'));assert.equal(r.ok,true,r.reason);
 assert.equal(r.state.players[0].hand.length,2);assert.equal(r.state.currentPlayer,'player-3');
});

test('如風吹來的無效棄牌選擇不會觸發應對',()=>{
 const s=setup(),p=s.players[0];s.deck.push(...p.hand.splice(0));const i=s.deck.findIndex(c=>c.definitionId==='miracle-05');p.hand.push(s.deck.splice(i,1)[0]);
 const action=getNormalActions(s).find(a=>a.instanceId==='miracle-05#1');const r=executeNormalAction(s,{...action,choice:'cycle',discardIds:['missing']});
 assert.equal(r.ok,false);assert.equal(r.state.pendingFalseTongue,undefined);
});

test('多位玩家同時觸發荊棘時，先後應對且不覆蓋前一個事件', async()=>{
 const {finishDeferredBurningBush}=await import('../src/game/game.js');const s=setup();
 for(const p of s.players.slice(2))p.effects.push({instanceId:`bush-${p.playerId}`,definitionId:'miracle-21'});
 s.deferredBurningBush=[{playerId:'player-3',lost:8},{playerId:'player-4',lost:8}];s.deferBurningBush=true;
 finishDeferredBurningBush(s);assert.equal(s.pendingFalseTongue.window.effect.playerId,'player-3');
 let r=executeNormalAction(s,getNormalActions(s).find(a=>a.choice==='pass'));assert.equal(r.ok,true,r.reason);
 assert.equal(r.state.players[2].fire,4);assert.equal(r.state.pendingFalseTongue.window.effect.playerId,'player-4');
 r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.choice==='use'));assert.equal(r.ok,true,r.reason);
 assert.equal(r.state.players[3].fire,0);assert.equal(r.state.pendingFalseTongue,null);
});

test('傾覆下於水中重生未被棄置的虛謊之舌可以應對',()=>{
 const s=setup(),p=s.players[0];s.deck.push(...p.hand.splice(0));
 for(const id of ['miracle-04','sheep-1','food-1']){const i=s.deck.findIndex(c=>c.definitionId===id);p.hand.push(s.deck.splice(i,1)[0]);}
 p.hand.push(s.players[1].hand.pop());s.activeJudgments={'異變':'overturn-2'};
 let r=executeNormalAction(s,getNormalActions(s).find(a=>a.instanceId==='miracle-04#1'));assert.equal(r.ok,true,r.reason);
 assert.equal(r.state.pendingFalseTongue.window.effect.playerId,'player-1');assert.ok(r.state.discardPile.some(c=>c.definitionId==='sheep-1'));
 r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.choice==='use'));assert.equal(r.ok,true,r.reason);assert.ok(!r.state.players[0].hand.some(c=>c.definitionId==='disaster-16'));
});

test('銀幣移轉導致兩個第二次生命時，抽牌依序應對',()=>{
 const s=setup(),holder=s.players[2],recipient=s.players[3];holder.fire=6;recipient.fire=0;
 holder.effects.push({instanceId:'silver-qa',definitionId:'disaster-10'}, {instanceId:'life-holder',definitionId:'miracle-23'});
 recipient.effects.push({instanceId:'life-recipient',definitionId:'miracle-23'});
 s.pendingSilverGains=[{playerId:holder.playerId,amount:3,source:'effect',afterGain:'effect'}];s.currentPlayer=holder.playerId;
 let action=getNormalActions(s).find(a=>a.type==='resolveSilverTransfer'&&a.recipientPlayerId===recipient.playerId);assert.ok(action);
 let r=executeNormalAction(s,action);assert.equal(r.ok,true,r.reason);assert.equal(r.state.pendingFalseTongue.window.effect.playerId,recipient.playerId);
 r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.choice==='pass'));assert.equal(r.ok,true,r.reason);
 // The second triggered draw must remain pending even if the first draw grants an extra resource reward.
 let guard=0;while(r.state.pendingFalseTongue?.window.effect.playerId===recipient.playerId&&guard++<3){r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.choice==='pass'));assert.equal(r.ok,true,r.reason);}
 assert.equal(r.state.pendingFalseTongue?.window.effect.playerId,holder.playerId);
 r=executeNormalAction(r.state,getNormalActions(r.state).find(a=>a.choice==='use'));assert.equal(r.ok,true,r.reason);assert.equal(r.state.pendingFalseTongue,null);
});

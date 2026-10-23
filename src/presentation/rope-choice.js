import {GameController} from '../controller/game-controller.js';
import {getNormalActions} from '../game/game.js';
import {getDefinition} from '../game/cards.js';
const originalAct=GameController.prototype.act;
let panel;
const cardLabel=c=>{const d=getDefinition(c);return d.name??`${{sheep:'群羊',food:'糧食',money:'金錢',grace:'恩典'}[d.type]??d.type} ${d.number}`;};
GameController.prototype.act=function(action,token=this.snapshot()){
 const actor=this.state?.players.find(p=>p.playerId===action?.playerId),card=actor?.hand.find(c=>c.instanceId===action?.instanceId);
 if(action?.type!=='playMiracle'||card?.definitionId!=='miracle-15'||actor.type!=='human'||action.ropeChoiceComplete)return originalAct.call(this,action,token);
 if(!panel){panel=document.createElement('section');panel.id='rope-choice';panel.style.cssText='position:fixed;z-index:42000;right:20px;bottom:24%;width:min(440px,90vw);max-height:60vh;overflow:auto;padding:16px;border:1px solid #d7bd7a;border-radius:14px;background:#17221f;color:#fff;box-shadow:0 5px 24px #0009';document.body.append(panel);}
 panel.replaceChildren();const h=document.createElement('h3');h.textContent='三股合成的繩子';panel.append(h);const p=document.createElement('p');p.textContent=action.glassRandom?'玻璃海已隨機選定玩家，請選擇自己要展示的牌。':'選擇兩名有手牌的玩家，並選擇自己要展示的牌。其他玩家也會各選一張，同時展示。';panel.append(p);
 const label=document.createElement('label');label.textContent='你的展示牌：';const select=document.createElement('select');for(const c of actor.hand.filter(x=>x.instanceId!==card.instanceId)){const option=document.createElement('option');option.value=c.instanceId;option.textContent=cardLabel(c);select.append(option);}label.append(select);panel.append(label);
 for(const candidate of getNormalActions(this.state).filter(x=>x.instanceId===card.instanceId)){const names=candidate.targetPlayerIds.map(id=>this.state.players.find(x=>x.playerId===id)).map(x=>x.displayName??`玩家 ${x.seat}`);const b=document.createElement('button');b.style.cssText='display:block;margin:6px;padding:8px;cursor:pointer';b.textContent=action.glassRandom?`確認展示（隨機目標：${names.join('、')}）`:`選擇 ${names.join('、')}`;b.onclick=()=>{panel.hidden=true;const r=originalAct.call(this,{...candidate,selectedOwnCardId:select.value,ropeChoiceComplete:true},token);if(r.ok&&r.state.lastRope){const v=r.state.lastRope,labels={same:'牌型皆相同',different:'牌型皆不同',mixed:'牌型部分相同，無額外效果'};this.logAi(`↳ 三股展示：${v.definitionIds.map((id,i)=>`${r.state.players.find(p=>p.playerId===v.playerIds[i])?.displayName??v.playerIds[i]} ${cardLabel({definitionId:id})}`).join('；')}；${labels[v.mode]}`);}};panel.append(b);}
 const cancel=document.createElement('button');cancel.textContent='取消';cancel.onclick=()=>{panel.hidden=true;};panel.append(cancel);panel.hidden=false;return{ok:false,pendingChoice:true,state:this.state};
};
const originalPrepare=GameController.prototype.prepare;
let revealPanel;
GameController.prototype.prepare=function(){
 const result=originalPrepare.call(this),pending=this.state?.pendingRope,human=this.state?.players.find(p=>p.type==='human');
 if(!pending?.waitingPlayerId||pending.waitingPlayerId!==human?.playerId){if(revealPanel)revealPanel.hidden=true;return result;}
 if(!revealPanel){revealPanel=document.createElement('section');revealPanel.id='rope-reveal-choice';revealPanel.style.cssText='position:fixed;z-index:42000;right:20px;bottom:24%;width:min(440px,90vw);max-height:60vh;overflow:auto;padding:16px;border:1px solid #d7bd7a;border-radius:14px;background:#17221f;color:#fff';document.body.append(revealPanel);}
 revealPanel.replaceChildren();const h=document.createElement('h3');h.textContent='三股合成的繩子：選擇展示牌';revealPanel.append(h);const p=document.createElement('p');p.textContent='請選擇一張手牌，其他參與者也會各選一張，之後一起展示。';revealPanel.append(p);
 for(const action of getNormalActions(this.state)){const card=human.hand.find(c=>c.instanceId===action.discardInstanceId),button=document.createElement('button');button.textContent=cardLabel(card);button.style.cssText='display:block;margin:5px;padding:8px';button.onclick=()=>{revealPanel.hidden=true;const r=this.act(action,this.snapshot());if(r.ok&&r.state.lastRope){const v=r.state.lastRope;this.logAi(`↳ 三股展示：${v.definitionIds.map((id,i)=>`${r.state.players.find(p=>p.playerId===v.playerIds[i])?.displayName??v.playerIds[i]} ${cardLabel({definitionId:id})}`).join('；')}`);}};revealPanel.append(button);}
 revealPanel.hidden=false;return result;
};

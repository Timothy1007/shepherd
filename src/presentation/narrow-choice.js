import {GameController} from '../controller/game-controller.js';
import {getDefinition} from '../game/cards.js';
const originalAct=GameController.prototype.act;
let panel;
function show(controller,action,token,player){
 if(!panel){panel=document.createElement('section');panel.id='narrow-choice';panel.style.cssText='position:fixed;z-index:42000;right:20px;bottom:20%;width:min(480px,90vw);max-height:65vh;overflow:auto;padding:16px;border:1px solid #d7bd7a;border-radius:14px;background:#17221f;color:#fff;box-shadow:0 5px 24px #0009';document.body.append(panel);}
 panel.replaceChildren();const h=document.createElement('h3');h.textContent='窄門與窄路：分配 3 張手牌';panel.append(h);const p=document.createElement('p');p.textContent='為三張牌各選擇一名接收者；可分配給同一人。';panel.append(p);
 const others=controller.state.players.filter(x=>x.playerId!==player.playerId),selects=[];
 for(const card of player.hand.filter(c=>c.instanceId!==action.instanceId)){const row=document.createElement('label');row.style.cssText='display:flex;justify-content:space-between;gap:8px;margin:6px';const d=getDefinition(card),label=document.createElement('span');label.textContent=d.name??`${d.type} ${d.number}`;const select=document.createElement('select');const none=document.createElement('option');none.value='';none.textContent='不分配';select.append(none);for(const target of others){const option=document.createElement('option');option.value=target.playerId;option.textContent=target.displayName??`玩家 ${target.seat}`;select.append(option);}row.append(label,select);panel.append(row);selects.push({card,select});}
 const confirm=document.createElement('button');confirm.textContent='確認分配';confirm.disabled=true;const cancel=document.createElement('button');cancel.textContent='取消';for(const {select} of selects)select.onchange=()=>{confirm.disabled=selects.filter(x=>x.select.value).length!==3;};confirm.onclick=()=>{const gifts=selects.filter(x=>x.select.value).map(x=>({instanceId:x.card.instanceId,recipientPlayerId:x.select.value}));panel.hidden=true;originalAct.call(controller,{...action,gifts},token);};cancel.onclick=()=>{panel.hidden=true;};panel.append(confirm,cancel);panel.hidden=false;
}
GameController.prototype.act=function(action,token=this.snapshot()){
 const player=this.state?.players.find(p=>p.playerId===action?.playerId),card=player?.hand.find(c=>c.instanceId===action?.instanceId);
 if(action?.type==='playMiracle'&&card?.definitionId==='miracle-11'&&player.type==='human'&&!action.gifts&&player.hand.length>=4){show(this,action,token,player);return{ok:false,pendingChoice:true,state:this.state};}
 return originalAct.call(this,action,token);
};

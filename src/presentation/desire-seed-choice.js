import {GameController} from '../controller/game-controller.js';
import {getNormalActions} from '../game/game.js';
import {getDefinition} from '../game/cards.js';
const previousPrepare=GameController.prototype.prepare;
let panel;
GameController.prototype.prepare=function(){
 const result=previousPrepare.call(this),event=this.state?.pendingDesireSeeds?.[0],player=this.state?.players.find(p=>p.playerId===event?.playerId);
 if(!event||player?.type!=='human'){if(panel)panel.hidden=true;return result;}
 if(!panel){panel=document.createElement('section');panel.id='desire-seed-choice';panel.style.cssText='position:fixed;right:20px;bottom:24%;z-index:42000;width:min(460px,90vw);max-height:60vh;overflow:auto;padding:16px;border:1px solid #d7bd7a;border-radius:14px;background:#17221f;color:white';document.body.append(panel);}
 panel.replaceChildren();const heading=document.createElement('h3');heading.textContent='愛慾之種：選擇轉交';panel.append(heading);
 const info=document.createElement('p');info.textContent='你同時取得多張牌。選擇其中一張與一位其他玩家；這張災難也會移至該玩家的效果區。';panel.append(info);
 const cardSelect=document.createElement('select'),targetSelect=document.createElement('select'),actions=getNormalActions(this.state),cardIds=[...new Set(actions.map(a=>a.discardInstanceId))],targetIds=[...new Set(actions.map(a=>a.recipientPlayerId))];
 for(const id of cardIds){const card=player.hand.find(c=>c.instanceId===id),d=getDefinition(card),o=document.createElement('option');o.value=id;o.textContent=d.name??`${{sheep:'群羊',food:'糧食',money:'金錢',grace:'恩典'}[d.type]} ${d.number}`;cardSelect.append(o);}for(const id of targetIds){const p=this.state.players.find(p=>p.playerId===id),o=document.createElement('option');o.value=id;o.textContent=p.displayName??`玩家 ${p.seat}`;targetSelect.append(o);}panel.append(cardSelect,targetSelect);
 const button=document.createElement('button');button.textContent='確認轉交';button.onclick=()=>{const a=actions.find(x=>x.discardInstanceId===cardSelect.value&&x.recipientPlayerId===targetSelect.value);if(a){panel.hidden=true;this.act(a,this.snapshot());}};panel.append(button);panel.hidden=false;return result;
};

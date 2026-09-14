import { getDefinition } from '../game/cards.js';
import { GameController } from '../controller/game-controller.js';

const LONG_PRESS_MS=480;
const names={sheep:'群羊',food:'糧食',money:'金錢',grace:'恩典',miracle:'神蹟',disaster:'災難'};
const $=s=>document.querySelector(s);
let state=null,pressTimer=null,longPressed=false;
const originalSafeRender=GameController.prototype.safeRender;
GameController.prototype.safeRender=function(...args){state=this.state;const result=originalSafeRender.apply(this,args);queueMicrotask(renderAiHands);return result;};

function title(card){const d=getDefinition(card);return d.kind==='resource'?`${names[d.type]} ${d.number}`:d.name;}
function detail(card){const d=getDefinition(card),host=$('#detail-content');if(!host)return;host.innerHTML=`<img class="detail-card-art" src="${d.image}" alt="${title(card)}"><div class="detail-copy"><span class="eyebrow">${d.kind==='resource'?names[d.type]:names[d.kind]}</span><h2>${title(card)}</h2><p>測試查看模式</p><p class="detail-note">${d.text??'物資牌依目前物資與克制規則判定。'}</p></div>`;$('#detail-overlay').hidden=false;}
function ensureHistory(){let o=$('#played-history-overlay');if(o)return o;o=document.createElement('div');o.id='played-history-overlay';o.className='overlay';o.hidden=true;o.innerHTML=`<div class="overlay-backdrop" data-history-close></div><article class="played-history-panel"><button class="overlay-close" data-history-close>×</button><span class="eyebrow">本輪已出牌區</span><h2>過去打出的所有牌</h2><p>短按任一張牌可查看詳細資訊。</p><div class="played-history-grid"></div></article>`;document.body.append(o);o.querySelectorAll('[data-history-close]').forEach(x=>x.onclick=()=>o.hidden=true);return o;}
function history(){if(!state)return;const o=ensureHistory(),host=o.querySelector('.played-history-grid');host.replaceChildren();for(const card of state.playedArea){const d=getDefinition(card),b=document.createElement('button');b.className='played-history-card';b.innerHTML=`<img src="${d.image}" alt="${title(card)}"><span>${title(card)}</span>`;b.onclick=()=>detail(card);host.append(b);}if(!state.playedArea.length)host.textContent='目前還沒有已打出的牌。';o.hidden=false;}
function aiMarkup(p){return `<div class="qa-ai-hand" aria-label="Player ${p.seat} 公開手牌">${p.hand.map(c=>{const d=getDefinition(c);return `<button class="qa-ai-card" data-ai-card="${c.instanceId}" title="${title(c)}"><img src="${d.image}" alt="${title(c)}"></button>`;}).join('')}</div>`;}
function renderAiHands(){if(!state)return;const targets=['#seat-left','#seat-top','#seat-right'];state.players.slice(1).forEach((p,i)=>{const seat=$(targets[i]);if(!seat)return;seat.querySelector('.qa-ai-hand')?.remove();seat.insertAdjacentHTML('beforeend',aiMarkup(p));seat.querySelectorAll('[data-ai-card]').forEach(b=>b.onclick=e=>{e.stopPropagation();const c=p.hand.find(x=>x.instanceId===b.dataset.aiCard);if(c)detail(c);});});}
function bindPile(){const targets=[$('#table-card'),$('.tabletop-pile')].filter(Boolean);for(const target of targets){if(target.dataset.inspectorBound)return;target.dataset.inspectorBound='1';target.style.cursor='pointer';target.addEventListener('pointerdown',e=>{if(e.button!==undefined&&e.button!==0)return;longPressed=false;clearTimeout(pressTimer);pressTimer=setTimeout(()=>{longPressed=true;history();if(navigator.vibrate)navigator.vibrate(18);},LONG_PRESS_MS);});target.addEventListener('pointerup',()=>{clearTimeout(pressTimer);pressTimer=null;if(longPressed){longPressed=false;return;}if(!state?.playedArea.length)return;detail(state.playedArea[state.playedArea.length-1]);});target.addEventListener('pointercancel',()=>clearTimeout(pressTimer));}}
bindPile();
new MutationObserver(bindPile).observe($('#drop-zone'),{childList:true,subtree:true});

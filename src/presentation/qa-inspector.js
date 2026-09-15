import { getDefinition } from '../game/cards.js';
import { GameController } from '../controller/game-controller.js';

const LONG_PRESS_MS=480;
const names={sheep:'群羊',food:'糧食',money:'金錢',grace:'恩典',miracle:'神蹟',disaster:'災難'};
const $=s=>document.querySelector(s);
let state=null,pilePress=null;
const originalSafeRender=GameController.prototype.safeRender;
GameController.prototype.safeRender=function(...args){state=this.state;const result=originalSafeRender.apply(this,args);queueMicrotask(renderAiHands);return result;};

function title(card){const d=getDefinition(card);return d.kind==='resource'?`${names[d.type]} ${d.number}`:d.name;}
function ensureDetailPortal(){const overlay=$('#detail-overlay');if(!overlay)return null;/* .dispute-shell is an isolated stacking context, while runtime modals are appended to body. A child can never out-z-index a body sibling, so inspection must share the body stacking context. */if(overlay.parentElement!==document.body)document.body.append(overlay);overlay.dataset.overlayLevel='inspect';return overlay;}
function openDetailMarkup(markup){const overlay=ensureDetailPortal(),host=$('#detail-content');if(!host||!overlay)return;host.innerHTML=markup;overlay.hidden=false;document.body.classList.add('card-inspection-open');}
function detail(card){const d=getDefinition(card);openDetailMarkup(`<img class="detail-card-art" src="${d.image}" alt="${title(card)}"><div class="detail-copy"><span class="eyebrow">${d.kind==='resource'?names[d.type]:names[d.kind]}</span><h2>${title(card)}</h2><p>測試查看模式</p><p class="detail-note">${d.text??'物資牌依目前物資與克制規則判定。'}</p></div>`);}
function domDetail(){const img=$('.tabletop-pile .pile-latest img')??$('.tabletop-pile img:last-child');if(!img)return;openDetailMarkup(`<img class="detail-card-art" src="${img.src}" alt="${img.alt}"><div class="detail-copy"><span class="eyebrow">本輪已出牌</span><h2>${img.alt?.replace(/^目前出牌：/,'')||'目前牌'}</h2><p>中央牌堆查看模式</p></div>`);}
function ensureHistory(){let o=$('#played-history-overlay');if(o)return o;o=document.createElement('div');o.id='played-history-overlay';o.className='overlay';o.dataset.overlayLevel='decision';o.hidden=true;o.innerHTML=`<div class="overlay-backdrop" data-history-close></div><article class="played-history-panel"><button class="overlay-close" data-history-close>×</button><span class="eyebrow">本輪已出牌區</span><h2>過去打出的所有牌</h2><p>短按任一張牌可查看。</p><div class="played-history-grid"></div></article>`;document.body.append(o);o.querySelectorAll('[data-history-close]').forEach(x=>x.onclick=()=>o.hidden=true);return o;}
function history(){const o=ensureHistory(),host=o.querySelector('.played-history-grid');host.replaceChildren();if(state?.playedArea?.length){for(const card of state.playedArea){const d=getDefinition(card),b=document.createElement('button');b.className='played-history-card';b.innerHTML=`<img src="${d.image}" alt="${title(card)}"><span>${title(card)}</span>`;b.onclick=e=>{e.stopPropagation();detail(card);};host.append(b);}}else{document.querySelectorAll('.tabletop-pile .pile-card').forEach(node=>{const img=node.querySelector('img');if(!img)return;const b=document.createElement('button');b.className='played-history-card';b.innerHTML=`<img src="${img.src}" alt="${img.alt}"><span>${img.alt?.replace(/^目前出牌：/,'')||'已出牌'}</span>`;b.onclick=e=>{e.stopPropagation();openDetailMarkup(`<img class="detail-card-art" src="${img.src}" alt="${img.alt}"><div class="detail-copy"><span class="eyebrow">本輪已出牌</span><h2>${img.alt?.replace(/^目前出牌：/,'')||'已出牌'}</h2></div>`);};host.append(b);});}if(!host.children.length)host.textContent='目前還沒有已打出的牌。';o.hidden=false;}
function aiMarkup(p){return `<div class="qa-ai-hand" aria-label="Player ${p.seat} 公開手牌">${p.hand.map(c=>{const d=getDefinition(c);return `<button class="qa-ai-card" data-ai-card="${c.instanceId}" title="${title(c)}"><img src="${d.image}" alt="${title(c)}"></button>`;}).join('')}</div>`;}
function renderAiHands(){if(!state)return;const targets=['#seat-left','#seat-top','#seat-right'];state.players.slice(1).forEach((p,i)=>{const seat=$(targets[i]);if(!seat)return;seat.querySelector('.qa-ai-hand')?.remove();seat.insertAdjacentHTML('beforeend',aiMarkup(p));seat.querySelectorAll('[data-ai-card]').forEach(b=>b.onclick=e=>{e.stopPropagation();const c=p.hand.find(x=>x.instanceId===b.dataset.aiCard);if(c)detail(c);});});}
function pileRect(){const pile=$('.tabletop-pile');if(!pile||!pile.classList.contains('has-cards'))return null;const r=pile.getBoundingClientRect();return{left:r.left,top:r.top,right:r.right,bottom:r.bottom};}
function inPile(e){const r=pileRect();return !!r&&e.clientX>=r.left&&e.clientX<=r.right&&e.clientY>=r.top&&e.clientY<=r.bottom;}

// 中央牌堆查看是最低優先互動：只在桌面完全沒有其他選擇／彈窗時才允許。
// 舊版用座標判斷，因此即使 modal 蓋在牌堆上，window capture 還是會把點擊當成牌堆短按。
function visible(el){if(!el||el.hidden)return false;const style=getComputedStyle(el);return style.display!=='none'&&style.visibility!=='hidden'&&style.pointerEvents!=='none';}
function hasActiveInteraction(){
  const blockers=[
    ...document.querySelectorAll('.overlay:not([hidden])'),
    ...document.querySelectorAll('[role="dialog"]'),
    ...document.querySelectorAll('#wind-choice-overlay:not([hidden]),#reaction-overlay:not([hidden]),#empty-tomb-overlay,#overcome-death-overlay,#hope-insight-overlay:not([hidden])')
  ];
  if(blockers.some(el=>el.id!=='played-history-overlay'&&el.id!=='detail-overlay'&&visible(el)))return true;
  const selection=$('#selection-actions');
  if(selection&&visible(selection)&&selection.querySelector('button:not([disabled]),[role="button"]'))return true;
  return false;
}
function coveredByInteractiveUi(e){
  const top=document.elementFromPoint(e.clientX,e.clientY);
  if(!top)return false;
  if(top.closest('.overlay,[role="dialog"],button,input,select,textarea,a,[data-overlay-level]'))return true;
  return false;
}
function canInspectPile(e){return inPile(e)&&!hasActiveInteraction()&&!coveredByInteractiveUi(e);}

window.addEventListener('pointerdown',e=>{if((e.button??0)!==0||!canInspectPile(e))return;pilePress={pointerId:e.pointerId,x:e.clientX,y:e.clientY,long:false,timer:setTimeout(()=>{if(!pilePress||hasActiveInteraction())return;pilePress.long=true;history();if(navigator.vibrate)navigator.vibrate(18);},LONG_PRESS_MS)};},{capture:true});
window.addEventListener('pointermove',e=>{if(!pilePress||e.pointerId!==pilePress.pointerId)return;if(Math.hypot(e.clientX-pilePress.x,e.clientY-pilePress.y)>8){clearTimeout(pilePress.timer);pilePress=null;}},{capture:true});
window.addEventListener('pointerup',e=>{if(!pilePress||e.pointerId!==pilePress.pointerId)return;const p=pilePress;pilePress=null;clearTimeout(p.timer);if(!p.long&&canInspectPile(e)){if(state?.playedArea?.length)detail(state.playedArea.at(-1));else domDetail();}},{capture:true});
window.addEventListener('pointercancel',()=>{if(pilePress)clearTimeout(pilePress.timer);pilePress=null;},{capture:true});

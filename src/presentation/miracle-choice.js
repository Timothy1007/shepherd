import { GameController } from '../controller/game-controller.js';
import { getDefinition } from '../game/cards.js';

const originalAct = GameController.prototype.act;
const CHOICE_SECONDS = 30;
let active = null;

function ensureUi() {
  if (document.querySelector('#wind-choice-overlay')) return;
  const style = document.createElement('style');
  style.textContent = `
    #wind-choice-overlay[hidden]{display:none!important}
    #wind-choice-overlay{position:fixed;inset:0;z-index:30000;pointer-events:none;color:#f8f2e4}
    .wind-panel{position:absolute;inset:0;pointer-events:none;text-align:center;text-shadow:0 2px 5px #000,0 0 12px #000}
    .wind-panel h2{position:absolute;left:50%;bottom:calc(60% + min(105px,14vh) + 18px);transform:translateX(-50%);margin:0;font-size:22px;white-space:nowrap}
    .wind-face{display:block;position:absolute;left:50%;top:40%;width:min(150px,20vh);aspect-ratio:63/88;transform:translate(-50%,-50%);perspective:900px;pointer-events:auto}
    .wind-face .wind-tilt{width:100%;height:100%}
    .wind-tilt{display:block;position:relative;overflow:hidden;border-radius:9px;box-shadow:0 8px 24px #0009;transform:perspective(900px) rotateX(var(--wind-rx,0deg)) rotateY(var(--wind-ry,0deg));transition:transform .12s ease-out;will-change:transform}
    .wind-tilt>img{width:100%;height:100%;display:block;object-fit:cover}.wind-tilt .runtime-tag-glyph{z-index:2}
    @property --remaining{syntax:"<percentage>";inherits:false;initial-value:100%}
    #wind-choice-clock[hidden]{display:none!important}
    #wind-choice-clock{position:fixed;z-index:30001;width:40px;height:40px;box-sizing:border-box;border:1px solid #d9bd84;border-radius:50%;display:grid;place-items:center;pointer-events:none;transition:--remaining .25s linear;color:#fff2cf;font:700 15px/1 system-ui,sans-serif;text-shadow:0 1px 4px #000;background:radial-gradient(circle at 32% 23%,#ffffff36,transparent 55%),conic-gradient(#bb7545 var(--remaining,100%),#211b19e8 0);box-shadow:inset 0 0 8px #0008,0 3px 12px #0009}
    .wind-options{position:absolute;top:calc(40% + min(105px,14vh) + 18px);left:50%;transform:translateX(-50%);width:min(560px,90vw);display:grid;gap:6px;pointer-events:auto}
    .wind-option,.wind-confirm button{border:1px solid #e1cf9b88;border-radius:10px;background:#14201bb8;color:#fff;padding:10px 14px;cursor:pointer;font:inherit;text-shadow:0 1px 4px #000}.wind-option:hover,.wind-confirm button:hover{background:#324538db}.wind-option{position:relative}.wind-option strong{display:block;font-size:16px}.wind-option[data-pointed="true"]::after{content:"◀";position:absolute;left:calc(100% + 10px);top:50%;transform:translateY(-50%);color:#ffe190;font-size:16px;pointer-events:none}.wind-option:disabled,.wind-confirm button:disabled{opacity:.45;cursor:not-allowed}
    .wind-discard-title{position:absolute;left:50%;bottom:calc(50% + min(120px,18vh) + 22px);transform:translateX(-50%);white-space:nowrap}
    .wind-discard-title h2{position:static;transform:none;margin:0 0 12px}.wind-discard-title p{margin:0}
    .wind-cards{position:absolute;left:50%;top:50%;transform:translate(-50%,-50%);display:flex;align-items:center;gap:14px;width:min(994px,96vw);height:min(265px,38vh);overflow-x:auto;overflow-y:hidden;padding:14px 12px;box-sizing:border-box;pointer-events:auto;scrollbar-width:none;-ms-overflow-style:none;touch-action:none;user-select:none;-webkit-user-select:none;overscroll-behavior-x:contain}
    .wind-cards::-webkit-scrollbar{display:none}.wind-cards::before,.wind-cards::after{content:"";margin:auto}.wind-card{flex:0 0 auto;position:relative;width:min(150px,21vh,calc(16vw - 16px));aspect-ratio:63/88;padding:0;border:3px solid transparent;border-radius:10px;background:transparent;cursor:pointer;transition:transform .15s ease,border-color .15s ease}.wind-card.selected{border-color:#ffe190;transform:translateY(-8px)}.wind-card-face{display:block;width:100%;height:100%}.wind-card-face .wind-tilt{width:100%;height:100%}.wind-card-face .runtime-tag-glyph{z-index:2}
    .wind-bottom{position:absolute;left:50%;top:calc(50% + min(112px,15vh) + 8px);transform:translateX(-50%);width:min(600px,90vw);pointer-events:auto}.wind-bottom p{margin:4px}.wind-confirm{display:flex;justify-content:center;gap:8px}.wind-limit{color:#ffd1bf}.wind-limit:empty{display:none}
    @media(prefers-reduced-motion:reduce){.wind-tilt{transform:none!important;transition:none!important}}

  `;
  document.head.append(style);
  const overlay = document.createElement('div');
  overlay.id = 'wind-choice-overlay';
  overlay.hidden = true;
  document.body.append(overlay);
  const clock = document.createElement('div');
  clock.id = 'wind-choice-clock';
  clock.hidden = true;
  clock.setAttribute('role', 'timer');
  document.body.append(clock);
}
function clear() {
  if (active?.timer) clearInterval(active.timer);
  const overlay = document.querySelector('#wind-choice-overlay');
  if (overlay) overlay.hidden = true;
  const clock = document.querySelector('#wind-choice-clock');
  if (clock) clock.hidden = true;
  active = null;
}
function commit(extra) {
  const pending = active;
  if (!pending) return;
  clear();
  originalAct.call(pending.controller, { ...pending.action, ...extra }, pending.token);
}
function face(card, className) {
  const definition = getDefinition(card);
  const src = globalThis.__shepherdCardFaceAsset?.(definition) ?? definition.image;
  const glyph = globalThis.__shepherdCardFaceGlyph?.(definition) ?? '';
  return `<span class="${className}"><span class="wind-tilt"><img draggable="false" src="${src}" alt="${definition.name ?? ''}">${glyph}</span></span>`;
}
function bindTilt(target) {
  const tilt = target?.querySelector('.wind-tilt');
  if (!tilt) return;
  target.addEventListener('pointermove',event=>{
    if(event.buttons || event.pointerType==='touch') return;
    const rect=target.getBoundingClientRect();
    if(!rect.width || !rect.height) return;
    const x=Math.min(1,Math.max(0,(event.clientX-rect.left)/rect.width));
    const y=Math.min(1,Math.max(0,(event.clientY-rect.top)/rect.height));
    tilt.style.setProperty('--wind-rx',`${((.5-y)*18).toFixed(2)}deg`);
    tilt.style.setProperty('--wind-ry',`${((x-.5)*22).toFixed(2)}deg`);
  });
  target.addEventListener('pointerleave',()=>{tilt.style.setProperty('--wind-rx','0deg');tilt.style.setProperty('--wind-ry','0deg');});
}
function renderChoice() {
  if (!active) return;
  ensureUi();
  const { controller, action, selected, stage } = active;
  const player = controller.state.players.find(p => p.playerId === action.playerId);
  const card = player?.hand.find(c => c.instanceId === action.instanceId);
  if (!card) return clear();
  const candidates = player.hand.filter(c => c.instanceId !== action.instanceId);
  const overlay = document.querySelector('#wind-choice-overlay');
  overlay.innerHTML = `<article class="wind-panel ${stage === 'discard' ? 'wind-discard' : 'wind-main'}" aria-label="如風吹來的選擇">${stage === 'main' ? `<h2>如風吹來</h2>${face(card,'wind-face')}<div class="wind-options"><button class="wind-option" data-pointed="${candidates.length ? 'true' : 'false'}" data-wind="cycle" ${candidates.length?'':'disabled'}><strong>效果 1 · 流轉手牌</strong>棄置 1 至 3 張手牌，抽取等量的牌。</button><button class="wind-option" data-pointed="${candidates.length ? 'false' : 'true'}" data-wind="fire"><strong>效果 2 · 守住火種</strong>不棄置手牌，獲得 4 點火種。</button></div>` : `<div class="wind-discard-title"><h2>如風吹來</h2><p>選擇 1 至 3 張手牌，然後抽取等量的牌。</p></div><div class="wind-cards" aria-label="可選擇的手牌"></div><div class="wind-bottom"><p class="wind-count">已選 ${selected.size} / 3</p><p class="wind-limit" aria-live="polite"></p><div class="wind-confirm"><button data-wind="back">返回效果選擇</button><button data-wind="confirm" ${selected.size?'':'disabled'}>確認棄置並抽牌</button></div></div>`}</article>`;
  overlay.hidden = false;
  if (stage === 'main') {
    bindTilt(overlay.querySelector('.wind-face'));
    const options = overlay.querySelectorAll('.wind-option');
    for (const option of options) {
      const point = () => { if (!option.disabled) for (const item of options) item.dataset.pointed = String(item === option); };
      option.addEventListener('pointerenter', point);
      option.addEventListener('focus', point);
    }
    overlay.querySelector('[data-wind="fire"]').onclick = () => commit({choice:'fire'});
    overlay.querySelector('[data-wind="cycle"]').onclick = () => { active.stage='discard'; renderChoice(); };
  } else {
    const host = overlay.querySelector('.wind-cards');
    for (const c of candidates) {
      const definition = getDefinition(c), button = document.createElement('button');
      button.className = `wind-card${selected.has(c.instanceId)?' selected':''}`;
      button.setAttribute('aria-label', definition.name ?? `${definition.type} ${definition.number}`);
      button.innerHTML = face(c,'wind-card-face');
      button.onclick = () => { if(selected.has(c.instanceId))selected.delete(c.instanceId);else if(selected.size<3)selected.add(c.instanceId);else {overlay.querySelector('.wind-limit').textContent='最多選 3 張';return;} button.classList.toggle('selected',selected.has(c.instanceId));button.setAttribute('aria-pressed',String(selected.has(c.instanceId)));overlay.querySelector('.wind-count').textContent=`已選 ${selected.size} / 3`;overlay.querySelector('.wind-limit').textContent='';overlay.querySelector('[data-wind=\"confirm\"]').disabled=selected.size===0; };
      button.setAttribute('aria-pressed',String(selected.has(c.instanceId)));
      bindTilt(button);
      host.append(button);
    }
    host.addEventListener('wheel',event=>{if(event.deltaY && host.scrollWidth>host.clientWidth){event.preventDefault();host.scrollLeft+=event.deltaY;}},{passive:false});
    let dragStart=null, suppressClick=false;
    host.addEventListener('pointerdown',event=>{
      if (!event.isPrimary || (event.pointerType==='mouse' && event.button!==0)) return;
      dragStart={id:event.pointerId,x:event.clientX,left:host.scrollLeft,time:Date.now(),dragged:false};
      suppressClick=false;
    });
    host.addEventListener('pointermove',event=>{
      if (!dragStart || event.pointerId!==dragStart.id) return;
      const delta=event.clientX-dragStart.x;
      if (Math.abs(delta)>5 && host.scrollWidth>host.clientWidth) {
        if (!dragStart.dragged) { dragStart.dragged=true; host.setPointerCapture(event.pointerId); }
        host.scrollLeft=dragStart.left-delta;
        event.preventDefault();
      }
    });
    host.addEventListener('pointerup',event=>{
      if (!dragStart || event.pointerId!==dragStart.id) return;
      suppressClick=dragStart.dragged || Date.now()-dragStart.time>500;
      dragStart=null;
    });
    host.addEventListener('pointercancel',()=>{dragStart=null;suppressClick=true;});
    host.addEventListener('click',event=>{
      if (suppressClick) {event.preventDefault();event.stopPropagation();suppressClick=false;}
    },true);
    overlay.querySelector('[data-wind="back"]').onclick = () => {active.stage='main';renderChoice();};
    overlay.querySelector('[data-wind="confirm"]').onclick = () => commit({choice:'cycle',discardIds:[...selected]});
  }
  updateClock();
}
function updateClock() {
  if (!active) return;
  const remainingMs = Math.max(0,active.deadline-Date.now());
  const left = Math.ceil(remainingMs/1000);
  const clock = document.querySelector('#wind-choice-clock');
  const player = active.controller.state.players.find(p => p.playerId === active.action.playerId);
  const candidates = player?.hand.filter(c => c.instanceId !== active.action.instanceId) ?? [];
  if (clock) {
    const anchor = document.querySelector('.corruption-hud')?.getBoundingClientRect();
    clock.style.left = `${anchor ? anchor.right + 12 : 180}px`;
    clock.style.top = `${anchor ? anchor.top + anchor.height / 2 - 20 : 24}px`;
    clock.style.setProperty('--remaining', `${Math.min(100,remainingMs/(CHOICE_SECONDS*1000)*100)}%`);
    clock.textContent = String(left);
    clock.setAttribute('aria-label', `剩 ${left} 秒，時間到自動選擇效果${candidates.length ? '一' : '二'}`);
    clock.hidden = false;
  }
  if (left===0) {
    if (!candidates.length) return commit({choice:'fire'});
    const eligible = new Set(candidates.map(c => c.instanceId));
    const selected = [...active.selected].filter(id => eligible.has(id));
    commit({choice:'cycle',discardIds:selected.length ? selected : [candidates[0].instanceId]});
  }
}
document.addEventListener('pointerdown', event => {
  if (!active) return;
  const card = event.target.closest?.('.card[data-card-id]');
  if (card?.dataset.cardId === active.action.instanceId) {
    event.preventDefault();event.stopImmediatePropagation();renderChoice();return;
  }
  if (!event.target.closest?.('#wind-choice-overlay .wind-panel')) {
    const overlay = document.querySelector('#wind-choice-overlay');
    if (overlay) overlay.hidden=true;
  }
}, true);
GameController.prototype.act = function patchedAct(action, token = this.snapshot()) {
  if (active?.controller === this) return {ok:false,pendingChoice:true,state:this.state};
  if (action?.type === 'playMiracle') {
    const player = this.state?.players.find(p=>p.playerId===action.playerId);
    const card = player?.hand.find(c=>c.instanceId===action.instanceId);
    if (card && getDefinition(card).definitionId==='miracle-05' && !action.choice && player.type==='human') {
      if (active) clear();
      active={controller:this,action,token,stage:'main',selected:new Set(),deadline:Date.now()+CHOICE_SECONDS*1000,timer:null};
      active.timer=setInterval(updateClock,250);
      renderChoice();
      return {ok:false,pendingChoice:true,state:this.state};
    }
  }
  return originalAct.call(this,action,token);
};
const originalStart=GameController.prototype.start;
GameController.prototype.start=function(...args){clear();return originalStart.apply(this,args);};

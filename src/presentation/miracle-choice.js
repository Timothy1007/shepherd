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
    .wind-panel{position:absolute;left:50%;top:46%;transform:translate(-50%,-50%);width:min(560px,90vw);max-height:78vh;overflow:auto;text-align:center;pointer-events:auto;text-shadow:0 2px 5px #000,0 0 12px #000}
    .wind-face{display:block;position:relative;width:min(150px,20vh);aspect-ratio:63/88;margin:0 auto 8px;overflow:hidden;border-radius:9px;box-shadow:0 4px 18px #0008}
    .wind-face>img{width:100%;height:100%;display:block;object-fit:cover}.wind-face .runtime-tag-glyph{z-index:2}
    .wind-panel h2{font-size:22px;margin:4px}.wind-panel p{margin:5px}.wind-clock{font-weight:700;color:#ffe6a3}
    .wind-options{display:grid;gap:6px;margin:10px auto}.wind-option,.wind-confirm button{border:1px solid #e1cf9b88;border-radius:10px;background:#14201bb8;color:#fff;padding:10px 14px;cursor:pointer;font:inherit;text-shadow:0 1px 4px #000}.wind-option:hover,.wind-confirm button:hover{background:#324538db}.wind-option strong{display:block;font-size:16px}.wind-option:disabled,.wind-confirm button:disabled{opacity:.45;cursor:not-allowed}
    .wind-cards{display:flex;justify-content:center;flex-wrap:wrap;gap:6px;margin:8px 0}.wind-card{position:relative;width:min(68px,12vw);aspect-ratio:63/88;overflow:hidden;padding:0;border:2px solid transparent;border-radius:7px;background:#17221f;cursor:pointer}.wind-card.selected{border-color:#ffe190;transform:translateY(-4px)}.wind-card img{width:100%;height:100%;object-fit:cover}.wind-card .runtime-tag-glyph{z-index:2}.wind-confirm{display:flex;justify-content:center;gap:8px}.wind-limit{color:#ffd1bf}
  `;
  document.head.append(style);
  const overlay = document.createElement('div');
  overlay.id = 'wind-choice-overlay';
  overlay.hidden = true;
  document.body.append(overlay);
}
function clear() {
  if (active?.timer) clearInterval(active.timer);
  const overlay = document.querySelector('#wind-choice-overlay');
  if (overlay) overlay.hidden = true;
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
  return `<span class="${className}"><img src="${src}" alt="${definition.name ?? ''}">${glyph}</span>`;
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
  overlay.innerHTML = `<article class="wind-panel" aria-label="如風吹來的選擇">${face(card,'wind-face')}<h2>如風吹來</h2><p class="wind-clock" aria-live="polite"></p>${stage === 'main' ? `<p>選擇一項效果</p><div class="wind-options"><button class="wind-option" data-wind="cycle" ${candidates.length?'':'disabled'}><strong>效果 1 · 流轉手牌</strong>棄置 1 至 3 張手牌，抽取等量的牌。</button><button class="wind-option" data-wind="fire"><strong>效果 2 · 守住火種</strong>不棄置手牌，獲得 4 點火種。</button></div>` : `<p>選擇 1 至 3 張手牌，然後抽取等量的牌。</p><p class="wind-count">已選 ${selected.size} / 3</p><p class="wind-limit" aria-live="polite"></p><div class="wind-cards"></div><div class="wind-confirm"><button data-wind="back">返回效果選擇</button><button data-wind="confirm" ${selected.size?'':'disabled'}>確認棄置並抽牌</button></div>`}</article>`;
  overlay.hidden = false;
  if (stage === 'main') {
    overlay.querySelector('[data-wind="fire"]').onclick = () => commit({choice:'fire'});
    overlay.querySelector('[data-wind="cycle"]').onclick = () => { active.stage='discard'; renderChoice(); };
  } else {
    const host = overlay.querySelector('.wind-cards');
    for (const c of candidates) {
      const definition = getDefinition(c), button = document.createElement('button');
      button.className = `wind-card${selected.has(c.instanceId)?' selected':''}`;
      button.setAttribute('aria-label', definition.name ?? `${definition.type} ${definition.number}`);
      button.innerHTML = face(c,'wind-card-face');
      button.onclick = () => { if(selected.has(c.instanceId))selected.delete(c.instanceId);else if(selected.size<3)selected.add(c.instanceId);else {overlay.querySelector('.wind-limit').textContent='最多選 3 張';return;}renderChoice(); };
      host.append(button);
    }
    overlay.querySelector('[data-wind="back"]').onclick = () => {active.stage='main';renderChoice();};
    overlay.querySelector('[data-wind="confirm"]').onclick = () => commit({choice:'cycle',discardIds:[...selected]});
  }
  updateClock();
}
function updateClock() {
  if (!active) return;
  const left = Math.max(0,Math.ceil((active.deadline-Date.now())/1000));
  const label = document.querySelector('#wind-choice-overlay .wind-clock');
  if (label) label.textContent = `剩餘 ${left} 秒 · 時間到自動選擇守住火種`;
  if (left===0) commit({choice:'fire'});
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

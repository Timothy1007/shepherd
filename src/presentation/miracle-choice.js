import { GameController } from '../controller/game-controller.js';
import { getDefinition } from '../game/cards.js';

const originalAct = GameController.prototype.act;
let active = null;

function ensureUi() {
  if (document.querySelector('#wind-choice-overlay')) return;
  const style = document.createElement('style');
  style.textContent = `
    #wind-choice-overlay[hidden]{display:none!important}#wind-choice-overlay{position:fixed;inset:0;z-index:30000;display:grid;place-items:center;background:#050807b8;backdrop-filter:blur(8px)}
    .wind-panel{width:min(880px,92vw);max-height:86vh;overflow:auto;padding:28px;border:1px solid #d7bd7a55;border-radius:20px;background:linear-gradient(145deg,#171b18,#0c100e);box-shadow:0 28px 80px #000b;color:#eee8d8}
    .wind-panel h2{margin:5px 0 8px;font-size:28px}.wind-panel p{opacity:.78}.wind-options{display:grid;grid-template-columns:1fr 1fr;gap:14px;margin:22px 0}.wind-option{padding:18px;text-align:left;border:1px solid #d7bd7a55;border-radius:14px;background:#ffffff08;color:inherit;cursor:pointer}.wind-option:hover{background:#ffffff12}.wind-option strong{display:block;font-size:18px;margin-bottom:6px}.wind-cards{display:flex;flex-wrap:wrap;gap:10px;margin:14px 0 20px}.wind-card{width:92px;padding:5px;border:2px solid transparent;border-radius:10px;background:#ffffff08;cursor:pointer}.wind-card.selected{border-color:#d7bd7a;transform:translateY(-5px)}.wind-card img{display:block;width:100%;border-radius:7px}.wind-confirm{display:flex;gap:10px;justify-content:flex-end}.wind-confirm button{padding:10px 16px;border-radius:10px;border:1px solid #d7bd7a55;background:#ffffff0d;color:inherit;cursor:pointer}.wind-confirm .primary{background:#d7bd7a;color:#17130b;font-weight:700}.wind-confirm .primary:disabled{opacity:.35;cursor:not-allowed}.wind-count{font-weight:700;color:#e6cc8d}
  `;
  document.head.append(style);
  const overlay = document.createElement('div');
  overlay.id = 'wind-choice-overlay';
  overlay.hidden = true;
  document.body.append(overlay);
}

function close() {
  const overlay = document.querySelector('#wind-choice-overlay');
  if (overlay) overlay.hidden = true;
  active = null;
}

function commit(extra) {
  const pending = active;
  if (!pending) return;
  close();
  originalAct.call(pending.controller, { ...pending.action, ...extra }, pending.token);
}

function showMainChoice() {
  ensureUi();
  const overlay = document.querySelector('#wind-choice-overlay');
  overlay.innerHTML = `<article class="wind-panel" role="dialog" aria-modal="true"><span>神蹟 · 流轉｜火種</span><h2>如風吹來</h2><p>風向已起。選擇這次神蹟要帶來的改變。</p><div class="wind-options"><button class="wind-option" data-wind="cycle"><strong>流轉手牌</strong>棄置 1 至 3 張手牌，然後抽取等量的牌。</button><button class="wind-option" data-wind="fire"><strong>守住火種</strong>不棄置手牌，獲得 3 點火種。</button></div><div class="wind-confirm"><button data-wind="cancel">取消，放回手牌</button></div></article>`;
  overlay.hidden = false;
  overlay.querySelector('[data-wind="fire"]').addEventListener('click', () => commit({ choice: 'fire' }));
  overlay.querySelector('[data-wind="cycle"]').addEventListener('click', showDiscardChoice);
  overlay.querySelector('[data-wind="cancel"]').addEventListener('click', close);
}

function showDiscardChoice() {
  const { controller, action } = active;
  const player = controller.state.players.find((candidate) => candidate.playerId === action.playerId);
  const candidates = player.hand.filter((card) => card.instanceId !== action.instanceId);
  const overlay = document.querySelector('#wind-choice-overlay');
  const selected = new Set();
  overlay.innerHTML = `<article class="wind-panel" role="dialog" aria-modal="true"><span>如風吹來 · 流轉</span><h2>選擇要放下的牌</h2><p>選擇 1 至 3 張。確認後，這些牌會進入棄牌區，再從牌庫頂抽取相同數量。</p><div class="wind-count">已選 0 / 3</div><div class="wind-cards"></div><div class="wind-confirm"><button data-wind="back">返回</button><button class="primary" data-wind="confirm" disabled>棄置並重新抽取</button></div></article>`;
  const host = overlay.querySelector('.wind-cards');
  for (const card of candidates) {
    const definition = getDefinition(card);
    const button = document.createElement('button');
    button.className = 'wind-card';
    button.title = definition.name ?? `${definition.type} ${definition.number}`;
    button.innerHTML = `<img src="${definition.image}" alt="${button.title}">`;
    button.addEventListener('click', () => {
      if (selected.has(card.instanceId)) selected.delete(card.instanceId);
      else if (selected.size < 3) selected.add(card.instanceId);
      button.classList.toggle('selected', selected.has(card.instanceId));
      overlay.querySelector('.wind-count').textContent = `已選 ${selected.size} / 3`;
      overlay.querySelector('[data-wind="confirm"]').disabled = selected.size < 1;
    });
    host.append(button);
  }
  overlay.querySelector('[data-wind="back"]').addEventListener('click', showMainChoice);
  overlay.querySelector('[data-wind="confirm"]').addEventListener('click', () => commit({ choice: 'cycle', discardIds: [...selected] }));
}

GameController.prototype.act = function patchedAct(action, token = this.snapshot()) {
  if (action?.type === 'playMiracle') {
    const card = this.state?.players.flatMap((player) => player.hand).find((held) => held.instanceId === action.instanceId);
    if (card && getDefinition(card).definitionId === 'miracle-05' && !action.choice) {
      const player = this.state.players.find((candidate) => candidate.playerId === action.playerId);
      if (player?.type === 'human') {
        active = { controller: this, action, token };
        showMainChoice();
        return { ok: false, pendingChoice: true, state: this.state };
      }
    }
  }
  return originalAct.call(this, action, token);
};

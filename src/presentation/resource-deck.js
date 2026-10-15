const status = document.querySelector('#status');
const deck = document.querySelector('#resource-deck');
const discardPile = document.querySelector('#discard-pile');
const count = document.querySelector('#resource-deck-count');
const discardCount = document.querySelector('#discard-pile-count');
let lastCount = null;
let disposed = false;

function readDeckCount() {
  const match = status?.textContent?.match(/牌庫\s*(\d+)/);
  return match ? Number(match[1]) : null;
}

function readDiscardCount() {
  const match = status?.textContent?.match(/棄牌\s*(\d+)/);
  return match ? Number(match[1]) : null;
}

function showPileDetail(title, pileCount, hidden) {
  const overlay = document.querySelector('#detail-overlay');
  const content = document.querySelector('#detail-content');
  if (!overlay || !content) return;
  content.innerHTML = `<div class="blindness-detail-card" aria-hidden="true">?</div><div class="detail-copy"><span class="eyebrow">${hidden ? '隱藏' : '覆蓋'}牌堆</span><h2>${title}</h2><p>${hidden ? '此區牌面不公開；只有卡牌效果明確允許時才會展示可查看的牌。' : '牌庫中的卡牌維持覆蓋狀態。'}</p><p class="detail-note">目前共 ${pileCount} 張。</p></div>`;
  overlay.hidden = false;
}

deck?.addEventListener('click', () => showPileDetail('牌庫', lastCount ?? 0, false));
discardPile?.addEventListener('click', () => showPileDetail('棄牌堆', Number(discardCount?.textContent ?? 0), true));

function tick() {
  if (disposed) return;
  const next = readDeckCount(), discarded = readDiscardCount();
  if (next !== null && next !== lastCount) {
    lastCount = next;
    count.textContent = String(next);
    deck.setAttribute('aria-label', `牌庫，剩餘 ${next} 張`);
    deck.classList.toggle('deck-low', next > 0 && next <= 12);
    deck.classList.toggle('deck-empty', next === 0);
  }
  if (discarded !== null && discardPile && discardCount) {
    discardCount.textContent = String(discarded);
    discardPile.setAttribute('aria-label', `隱藏棄牌堆，剩餘 ${discarded} 張`);
    discardPile.classList.toggle('deck-low', discarded > 0 && discarded <= 12);
    discardPile.classList.toggle('deck-empty', discarded === 0);
  }
  window.setTimeout(tick, 180);
}

window.setTimeout(tick, 180);
window.addEventListener('beforeunload', () => { disposed = true; }, { once: true });

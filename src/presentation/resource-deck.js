const status = document.querySelector('#status');
const deck = document.querySelector('#resource-deck');
const count = document.querySelector('#resource-deck-count');
let lastCount = null;
let disposed = false;

function readDeckCount() {
  const match = status?.textContent?.match(/牌庫\s*(\d+)/);
  return match ? Number(match[1]) : null;
}

function tick() {
  if (disposed) return;
  const next = readDeckCount();
  if (next !== null && next !== lastCount) {
    lastCount = next;
    count.textContent = String(next);
    deck.setAttribute('aria-label', `物資牌庫，剩餘 ${next} 張`);
    deck.classList.toggle('deck-low', next > 0 && next <= 12);
    deck.classList.toggle('deck-empty', next === 0);
  }
  window.setTimeout(tick, 180);
}

window.setTimeout(tick, 180);
window.addEventListener('beforeunload', () => { disposed = true; }, { once: true });

const resultEl = document.querySelector('#round-result');
const roundDisplay = document.querySelector('#round-display');
const pile = document.querySelector('.tabletop-pile');
const instruction = document.querySelector('#instruction');
const restartButton = document.querySelector('#restart');

const seatByPlayer = {
  'player-1': '#seat-human',
  'player-2': '#seat-left',
  'player-3': '#seat-top',
  'player-4': '#seat-right',
};

let resolvedRound = null;
let lastRoundLabel = roundDisplay?.textContent || '';

function center(rect) {
  return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
}

function clearResolutionVisuals() {
  document.querySelectorAll('.round-ember,.round-resolution-label').forEach((node) => node.remove());
  pile?.classList.remove('round-burning');
  if (pile) pile.style.opacity = '';
}

function createEmber(from, to, index, total) {
  const ember = document.createElement('span');
  ember.className = 'round-ember';
  ember.style.left = `${from.x + (index - total / 2) * 4}px`;
  ember.style.top = `${from.y}px`;
  document.body.append(ember);

  const dx = to.x - from.x + (index % 2 ? 10 : -10);
  const dy = to.y - from.y + ((index % 3) - 1) * 8;
  const arcX = dx * .48 + ((index % 2) ? 34 : -34);
  const arcY = dy * .42 - 70 - index * 2;
  const delay = index * 46;
  const animation = ember.animate([
    { transform: 'translate(0,0) scale(.7) rotate(0deg)', opacity: 0 },
    { offset: .14, transform: 'translate(0,-12px) scale(1.15) rotate(8deg)', opacity: 1 },
    { offset: .56, transform: `translate(${arcX}px,${arcY}px) scale(.95) rotate(-9deg)`, opacity: 1 },
    { transform: `translate(${dx}px,${dy}px) scale(.35) rotate(14deg)`, opacity: 0 },
  ], { duration: 820, delay, easing: 'cubic-bezier(.25,.7,.25,1)', fill: 'forwards' });
  animation.finished.catch(() => {}).then(() => ember.remove());
}

function animateResolution(round, apostle, reward) {
  if (!pile || resolvedRound === round) return;
  resolvedRound = round;

  const target = document.querySelector(seatByPlayer[apostle]);
  const sourceRect = pile.getBoundingClientRect();
  const targetRect = target?.getBoundingClientRect();
  if (!sourceRect.width || !sourceRect.height || !targetRect?.width) return;

  pile.style.opacity = '';
  pile.classList.remove('round-burning');
  void pile.offsetWidth;
  pile.classList.add('round-burning');

  const from = center(sourceRect);
  const to = center(targetRect);
  const count = Math.max(5, Math.min(11, Math.ceil(reward / 2)));
  for (let i = 0; i < count; i += 1) createEmber(from, to, i, count);

  const label = document.createElement('div');
  label.className = 'round-resolution-label';
  label.textContent = `使徒 ${apostle} · +${reward} 火種`;
  document.body.append(label);
  setTimeout(() => label.remove(), 1500);

  if (instruction) instruction.textContent = `第 ${round} 輪結算 · 火種歸於 ${apostle}`;
  setTimeout(() => {
    if (resolvedRound === round && pile) pile.style.opacity = '0';
  }, 920);
}

function readResult() {
  if (!resultEl) return;
  const match = resultEl.textContent.match(/第\s*(\d+)\s*輪\s*·\s*使徒\s*([^·]+?)\s*·\s*\+(\d+)\s*火種/);
  if (!match) return;
  animateResolution(Number(match[1]), match[2].trim(), Number(match[3]));
}

if (resultEl) {
  new MutationObserver(readResult).observe(resultEl, { childList: true, characterData: true, subtree: true });
  readResult();
}

if (roundDisplay) {
  new MutationObserver(() => {
    const next = roundDisplay.textContent || '';
    if (next === lastRoundLabel) return;
    lastRoundLabel = next;
    clearResolutionVisuals();
  }).observe(roundDisplay, { childList: true, characterData: true, subtree: true });
}

restartButton?.addEventListener('click', () => {
  resolvedRound = null;
  lastRoundLabel = '第 1 / 7 輪';
  clearResolutionVisuals();
});

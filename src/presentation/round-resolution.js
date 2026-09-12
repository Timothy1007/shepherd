const resultEl = document.querySelector('#round-result');

const seatByPlayer = {
  'player-1': '#seat-human',
  'player-2': '#seat-left',
  'player-3': '#seat-top',
  'player-4': '#seat-right',
};

let lastAnimatedRound = null;
let activeOverlay = null;

function center(rect) {
  return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
}

function cleanup() {
  activeOverlay?.remove();
  activeOverlay = null;
  document.querySelectorAll('.round-ember,.round-resolution-label').forEach((node) => node.remove());
}

function readRoundResult(expectedRound) {
  const match = resultEl?.textContent?.match(/第\s*(\d+)\s*輪\s*·\s*使徒\s*([^·]+?)\s*·\s*\+(\d+)\s*火種/);
  if (!match) return null;
  const round = Number(match[1]);
  if (round !== expectedRound) return null;
  return { round, apostle: match[2].trim(), reward: Number(match[3]) };
}

function createEmber(from, to, index, total) {
  const ember = document.createElement('span');
  ember.className = 'round-ember';
  ember.style.left = `${from.x + (index - total / 2) * 4}px`;
  ember.style.top = `${from.y}px`;
  document.body.append(ember);

  const dx = to.x - from.x + (index % 2 ? 12 : -12);
  const dy = to.y - from.y + ((index % 3) - 1) * 8;
  const arcX = dx * .48 + (index % 2 ? 34 : -34);
  const arcY = dy * .42 - 72 - index * 2;
  const animation = ember.animate([
    { transform: 'translate(0,0) scale(.7)', opacity: 0 },
    { offset: .14, transform: 'translate(0,-12px) scale(1.15)', opacity: 1 },
    { offset: .56, transform: `translate(${arcX}px,${arcY}px) scale(.95)`, opacity: 1 },
    { transform: `translate(${dx}px,${dy}px) scale(.35)`, opacity: 0 },
  ], {
    duration: 820,
    delay: index * 46,
    easing: 'cubic-bezier(.25,.7,.25,1)',
    fill: 'forwards',
  });
  animation.finished.catch(() => {}).then(() => ember.remove());
}

function animateSnapshot({ round, rect, pile }) {
  const result = readRoundResult(round);
  if (!result || !result.apostle || result.apostle === '無' || lastAnimatedRound === round) return;

  const target = document.querySelector(seatByPlayer[result.apostle]);
  const targetRect = target?.getBoundingClientRect();
  if (!targetRect?.width || !rect?.width || !pile) return;

  lastAnimatedRound = round;
  cleanup();

  const overlay = document.createElement('div');
  overlay.className = 'round-resolution-pile round-burning';
  overlay.style.left = `${rect.left}px`;
  overlay.style.top = `${rect.top}px`;
  overlay.style.width = `${rect.width}px`;
  overlay.style.height = `${rect.height}px`;
  overlay.append(...Array.from(pile.children).map((child) => child.cloneNode(true)));
  document.body.append(overlay);
  activeOverlay = overlay;

  const from = center(rect);
  const to = center(targetRect);
  const count = Math.max(5, Math.min(11, Math.ceil(result.reward / 2)));
  for (let index = 0; index < count; index += 1) createEmber(from, to, index, count);

  const label = document.createElement('div');
  label.className = 'round-resolution-label';
  label.textContent = `使徒 ${result.apostle} · +${result.reward} 火種`;
  document.body.append(label);
  setTimeout(() => label.remove(), 1500);
  setTimeout(() => {
    if (activeOverlay === overlay) {
      overlay.remove();
      activeOverlay = null;
    }
  }, 1150);
}

window.addEventListener('shepherd:round-pile-snapshot', (event) => {
  try {
    animateSnapshot(event.detail ?? {});
  } catch (error) {
    console.error('[Shepherd] round resolution visual failed; gameplay is unaffected.', error);
    cleanup();
  }
});

window.addEventListener('beforeunload', cleanup, { once: true });

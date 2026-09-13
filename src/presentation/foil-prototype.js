const hand = document.querySelector('#hand');
let activeCard = null;

function resetTilt(card) {
  if (!card) return;
  card.style.setProperty('--foil-rx', '0deg');
  card.style.setProperty('--foil-ry', '0deg');
  card.style.setProperty('--foil-x', '50%');
  card.style.setProperty('--foil-y', '50%');
}

function bindFoil(card) {
  if (!card || card.dataset.foilBound === '1') return;
  card.dataset.foilBound = '1';
  const glare = document.createElement('span');
  glare.className = 'foil-glare';
  const sparkle = document.createElement('span');
  sparkle.className = 'foil-sparkle';
  card.append(glare, sparkle);

  card.addEventListener('pointermove', (event) => {
    if (event.buttons) return;
    const rect = card.getBoundingClientRect();
    const px = Math.min(1, Math.max(0, (event.clientX - rect.left) / rect.width));
    const py = Math.min(1, Math.max(0, (event.clientY - rect.top) / rect.height));
    const ry = (px - .5) * 20;
    const rx = (.5 - py) * 16;
    card.style.setProperty('--foil-rx', `${rx.toFixed(2)}deg`);
    card.style.setProperty('--foil-ry', `${ry.toFixed(2)}deg`);
    card.style.setProperty('--foil-x', `${(px * 100).toFixed(1)}%`);
    card.style.setProperty('--foil-y', `${(py * 100).toFixed(1)}%`);
  });
  card.addEventListener('pointerleave', () => resetTilt(card));
  card.addEventListener('pointerdown', () => resetTilt(card));
}

function markPrototypeCard() {
  const cards = [...hand.querySelectorAll('.card')];
  if (!cards.length) return;

  if (activeCard && activeCard.isConnected && activeCard.parentElement === hand) return;

  cards.forEach((card) => card.classList.remove('foil-prototype'));
  activeCard = cards[0];
  activeCard.classList.add('foil-prototype');
  bindFoil(activeCard);
}

new MutationObserver(markPrototypeCard).observe(hand, { childList: true });
markPrototypeCard();
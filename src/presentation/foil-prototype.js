const hand = document.querySelector('#hand');
const detailContent = document.querySelector('#detail-content');
let activeCard = null;

function resetTilt(card) {
  if (!card) return;
  card.style.setProperty('--foil-rx', '0deg');
  card.style.setProperty('--foil-ry', '0deg');
  card.style.setProperty('--foil-x', '50%');
  card.style.setProperty('--foil-y', '50%');
}

function bindTilt(card) {
  if (!card || card.dataset.foilBound === '1') return;
  card.dataset.foilBound = '1';
  card.addEventListener('pointermove', (event) => {
    if (event.buttons) return;
    const rect = card.getBoundingClientRect();
    const px = Math.min(1, Math.max(0, (event.clientX - rect.left) / rect.width));
    const py = Math.min(1, Math.max(0, (event.clientY - rect.top) / rect.height));
    const ry = (px - .5) * 22;
    const rx = (.5 - py) * 18;
    card.style.setProperty('--foil-rx', `${rx.toFixed(2)}deg`);
    card.style.setProperty('--foil-ry', `${ry.toFixed(2)}deg`);
    card.style.setProperty('--foil-x', `${(px * 100).toFixed(1)}%`);
    card.style.setProperty('--foil-y', `${(py * 100).toFixed(1)}%`);
  });
  card.addEventListener('pointerleave', () => resetTilt(card));
  card.addEventListener('pointerdown', () => resetTilt(card));
}

function addLayers(card) {
  if (!card.querySelector('.foil-glare')) {
    const glare = document.createElement('span');
    glare.className = 'foil-glare';
    card.append(glare);
  }
  if (!card.querySelector('.foil-sparkle')) {
    const sparkle = document.createElement('span');
    sparkle.className = 'foil-sparkle';
    card.append(sparkle);
  }
  if (!card.querySelector('.foil-sheen')) {
    const sheen = document.createElement('span');
    sheen.className = 'foil-sheen';
    card.append(sheen);
  }
}

function bindFoil(card) {
  if (!card) return;
  addLayers(card);
  bindTilt(card);
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

function rebuildDetailShowcase() {
  if (!detailContent) return;
  const image = detailContent.querySelector('.detail-card-art');
  const copy = detailContent.querySelector('.detail-copy');
  if (!image || !copy || detailContent.querySelector('.detail-showcase')) return;

  const showcase = document.createElement('div');
  showcase.className = 'detail-showcase';
  const card = document.createElement('div');
  card.className = 'detail-foil-card';
  image.replaceWith(showcase);
  card.append(image);
  showcase.append(card);
  detailContent.insertBefore(showcase, copy);
  bindFoil(card);
}

new MutationObserver(markPrototypeCard).observe(hand, { childList: true });
if (detailContent) new MutationObserver(rebuildDetailShowcase).observe(detailContent, { childList: true, subtree: false });
markPrototypeCard();

const hand = document.querySelector('#hand');
const detailContent = document.querySelector('#detail-content');
const inspectorContent = document.querySelector('#card-inspector-card');
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

function rebuildDetailShowcase(host) {
  if (!host) return;
  const image = host.querySelector('.detail-card-art');
  const copy = host.querySelector('.detail-copy');
  if (!image || host.querySelector('.detail-showcase')) return;

  const showcase = document.createElement('div');
  showcase.className = 'detail-showcase';
  const card = document.createElement('div');
  card.className = 'detail-foil-card';
  image.replaceWith(showcase);
  card.append(image);
  const glyphs = Object.freeze({
    '新生': '<svg viewBox="0 0 100 100" fill="currentColor"><rect x="23" y="23" width="54" height="54" rx="10" transform="rotate(45 50 50)"/></svg>',
    '熄滅': '<svg viewBox="0 0 100 100" fill="currentColor" fill-rule="evenodd"><rect x="21" y="21" width="58" height="58" rx="10" transform="rotate(45 50 50)"/><rect x="38" y="38" width="24" height="24" rx="4" transform="rotate(45 50 50)" fill="#0c1717"/></svg>',
    '洞察': '<svg viewBox="0 0 100 100" fill="currentColor" fill-rule="evenodd"><path d="M18 43C34 22 68 22 84 47C85 49 85 51 84 53C68 78 34 78 18 57C14 53 14 47 18 43Z M50 40A10 10 0 1 0 50 60A10 10 0 1 0 50 40Z"/></svg>',
    '庇護': '<svg viewBox="0 0 100 100" fill="currentColor" aria-hidden="true"><path d="M45 81C25 80 14 66 14 46V29C32 29 45 43 45 61Z"/><path d="M52 81C73 80 86 63 86 40V13C66 13 52 29 52 52Z"/></svg>',
    '重構': '<svg viewBox="0 0 100 100" fill="currentColor" fill-rule="evenodd"><path d="M50 8L83 41Q92 50 83 59L59 83Q50 92 41 83L17 59Q8 50 17 41L41 17Q50 8 59 17Z M50 36.4L61.4 47.8Q63.6 50 61.4 52.2L52.2 61.4Q50 63.6 47.8 61.4L38.6 52.2Q36.4 50 38.6 47.8L47.8 38.6Q50 36.4 52.2 38.6Z"/></svg>',
    '崩解': '<svg viewBox="0 0 100 100" fill="currentColor"><rect x="36" y="10" width="24" height="24" rx="7"/><rect x="10" y="31" width="34" height="34" rx="8"/><rect x="36" y="61" width="28" height="28" rx="8"/><rect x="56" y="31" width="31" height="34" rx="8"/><rect x="72" y="61" width="28" height="28" rx="8"/><rect x="61" y="81" width="22" height="19" rx="7"/></svg>',
  });
  const tag = host.dataset.faceGlyph, glyph = glyphs[tag];
  if (glyph) {
    const glyph = document.createElement('span');
    glyph.className = `runtime-tag-glyph runtime-tag-glyph--${tag === '新生' ? 'rebirth' : tag === '熄滅' ? 'extinction' : tag === '洞察' ? 'insight' : tag === '庇護' ? 'sanctuary' : tag === '重構' ? 'reforge' : 'collapse'}${host.dataset.faceGlyphTone === 'disaster' ? ' is-disaster' : ''}`;
    glyph.setAttribute('aria-hidden', 'true');
    glyph.innerHTML = glyphs[tag];
    card.append(glyph);
  }
  showcase.append(card);
  if (copy) host.insertBefore(showcase, copy);
  else host.append(showcase);
  bindFoil(card);
}

new MutationObserver(markPrototypeCard).observe(hand, { childList: true });
if (detailContent) new MutationObserver(() => rebuildDetailShowcase(detailContent)).observe(detailContent, { childList: true, subtree: false });
if (inspectorContent) new MutationObserver(() => rebuildDetailShowcase(inspectorContent)).observe(inspectorContent, { childList: true, subtree: false });
markPrototypeCard();

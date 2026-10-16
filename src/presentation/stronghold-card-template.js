const CARD_NAME = '拆毀堅固營壘';
const CARD_BACKGROUND = 'assets/miracles/miracle%20test.png';

const icon = (kind) => {
  if (kind === 'claw') return '<svg class="stronghold-icon stronghold-claw" viewBox="0 0 24 24" aria-hidden="true"><path d="M3 5H21L12 21Z"/></svg>';
  if (kind === 'shield') return '<svg class="stronghold-icon stronghold-shield" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3 20 6v5.3c0 4.7-3.1 7.9-8 10.1-4.9-2.2-8-5.4-8-10.1V6l8-3Z"/><path d="m6 18 12-12"/></svg>';
  return '<svg class="stronghold-icon stronghold-card-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M7 18H4a1 1 0 0 1-1-1V3a1 1 0 0 1 1-1h11a1 1 0 0 1 1 1v2"/><rect x="7" y="6" width="13" height="16" rx="1.5"/></svg>';
};

function copyMarkup() {
  return `<div class="stronghold-card-copy" aria-hidden="true">
    <h3>拆毀堅固營壘</h3>
    <div class="stronghold-timing">應對</div>
    <div class="stronghold-rule-lines">當你即將受到具有2項及以上效果的<span class="stronghold-term">${icon('claw')}<b>災難</b></span>時，你可以打出此牌：選擇其中1項效果，使其對你<span class="stronghold-term">${icon('shield')}<b>無效</b></span>；其餘效果正常結算。結算後，你棄置<span class="stronghold-count">1${icon('card')}</span>，然後抽<span class="stronghold-count">1${icon('card')}</span>。</div>
  </div>`;
}

function handTemplate(image) {
  const card = image.closest('.card');
  if (!card || card.dataset.strongholdTemplate === 'true') return;
  card.dataset.strongholdTemplate = 'true';
  card.classList.add('stronghold-template-card');
  image.src = CARD_BACKGROUND;
  image.insertAdjacentHTML('afterend', copyMarkup());
}

function detailTemplate(image) {
  if (image.dataset.strongholdTemplate === 'true') return;
  const card = document.createElement('div');
  card.className = 'detail-card-art stronghold-template-card detail-stronghold-template';
  card.dataset.strongholdTemplate = 'true';
  card.innerHTML = copyMarkup();
  image.replaceWith(card);
}

function reactionTemplate(image) {
  if (image.dataset.strongholdTemplate === 'true') return;
  const card = document.createElement('div');
  card.className = 'stronghold-template-card reaction-stronghold-template';
  card.dataset.strongholdTemplate = 'true';
  card.innerHTML = copyMarkup();
  image.replaceWith(card);
}

function applyTemplates() {
  document.querySelectorAll(`.card .card-art[alt="${CARD_NAME}"]`).forEach(handTemplate);
  document.querySelectorAll(`.detail-card-art[alt="${CARD_NAME}"]`).forEach(detailTemplate);
  document.querySelectorAll('#stronghold-overlay img[src$="miracle-19.png"]').forEach(reactionTemplate);
}

applyTemplates();
new MutationObserver(applyTemplates).observe(document.body, { childList: true, subtree: true });

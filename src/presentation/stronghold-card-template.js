const CARD_NAME = '拆毀堅固營壘';
const CARD_BACKGROUND = 'assets/miracles/miracle%20test.png';

const icon = (kind) => {
  if (kind === 'claw') return '<svg class="stronghold-icon stronghold-claw" viewBox="0 0 24 24" aria-hidden="true"><path d="M5 19c1.2-4.8 2.1-8.6 3-11.5M10 19c.7-5.2 1.2-9 1.8-12M15 19c.2-4.6.4-7.7.8-10.5M19 18c-.3-3-.2-5.2.4-7"/><path d="M4 19h16"/></svg>';
  if (kind === 'shield') return '<svg class="stronghold-icon stronghold-shield" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3 20 6v5.3c0 4.7-3.1 7.9-8 10.1-4.9-2.2-8-5.4-8-10.1V6l8-3Z"/><path d="m6 18 12-12"/></svg>';
  return '<svg class="stronghold-icon stronghold-card-icon" viewBox="0 0 24 24" aria-hidden="true"><rect x="5" y="3" width="14" height="18" rx="2"/><path d="M8 7h8M8 11h8M8 15h5"/></svg>';
};

function copyMarkup() {
  return `<div class="stronghold-card-copy" aria-hidden="true">
    <h3>拆毀堅固營壘</h3>
    <div class="stronghold-timing">應對</div>
    <div class="stronghold-rule-lines">
      <span>當你即將受到具有2項及以上效果</span>
      <span>的 ${icon('claw')}<b>災難</b> 時，你可以打出此牌：選擇</span>
      <span>其中1項效果，使其對你 ${icon('shield')}<b>無效</b>；</span>
      <span>其餘效果正常結算。結算後，</span>
      <span>棄置 ${icon('card')}1，然後抽 ${icon('card')}1。</span>
    </div>
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

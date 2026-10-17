const CARD_STYLES = Object.freeze({
  '拆毀堅固營壘': Object.freeze({ kind: 'miracle', background: 'assets/miracles/miracle.png' }),
  '灰與燼': Object.freeze({ kind: 'disaster', background: 'assets/disasters/disasters.png' }),
});

const icon = (kind) => {
  if (kind === 'disaster') return '<svg class="card-template-icon disaster-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M3 5H21L12 21Z"/></svg>';
  if (kind === 'shield') return '<svg class="card-template-icon shield-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3 20 6v5.3c0 4.7-3.1 7.9-8 10.1-4.9-2.2-8-5.4-8-10.1V6l8-3Z"/><path d="m6 18 12-12"/></svg>';
  return '<svg class="card-template-icon card-count-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M7 18H4a1 1 0 0 1-1-1V3a1 1 0 0 1 1-1h11a1 1 0 0 1 1 1v2"/><rect x="7" y="6" width="13" height="16" rx="1.5"/></svg>';
};

function miracleCopy() {
  return `<div class="stronghold-card-copy" aria-hidden="true">
    <h3>拆毀堅固營壘</h3>
    <div class="stronghold-timing">應對</div>
    <div class="stronghold-rule-lines">當你即將受到具有2項及以上效果的<span class="stronghold-term">${icon('disaster')}<b>災難</b></span>時，你可以打出此牌：選擇其中1項效果，使其對你<span class="stronghold-term">${icon('shield')}<b>無效</b></span>；其餘效果正常結算。結算後，你棄置<span class="stronghold-count">1${icon('card')}</span>，然後抽<span class="stronghold-ending"><span class="stronghold-count">1${icon('card')}</span>。</span></div>
  </div>`;
}

function disasterCopy() {
  return `<div class="ashes-card-copy" aria-hidden="true">
    <h3>灰與燼</h3>
    <div class="ashes-rule-lines">每名玩家失去 <span class="ashes-number">4</span> 點火種。直到本輪結束，所有玩家因神蹟效果獲得的火種減少 <span class="ashes-number">2</span> 點，最低為 <span class="ashes-number">0</span>。</div>
  </div>`;
}

function copyFor(style) { return style.kind === 'miracle' ? miracleCopy() : disasterCopy(); }
function templateClass(style) { return style.kind === 'miracle' ? 'stronghold-template-card' : 'ashes-template-card'; }

function mountHand(image, style) {
  const card = image.closest('.card');
  if (!card || card.dataset.specialCardTemplate === 'true') return;
  card.dataset.specialCardTemplate = 'true';
  card.classList.add(templateClass(style));
  image.src = style.background;
  image.insertAdjacentHTML('afterend', copyFor(style));
}

function mountDetail(image, style) {
  if (image.dataset.specialCardTemplate === 'true') return;
  const card = document.createElement('div');
  card.className = `detail-card-art ${templateClass(style)} detail-special-template`;
  card.dataset.specialCardTemplate = 'true';
  card.innerHTML = copyFor(style);
  image.replaceWith(card);
}

function mountReaction(image) {
  if (image.dataset.specialCardTemplate === 'true') return;
  const card = document.createElement('div');
  card.className = 'stronghold-template-card reaction-stronghold-template';
  card.dataset.specialCardTemplate = 'true';
  card.innerHTML = miracleCopy();
  image.replaceWith(card);
}

function mountTable(image, style) {
  if (image.dataset.specialCardTemplate === 'true') return;
  const card = document.createElement('div');
  card.className = `table-card-template ${templateClass(style)}`;
  card.dataset.specialCardTemplate = 'true';
  card.innerHTML = copyFor(style);
  image.replaceWith(card);
}

function applyTemplates() {
  for (const [name, style] of Object.entries(CARD_STYLES)) {
    document.querySelectorAll(`.card .card-art[alt="${name}"]`).forEach((image) => mountHand(image, style));
    document.querySelectorAll(`.detail-card-art[alt="${name}"]`).forEach((image) => mountDetail(image, style));
    document.querySelectorAll(`#table-card img[alt*="${name}"]`).forEach((image) => mountTable(image, style));
  }
  document.querySelectorAll('#stronghold-overlay img[src$="miracle-19.png"]').forEach(mountReaction);
}

applyTemplates();
new MutationObserver(applyTemplates).observe(document.body, { childList: true, subtree: true });

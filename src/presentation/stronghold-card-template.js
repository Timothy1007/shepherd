import { CARD_DEFINITIONS } from '../game/cards.js';

const NEW_LIFE = '新生';
const NEW_LIFE_REVIEW_IDS = new Set([
  'miracle-01', 'miracle-02', 'miracle-03',
  'miracle-18', 'miracle-19', 'miracle-20',
]);
const CARD_STYLES = Object.freeze(Object.fromEntries(
  CARD_DEFINITIONS
    .filter(definition => NEW_LIFE_REVIEW_IDS.has(definition.definitionId))
    .map(definition => {
      return [definition.name, Object.freeze({
        definition,
        kind: 'miracle',
        background: 'assets/miracles/miracle.png',
      })];
    })
));

const icon = (kind) => {
  if (kind === 'disaster') return '<svg class="card-template-icon disaster-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M3 5H21L12 21Z"/></svg>';
  if (kind === 'fire') return '<svg class="card-template-icon fire-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M13.2 2.5c.5 4-2.2 5.1-3.5 7.7-.9-1-1.3-2.3-1-3.8-2.8 2.2-4.6 5.2-4.6 8.4 0 4.2 3.4 7.2 7.8 7.2s7.8-3.1 7.8-7.4c0-4.2-2.2-8.5-6.4-12.1ZM12 19.1c-1.8 0-3.1-1.2-3.1-3.1 0-1.8 1.3-3.2 3.4-5.3.1 1.4.9 2.1 1.7 2.7.9.8 1.3 1.6 1.3 2.6 0 1.9-1.3 3.1-3.3 3.1Z"/></svg>';
  if (kind === 'shield') return '<svg class="card-template-icon shield-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3 20 6v5.3c0 4.7-3.1 7.9-8 10.1-4.9-2.2-8-5.4-8-10.1V6l8-3Z"/><path d="m6 18 12-12"/></svg>';
  return '<svg class="card-template-icon card-count-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M7 18H4a1 1 0 0 1-1-1V3a1 1 0 0 1 1-1h11a1 1 0 0 1 1 1v2"/><rect x="7" y="6" width="13" height="16" rx="1.5"/></svg>';
};

function miracleCopy() {
  return `<div class="stronghold-card-copy" aria-hidden="true">
    <h3>拆毀堅固營壘</h3>
    <div class="stronghold-timing">應對</div>
    <div class="stronghold-rule-region"><div class="stronghold-rule-lines">當你即將受到具有<span class="stronghold-count">2</span>項及以上效果的<span class="stronghold-term">${icon('disaster')}<b>災難</b></span>時，你可以打出此牌：選擇其中<span class="stronghold-count">1</span>項效果，使其對你<span class="stronghold-term">${icon('shield')}<b>無效</b></span>；其餘效果正常結算。結算後，你棄置<span class="stronghold-count">1${icon('card')}</span>，然後抽<span class="stronghold-ending"><span class="stronghold-count">1${icon('card')}</span>。</span></div></div>
  </div>`;
}

function disasterCopy() {
  return `<div class="ashes-card-copy" aria-hidden="true">
    <h3>灰與燼</h3>
    <div class="ashes-tag">群體</div>
    <div class="ashes-rule-region"><div class="ashes-rule-lines">每名玩家減少 <span class="ashes-number">4</span>${icon('fire')}<b>火種</b>。直到本輪結束，所有玩家因神蹟效果獲得的${icon('fire')}<b>火種</b> - <span class="ashes-number">2</span>，最低為 <span class="ashes-number">0</span>。</div></div>
  </div>`;
}

function escapeHtml(value) {
  return String(value).replace(/[&<>"]/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[character]));
}

function genericTag(definition, category) {
  return definition.tags.filter(tag => tag !== category).join(' · ');
}

const cardCount = (amount, ending = '') => `<span class="stronghold-count">${amount}${icon('card')}${ending}</span>`;
const fireWord = () => `<span class="card-inline-term">${icon('fire')}<b>火種</b></span>`;
const disasterWord = () => `<span class="stronghold-term">${icon('disaster')}<b>災難</b></span>`;

function newbornEffect(definitionId, fallback) {
  const effects = {
    'miracle-01': { layout: 'layout-brief', html: `改變<b>出牌方向</b>，然後抽${cardCount(1, '。')}` },
    'miracle-02': { layout: 'layout-standard', html: `將此牌置於你的<b>效果區</b>。你下一張成功打出的物資牌數字 <b>+3</b>，且可超過原本的數字上限。成功打出物資牌後，<span class="keep-end">棄置此牌。</span>` },
    'miracle-03': { layout: 'layout-standard', html: `將此牌置於你的<b>效果區</b>。你下一張成功打出的物資牌<b>無視出牌限制</b>。成功打出物資牌後，<span class="keep-end">棄置此牌。</span>` },
    'miracle-18': { layout: 'layout-long', html: `當你即將因${disasterWord()}效果失去${fireWord()}時，你可以打出此牌：正常失去${fireWord()}後，你每實際失去 <b>3</b>${fireWord()}，抽${cardCount(1)}，最多抽${cardCount(2, '。')}` },
    'miracle-20': { layout: 'layout-long', html: `當你的手牌數量或${fireWord()}即將因其他牌或人物效果從 <b>1</b> 以上降至 <b>0</b> 時，你可以打出此牌：選擇其中一項，使其保留 <b>1</b>。此效果不能用於支付代價、<b>整備</b>或其他由你主動選擇的<span class="keep-end">消耗。</span>` },
  };
  return effects[definitionId] ?? { layout: '', html: escapeHtml(fallback) };
}

function genericCopy(style) {
  const definition = style.definition;
  const category = style.kind === 'miracle' ? NEW_LIFE : EXTINGUISH;
  const tag = genericTag(definition, category);
  const effect = newbornEffect(definition.definitionId, definition.text);
  const density = definition.text.length > 130 ? 'ultra-dense' : definition.text.length > 82 ? 'dense' : '';
  if (style.kind === 'miracle') {
    return `<div class="stronghold-card-copy generic-card-copy ${density} ${effect.layout}${tag ? '' : ' no-subtag'}" aria-hidden="true">
      <h3>${escapeHtml(definition.name)}</h3>
      <div class="stronghold-timing">${escapeHtml(tag)}</div>
      <div class="stronghold-rule-region"><div class="stronghold-rule-lines">${effect.html}</div></div>
    </div>`;
  }
  return `<div class="ashes-card-copy generic-card-copy ${density}" aria-hidden="true">
    <h3>${escapeHtml(definition.name)}</h3>
    <div class="ashes-tag">${escapeHtml(tag)}</div>
    <div class="ashes-rule-region"><div class="ashes-rule-lines">${escapeHtml(definition.text)}</div></div>
  </div>`;
}

function copyFor(style) {
  if (style.definition.name === '拆毀堅固營壘') return miracleCopy();
  if (style.definition.name === '灰與燼') return disasterCopy();
  return genericCopy(style);
}
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

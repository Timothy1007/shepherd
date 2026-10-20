import { CARD_DEFINITIONS } from '../game/cards.js';

const NEW_LIFE = '新生';
const FACE_SCOPE_TAGS = new Set(['應對', '單體', '多體', '群體']);
const FACE_TAG_FILES = Object.freeze({
  '新生': 'Rebirth', '流轉': 'Flux', '洞察': 'Insight', '庇護': 'Sanctuary', '共鳴': 'Resonance', '火種': 'Ember', '使命': 'Mission',
  '重構': 'Reforge', '掠奪': 'Plunder', '熄滅': 'Extinction', '蔓延': 'Spread', '崩解': 'Collapse',
});
function primaryFaceTag(definition) { return definition.tags.find(tag => !FACE_SCOPE_TAGS.has(tag) && FACE_TAG_FILES[tag]); }
function cardFaceAsset(definition) {
  const tag = primaryFaceTag(definition), file = FACE_TAG_FILES[tag];
  if (!file) return definition.image;
  const response = definition.tags.includes('應對') ? '-Response' : '';
  return definition.kind === 'miracle' ? `assets/miracles/miracal-${file}${response}.png` : `assets/disasters/disater-${file}${response}.png`;
}
const REVIEWED_FACE_IDS = new Set([
  // 已由玩家看過的卡面，保留既有新版外觀，但不再放進本次 QA 手牌。
  'miracle-01', 'miracle-02', 'miracle-03', 'miracle-18', 'miracle-19', 'miracle-20', 'disaster-06', 'disaster-07',
]);
const IMPLEMENTED_FACE_IDS = new Set([
  ...REVIEWED_FACE_IDS,
  // 本次只補齊這些已實裝、尚未換新卡背與內文的牌。
  'miracle-04', 'miracle-05', 'miracle-06', 'miracle-07', 'miracle-08',
  'miracle-09', 'miracle-10', 'miracle-13', 'miracle-16', 'miracle-17', 'miracle-22',
  'disaster-01', 'disaster-02', 'disaster-08', 'disaster-09',
]);
const CARD_STYLES = Object.freeze(Object.fromEntries(
  CARD_DEFINITIONS
    .filter(definition => IMPLEMENTED_FACE_IDS.has(definition.definitionId))
    .map(definition => {
      return [definition.name, Object.freeze({
        definition,
        kind: definition.kind,
        tagFacePreview: true,
        background: cardFaceAsset(definition),
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
    <div class="stronghold-tag-markers"><span class="stronghold-tag-marker is-primary tag-新生">${TAG_ICON['新生']}</span><span class="stronghold-tag-marker is-secondary tag-新生">${TAG_ICON['新生']}</span></div>
    <h3>拆毀堅固營壘</h3>
    <div class="stronghold-timing">應對</div>
    <div class="stronghold-rule-region"><div class="stronghold-rule-lines">當你即將受到具有<span class="stronghold-count">2</span>項及以上效果的<span class="stronghold-term">${icon('disaster')}<b>災難</b></span>時，你可以打出此牌：選擇其中<span class="stronghold-count">1</span>項效果，使其對你<span class="stronghold-term">${icon('shield')}<b>無效</b></span>；其餘效果正常結算。結算後，你棄置<span class="stronghold-count">1${icon('card')}</span>，然後抽<span class="stronghold-ending"><span class="stronghold-count">1${icon('card')}</span>。</span></div></div>
  </div>`;
}

function disasterCopy() {
  return `<div class="ashes-card-copy" aria-hidden="true">
    <div class="ashes-tag-markers"><span class="ashes-tag-marker is-primary tag-熄滅">${TAG_ICON['熄滅']}</span><span class="ashes-tag-marker is-secondary tag-熄滅">${TAG_ICON['熄滅']}</span></div>
    <h3>灰與燼</h3>
    <div class="ashes-tag">群體</div>
    <div class="ashes-rule-region"><div class="ashes-rule-lines">每名玩家減少 <span class="ashes-number">4</span>${icon('fire')}<b>火種</b>。直到本輪結束，所有玩家因神蹟效果獲得的${icon('fire')}<b>火種</b> - <span class="ashes-number">2</span>，最低為 <span class="ashes-number">0</span>。</div></div>
  </div>`;
}

function escapeHtml(value) {
  return String(value).replace(/[&<>"]/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[character]));
}

const RESPONSE = '應對';
const TAG_PRIORITY = Object.freeze({
  // 三標籤卡：使命在這張牌中是結果，而非主要操作，因此暫不印在卡面。
  '恰如飛鳥經過': Object.freeze(['共鳴', '流轉']),
  // 雙標籤卡亦在此明示主、次關係，避免資料陣列順序成為視覺規則。
  '於水中重生': Object.freeze(['流轉', '新生']),
  '行向水深之處': Object.freeze(['洞察', '新生']),
  '方舟之外': Object.freeze(['掠奪', '群體']),
  '瘟疫': Object.freeze(['蔓延', '單體']),
  '蟲災': Object.freeze(['重構', '單體']),
});

const TAG_ICON = Object.freeze({
  '新生': '<svg viewBox="0 0 24 24" aria-hidden="true"><path class="tag-fill" d="M10 2.2a2.8 2.8 0 0 1 4 0l7.8 7.8a2.8 2.8 0 0 1 0 4L14 21.8a2.8 2.8 0 0 1-4 0L2.2 14a2.8 2.8 0 0 1 0-4Z"/></svg>',
  '熄滅': '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M10 2.2a2.8 2.8 0 0 1 4 0l7.8 7.8a2.8 2.8 0 0 1 0 4L14 21.8a2.8 2.8 0 0 1-4 0L2.2 14a2.8 2.8 0 0 1 0-4Z"/><path class="tag-cutout" d="m12 7 5 5-5 5-5-5Z"/></svg>',
  '流轉': '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 8.5A7.5 7.5 0 0 1 18.2 6M18.2 6v4M18.2 6h-4M18 15.5A7.5 7.5 0 0 1 5.8 18M5.8 18v-4M5.8 18h4"/></svg>',
  '洞察': '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M2.5 12s3.4-5.5 9.5-5.5 9.5 5.5 9.5 5.5-3.4 5.5-9.5 5.5S2.5 12 2.5 12Z"/><circle cx="12" cy="12" r="2.6"/><path d="M12 2.2v1.6M4.3 5.4l1.1 1.1M19.7 5.4l-1.1 1.1"/></svg>',
  '庇護': '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M11.4 18.8C6.2 18.4 3.8 14.4 3.5 8.8c3.1.2 5.9 1.1 7.9 4.3M12.6 18.8c5.2-.4 7.6-4.4 7.9-10-3.1.2-5.9 1.1-7.9 4.3M12 9v10"/></svg>',
  '共鳴': '<svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="8.5" cy="12" r="5.4"/><circle cx="15.5" cy="12" r="5.4"/></svg>',
  '火種': '<svg viewBox="0 0 24 24" aria-hidden="true"><path class="tag-fill" d="M13.2 2.5c.5 4-2.2 5.1-3.5 7.7-.9-1-1.3-2.3-1-3.8-2.8 2.2-4.6 5.2-4.6 8.4 0 4.2 3.4 7.2 7.8 7.2s7.8-3.1 7.8-7.4c0-4.2-2.2-8.5-6.4-12.1ZM12 19.1c-1.8 0-3.1-1.2-3.1-3.1 0-1.8 1.3-3.2 3.4-5.3.1 1.4.9 2.1 1.7 2.7.9.8 1.3 1.6 1.3 2.6 0 1.9-1.3 3.1-3.3 3.1Z"/></svg>',
  '使命': '<svg viewBox="0 0 24 24" aria-hidden="true"><path class="tag-fill" d="m12 2.4 2.2 6.1 6.1 2.2-6.1 2.2-2.2 6.7-2.2-6.7-6.1-2.2 6.1-2.2Z"/></svg>',
  '掠奪': '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 5v8.2a5.8 5.8 0 0 0 5.8 5.8H14"/><path d="m8 12 2.1 2.1M10 8.5l2.1 2.1M12.5 6l2.1 2.1M15 7.2v5.1"/><path d="m14 19 3-3m0 0-3-3m3 3H7"/></svg>',
  '重構': '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 8V4h4M12 4h8v8M20 16v4h-4M12 20H4v-8"/><path d="m8 11 2-2m4 6 2-2M7 16a6.2 6.2 0 0 0 10-2M17 8A6.2 6.2 0 0 0 7 10"/></svg>',
  '蔓延': '<svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="6" cy="12" r="2"/><circle cx="17.5" cy="6" r="2"/><circle cx="17.5" cy="18" r="2"/><path d="M7.8 11 15.7 7M7.8 13l7.9 4"/></svg>',
  '崩解': '<svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="8.5"/><path d="m12 3.5-1 5 3 1-3 3 2 3-1 5M4.5 12l4 1-1 3 4-3 3 2 1 4"/></svg>',
  '單體': '<svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="7" r="2.5"/><path d="M7.5 20c.2-4 2-6.2 4.5-6.2s4.3 2.2 4.5 6.2"/></svg>',
  '多體': '<svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="8" cy="8" r="2.2"/><circle cx="16" cy="8" r="2.2"/><path d="M3.8 19c.2-3.6 1.7-5.5 4.2-5.5s4 1.9 4.2 5.5M11.8 19c.2-3.6 1.7-5.5 4.2-5.5s4 1.9 4.2 5.5"/></svg>',
  '群體': '<svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="6" cy="8" r="2"/><circle cx="12" cy="6.5" r="2.3"/><circle cx="18" cy="8" r="2"/><path d="M2.5 19c.2-3.5 1.5-5.2 3.5-5.2s3.3 1.7 3.5 5.2M7.6 19c.2-3.8 1.7-5.7 4.4-5.7s4.2 1.9 4.4 5.7M14.5 19c.2-3.5 1.5-5.2 3.5-5.2s3.3 1.7 3.5 5.2"/></svg>',
});

// 主圖騰用使用者本次提供的原始 SVG；遮罩允許神蹟／災難共用同一圖案而保留不同顏色。
const TAG_FACE_GLYPH = Object.freeze({
  // 洞察：圓潤的實心眼形，中央只留一個小圓孔，避免瞳孔過大。
  '洞察': '<svg viewBox="0 0 100 100" fill="currentColor" fill-rule="evenodd" aria-hidden="true" style="width: 100%; height: 100%;"><path d="M8 50C26 14 74 14 92 50C74 86 26 86 8 50Z M50 40A10 10 0 1 0 50 60A10 10 0 1 0 50 40Z"/></svg>',
  '庇護': '', '蔓延': '', '流轉': '',
  // 重構：外圈放大，中央挖空維持與新生基準相同的絕對視覺尺寸。
  '重構': '<svg viewBox="0 0 100 100" fill="currentColor" fill-rule="evenodd" aria-hidden="true" style="width: 100%; height: 100%;"><path d="M50 8L83 41Q92 50 83 59L59 83Q50 92 41 83L17 59Q8 50 17 41L41 17Q50 8 59 17Z M50 36.4L61.4 47.8Q63.6 50 61.4 52.2L52.2 61.4Q50 63.6 47.8 61.4L38.6 52.2Q36.4 50 38.6 47.8L47.8 38.6Q50 36.4 52.2 38.6Z"/></svg>',
  // 崩解：相連但失衡的圓角方塊，保留「結構正在破裂」而非尖銳碎片的意象。
  '崩解': '<svg viewBox="0 0 100 100" aria-hidden="true"><g fill="none" stroke="currentColor" stroke-width="14" stroke-linecap="round" stroke-linejoin="round"><path d="M31 43H46V66M62 43H79V66M70 78V87"/></g><g fill="currentColor"><rect x="36" y="10" width="24" height="24" rx="7"/><rect x="10" y="31" width="34" height="34" rx="8"/><rect x="36" y="61" width="28" height="28" rx="8"/><rect x="56" y="31" width="31" height="34" rx="8"/><rect x="72" y="61" width="28" height="28" rx="8"/><rect x="61" y="81" width="22" height="19" rx="7"/></g></svg>',
});

function visualTags(definition) {
  const source = TAG_PRIORITY[definition.name] ?? definition.tags.filter(tag => tag !== RESPONSE);
  return source.filter(tag => tag !== RESPONSE && TAG_ICON[tag]).slice(0, 2);
}

function tagMarkers(definition, classPrefix) {
  const tags = visualTags(definition);
  if (!tags.length) return '';
  const placed = tags.length === 1 ? [tags[0], tags[0]] : tags;
  return `<div class="${classPrefix}-tag-markers">${placed.map((tag, index) => `<span class="${classPrefix}-tag-marker ${index === 0 ? 'is-primary' : 'is-secondary'} tag-${tag}" aria-label="${tag}">${TAG_ICON[tag]}</span>`).join('')}</div>`;
}

const cardCount = (amount, ending = '') => `<span class="stronghold-count">${amount}${icon('card')}${ending}</span>`;
const fireWord = () => `<span class="card-inline-term">${icon('fire')}<b>火種</b></span>`;
const disasterWord = () => `<span class="stronghold-term">${icon('disaster')}<b>災難</b></span>`;

function formatRuleText(text) {
  let value = escapeHtml(text).replace('【應對】', '').replace('【負面】', '').trim();
  if (value && !/[。！？]$/.test(value)) value += '。';
  const inserts = [];
  const insert = (html) => {
    const token = `\uE000${String.fromCharCode(65 + inserts.length)}\uE001`;
    inserts.push([token, html]);
    return token;
  };
  // 只在真正抽／棄牌的敘述放牌圖示；其他「牌」不濫用圖示。
  value = value
    .replace(/抽取(等量)的牌/g, (_, amount) => `抽取${amount}${insert(icon('card'))}`)
    .replace(/抽(\d+)張牌/g, (_, amount) => `抽${insert(cardCount(amount))}`)
    .replace(/棄置(\d+)張手牌/g, (_, amount) => `棄置${insert(cardCount(amount))}手牌`)
    .replace(/棄置(\d+)張；/g, (_, amount) => `棄置${insert(cardCount(amount))}；`);
  value = value
    .replace(/免疫/g, '<b>免疫</b>')
    .replace(/選擇(?=：)/g, '<b>選擇</b>')
    .replace(/(\d+)/g, '<b>$1</b>')
    .replace(/使命進度|效果區|牌庫|棄牌區|死亡牌|群羊牌|物資牌/g, '<b>$&</b>')
    .replace(/災難/g, disasterWord())
    .replace(/火種/g, fireWord());
  for (const [token, html] of inserts) value = value.replace(token, html);
  return value;
}

function newbornEffect(definitionId, fallback) {
  const effects = {
    'miracle-01': { layout: 'layout-brief', html: `改變<b>出牌方向</b>，然後抽${cardCount(1, '。')}` },
    'miracle-02': { layout: 'layout-standard', html: `將此牌置於你的<b>效果區</b>。你下一張成功打出的物資牌數字 <b>+3</b>，且可超過原本的數字上限。成功打出物資牌後，<span class="keep-end">棄置此牌。</span>` },
    'miracle-03': { layout: 'layout-standard', html: `將此牌置於你的<b>效果區</b>。你下一張成功打出的物資牌<b>無視出牌限制</b>。成功打出物資牌後，<span class="keep-end">棄置此牌。</span>` },
    'miracle-04': { layout: 'layout-brief', html: `棄置全部手牌，然後抽取等量${icon('card')}。` },
    'miracle-07': { layout: 'layout-deep-insight', html: `<span class="rule-sentence">依序展示<b>牌庫</b>頂的牌，直到展示出<b>3</b>種不同牌型、共展示<b>5</b>張，或展示到<b>死亡牌</b>。</span><span class="rule-sentence">未展示死亡牌時，選擇其中<b>1</b>張加入手牌；其餘以隨機順序置於<b>牌庫</b>底。展示到死亡牌時，依死亡規則處理；已展示的正常牌隨機置於<b>牌庫</b>底，不取得牌。</span>` },
    'miracle-09': { layout: 'layout-redline', html: formatRuleText(fallback.replace('然後你棄置此牌', '然後你棄§置此牌')).replace('§', '<span class="rule-next-line">') + '</span>' },
    'miracle-18': { layout: 'layout-long', html: `當你即將因${disasterWord()}效果失去${fireWord()}時，你可以打出此牌：正常失去${fireWord()}後，你每實際失去 <b>3</b>${fireWord()}，抽${cardCount(1)}，最多抽${cardCount(2, '。')}` },
    'miracle-20': { layout: 'layout-long', html: `當你的手牌數量或${fireWord()}即將因其他牌或人物效果從 <b>1</b> 以上降至 <b>0</b> 時，你可以打出此牌：選擇其中一項，使其保留 <b>1</b>。此效果不能用於支付代價、<b>整備</b>或其他由你主動選擇的<span class="keep-end">消耗。</span>` },
    'miracle-22': { layout: 'layout-standard', html: `選擇<b>1</b>名玩家，棄置其<b>效果區</b>中的<b>1</b>張持續${disasterWord()}牌，或清除其<b>1</b>個<b>負面</b>。<span class="keep-end">成功時，得<b>2</b>${fireWord()}。</span>` },
  };
  if (definitionId === 'miracle-17') {
    const copy = formatRuleText(fallback.replace('，或少棄置', '，或§少棄置'))
      .replace('§', '<span class="rule-next-line">') + '</span>';
    return { layout: 'layout-redline', html: copy };
  }
  return effects[definitionId] ?? { layout: '', html: formatRuleText(fallback) };
}

function genericCopy(style) {
  const definition = style.definition;
  const timing = definition.tags.includes(RESPONSE) ? RESPONSE : '';
  const status = timing || (definition.negative ? '負面' : '');
  const effect = newbornEffect(definition.definitionId, definition.text);
  const density = definition.text.length > 130 ? 'ultra-dense' : definition.text.length > 82 ? 'dense' : '';
  if (style.kind === 'miracle') {
    return `<div class="stronghold-card-copy generic-card-copy ${density} ${effect.layout}${timing ? '' : ' no-subtag'}" aria-hidden="true">
      ${tagMarkers(definition, 'stronghold')}
      <h3>${escapeHtml(definition.name)}</h3>
      ${timing ? `<div class="stronghold-timing">${timing}</div>` : ''}
      <div class="stronghold-rule-region"><div class="stronghold-rule-lines">${effect.html}</div></div>
    </div>`;
  }
  return `<div class="ashes-card-copy generic-card-copy ${density}" aria-hidden="true">
    ${tagMarkers(definition, 'ashes')}
    <h3>${escapeHtml(definition.name)}</h3>
    ${status ? `<div class="ashes-tag">${status}</div>` : ''}
    <div class="ashes-rule-region"><div class="ashes-rule-lines">${formatRuleText(definition.text)}</div></div>
  </div>`;
}

function copyFor(style, { detail = false } = {}) {
  if (style.tagFacePreview) {
    const tags = visualTags(style.definition), primary = tags[0] ?? NEW_LIFE, shape = FACE_TAG_FILES[primary]?.toLowerCase() ?? 'rebirth';
    return `<div class="tag-only-card-copy" aria-label="${primary}"><span class="tag-face-symbol tag-face-symbol--${shape}" aria-hidden="true">${TAG_FACE_GLYPH[primary] ?? ''}</span></div>`;
  }
  if (style.definition.name === '拆毀堅固營壘') return miracleCopy();
  if (style.definition.name === '灰與燼') return disasterCopy();
  return genericCopy(style);
}
function templateClass(style) { return style.kind === 'miracle' ? 'stronghold-template-card' : 'ashes-template-card'; }

function mountHand(image, style) {
  const card = image.closest('.card');
  // 洞察由主手牌渲染器直接插入圖騰，不再被舊模板的非同步掛載流程覆蓋。
  if (card?.querySelector('[data-runtime-face-glyph]')) return;
  if (!card || card.dataset.specialCardTemplate === 'true') return;
  card.dataset.specialCardTemplate = 'true';
  card.classList.add(templateClass(style));
  card.style.setProperty('--card-template-art', `url("${style.background}")`);
  image.src = style.background;
  image.insertAdjacentHTML('afterend', copyFor(style));
}

function mountDetail(image, style) {
  if (image.dataset.specialCardTemplate === 'true') return;
  const card = document.createElement('div');
  card.className = `detail-card-art ${templateClass(style)} detail-special-template${style.tagFacePreview ? ' tag-face-detail-preview' : ''}${style.tagFacePreview && style.definition.tags.includes(RESPONSE) ? ' tag-face-detail-response' : ''}`;
  card.dataset.specialCardTemplate = 'true';
  card.style.setProperty('--card-template-art', `url("${style.background}")`);
  card.innerHTML = copyFor(style, { detail: true });
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
  card.style.setProperty('--card-template-art', `url("${style.background}")`);
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

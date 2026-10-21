import { CARD_DEFINITIONS } from '../game/cards.js';

const NEW_LIFE = '新生';
const FACE_SCOPE_TAGS = new Set(['應對', '單體', '多體', '群體']);
const FACE_TAG_FILES = Object.freeze({
  '新生': 'Rebirth', '流轉': '<svg viewBox="0 0 100 100" fill="currentColor" aria-hidden="true"><path d="M98.58 66.25L96.64 59.92L93.67 56.20L89.41 53.99L84.88 54.13L80.62 57.02L75.19 66.94L69.12 73.55L62.02 77.41L53.23 78.51L45.99 76.72L41.86 74.24L38.11 70.52L34.37 62.81L34.11 56.06L32.04 59.09L30.88 64.46L31.14 70.52L32.56 75.76L35.66 81.68L39.66 86.91L45.09 91.87L50.65 95.32L62.14 98.48L69.38 98.35L74.94 97.11L82.56 93.66L87.34 90.22L91.73 85.67L95.61 79.75L98.19 72.73Z"/><path d="M56.33 35.54L51.42 32.51L46.12 30.72L41.09 30.03L33.33 30.44L25.97 32.64L18.86 36.09L9.69 44.08L5.94 49.45L3.10 55.79L1.42 63.22L1.29 69.83L3.88 80.99L6.59 86.09L10.21 90.22L14.34 92.84L19.25 93.80L23.64 92.84L28.55 88.98L30.49 84.99L30.75 81.82L26.36 69.42L25.58 64.60L26.10 58.40L27.91 53.31L34.63 44.63L43.80 38.84L48.45 37.60L53.23 37.60L60.08 40.63Z"/><path d="M42.12 2.34L36.95 4.82L32.82 8.68L30.36 14.05L30.49 18.87L32.30 22.45L35.92 25.21L48.45 26.31L57.62 29.06L62.40 32.37L66.80 37.88L69.25 45.04L69.51 52.75L67.96 58.26L64.99 63.77L61.11 68.04L57.62 70.25L62.02 69.83L65.89 68.04L70.28 64.60L74.94 58.95L78.42 52.34L81.40 39.81L81.27 33.20L79.97 27.00L74.29 14.88L70.41 10.33L64.86 5.92L60.21 3.58L52.97 1.65L47.42 1.38Z"/></svg>', '洞察': 'Insight', '庇護': 'Sanctuary', '共鳴': 'Resonance', '火種': 'Ember', '使命': 'Mission',
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
  '洞察': '<svg viewBox="0 0 100 100" fill="currentColor" aria-hidden="true"><path d="M50 16C53 16 55 18 57 21L86 70C89 75 86 81 80 81H20C14 81 11 75 14 70L43 21C45 18 47 16 50 16Z"/></svg>',
  '庇護': '<svg viewBox="0 0 100 100" fill="currentColor" aria-hidden="true"><path fill-rule="evenodd" d="M50 16C70 16 86 34 86 58V74C86 78 83 81 79 81H21C17 81 14 78 14 74V58C14 34 30 16 50 16ZM50 40C60 40 67 49 67 61V77C67 79.2 65.2 81 63 81H37C34.8 81 33 79.2 33 77V61C33 49 40 40 50 40Z"/></svg>',
  '共鳴': '<svg viewBox="0 0 100 100" fill="currentColor" aria-hidden="true"><path d="M 62.73 16.16 C 59.02 15.43 56.27 24.04 51.26 24.88 C 46.25 25.72 36.35 20.60 32.68 21.21 C 29.01 21.82 28.85 25.60 29.24 28.55 C 29.62 31.49 31.65 39.10 34.97 38.87 C 38.30 38.64 45.76 28.97 49.20 27.17 C 52.64 25.37 53.48 26.87 55.62 28.09 C 57.76 29.31 59.33 33.79 62.04 34.51 C 64.76 35.24 69.96 31.68 71.91 32.45 C 73.86 33.21 74.66 35.66 73.75 39.10 C 72.83 42.54 72.52 49.77 66.40 53.10 C 60.29 56.42 44.38 59.75 37.04 59.06 C 29.70 58.37 26.75 50.19 22.35 48.97 C 17.96 47.74 11.46 48.89 10.65 51.72 C 9.85 54.55 15.85 60.36 17.54 65.95 C 19.22 71.53 19.03 82.12 20.75 85.22 C 22.47 88.31 26.33 86.59 27.86 84.53 C 29.39 82.46 28.93 75.28 29.93 72.83 C 30.92 70.38 31.11 66.44 33.83 69.85 C 36.54 73.25 42.01 88.96 46.21 93.25 C 50.42 97.53 55.09 100.32 59.06 95.54 C 63.04 90.76 67.78 70.00 70.07 64.57 C 72.37 59.14 71.15 62.43 72.83 62.96 C 74.51 63.50 77.91 67.28 80.17 67.78 C 82.43 68.28 85.56 68.62 86.36 65.95 C 87.17 63.27 84.49 56.58 84.99 51.72 C 85.48 46.86 91.26 40.56 89.35 36.81 C 87.43 33.06 77.95 32.68 73.52 29.24 C 69.08 25.80 66.44 16.89 62.73 16.16 Z"/><path d="M 48.05 34.97 C 47.02 35.13 46.41 35.35 45.53 35.89 C 44.65 36.43 43.50 37.27 42.77 38.18 C 42.05 39.10 41.47 40.17 41.17 41.40 C 40.86 42.62 40.78 44.34 40.94 45.53 C 41.09 46.71 41.47 47.55 42.08 48.51 C 42.70 49.46 43.58 50.54 44.61 51.26 C 45.64 51.99 47.09 52.60 48.28 52.87 C 49.46 53.14 50.61 53.10 51.72 52.87 C 52.83 52.64 53.94 52.18 54.93 51.49 C 55.93 50.80 57.04 49.66 57.69 48.74 C 58.34 47.82 58.64 47.13 58.83 45.99 C 59.02 44.84 59.10 43.12 58.83 41.86 C 58.57 40.59 57.92 39.37 57.23 38.41 C 56.54 37.46 55.62 36.69 54.70 36.12 C 53.79 35.55 52.83 35.16 51.72 34.97 C 50.61 34.78 49.08 34.82 48.05 34.97 Z"/><path d="M 15.24 28.09 C 13.98 28.36 13.29 28.89 12.49 29.70 C 11.69 30.50 10.81 31.68 10.42 32.91 C 10.04 34.13 10.00 35.85 10.19 37.04 C 10.39 38.22 11.11 39.29 11.57 40.02 C 12.03 40.75 12.37 40.98 12.95 41.40 C 13.52 41.82 14.36 42.28 15.01 42.54 C 15.66 42.81 15.93 43.00 16.85 43.00 C 17.77 43.00 19.64 42.77 20.52 42.54 C 21.40 42.31 21.55 42.08 22.12 41.63 C 22.70 41.17 23.50 40.40 23.96 39.79 C 24.42 39.18 24.65 38.72 24.88 37.96 C 25.11 37.19 25.37 36.16 25.34 35.20 C 25.30 34.25 25.07 33.14 24.65 32.22 C 24.23 31.30 23.58 30.38 22.81 29.70 C 22.05 29.01 21.32 28.36 20.06 28.09 C 18.80 27.82 16.50 27.82 15.24 28.09 Z"/><path d="M 79.25 16.39 C 78.37 16.62 77.76 17.12 77.19 17.54 C 76.61 17.96 76.23 18.22 75.81 18.91 C 75.39 19.60 74.82 20.67 74.66 21.67 C 74.51 22.66 74.70 24.04 74.89 24.88 C 75.08 25.72 75.35 26.10 75.81 26.71 C 76.27 27.33 76.96 28.09 77.65 28.55 C 78.33 29.01 79.10 29.31 79.94 29.47 C 80.78 29.62 81.85 29.62 82.69 29.47 C 83.53 29.31 84.26 29.05 84.99 28.55 C 85.71 28.05 86.56 27.21 87.05 26.48 C 87.55 25.76 87.82 25.03 87.97 24.19 C 88.12 23.35 88.08 22.20 87.97 21.44 C 87.86 20.67 87.74 20.29 87.28 19.60 C 86.82 18.91 86.02 17.88 85.22 17.31 C 84.41 16.73 83.46 16.31 82.46 16.16 C 81.47 16.01 80.13 16.16 79.25 16.39 Z"/><path d="M 60.21 4.00 C 59.48 4.15 58.79 4.42 58.14 4.92 C 57.49 5.41 56.69 6.45 56.31 6.98 C 55.93 7.52 55.93 7.48 55.85 8.13 C 55.77 8.78 55.70 10.08 55.85 10.88 C 56.00 11.69 56.42 12.41 56.77 12.95 C 57.11 13.48 57.42 13.75 57.92 14.09 C 58.41 14.44 59.06 14.82 59.75 15.01 C 60.44 15.20 61.24 15.36 62.04 15.24 C 62.85 15.13 63.92 14.71 64.57 14.32 C 65.22 13.94 65.56 13.48 65.95 12.95 C 66.33 12.41 66.71 11.92 66.86 11.11 C 67.02 10.31 66.98 8.89 66.86 8.13 C 66.75 7.36 66.56 7.06 66.17 6.52 C 65.79 5.99 65.18 5.34 64.57 4.92 C 63.96 4.50 63.23 4.15 62.50 4.00 C 61.78 3.85 60.94 3.85 60.21 4.00 Z"/><path d="M 31.30 9.51 C 30.58 9.70 30.15 10.00 29.70 10.42 C 29.24 10.84 28.82 11.53 28.55 12.03 C 28.28 12.53 28.13 12.76 28.09 13.41 C 28.05 14.06 28.13 15.24 28.32 15.93 C 28.51 16.62 28.78 17.04 29.24 17.54 C 29.70 18.03 30.65 18.65 31.07 18.91 C 31.49 19.18 31.23 19.10 31.76 19.14 C 32.30 19.18 33.60 19.26 34.28 19.14 C 34.97 19.03 35.39 18.80 35.89 18.45 C 36.39 18.11 36.92 17.57 37.27 17.08 C 37.61 16.58 37.84 16.16 37.96 15.47 C 38.07 14.78 37.99 13.48 37.96 12.95 C 37.92 12.41 37.99 12.68 37.73 12.26 C 37.46 11.84 36.96 10.92 36.35 10.42 C 35.74 9.93 34.90 9.43 34.05 9.28 C 33.21 9.12 32.03 9.32 31.30 9.51 Z"/></svg>',
  '火種': '<svg viewBox="0 0 24 24" aria-hidden="true"><path class="tag-fill" d="M13.2 2.5c.5 4-2.2 5.1-3.5 7.7-.9-1-1.3-2.3-1-3.8-2.8 2.2-4.6 5.2-4.6 8.4 0 4.2 3.4 7.2 7.8 7.2s7.8-3.1 7.8-7.4c0-4.2-2.2-8.5-6.4-12.1ZM12 19.1c-1.8 0-3.1-1.2-3.1-3.1 0-1.8 1.3-3.2 3.4-5.3.1 1.4.9 2.1 1.7 2.7.9.8 1.3 1.6 1.3 2.6 0 1.9-1.3 3.1-3.3 3.1Z"/></svg>',
  '使命': '<svg viewBox="0 0 24 24" aria-hidden="true"><path class="tag-fill" d="m12 2.4 2.2 6.1 6.1 2.2-6.1 2.2-2.2 6.7-2.2-6.7-6.1-2.2 6.1-2.2Z"/></svg>',
  '掠奪': '<svg viewBox="0 0 100 100" fill="currentColor" aria-hidden="true"><g stroke="currentColor" stroke-width="1.1" stroke-linejoin="round" stroke-linecap="round"><path d="M19.55 11.85L19.55 18.01L19.99 21.09L20.43 22.86L21.54 25.94L22.31 27.48L23.63 29.57L24.95 31.23L28.03 34.20L31.45 36.62L37.61 39.92L43.56 42.57L48.62 45.10L51.27 46.75L53.25 48.29L55.78 51.05L57.32 53.91L58.09 56.88L58.09 60.74L60.52 59.08L63.05 56.88L64.70 55.12L66.57 52.70L68.00 50.39L69.10 48.18L70.76 43.23L71.42 38.93L71.42 33.98L71.20 32.11L70.43 28.47L69.55 25.72L68.66 23.63L67.01 20.54L64.92 17.57L63.60 16.03L61.95 14.38L59.08 12.07L57.21 10.85L54.68 9.53L51.16 8.21L48.62 7.55L45.87 7.11L43.89 7.00L44.66 7.22L46.86 8.32L48.95 9.64L51.38 11.51L54.13 14.16L56.22 16.69L57.65 18.89L58.86 21.31L59.52 23.41L59.41 23.52L57.65 21.54L56.55 20.87L52.70 20.87L47.19 21.65L43.67 22.53L40.36 23.63L38.27 24.51L34.75 26.38L34.64 26.60L36.73 28.25L36.51 28.36L33.21 27.37L30.67 26.16L28.58 24.84L26.38 23.08L24.62 21.31L22.31 18.23L20.76 15.26Z"/><path d="M41.25 43.45L41.03 44.66L41.03 45.98L41.58 48.40L42.68 50.39L43.56 51.38L44.66 52.26L46.42 53.14L48.07 53.58L51.05 53.47L52.70 52.92L53.47 52.48L52.92 51.49L52.15 50.72L51.27 50.17L48.95 50.17L47.52 49.94L46.42 49.61L44.44 48.51L43.01 47.19L42.02 45.76L41.47 44.55Z"/><path d="M26.49 37.61L24.07 42.57L22.42 47.74L21.65 52.59L21.54 56.66L22.09 61.73L23.30 66.46L24.62 69.88L26.16 72.96L27.70 75.49L30.90 79.68L34.64 83.42L37.83 85.95L39.81 87.27L43.78 89.48L46.86 90.80L49.83 91.79L51.93 92.34L55.67 93.00L57.87 92.78L59.64 92.12L61.40 91.13L64.81 88.49L66.79 86.39L67.67 85.07L62.72 84.85L56.22 83.97L50.50 82.54L45.21 80.56L40.25 77.91L37.06 75.60L33.87 72.41L32.11 69.77L32.22 69.66L34.20 71.97L35.52 73.18L38.05 75.16L41.80 77.47L45.98 79.46L50.28 81.00L54.46 82.10L58.64 82.87L64.37 83.42L67.78 83.42L68.66 83.20L70.32 82.43L71.53 81.55L73.18 79.90L74.94 77.47L76.48 74.39L77.14 71.86L71.42 73.18L64.59 74.06L57.98 74.17L54.90 73.95L51.05 73.40L47.19 72.52L44.11 71.53L39.92 69.66L37.17 68.00L35.52 66.79L33.87 65.36L31.78 63.16L32.00 63.05L34.86 65.47L38.60 67.78L42.13 69.44L47.41 71.20L51.49 72.08L55.89 72.63L61.62 72.74L65.69 72.41L69.77 71.75L74.06 70.65L76.37 69.77L77.80 68.44L79.02 66.46L79.68 64.70L80.23 62.50L80.56 59.64L80.56 57.76L80.34 56.55L75.93 59.19L70.87 61.51L66.57 62.94L61.40 64.04L58.42 64.37L51.60 64.26L47.63 63.60L43.12 62.28L39.59 60.74L35.63 58.31L32.99 56.11L31.67 54.79L29.79 52.48L28.69 50.83L27.48 48.40L26.49 45.32L26.05 42.02L26.16 39.26Z"/></g></svg>',
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
  // Non-modal card inspector is owned exclusively by app.js runtime rendering.
  // Never let the legacy template observer replace or append content inside it,
  // otherwise the inspector can end up with two glyph renderers.
  if (image.closest('#card-inspector')) return;
  if (image.dataset.runtimeInspectorFace === 'true') return;
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
    document.querySelectorAll(`.detail-card-art[alt="${name}"]:not(#card-inspector .detail-card-art)`).forEach((image) => mountDetail(image, style));
    document.querySelectorAll(`#table-card img[alt*="${name}"]`).forEach((image) => mountTable(image, style));
  }
  document.querySelectorAll('#stronghold-overlay img[src$="miracle-19.png"]').forEach(mountReaction);
}

applyTemplates();
new MutationObserver(applyTemplates).observe(document.body, { childList: true, subtree: true });

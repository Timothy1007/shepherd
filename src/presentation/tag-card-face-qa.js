import { CARD_DEFINITIONS } from '../game/cards.js';
import { getNormalActions } from '../game/game.js';

const query = new URLSearchParams(globalThis.location?.search ?? '');
const MODE = query.get('qa');
const TAG_GLYPH_CARDS = Object.freeze({
  Rebirth: 'miracle-01', Flux: 'miracle-04', Insight: 'miracle-06', Sanctuary: 'miracle-22',
  Resonance: 'miracle-10', Ember: 'miracle-05', Mission: 'miracle-09', Reforge: 'disaster-06',
  Plunder: 'disaster-01', Extinction: 'disaster-07', Spread: 'disaster-09', Collapse: 'disaster-15',
});
const IMPLEMENTED_TAG_GLYPH_KEYS = Object.freeze(['Rebirth', 'Flux', 'Insight', 'Sanctuary', 'Resonance', 'Ember', 'Mission', 'Reforge', 'Plunder', 'Extinction', 'Spread', 'Collapse']);
const TAG_GLYPH_LABELS = Object.freeze({Rebirth:'新生',Flux:'流轉',Insight:'洞察',Sanctuary:'庇護',Resonance:'共鳴',Ember:'火種',Mission:'使命',Reforge:'重構',Plunder:'掠奪',Extinction:'熄滅',Spread:'蔓延',Collapse:'崩解'});
const GLYPH_KEY = query.get('tag');
const ART_BATCH_SIZE = 6;
const ART_BATCHES = ['miracle', 'disaster'].flatMap(kind => {
  const definitions = CARD_DEFINITIONS.filter(d => d.kind === kind)
    .sort((a, b) => Number(a.definitionId.split('-')[1]) - Number(b.definitionId.split('-')[1]));
  const batches = [];
  for (let i = 0; i < definitions.length; i += ART_BATCH_SIZE) batches.push(definitions.slice(i, i + ART_BATCH_SIZE));
  return batches;
});
const ART_BATCH_COUNT = ART_BATCHES.length;
const requestedBatch = Number(query.get('batch') ?? 1);
const ART_BATCH = Number.isInteger(requestedBatch) && requestedBatch >= 1 && requestedBatch <= ART_BATCH_COUNT ? requestedBatch : 1;
const ART_BATCH_DEFINITIONS = ART_BATCHES[ART_BATCH - 1] ?? [];

const ACTIVE = MODE === 'art-batch' || MODE === 'tag-card-faces' || MODE === 'tag-glyphs' || MODE === 'rebirth-card-face' || MODE === 'flux-card-face' || MODE === 'insight-card-face' || MODE === 'sanctuary-card-face' || MODE === 'resonance-card-face' || MODE === 'plunder-card-face' || (MODE === 'tag-glyph' && Boolean(TAG_GLYPH_CARDS[GLYPH_KEY]));
const REVIEW_IDS = new Set(MODE === 'art-batch' ? ART_BATCH_DEFINITIONS.map(definition => definition.definitionId) : MODE === 'tag-glyphs' ? IMPLEMENTED_TAG_GLYPH_KEYS.map(key => TAG_GLYPH_CARDS[key]) : MODE === 'flux-card-face' ? ['miracle-04','miracle-05'] : MODE === 'tag-glyph' ? [
  TAG_GLYPH_CARDS[GLYPH_KEY],
] : MODE === 'insight-card-face' ? [
  // 洞察圖騰測試：三張洞察神蹟，直接確認眼睛卡面。
  'miracle-06', 'miracle-07', 'miracle-08',
] : MODE === 'sanctuary-card-face' ? [
  // 庇護主標籤目前僅《雨幕之下》。
  'miracle-22',
] : MODE === 'resonance-card-face' ? [
  // 共鳴主標籤：兩張神蹟。
  'miracle-09', 'miracle-10',
] : MODE === 'plunder-card-face' ? [
  // 掠奪主標籤：兩張災難。
  'disaster-01', 'disaster-02',
] : MODE === 'rebirth-card-face' ? [
  // 新生圖騰測試：四張一般牌、三張應對牌，僅檢視新卡面與中心圖騰。
  'miracle-01', 'miracle-02', 'miracle-03', 'miracle-13',
  'miracle-18', 'miracle-19', 'miracle-20',
] : [
  // 第一批：只發本批要審看的四張，其他未審卡不得混入。
  'miracle-07', 'miracle-09', 'miracle-17', 'miracle-22',
]);

function take(state, definitionId) {
  const zones = [state.deck, state.discardPile, state.playedArea, state.removedForRound];
  for (const player of state.players) zones.push(player.hand, player.effects);
  for (const zone of zones) {
    const index = zone.findIndex(card => card.definitionId === definitionId);
    if (index >= 0) return zone.splice(index, 1)[0];
  }
  return null;
}

function setup() {
  if (!ACTIVE) return true;
  const controller = globalThis.__shepherdController;
  const state = controller?.state;
  const human = state?.players.find(player => player.type === 'human');
  if (!controller || !state || !human) return false;

  controller.cancelTimer?.();
  if (MODE === 'art-batch') {
    document.body.classList.add('qa-art-batch-only');
    if (!document.querySelector('#qa-art-batch-style')) {
      const style = document.createElement('style');
      style.id = 'qa-art-batch-style';
      // QA-only: do not alter actual card art, global hand layout or production glyph behavior.
      style.textContent = 'body.qa-art-batch-only .runtime-tag-glyph,body.qa-art-batch-only .tag-face-symbol,body.qa-art-batch-only .tag-only-card-copy{display:none!important}';
      document.head.append(style);
    }
  }
  state.deck.push(...human.hand.splice(0));
  const definitions = CARD_DEFINITIONS.filter(definition => REVIEW_IDS.has(definition.definitionId));
  const cards = definitions.map(definition => take(state, definition.definitionId)).filter(Boolean);
  human.hand.push(...cards);
  state.currentPlayer = human.playerId;
  state.qaSeed = 'tag-card-faces';
  const message = MODE === 'art-batch'
    ? `底圖批次驗收 ${ART_BATCH}/${ART_BATCH_COUNT}：只發 ${cards.length} 張（${ART_BATCH_DEFINITIONS.map(d => d.name).join('、')}），圖騰僅在本 QA 模式隱藏；請檢查新 PNG 和應對卡背。`
    : MODE === 'flux-card-face'
    ? `流轉卡面測試：已將 ${cards.length} 張主標籤為流轉的神蹟發至你的手牌；兩張共用既有圓端循環圖騰規格。`
    : MODE === 'tag-glyphs'
    ? `12 個標籤圖騰檢視：已將 ${cards.length} 張代表牌發至你的手牌；火種與使命使用測試卡面。`
    : MODE === 'tag-glyph' && GLYPH_KEY === 'Ember'
    ? `火種圖騰單獨測試：使用《如風吹來》預覽火種卡面；正常對局此牌仍以流轉為主標籤。`
    : MODE === 'tag-glyph' && GLYPH_KEY === 'Mission'
    ? `使命圖騰單獨測試：使用《勝利歸於我們》預覽使命卡面；正常對局此牌仍以共鳴為主標籤。`
    : MODE === 'tag-glyph'
    ? `${TAG_GLYPH_LABELS[GLYPH_KEY]}圖騰單獨測試：已將《${cards[0] ? CARD_DEFINITIONS.find(definition => definition.definitionId === cards[0].definitionId)?.name : '測試牌'}》發至你的手牌；本局只保留這一張牌。`
    : MODE === 'insight-card-face'
    ? `洞察卡面測試：已將 ${cards.length} 張洞察神蹟發至你的手牌；中央圖騰為圓潤眼形。`
    : MODE === 'sanctuary-card-face'
    ? `庇護卡面測試：已將《雨幕之下》發至你的手牌；圖騰使用已確認的雙葉庇護符號。`
    : MODE === 'resonance-card-face'
    ? `共鳴卡面測試：已將 ${cards.length} 張主標籤為共鳴的神蹟發至你的手牌；圖騰使用已確認的有機共鳴符號。`
    : MODE === 'plunder-card-face'
    ? `掠奪卡面測試：已將 ${cards.length} 張主標籤為掠奪的災難發至你的手牌；圖騰使用已確認的流線勾形符號。`
    : MODE === 'rebirth-card-face'
    ? `新生卡面測試：已將 ${cards.length} 張新生神蹟發至你的手牌；前四張為一般版，後三張為應對版。`
    : `卡面檢視第一批：已將 ${cards.length} 張卡發至你的手牌；僅含行向水深之處、勝利歸於我們、替罪羊、雨幕之下。`;
  controller.logAi?.(message);
  controller.safeRender?.(getNormalActions(state));
  return true;
}

function startWhenGameIsReady(attempt = 0) {
  if (setup() || attempt >= 120) return;
  globalThis.setTimeout(() => startWhenGameIsReady(attempt + 1), 16);
}

startWhenGameIsReady();

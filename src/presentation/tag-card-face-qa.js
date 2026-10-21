import { CARD_DEFINITIONS } from '../game/cards.js';
import { getNormalActions } from '../game/game.js';

const query = new URLSearchParams(globalThis.location?.search ?? '');
const MODE = query.get('qa');
const TAG_GLYPH_CARDS = Object.freeze({
  Rebirth: 'miracle-01', Flux: 'miracle-04', Insight: 'miracle-06', Sanctuary: 'miracle-22',
  Resonance: 'miracle-10', Ember: 'miracle-05', Mission: 'miracle-09', Reforge: 'disaster-06',
  Plunder: 'disaster-01', Extinction: 'disaster-07', Spread: 'disaster-09', Collapse: 'disaster-15',
});
const TAG_GLYPH_LABELS = Object.freeze({Rebirth:'新生',Flux:'流轉',Insight:'洞察',Sanctuary:'庇護',Resonance:'共鳴',Ember:'火種',Mission:'使命',Reforge:'重構',Plunder:'掠奪',Extinction:'熄滅',Spread:'蔓延',Collapse:'崩解'});
const GLYPH_KEY = query.get('tag');
const ART_BATCH_SIZE = 6;
const ART_DEFINITIONS = CARD_DEFINITIONS.filter(definition =>
  definition.kind === 'miracle' || definition.kind === 'disaster'
).sort((a, b) => {
  if (a.kind !== b.kind) return a.kind === 'miracle' ? -1 : 1;
  return Number(a.definitionId.split('-')[1]) - Number(b.definitionId.split('-')[1]);
});
const ART_BATCH_COUNT = Math.ceil(ART_DEFINITIONS.length / ART_BATCH_SIZE);
const requestedBatch = Number(query.get('batch') ?? 1);
const ART_BATCH = Number.isInteger(requestedBatch) && requestedBatch >= 1 && requestedBatch <= ART_BATCH_COUNT ? requestedBatch : 1;
const ART_BATCH_DEFINITIONS = ART_DEFINITIONS.slice((ART_BATCH - 1) * ART_BATCH_SIZE, ART_BATCH * ART_BATCH_SIZE);

const ACTIVE = MODE === 'art-batch' || MODE === 'tag-card-faces' || MODE === 'rebirth-card-face' || MODE === 'insight-card-face' || (MODE === 'tag-glyph' && Boolean(TAG_GLYPH_CARDS[GLYPH_KEY]));
const REVIEW_IDS = new Set(MODE === 'art-batch' ? ART_BATCH_DEFINITIONS.map(definition => definition.definitionId) : MODE === 'tag-glyph' ? [
  TAG_GLYPH_CARDS[GLYPH_KEY],
] : MODE === 'insight-card-face' ? [
  // 洞察圖騰測試：三張洞察神蹟，直接確認眼睛卡面。
  'miracle-06', 'miracle-07', 'miracle-08',
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
  if (!ACTIVE) return;
  const controller = globalThis.__shepherdController;
  const state = controller?.state;
  const human = state?.players.find(player => player.type === 'human');
  if (!controller || !state || !human) return;

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
    : MODE === 'tag-glyph'
    ? `${TAG_GLYPH_LABELS[GLYPH_KEY]}圖騰單獨測試：已將《${cards[0] ? CARD_DEFINITIONS.find(definition => definition.definitionId === cards[0].definitionId)?.name : '測試牌'}》發至你的手牌；本局只保留這一張牌。`
    : MODE === 'insight-card-face'
    ? `洞察卡面測試：已將 ${cards.length} 張洞察神蹟發至你的手牌；中央圖騰為圓潤眼形。`
    : MODE === 'rebirth-card-face'
    ? `新生卡面測試：已將 ${cards.length} 張新生神蹟發至你的手牌；前四張為一般版，後三張為應對版。`
    : `卡面檢視第一批：已將 ${cards.length} 張卡發至你的手牌；僅含行向水深之處、勝利歸於我們、替罪羊、雨幕之下。`;
  controller.logAi?.(message);
  controller.safeRender?.(getNormalActions(state));
}

setup();

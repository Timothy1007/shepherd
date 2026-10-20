import { CARD_DEFINITIONS } from '../game/cards.js';
import { getNormalActions } from '../game/game.js';

const query = new URLSearchParams(globalThis.location?.search ?? '');
const MODE = query.get('qa');
const ACTIVE = MODE === 'tag-card-faces' || MODE === 'rebirth-card-face' || MODE === 'insight-card-face';
const REVIEW_IDS = new Set(MODE === 'insight-card-face' ? [
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
  state.deck.push(...human.hand.splice(0));
  const definitions = CARD_DEFINITIONS.filter(definition => REVIEW_IDS.has(definition.definitionId));
  const cards = definitions.map(definition => take(state, definition.definitionId)).filter(Boolean);
  human.hand.push(...cards);
  state.currentPlayer = human.playerId;
  state.qaSeed = 'tag-card-faces';
  const message = MODE === 'insight-card-face'
    ? `洞察卡面測試：已將 ${cards.length} 張洞察神蹟發至你的手牌；中央圖騰為圓潤眼形。`
    : MODE === 'rebirth-card-face'
    ? `新生卡面測試：已將 ${cards.length} 張新生神蹟發至你的手牌；前四張為一般版，後三張為應對版。`
    : `卡面檢視第一批：已將 ${cards.length} 張卡發至你的手牌；僅含行向水深之處、勝利歸於我們、替罪羊、雨幕之下。`;
  controller.logAi?.(message);
  controller.safeRender?.(getNormalActions(state));
}

setup();

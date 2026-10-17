import { CARD_DEFINITIONS } from '../game/cards.js';
import { getNormalActions } from '../game/game.js';

const query = new URLSearchParams(globalThis.location?.search ?? '');
const ACTIVE = query.get('qa') === 'tag-card-faces';
const REVIEW_IDS = new Set([
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
  controller.logAi?.(`卡面檢視第一批：已將 ${cards.length} 張卡發至你的手牌；僅含行向水深之處、勝利歸於我們、替罪羊、雨幕之下。`);
  controller.safeRender?.(getNormalActions(state));
}

setup();

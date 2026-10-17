import { CARD_DEFINITIONS } from '../game/cards.js';
import { getNormalActions } from '../game/game.js';

const query = new URLSearchParams(globalThis.location?.search ?? '');
const ACTIVE = query.get('qa') === 'tag-card-faces';
const REVIEW_IDS = new Set([
  'miracle-01', 'miracle-02', 'miracle-03', 'miracle-18', 'miracle-19', 'miracle-20',
  // 標籤圖案測試：雙標籤、應對文字、三標籤取兩個高權重，以及災難範圍。
  'miracle-04', 'miracle-07', 'miracle-10', 'miracle-16', 'miracle-17',
  'disaster-01', 'disaster-09',
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
  controller.logAi?.(`卡面檢視：已將 ${cards.length} 張神蹟／災難發至你的手牌；測試雙標籤、應對文字與三標籤取前兩個圖案。`);
  controller.safeRender?.(getNormalActions(state));
}

setup();

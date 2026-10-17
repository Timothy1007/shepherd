import { CARD_DEFINITIONS } from '../game/cards.js';
import { getNormalActions } from '../game/game.js';

const query = new URLSearchParams(globalThis.location?.search ?? '');
const ACTIVE = query.get('qa') === 'tag-card-faces';
const REVIEW_IDS = new Set([
  // 已測過的新生／熄滅卡不重送；只檢視尚未更新卡背與內文的已實裝卡。
  'miracle-04', 'miracle-05', 'miracle-06', 'miracle-07', 'miracle-08',
  'miracle-09', 'miracle-10', 'miracle-13', 'miracle-16', 'miracle-17', 'miracle-22',
  'disaster-01', 'disaster-02', 'disaster-08', 'disaster-09',
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
  controller.logAi?.(`卡面檢視：已將 ${cards.length} 張已實裝、尚未更新卡背與內文的神蹟／災難發至你的手牌。已測過的卡不重送。`);
  controller.safeRender?.(getNormalActions(state));
}

setup();

import { CARD_DEFINITIONS } from '../game/cards.js';
import { getNormalActions } from '../game/game.js';

const query = new URLSearchParams(globalThis.location?.search ?? '');
const ACTIVE = query.get('qa') === 'tag-card-faces';
const TAGS = new Set(['新生', '熄滅']);

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
  const definitions = CARD_DEFINITIONS.filter(definition => definition.tags?.some(tag => TAGS.has(tag)));
  const cards = definitions.map(definition => take(state, definition.definitionId)).filter(Boolean);
  human.hand.push(...cards);
  state.currentPlayer = human.playerId;
  state.qaSeed = 'tag-card-faces';
  controller.logAi?.(`卡面檢視：已將 ${cards.length} 張帶有「新生」或「熄滅」標籤的牌發至你的手牌。`);
  controller.safeRender?.(getNormalActions(state));
}

setup();

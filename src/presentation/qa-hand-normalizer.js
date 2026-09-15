import { GameController } from '../controller/game-controller.js';

const QA_HAND_SIZE = 7;
const originalStart = GameController.prototype.start;

function normalizeHumanQaHand(controller) {
  const state = controller.state;
  if (!state || state.round !== 1 || state.qaSeed !== 'recovery-preview') return;

  const human = state.players.find(player => player.type === 'human');
  if (!human) return;

  // QA cards replace ordinary starting cards. They must never change the
  // official seven-card opening hand size.
  while (human.hand.length > QA_HAND_SIZE) {
    const removableIndex = human.hand.findLastIndex(card =>
      !['miracle-17', 'miracle-18'].includes(card.definitionId)
    );
    const index = removableIndex >= 0 ? removableIndex : human.hand.length - 1;
    state.deck.push(human.hand.splice(index, 1)[0]);
  }

  while (human.hand.length < QA_HAND_SIZE && state.deck.length) {
    human.hand.push(state.deck.shift());
  }
}

GameController.prototype.start = function startWithNormalizedQaHand(seed) {
  const result = originalStart.call(this, seed);
  normalizeHumanQaHand(this);
  this.safeRender?.();
  return this.state ?? result;
};

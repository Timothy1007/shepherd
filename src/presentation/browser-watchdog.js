import { GameController } from '../controller/game-controller.js';
import { getNormalActions } from '../game/game.js';

const originalSafeRender = GameController.prototype.safeRender;
let activeController = null;
let lastProgressKey = '';
let lastProgressAt = Date.now();
let recovering = false;

function stateKey(controller) {
  const state = controller?.state;
  if (!state) return '';
  return `${controller.generation}|${controller.actionToken}|${state.round}|${state.currentPlayer ?? 'none'}|${state.phase}`;
}

GameController.prototype.safeRender = function watchdogSafeRender(available) {
  activeController = this;
  const key = stateKey(this);
  if (key !== lastProgressKey) {
    lastProgressKey = key;
    lastProgressAt = Date.now();
  }
  return originalSafeRender.call(this, available);
};

function displayedRound() {
  const text = document.querySelector('#round-display')?.textContent ?? '';
  const match = text.match(/第\s*(\d+)\s*\/\s*7\s*輪/);
  return match ? Number(match[1]) : null;
}

function recover(reason) {
  const controller = activeController;
  if (!controller?.state || controller.state.gameOver || recovering) return;
  recovering = true;
  try {
    console.warn(`[Shepherd] browser watchdog recovery: ${reason}`);
    controller.cancelTimer();
    controller.prepare();
    lastProgressKey = stateKey(controller);
    lastProgressAt = Date.now();
  } catch (error) {
    console.error('[Shepherd] browser watchdog recovery failed.', error);
  } finally {
    recovering = false;
  }
}

window.setInterval(() => {
  const controller = activeController;
  const state = controller?.state;
  if (!state || state.gameOver || state.phase !== 'playing') return;

  const available = getNormalActions(state);
  const current = state.players.find((player) => player.playerId === state.currentPlayer);

  // If the game state advanced but a presentation extension failed to reflect it,
  // force a clean render without changing gameplay state.
  const shownRound = displayedRound();
  if (shownRound !== null && shownRound !== state.round) {
    console.warn(`[Shepherd] UI round ${shownRound} is stale; state is round ${state.round}. Re-rendering.`);
    controller.safeRender(available);
    return;
  }

  // Bookkeeping states must never remain visible. prepare() already knows how to
  // auto-resolve redraw withdrawal / round settlement safely.
  if (!available.length) {
    recover('playing state has no available actions');
    return;
  }

  // The freezes seen in preview have historically appeared as an AI turn whose
  // scheduling timer disappeared after presentation/animation work. Restore only
  // that missing scheduling step; never auto-play a human turn.
  if (current?.type === 'ai' && controller.timer === null) {
    recover('AI turn has no scheduled timer');
    return;
  }

  // Last-resort guard: if an AI turn has not changed state for several seconds,
  // replace its timer and let the normal controller schedule the turn again.
  if (current?.type === 'ai' && Date.now() - lastProgressAt > 6000) {
    recover('AI turn exceeded progress timeout');
  }
}, 700);

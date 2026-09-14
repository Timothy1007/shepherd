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

function hasVisibleDecision() {
  return ['#ark-choice-overlay','#reaction-overlay','#target-choice-overlay','#wind-choice-overlay','#grace-overlay']
    .some(selector => { const el=document.querySelector(selector); return !!el && !el.hidden; });
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

function recover(reason, forceAi = false) {
  const controller = activeController;
  if (!controller?.state || controller.state.gameOver || recovering || hasVisibleDecision()) return;
  recovering = true;
  try {
    console.warn(`[Shepherd] browser watchdog recovery: ${reason}`);
    controller.cancelTimer();
    const current = controller.state.players.find(player => player.playerId === controller.state.currentPlayer);
    if (forceAi && current?.type === 'ai') controller.runAiTurn(controller.generation, current.playerId);
    else controller.prepare();
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
  if (!state || state.gameOver || state.phase !== 'playing' || hasVisibleDecision()) return;

  const available = getNormalActions(state);
  const current = state.players.find((player) => player.playerId === state.currentPlayer);

  const shownRound = displayedRound();
  if (shownRound !== null && shownRound !== state.round) {
    console.warn(`[Shepherd] UI round ${shownRound} is stale; state is round ${state.round}. Re-rendering.`);
    controller.safeRender(available);
    return;
  }

  if (!available.length) {
    recover('playing state has no available actions');
    return;
  }

  if (current?.type === 'ai' && controller.timer === null) {
    recover('AI turn has no scheduled timer', true);
    return;
  }

  if (current?.type === 'ai' && Date.now() - lastProgressAt > 6000) {
    recover('AI turn exceeded progress timeout', true);
  }
}, 700);

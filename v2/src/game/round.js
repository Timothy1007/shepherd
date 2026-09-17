import { ROUND_PHASES, TOTAL_ROUNDS, isMissionRound } from './constants.js';
import { buildLockedDeathOrder } from './death.js';
import { getPlayer } from './state.js';

export function beginRound(state, roundNumber = state.round) {
  if (!Number.isInteger(roundNumber) || roundNumber < 1 || roundNumber > TOTAL_ROUNDS) {
    throw new RangeError(`roundNumber must be between 1 and ${TOTAL_ROUNDS}`);
  }

  state.round = roundNumber;
  state.phase = ROUND_PHASES.SETUP;
  state.playedArea = [];
  state.currentResource = null;
  state.currentApostle = null;
  state.death = {
    triggered: false,
    revealerId: null,
    finalOrder: [],
    finalActionsRemaining: [],
  };

  for (const playerId of state.seats) {
    const player = getPlayer(state, playerId);
    player.roundFlags = {};
    player.missionProgress = {};
    if (!isMissionRound(roundNumber)) player.mission = null;
  }

  state.log.push({ type: 'round-begin', round: roundNumber });
  return state;
}

export function startNormalActions(state) {
  if (state.phase !== ROUND_PHASES.SETUP) {
    throw new Error('Normal actions can only start from setup phase');
  }
  state.phase = ROUND_PHASES.ACTIONS;
  state.log.push({ type: 'normal-actions-begin', round: state.round });
}

export function recordSuccessfulResourcePlay(state, playerId, resourceCard) {
  getPlayer(state, playerId);
  state.currentResource = structuredClone(resourceCard);
  state.currentApostle = playerId;
  state.log.push({
    type: 'resource-success',
    playerId,
    cardId: resourceCard?.id ?? null,
    printedNumber: resourceCard?.printedNumber ?? null,
    resourceType: resourceCard?.resourceType ?? null,
  });
  return playerId;
}

export function markDeathTriggered(state, revealerId) {
  if (state.death.triggered) return state.death.finalOrder;
  getPlayer(state, revealerId);

  state.death.triggered = true;
  state.death.revealerId = revealerId;
  state.death.finalOrder = buildLockedDeathOrder(state.seats, revealerId, state.direction);
  state.death.finalActionsRemaining = [...state.death.finalOrder];
  state.phase = ROUND_PHASES.DEATH_PENDING;
  state.log.push({
    type: 'death-triggered',
    revealerId,
    finalOrder: [...state.death.finalOrder],
  });
  return state.death.finalOrder;
}

export function enterDeathRound(state) {
  if (!state.death.triggered || state.phase !== ROUND_PHASES.DEATH_PENDING) {
    throw new Error('Death round can only begin after Death has triggered and the current effect has finished');
  }
  state.phase = ROUND_PHASES.DEATH_ROUND;
  state.log.push({ type: 'death-round-begin', finalOrder: [...state.death.finalOrder] });
}

export function canPrepare(state) {
  return state.phase === ROUND_PHASES.ACTIONS;
}

export function consumeDeathRoundAction(state, playerId) {
  if (state.phase !== ROUND_PHASES.DEATH_ROUND) throw new Error('Not in Death round');
  const expected = state.death.finalActionsRemaining[0];
  if (expected !== playerId) {
    throw new Error(`Expected Death-round action from ${expected}, received ${playerId}`);
  }
  state.death.finalActionsRemaining.shift();
  state.log.push({ type: 'death-round-action-consumed', playerId });

  if (state.death.finalActionsRemaining.length === 0) {
    state.phase = ROUND_PHASES.SETTLEMENT;
  }
}

export function getApostleJackpotSize(state) {
  return state.playedArea.length;
}

export function advanceToNextRound(state) {
  if (state.round >= TOTAL_ROUNDS) {
    state.phase = ROUND_PHASES.COMPLETE;
    return false;
  }
  beginRound(state, state.round + 1);
  return true;
}

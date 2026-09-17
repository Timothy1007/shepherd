import {
  DIRECTIONS,
  MAX_PLAYERS,
  MIN_PLAYERS,
  ROUND_PHASES,
  TOTAL_ROUNDS,
} from './constants.js';

function assertPlayerIds(playerIds) {
  if (!Array.isArray(playerIds)) throw new TypeError('playerIds must be an array');
  if (playerIds.length < MIN_PLAYERS || playerIds.length > MAX_PLAYERS) {
    throw new RangeError(`PvP V2 requires ${MIN_PLAYERS}-${MAX_PLAYERS} seats`);
  }
  if (new Set(playerIds).size !== playerIds.length) {
    throw new Error('playerIds must be unique');
  }
}

export function createPlayerState(id) {
  return {
    id,
    hand: [],
    fire: 0,
    lowTemperatureFire: 0,
    mission: null,
    missionProgress: {},
    completedMissionCount: 0,
    carryOverHandCount: 0,
    blessings: [],
    effectZone: [],
    specialZones: {},
    roundFlags: {},
  };
}

export function createGameState({ playerIds, corruptionThreshold = null } = {}) {
  assertPlayerIds(playerIds);

  if (corruptionThreshold !== null && (!Number.isInteger(corruptionThreshold) || corruptionThreshold <= 0)) {
    throw new RangeError('corruptionThreshold must be null or a positive integer');
  }

  const players = Object.fromEntries(playerIds.map((id) => [id, createPlayerState(id)]));

  return {
    version: 2,
    seats: [...playerIds],
    players,
    round: 1,
    maxRounds: TOTAL_ROUNDS,
    direction: DIRECTIONS.CLOCKWISE,
    phase: ROUND_PHASES.SETUP,
    deck: [],
    discardPile: [],
    playedArea: [],
    currentResource: null,
    currentApostle: null,
    death: {
      triggered: false,
      revealerId: null,
      finalOrder: [],
      finalActionsRemaining: [],
    },
    corruption: {
      value: 0,
      threshold: corruptionThreshold,
      totalJudgmentsTriggered: 0,
    },
    judgments: {
      activeByDomain: {},
      history: [],
      deck: [],
    },
    log: [],
  };
}

export function getPlayer(state, playerId) {
  const player = state.players[playerId];
  if (!player) throw new Error(`Unknown player: ${playerId}`);
  return player;
}

export function cloneGameState(state) {
  return structuredClone(state);
}

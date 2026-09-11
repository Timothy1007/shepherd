import { createResourceDeck, getDefinition, isLegalResourcePlay, RESOURCE_TYPES } from './cards.js';
import { normalizeSeed, randomInteger, shuffleWithState } from './rng.js';

export const SCHEMA_VERSION = 1;
export const PLAYER_COUNT = 4;
export const MAX_ROUNDS = 7;
export const COUNTERCLOCKWISE = -1;

const clone = (value) => structuredClone(value);
const playerIndex = (state, playerId) => state.players.findIndex((player) => player.playerId === playerId);

function rollStartingPlayer(rngState) {
  let contenders = [0, 1, 2, 3];
  let state = rngState;
  const history = [];
  while (contenders.length > 1) {
    const round = [];
    let highest = 0;
    for (const index of contenders) {
      const roll = randomInteger(state, 1, 6);
      state = roll.state;
      highest = Math.max(highest, roll.value);
      round.push({ playerId: `player-${index + 1}`, value: roll.value });
    }
    history.push(round);
    contenders = contenders.filter((index) => round.find((roll) => roll.playerId === `player-${index + 1}`).value === highest);
  }
  return { playerIndex: contenders[0], history, rngState: state };
}

function dealCards(state) {
  for (const player of state.players) {
    const count = 7 + player.startingHandBonus;
    player.hand.push(...state.deck.splice(0, count));
  }
}

function prepareRound(state, startingIndex) {
  state.round += 1;
  state.phase = 'playing';
  state.direction = COUNTERCLOCKWISE;
  state.startingPlayer = state.players[startingIndex].playerId;
  state.currentPlayer = state.startingPlayer;
  state.currentResource = null;
  state.currentApostle = null;
  for (const player of state.players) {
    player.hasNormalAction = true;
    player.hasRedrawnThisRound = false;
    player.hand = [];
  }
  const shuffled = shuffleWithState(state.deck, state.rngState);
  state.deck = shuffled.items;
  state.rngState = shuffled.state;
  dealCards(state);
}

export function createGame({ seed = 'shepherd-recovery' } = {}) {
  const registry = createResourceDeck();
  const initialRng = normalizeSeed(seed);
  const shuffled = shuffleWithState(registry, initialRng);
  const roll = rollStartingPlayer(shuffled.state);
  const state = {
    schemaVersion: SCHEMA_VERSION,
    round: 0,
    phase: 'setup',
    gameOver: false,
    winner: [],
    players: Array.from({ length: PLAYER_COUNT }, (_, index) => ({
      playerId: `player-${index + 1}`,
      seat: index + 1,
      type: index === 0 ? 'human' : 'ai',
      hand: [],
      fire: 0,
      lowTemperatureFire: 0,
      losingStreak: 0,
      startingHandBonus: 0,
      hasRedrawnThisRound: false,
      hasNormalAction: true,
      effects: [],
    })),
    deck: shuffled.items,
    discardPile: [],
    playedArea: [],
    removedForRound: [],
    currentResource: null,
    currentApostle: null,
    currentPlayer: null,
    direction: COUNTERCLOCKWISE,
    startingPlayer: null,
    startingPlayerDirection: COUNTERCLOCKWISE,
    cardRegistry: Object.fromEntries(registry.map((card) => [card.instanceId, card.definitionId])),
    rngState: roll.rngState,
    startingRolls: roll.history,
    initialStartingPlayer: `player-${roll.playerIndex + 1}`,
    lastRoundResult: null,
  };
  prepareRound(state, roll.playerIndex);
  return state;
}

function legalPlayActions(state, player) {
  const actions = [];
  for (const card of player.hand) {
    const definition = getDefinition(card);
    const declarations = definition.type === 'grace' ? RESOURCE_TYPES : [definition.type];
    for (const declaredType of declarations) {
      if (isLegalResourcePlay(card, state.currentResource, declaredType)) {
        actions.push({ type: 'playResource', playerId: player.playerId, instanceId: card.instanceId, declaredType });
      }
    }
  }
  return actions;
}

export function getNormalActions(state) {
  if (state.phase !== 'playing' || state.gameOver) return [];
  const player = state.players.find((candidate) => candidate.playerId === state.currentPlayer);
  if (!player?.hasNormalAction) return [];
  const plays = legalPlayActions(state, player);
  if (plays.length) return plays;
  if (player.hand.length === 0) return [{ type: 'endEmptyHand', playerId: player.playerId }];
  if (!player.hasRedrawnThisRound) return [{ type: 'redraw', playerId: player.playerId }];
  return [{ type: 'endParticipation', playerId: player.playerId }];
}

function nextEligibleIndex(state, fromIndex) {
  for (let offset = 1; offset <= state.players.length; offset += 1) {
    const index = (fromIndex + state.direction * offset + state.players.length) % state.players.length;
    if (state.players[index].hasNormalAction) return index;
  }
  return -1;
}

function advanceOrSettle(state, fromIndex) {
  const eligible = state.players.filter((player) => player.hasNormalAction);
  if (eligible.length <= 1) return settleRoundMutable(state);
  const next = nextEligibleIndex(state, fromIndex);
  state.currentPlayer = state.players[next].playerId;
  return state;
}

function drawWithDiscardRecycle(state, count) {
  const drawn = [];
  while (drawn.length < count) {
    if (!state.deck.length) {
      if (!state.discardPile.length) break;
      const shuffled = shuffleWithState(state.discardPile, state.rngState);
      state.deck = shuffled.items;
      state.discardPile = [];
      state.rngState = shuffled.state;
    }
    drawn.push(state.deck.shift());
  }
  return drawn;
}

function playResourceMutable(state, action) {
  const index = playerIndex(state, action.playerId);
  const player = state.players[index];
  const handIndex = player.hand.findIndex((card) => card.instanceId === action.instanceId);
  if (handIndex < 0) return { error: 'Card is not in the current player hand.' };
  const card = player.hand[handIndex];
  if (!isLegalResourcePlay(card, state.currentResource, action.declaredType)) return { error: 'Illegal resource play.' };
  const definition = getDefinition(card);
  const playedCard = { ...card, declaredType: definition.type === 'grace' ? action.declaredType : definition.type };
  player.hand.splice(handIndex, 1);
  state.playedArea.push(playedCard);
  state.currentResource = { type: playedCard.declaredType, number: definition.number, instanceId: card.instanceId };
  state.currentApostle = player.playerId;
  return { state: advanceOrSettle(state, index) };
}

function redrawMutable(state, action) {
  const player = state.players[playerIndex(state, action.playerId)];
  if (!player.hand.length) return { error: 'An empty hand cannot redraw.' };
  if (player.hasRedrawnThisRound) return { error: 'Player already redrew this round.' };
  const count = player.hand.length;
  state.discardPile.push(...player.hand);
  player.hand = [];
  player.hasRedrawnThisRound = true;
  player.hand.push(...drawWithDiscardRecycle(state, count));
  return { state };
}

function endMutable(state, action, expectedType) {
  const index = playerIndex(state, action.playerId);
  const player = state.players[index];
  if (expectedType === 'endEmptyHand' && player.hand.length !== 0) return { error: 'Hand is not empty.' };
  if (expectedType === 'endParticipation' && (!player.hasRedrawnThisRound || player.hand.length === 0)) return { error: 'Participation cannot end yet.' };
  player.hasNormalAction = false;
  return { state: advanceOrSettle(state, index) };
}

function settleRoundMutable(state) {
  if (state.phase !== 'playing') return state;
  const apostle = state.players.find((player) => player.playerId === state.currentApostle);
  if (apostle) apostle.fire += state.playedArea.length;
  for (const player of state.players) {
    player.losingStreak = player === apostle ? 0 : Math.min(3, player.losingStreak + 1);
    player.startingHandBonus = player.losingStreak;
  }
  state.lastRoundResult = {
    round: state.round,
    apostle: state.currentApostle,
    reward: apostle ? state.playedArea.length : 0,
  };
  state.currentPlayer = null;

  if (state.round === MAX_ROUNDS) {
    const highest = Math.max(...state.players.map((player) => player.fire));
    state.winner = state.players.filter((player) => player.fire === highest).map((player) => player.playerId);
    state.gameOver = true;
    state.phase = 'gameOver';
    return state;
  }

  // Keep the completed round intact for a short presentation phase. The controller
  // advances only after the round-resolution animation has had time to play.
  state.phase = 'roundEnd';
  return state;
}

export function advanceToNextRound(state) {
  if (!state || state.gameOver) return { ok: false, reason: 'Game is already over.', state };
  if (state.phase !== 'roundEnd') return { ok: false, reason: 'Round is not waiting for advancement.', state };
  const next = clone(state);
  next.deck.push(
    ...next.players.flatMap((player) => player.hand),
    ...next.playedArea,
    ...next.discardPile,
    ...next.removedForRound,
  );
  next.playedArea = [];
  next.discardPile = [];
  next.removedForRound = [];
  const previousStart = playerIndex(next, next.startingPlayer);
  const nextStart = (previousStart + next.startingPlayerDirection + next.players.length) % next.players.length;
  prepareRound(next, nextStart);
  return { ok: true, state: next };
}

export function executeNormalAction(state, action) {
  if (!action || typeof action !== 'object') return { ok: false, reason: 'Action is required.', state };
  const legal = getNormalActions(state);
  const matching = legal.find((candidate) => candidate.type === action.type
    && candidate.playerId === action.playerId
    && (candidate.instanceId ?? null) === (action.instanceId ?? null)
    && (candidate.declaredType ?? null) === (action.declaredType ?? null));
  if (!matching) return { ok: false, reason: 'Action is stale or illegal.', state };
  const next = clone(state);
  let result;
  if (action.type === 'playResource') result = playResourceMutable(next, action);
  else if (action.type === 'redraw') result = redrawMutable(next, action);
  else if (action.type === 'endParticipation') result = endMutable(next, action, action.type);
  else if (action.type === 'endEmptyHand') result = endMutable(next, action, action.type);
  else return { ok: false, reason: 'Unknown action.', state };
  if (result.error) return { ok: false, reason: result.error, state };
  return { ok: true, state: result.state };
}

export function settleRound(state) {
  if (state.phase !== 'playing') return { ok: false, reason: 'Round is not active.', state };
  if (state.players.filter((player) => player.hasNormalAction).length > 1) return { ok: false, reason: 'More than one player remains eligible.', state };
  return { ok: true, state: settleRoundMutable(clone(state)) };
}

export const playResource = (state, action) => executeNormalAction(state, { ...action, type: 'playResource' });
export const redraw = (state, action) => executeNormalAction(state, { ...action, type: 'redraw' });
export const endParticipation = (state, action) => executeNormalAction(state, { ...action, type: 'endParticipation' });
export const endEmptyHand = (state, action) => executeNormalAction(state, { ...action, type: 'endEmptyHand' });

export function listCardLocations(state) {
  return [
    ...state.deck,
    ...state.discardPile,
    ...state.playedArea,
    ...state.removedForRound,
    ...state.players.flatMap((player) => player.hand),
  ].map((card) => card.instanceId);
}

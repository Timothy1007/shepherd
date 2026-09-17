export const MAX_PLAYERS = 4;
export const MIN_PLAYERS = 2;
export const TOTAL_ROUNDS = 7;
export const FINAL_ROUND = 7;
export const FIRST_MISSION_ROUND = 1;
export const LAST_MISSION_ROUND = 6;

export const CARD_TYPES = Object.freeze({
  RESOURCE: 'resource',
  MIRACLE: 'miracle',
  DISASTER: 'disaster',
  DEATH: 'death',
});

export const RESOURCE_TYPES = Object.freeze({
  SHEEP: 'sheep',
  FOOD: 'food',
  MONEY: 'money',
  GRACE: 'grace',
});

export const ROUND_PHASES = Object.freeze({
  SETUP: 'setup',
  ACTIONS: 'actions',
  DEATH_PENDING: 'death-pending',
  DEATH_ROUND: 'death-round',
  SETTLEMENT: 'settlement',
  COMPLETE: 'complete',
});

export const DIRECTIONS = Object.freeze({
  CLOCKWISE: 1,
  COUNTER_CLOCKWISE: -1,
});

export const JUDGMENT_DOMAINS = Object.freeze({
  ORDER: 'order',
  INFORMATION: 'information',
  CALAMITY: 'calamity',
  RESOURCE: 'resource',
  MUTATION: 'mutation',
  BATTLEFIELD: 'battlefield',
  MISSION: 'mission',
  BLESSING: 'blessing',
});

export function isMissionRound(roundNumber) {
  return roundNumber >= FIRST_MISSION_ROUND && roundNumber <= LAST_MISSION_ROUND;
}

export function isFinalRound(roundNumber) {
  return roundNumber === FINAL_ROUND;
}

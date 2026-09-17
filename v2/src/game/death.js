import { CARD_TYPES, DIRECTIONS } from './constants.js';

export function isDeathCard(card) {
  return Boolean(card && card.type === CARD_TYPES.DEATH);
}

export function drawNormalCards(deck, count) {
  if (!Array.isArray(deck)) throw new TypeError('deck must be an array');
  if (!Number.isInteger(count) || count < 0) throw new RangeError('count must be a non-negative integer');

  const drawn = [];
  let deathTriggered = false;

  while (drawn.length < count) {
    if (deck.length === 0) throw new Error('Deck exhausted before required normal draw could finish');
    const card = deck.shift();
    if (isDeathCard(card)) {
      deathTriggered = true;
      continue;
    }
    drawn.push(card);
  }

  return { drawn, deathTriggered };
}

export function takeTopNormalCardsUntilDeath(deck, limit) {
  if (!Array.isArray(deck)) throw new TypeError('deck must be an array');
  if (!Number.isInteger(limit) || limit < 0) throw new RangeError('limit must be a non-negative integer');

  const taken = [];
  while (taken.length < limit && deck.length > 0) {
    if (isDeathCard(deck[0])) break;
    taken.push(deck.shift());
  }
  return taken;
}

export function buildLockedDeathOrder(seats, revealerId, direction = DIRECTIONS.CLOCKWISE) {
  const start = seats.indexOf(revealerId);
  if (start < 0) throw new Error(`Unknown revealer seat: ${revealerId}`);
  if (![DIRECTIONS.CLOCKWISE, DIRECTIONS.COUNTER_CLOCKWISE].includes(direction)) {
    throw new Error('Invalid direction');
  }

  const order = [];
  for (let step = 1; step <= seats.length; step += 1) {
    const index = (start + direction * step + seats.length * 10) % seats.length;
    order.push(seats[index]);
  }
  return order;
}

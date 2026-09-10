export function normalizeSeed(seed) {
  if (typeof seed === 'number' && Number.isFinite(seed)) return seed >>> 0 || 1;
  let value = 2166136261;
  for (const character of String(seed ?? 'shepherd')) {
    value ^= character.codePointAt(0);
    value = Math.imul(value, 16777619);
  }
  return value >>> 0 || 1;
}

export function nextRandom(rngState) {
  let value = rngState >>> 0;
  value ^= value << 13;
  value ^= value >>> 17;
  value ^= value << 5;
  const state = value >>> 0 || 1;
  return { state, value: state / 0x100000000 };
}

export function randomInteger(rngState, minimum, maximum) {
  const next = nextRandom(rngState);
  return { state: next.state, value: minimum + Math.floor(next.value * (maximum - minimum + 1)) };
}

export function shuffleWithState(items, rngState) {
  const shuffled = [...items];
  let state = rngState;
  for (let index = shuffled.length - 1; index > 0; index -= 1) {
    const roll = randomInteger(state, 0, index);
    state = roll.state;
    [shuffled[index], shuffled[roll.value]] = [shuffled[roll.value], shuffled[index]];
  }
  return { items: shuffled, state };
}

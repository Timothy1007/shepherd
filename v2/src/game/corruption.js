export function addCorruption(state, amount, source = null) {
  if (!Number.isFinite(amount) || amount < 0) {
    throw new RangeError('Corruption gain must be a non-negative number');
  }

  state.corruption.value += amount;
  state.log.push({
    type: 'corruption-gain',
    amount,
    source,
    value: state.corruption.value,
  });

  return getPendingJudgmentCount(state);
}

export function reduceCorruption(state, amount, source = null) {
  if (!Number.isFinite(amount) || amount < 0) {
    throw new RangeError('Corruption reduction must be a non-negative number');
  }

  const reduced = Math.min(state.corruption.value, amount);
  state.corruption.value -= reduced;
  state.log.push({
    type: 'corruption-reduction',
    amount: reduced,
    source,
    value: state.corruption.value,
  });
  return reduced;
}

export function getPendingJudgmentCount(state) {
  const threshold = state.corruption.threshold;
  if (!Number.isInteger(threshold) || threshold <= 0) return 0;
  return Math.floor(state.corruption.value / threshold);
}

export function consumeJudgmentThreshold(state) {
  const threshold = state.corruption.threshold;
  if (!Number.isInteger(threshold) || threshold <= 0) {
    throw new Error('Corruption threshold has not been finalized/configured');
  }
  if (state.corruption.value < threshold) return false;

  state.corruption.value -= threshold;
  state.corruption.totalJudgmentsTriggered += 1;
  state.log.push({
    type: 'judgment-threshold-consumed',
    threshold,
    remainingCorruption: state.corruption.value,
  });
  return true;
}

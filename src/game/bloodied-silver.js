// This module models one resolved gain and its resulting transfer. The game
// orchestrator must present the choices before calling completeSilverTransfer.
export const SILVER_DEFINITION_ID = 'disaster-10';
export const SILVER_TRANSFER_SOURCE = 'bloodied-silver-transfer';

export function silverTransferChoices(state, { playerId, amount, source }) {
  if (!Number.isFinite(amount) || amount <= 0 || source === SILVER_TRANSFER_SOURCE) return [];
  const holder = state.players.find(player => player.playerId === playerId);
  if (!holder || holder.fire < 1 || !holder.effects.some(card => card.definitionId === SILVER_DEFINITION_ID)) return [];
  return state.players.filter(player => player.playerId !== playerId).map(player => player.playerId);
}

export function completeSilverTransfer(state, event, recipientPlayerId) {
  if (!silverTransferChoices(state, event).includes(recipientPlayerId)) {
    return { ok: false, reason: 'Invalid 染血銀幣 recipient or gain event.', state };
  }
  const next = structuredClone(state);
  const holder = next.players.find(player => player.playerId === event.playerId);
  const recipient = next.players.find(player => player.playerId === recipientPlayerId);
  holder.fire -= 1;
  recipient.fire += 1;
  return { ok: true, state: next, gainEvent: { playerId: recipientPlayerId, amount: 1, source: SILVER_TRANSFER_SOURCE } };
}

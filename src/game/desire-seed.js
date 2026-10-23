// A gain is one resolved effect, rather than a net hand-size change.
export function desireSeedEvent(player, gainedCards, source) {
  if (source !== 'effect' || gainedCards.length < 2) return null;
  const effects = player.effects.filter(card => card.definitionId === 'disaster-14');
  if (!effects.length) return null;
  return {playerId: player.playerId, cardIds: gainedCards.map(card => card.instanceId), effectIds: effects.map(card => card.instanceId)};
}

export function desireSeedChoices(state, event) {
  const player = state.players.find(p => p.playerId === event.playerId);
  if (!player) return [];
  const effect = player.effects.find(c => event.effectIds.includes(c.instanceId));
  if (!effect) return [];
  return event.cardIds.filter(id => player.hand.some(c => c.instanceId === id)).flatMap(cardId =>
    state.players.filter(p => p.playerId !== player.playerId).map(p => ({playerId: player.playerId, cardId, recipientPlayerId: p.playerId, effectId: effect.instanceId})));
}

export function completeDesireSeed(state, event, choice) {
  if (!desireSeedChoices(state, event).some(c => Object.keys(c).every(k => c[k] === choice[k]))) return false;
  const player = state.players.find(p => p.playerId === choice.playerId);
  const recipient = state.players.find(p => p.playerId === choice.recipientPlayerId);
  const card = player.hand.splice(player.hand.findIndex(c => c.instanceId === choice.cardId), 1)[0];
  const effect = player.effects.splice(player.effects.findIndex(c => c.instanceId === choice.effectId), 1)[0];
  recipient.hand.push(card);
  recipient.effects.push({...effect, effectOwnerPlayerId: recipient.playerId});
  return true;
}

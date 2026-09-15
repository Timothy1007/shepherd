import { getDefinition } from './cards.js';

export function isPersistentDisasterEffect(card) {
  if (!card) return false;
  const definition = getDefinition(card);
  return definition.kind === 'disaster' && Boolean(definition.duration);
}

export function isNegativeEffect(card) {
  if (!card) return false;
  return Boolean(getDefinition(card).negative);
}

export function isCleanseableEffect(card) {
  return isPersistentDisasterEffect(card) || isNegativeEffect(card);
}

export function getCleanseableEffects(player) {
  return (player?.effects ?? []).filter(isCleanseableEffect);
}

export function removeEffectByInstanceId(state, player, instanceId) {
  if (!state || !player || !instanceId) return null;
  const index = player.effects.findIndex(card => card.instanceId === instanceId && isCleanseableEffect(card));
  if (index < 0) return null;
  const [removed] = player.effects.splice(index, 1);
  state.discardPile.push(removed);
  return removed;
}

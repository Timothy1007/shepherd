// The marker reflects only the human player's active Victory effects.
export function isHumanVictoryTarget(state, playerId) {
  const human=state?.players?.find(player=>player.type==='human');
  return Boolean(human?.effects?.some(card=>card.definitionId==='miracle-09' && card.targetPlayerId===playerId));
}

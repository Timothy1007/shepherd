import { getPlayer } from './state.js';

function assertPrintedNumber(card) {
  if (!card || card.type !== 'resource') throw new Error('Mission progress requires a resource card');
  if (!Number.isFinite(card.printedNumber)) throw new Error('Resource card requires printedNumber');
}

export function resetMissionProgress(player) {
  player.missionProgress = {};
  player.mission = null;
}

export function assignMission(state, playerId, mission) {
  const player = getPlayer(state, playerId);
  player.mission = structuredClone(mission);
  player.missionProgress = {};
  return player.mission;
}

export function addResourceMissionProgress(state, playerId, resourceCard) {
  assertPrintedNumber(resourceCard);
  const player = getPlayer(state, playerId);
  const resourceType = resourceCard.resourceType;
  if (!resourceType) throw new Error('Resource card requires resourceType');

  const amount = resourceCard.printedNumber;
  player.missionProgress[resourceType] = (player.missionProgress[resourceType] ?? 0) + amount;
  return amount;
}

export function addDirectMissionProgress(state, playerId, resourceType, amount) {
  if (!resourceType) throw new Error('resourceType is required');
  if (!Number.isFinite(amount)) throw new Error('amount must be numeric');
  const player = getPlayer(state, playerId);
  player.missionProgress[resourceType] = Math.max(0, (player.missionProgress[resourceType] ?? 0) + amount);
  return player.missionProgress[resourceType];
}

export function isMissionComplete(player) {
  if (!player.mission?.requirements) return false;
  return Object.entries(player.mission.requirements).every(
    ([resourceType, required]) => (player.missionProgress[resourceType] ?? 0) >= required,
  );
}

export function settleMission(state, playerId) {
  const player = getPlayer(state, playerId);
  const completed = isMissionComplete(player);
  if (completed && !player.roundFlags.missionCompleted) {
    player.completedMissionCount += 1;
    player.roundFlags.missionCompleted = true;
  }
  return completed;
}

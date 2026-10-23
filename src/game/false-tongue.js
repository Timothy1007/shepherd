// This module only calculates the effect of the reaction. It does not open a
// reaction window or mutate game state; the caller must do so at the actual
// miracle resolution point.
const REDUCTIONS = Object.freeze({ draw: 1, fire: 5, mission: 2 });

export function falseTongueOptions(effect) {
  if (!effect || effect.sourceKind !== 'miracle') return [];
  return Object.keys(REDUCTIONS).filter(kind =>
    Number.isFinite(effect[kind]) && effect[kind] > 0
  );
}

export function applyFalseTongue(effect, kind) {
  if (!falseTongueOptions(effect).includes(kind)) {
    throw new RangeError('That miracle effect cannot be reduced.');
  }
  return {...effect, [kind]: Math.max(0, effect[kind] - REDUCTIONS[kind])};
}

// One reaction is represented independently of turn order. The caller supplies
// the eligible responder and holds the miracle continuation while this is open.
export function openFalseTongue(effect, responderPlayerId, cardInstanceId) {
  const options = falseTongueOptions(effect);
  if (!options.length || !effect.playerId || !responderPlayerId || !cardInstanceId) return null;
  return {stage:'offer', effect:{...effect}, responderPlayerId, cardInstanceId};
}

export function falseTongueActions(window) {
  if (!window) return [];
  if (window.stage === 'offer') return ['pass','use'].map(choice => ({playerId:window.responderPlayerId,choice}));
  if (window.stage === 'choose') return falseTongueOptions(window.effect).map(choice => ({playerId:window.effect.playerId,choice}));
  return [];
}

export function resolveFalseTongue(window, action) {
  if (!falseTongueActions(window).some(option => option.playerId === action?.playerId && option.choice === action?.choice)) {
    throw new RangeError('Invalid false tongue response.');
  }
  if (window.stage === 'offer') {
    if (action.choice === 'pass') return {stage:'done',effect:window.effect,consumeCard:false};
    const options = falseTongueOptions(window.effect);
    if (options.length === 1) return {stage:'done',effect:applyFalseTongue(window.effect,options[0]),consumeCard:true};
    return {...window,stage:'choose'};
  }
  return {stage:'done',effect:applyFalseTongue(window.effect,action.choice),consumeCard:true};
}

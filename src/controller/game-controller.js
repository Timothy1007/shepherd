import { createGame, executeNormalAction, getNormalActions, settleRound } from '../game/game.js';
import { getDefinition } from '../game/cards.js';

const defaultSetTimer = (callback, delay) => globalThis.setTimeout(callback, delay);
const defaultClearTimer = (timer) => globalThis.clearTimeout(timer);

function playerLabel(state, playerId) {
  const player = state?.players?.find((candidate) => candidate.playerId === playerId);
  return player ? `P${player.seat}` : playerId;
}

function actionCard(state, action) {
  if (!action?.instanceId) return null;
  const player = state.players.find((candidate) => candidate.playerId === state.currentPlayer);
  return player?.hand?.find((card) => card.instanceId === action.instanceId) ?? null;
}

function actionDescription(state, action) {
  if (!action) return '未知行動';
  if (action.type === 'redraw') return '重新整備手牌';
  if (action.type === 'endParticipation') return '退出本輪';
  if (action.type === 'endEmptyHand') return '空手結束參與';
  const card = actionCard(state, action);
  if (!card) return action.type;
  const definition = getDefinition(card);
  if (definition.kind === 'resource') {
    const type = definition.type === 'grace' ? action.declaredType ?? definition.type : definition.type;
    const names = { sheep: '群羊', food: '糧食', money: '金錢', grace: '恩典' };
    return `打出 ${names[type] ?? type} ${definition.number}`;
  }
  return `打出${definition.kind === 'miracle' ? '神蹟' : '災難'}〈${definition.name}〉`;
}

export class GameController {
  constructor({ render = () => {}, setTimer = defaultSetTimer, clearTimer = defaultClearTimer, delay = aiDelay } = {}) {
    this.render = render;
    this.setTimer = setTimer;
    this.clearTimer = clearTimer;
    this.delay = delay;
    this.state = null;
    this.generation = 0;
    this.actionToken = 0;
    this.timer = null;
    this.lastRenderError = null;
    this.recovering = false;
    this.aiLog = [];
  }

  logAi(message) {
    const documentRef = globalThis.document;
    const entry = `[${new Date().toLocaleTimeString('zh-TW', { hour12: false })}] ${message}`;
    this.aiLog.push(entry);
    if (this.aiLog.length > 14) this.aiLog.shift();
    const host = documentRef?.querySelector?.('#ai-debug-log');
    if (host) {
      host.replaceChildren(...this.aiLog.map((text) => {
        const line = documentRef.createElement('div');
        line.textContent = text;
        return line;
      }));
      host.scrollTop = host.scrollHeight;
    }
  }

  start(seed) {
    this.cancelTimer();
    this.generation += 1;
    this.actionToken += 1;
    this.aiLog = [];
    this.state = createGame({ seed });
    this.prepare();
    return this.state;
  }

  restart(seed) { return this.start(seed); }
  cancelTimer() { if (this.timer !== null) this.clearTimer(this.timer); this.timer = null; }
  snapshot() { return { generation: this.generation, actionToken: this.actionToken }; }

  updateDebug(available) {
    const documentRef = globalThis.document;
    if (!documentRef || !this.state) return;
    const host = documentRef.querySelector('#debug-state');
    if (!host) return;
    const current = this.state.players.find((player) => player.playerId === this.state.currentPlayer);
    const eligible = this.state.players.filter((player) => player.hasNormalAction).map((player) => player.playerId).join(',') || 'none';
    host.textContent = `R${this.state.round} ${this.state.phase} | turn=${this.state.currentPlayer ?? 'none'} | actions=${available.map((action) => action.type).join(',') || 'none'} | eligible=${eligible} | redraw=${current?.hasRedrawnThisRound ?? '-'} | hand=${current?.hand.length ?? '-'}`;
  }

  safeRender(available = getNormalActions(this.state)) {
    this.updateDebug(available);
    try { this.render(this.state, available, this.snapshot()); this.lastRenderError = null; return true; }
    catch (error) { this.lastRenderError = error; console.error('[Shepherd] presentation render failed; gameplay will continue.', error); return false; }
  }

  act(action, token = this.snapshot()) {
    if (!this.state) return { ok: false, reason: 'No game is active.', state: this.state };
    if (token.generation !== this.generation || token.actionToken !== this.actionToken) return { ok: false, reason: 'Action token expired.', state: this.state };
    const result = executeNormalAction(this.state, action);
    if (!result.ok) return result;
    this.state = result.state; this.actionToken += 1; this.prepare();
    return { ok: true, state: this.state };
  }

  recoverStalledState(available) {
    if (this.recovering || !this.state || this.state.gameOver || available.length) return false;
    this.recovering = true;
    try {
      const eligible = this.state.players.filter((player) => player.hasNormalAction);
      if (eligible.length <= 1) { const result = settleRound(this.state); if (result.ok) { this.state = result.state; this.actionToken += 1; return true; } }
      const current = this.state.players.find((player) => player.playerId === this.state.currentPlayer);
      if ((!current || !current.hasNormalAction) && eligible.length > 0) { this.state = structuredClone(this.state); this.state.currentPlayer = eligible[0].playerId; this.actionToken += 1; return true; }
      return false;
    } finally { this.recovering = false; }
  }

  prepare() {
    if (!this.state) return;
    let available = getNormalActions(this.state);
    this.cancelTimer();
    if (this.state.gameOver) { this.safeRender(available); return; }
    if (available.length === 1 && ['endEmptyHand', 'endParticipation'].includes(available[0].type)) {
      const result = executeNormalAction(this.state, available[0]);
      if (result.ok) { this.state = result.state; this.actionToken += 1; this.prepare(); return; }
    }
    this.safeRender(available);
    if (!available.length && this.recoverStalledState(available)) { available = getNormalActions(this.state); this.safeRender(available); if (this.state.gameOver) return; }
    const player = this.state.players.find((candidate) => candidate.playerId === this.state.currentPlayer);
    if (player?.type !== 'ai') return;
    const generation = this.generation, playerId = player.playerId;
    const wait = this.delay();
    this.logAi(`${playerLabel(this.state, playerId)} 思考中…（約 ${(wait / 1000).toFixed(1)} 秒）`);
    this.timer = this.setTimer(() => {
      this.timer = null;
      if (generation !== this.generation || !this.state || this.state.gameOver || this.state.currentPlayer !== playerId) return;
      this.runAiTurn(generation, playerId);
    }, wait);
  }

  runAiTurn(generation, playerId) {
    if (generation !== this.generation || !this.state || this.state.gameOver) return;
    const player = this.state.players.find((candidate) => candidate.playerId === this.state.currentPlayer);
    if (player?.type !== 'ai' || player.playerId !== playerId) return;
    const actions = getNormalActions(this.state);
    if (!actions.length) { if (this.recoverStalledState(actions)) { this.prepare(); return; } this.safeRender(actions); return; }
    const action = actions[0];
    const beforeHand = player.hand.map((card) => card.instanceId);
    this.logAi(`${playerLabel(this.state, playerId)} ${actionDescription(this.state, action)}`);
    const result = executeNormalAction(this.state, action);
    if (!result.ok) { this.logAi(`${playerLabel(this.state, playerId)} 行動失敗：${result.reason ?? '未知原因'}`); this.safeRender(actions); return; }
    this.state = result.state; this.actionToken += 1;
    if (action.type === 'redraw') {
      const after = this.state.players.find((candidate) => candidate.playerId === playerId)?.hand ?? [];
      this.logAi(`${playerLabel(this.state, playerId)} 已重洗：${beforeHand.length} 張 → ${after.length} 張`);
      this.safeRender(getNormalActions(this.state));
      const nextGeneration = this.generation;
      this.timer = this.setTimer(() => { this.timer = null; if (nextGeneration === this.generation && this.state?.currentPlayer === playerId) this.runAiTurn(nextGeneration, playerId); }, aiFollowupDelay());
      return;
    }
    this.prepare();
  }
}

export function aiDelay() { return 1900 + Math.floor(Math.random() * 1601); }
export function aiFollowupDelay() { return 1100 + Math.floor(Math.random() * 901); }

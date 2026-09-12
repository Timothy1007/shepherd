import { createGame, executeNormalAction, getNormalActions, settleRound } from '../game/game.js';

const defaultSetTimer = (callback, delay) => globalThis.setTimeout(callback, delay);
const defaultClearTimer = (timer) => globalThis.clearTimeout(timer);

export class GameController {
  constructor({ render = () => {}, setTimer = defaultSetTimer, clearTimer = defaultClearTimer, delay = () => 1400 } = {}) {
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
  }

  start(seed) {
    this.cancelTimer();
    this.generation += 1;
    this.actionToken += 1;
    this.state = createGame({ seed });
    this.prepare();
    return this.state;
  }

  restart(seed) {
    return this.start(seed);
  }

  cancelTimer() {
    if (this.timer !== null) this.clearTimer(this.timer);
    this.timer = null;
  }

  snapshot() {
    return { generation: this.generation, actionToken: this.actionToken };
  }

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
    try {
      this.render(this.state, available, this.snapshot());
      this.lastRenderError = null;
      return true;
    } catch (error) {
      this.lastRenderError = error;
      console.error('[Shepherd] presentation render failed; gameplay will continue.', error);
      return false;
    }
  }

  act(action, token = this.snapshot()) {
    if (!this.state) return { ok: false, reason: 'No game is active.', state: this.state };
    if (token.generation !== this.generation || token.actionToken !== this.actionToken) {
      return { ok: false, reason: 'Action token expired.', state: this.state };
    }
    const result = executeNormalAction(this.state, action);
    if (!result.ok) return result;
    this.state = result.state;
    this.actionToken += 1;
    this.prepare();
    return { ok: true, state: this.state };
  }

  recoverStalledState(available) {
    if (this.recovering || !this.state || this.state.gameOver || available.length) return false;
    this.recovering = true;
    try {
      const eligible = this.state.players.filter((player) => player.hasNormalAction);
      if (eligible.length <= 1) {
        const result = settleRound(this.state);
        if (result.ok) {
          this.state = result.state;
          this.actionToken += 1;
          return true;
        }
      }

      const current = this.state.players.find((player) => player.playerId === this.state.currentPlayer);
      if ((!current || !current.hasNormalAction) && eligible.length > 0) {
        this.state = structuredClone(this.state);
        this.state.currentPlayer = eligible[0].playerId;
        this.actionToken += 1;
        return true;
      }
      return false;
    } finally {
      this.recovering = false;
    }
  }

  prepare() {
    if (!this.state) return;
    let available = getNormalActions(this.state);

    this.cancelTimer();
    if (this.state.gameOver) {
      this.safeRender(available);
      return;
    }

    // These are bookkeeping states, not player decisions. In particular,
    // endParticipation must never surface as a button: once a player has already
    // used the one redraw and still cannot play, they immediately leave the round.
    if (available.length === 1 && ['endEmptyHand', 'endParticipation'].includes(available[0].type)) {
      const result = executeNormalAction(this.state, available[0]);
      if (result.ok) {
        this.state = result.state;
        this.actionToken += 1;
        this.prepare();
        return;
      }
    }

    this.safeRender(available);

    if (!available.length && this.recoverStalledState(available)) {
      available = getNormalActions(this.state);
      this.safeRender(available);
      if (this.state.gameOver) return;
    }

    const player = this.state.players.find((candidate) => candidate.playerId === this.state.currentPlayer);
    if (player?.type !== 'ai') return;

    const generation = this.generation;
    const playerId = player.playerId;
    this.timer = this.setTimer(() => {
      this.timer = null;
      if (generation !== this.generation) return;
      if (!this.state || this.state.gameOver || this.state.currentPlayer !== playerId) return;
      this.runAiTurn(generation, playerId);
    }, this.delay());
  }

  runAiTurn(generation, playerId) {
    for (let step = 0; step < 8; step += 1) {
      if (generation !== this.generation || !this.state || this.state.gameOver) return;
      const player = this.state.players.find((candidate) => candidate.playerId === this.state.currentPlayer);
      if (player?.type !== 'ai' || player.playerId !== playerId) return;

      const actions = getNormalActions(this.state);
      if (!actions.length) {
        if (this.recoverStalledState(actions)) {
          this.prepare();
          return;
        }
        this.safeRender(actions);
        return;
      }

      const action = actions[0];
      const result = executeNormalAction(this.state, action);
      if (!result.ok) {
        this.safeRender(actions);
        return;
      }

      this.state = result.state;
      this.actionToken += 1;

      if (action.type !== 'redraw') {
        this.prepare();
        return;
      }

      this.safeRender(getNormalActions(this.state));
    }

    this.prepare();
  }
}

export function aiDelay() {
  return 1400 + Math.floor(Math.random() * 1001);
}

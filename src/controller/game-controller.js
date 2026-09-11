import { createGame, executeNormalAction, getNormalActions } from '../game/game.js';

const defaultSetTimer = (callback, delay) => setTimeout(callback, delay);
const defaultClearTimer = (timer) => clearTimeout(timer);
const AI_HOUSEKEEPING_DELAY = 140;

export class GameController {
  constructor({ render = () => {}, setTimer = defaultSetTimer, clearTimer = defaultClearTimer, delay = () => 1400 } = {}) {
    this.render = render;
    this.setTimer = setTimer;
    this.clearTimer = clearTimer;
    this.delay = delay;
    this.state = null;
    this.generation = 0;
    this.actionToken = 0;
    this.aiTimer = null;
  }

  start(seed) {
    this.cancelAiTimer();
    this.generation += 1;
    this.actionToken += 1;
    this.state = createGame({ seed });
    this.prepare();
    return this.state;
  }

  restart(seed) {
    return this.start(seed);
  }

  cancelAiTimer() {
    if (this.aiTimer !== null) this.clearTimer(this.aiTimer);
    this.aiTimer = null;
  }

  snapshot() {
    return { generation: this.generation, actionToken: this.actionToken };
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

  scheduleAi(action, token, delay) {
    this.cancelAiTimer();
    this.aiTimer = this.setTimer(() => {
      this.aiTimer = null;
      if (token.generation !== this.generation || token.actionToken !== this.actionToken) return;
      if (action) this.act(action, token);
      else this.runAiTurn(token);
    }, delay);
  }

  prepare() {
    if (!this.state) return;
    const available = getNormalActions(this.state);
    this.render(this.state, available, this.snapshot());
    this.cancelAiTimer();
    if (this.state.gameOver) return;

    if (available.length === 1 && available[0].type === 'endEmptyHand') {
      this.scheduleAi(available[0], this.snapshot(), 0);
      return;
    }

    const player = this.state.players.find((candidate) => candidate.playerId === this.state.currentPlayer);
    if (player?.type !== 'ai') return;

    const automatic = available.length === 1 && ['redraw', 'endParticipation'].includes(available[0].type);
    this.scheduleAi(null, this.snapshot(), automatic ? AI_HOUSEKEEPING_DELAY : this.delay());
  }

  runAiTurn(token) {
    if (!this.state || this.state.gameOver) return;
    if (token.generation !== this.generation || token.actionToken !== this.actionToken) return;

    const player = this.state.players.find((candidate) => candidate.playerId === this.state.currentPlayer);
    if (player?.type !== 'ai') return;

    const actions = getNormalActions(this.state);
    if (!actions.length) {
      this.render(this.state, actions, this.snapshot());
      return;
    }

    const result = executeNormalAction(this.state, actions[0]);
    if (!result.ok) {
      this.render(this.state, actions, this.snapshot());
      return;
    }

    this.state = result.state;
    this.actionToken += 1;
    this.prepare();
  }
}

export function aiDelay() {
  return 1400 + Math.floor(Math.random() * 1001);
}

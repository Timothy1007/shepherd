import { createGame, executeNormalAction, getNormalActions } from '../game/game.js';

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

  safeRender(available = getNormalActions(this.state)) {
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

  prepare() {
    if (!this.state) return;
    const available = getNormalActions(this.state);

    // Rendering must never own the game loop. A visual-layer exception previously
    // prevented the code below from scheduling the next AI action, which looked
    // exactly like a frozen match and also made restart appear broken.
    this.safeRender(available);

    this.cancelTimer();
    if (this.state.gameOver) return;

    // Empty hand is pure bookkeeping. Resolve it immediately so the turn cannot
    // visually stall on a player who has no cards left.
    if (available.length === 1 && available[0].type === 'endEmptyHand') {
      this.act(available[0], this.snapshot());
      return;
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
    // One visible AI turn may contain a redraw followed immediately by either a
    // legal play or endParticipation. Keep that chain inside the same callback so
    // there is no timer gap where the game can appear frozen.
    for (let step = 0; step < 8; step += 1) {
      if (generation !== this.generation || !this.state || this.state.gameOver) return;
      const player = this.state.players.find((candidate) => candidate.playerId === this.state.currentPlayer);
      if (player?.type !== 'ai' || player.playerId !== playerId) return;

      const actions = getNormalActions(this.state);
      if (!actions.length) {
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

      // Redraw keeps the same player on turn and must immediately resolve into a
      // play or withdrawal. Any other action ends this visible AI turn.
      if (action.type !== 'redraw') {
        this.prepare();
        return;
      }

      this.safeRender(getNormalActions(this.state));
    }

    // Safety fallback: never spin forever if future rules add more bookkeeping.
    this.prepare();
  }
}

export function aiDelay() {
  return 1400 + Math.floor(Math.random() * 1001);
}

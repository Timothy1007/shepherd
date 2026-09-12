import { createGame, executeNormalAction, getNormalActions } from '../game/game.js';

export class GameController {
  constructor({ render = () => {}, setTimer = setTimeout, clearTimer = clearTimeout, delay = () => 1400 } = {}) {
    this.render = render;
    this.setTimer = setTimer;
    this.clearTimer = clearTimer;
    this.delay = delay;
    this.state = null;
    this.generation = 0;
    this.actionToken = 0;
    this.timer = null;
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
    this.render(this.state, available, this.snapshot());
    this.cancelTimer();
    if (this.state.gameOver) return;

    // Empty-hand exit is bookkeeping, not a visible player decision.
    if (available.length === 1 && available[0].type === 'endEmptyHand') {
      this.act(available[0], this.snapshot());
      return;
    }

    const player = this.state.players.find((candidate) => candidate.playerId === this.state.currentPlayer);
    if (player?.type !== 'ai') return;

    const token = this.snapshot();
    this.timer = this.setTimer(() => {
      this.timer = null;
      if (token.generation !== this.generation || token.actionToken !== this.actionToken) return;
      this.runAiTurn(token);
    }, this.delay());
  }

  runAiTurn(token) {
    // Keep the original proven behavior: an AI redraw and the immediately-following
    // play/exit resolve in one AI turn. This prevents the game from being left in a
    // redraw-complete / endParticipation waiting state after another player exits.
    while (token.generation === this.generation && token.actionToken === this.actionToken) {
      const player = this.state.players.find((candidate) => candidate.playerId === this.state.currentPlayer);
      if (player?.type !== 'ai' || this.state.gameOver) return;

      const actions = getNormalActions(this.state);
      if (!actions.length) return;

      const wasRedraw = actions[0].type === 'redraw';
      const result = executeNormalAction(this.state, actions[0]);
      if (!result.ok) return;

      this.state = result.state;
      this.actionToken += 1;
      token = this.snapshot();

      if (!wasRedraw) {
        this.prepare();
        return;
      }

      // A redraw keeps the same player's turn. Re-render the new hand, then loop
      // immediately so that either a legal play or endParticipation is resolved.
      this.render(this.state, getNormalActions(this.state), token);
    }
  }
}

export function aiDelay() {
  return 1400 + Math.floor(Math.random() * 1001);
}

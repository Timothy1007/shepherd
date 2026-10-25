import { renderPlayers, renderOptions } from './player-choice-row.js';
import { GameController } from '../controller/game-controller.js';
import { getNormalActions } from '../game/game.js';

const originalAct = GameController.prototype.act;
let panel;

function finishChoice(controller, choice) {
  panel.hidden = true;
  // Re-enter the current action chain with a fresh token. Other choice modules
  // may have wrapped act() after this one was installed.
  return controller.act({ ...choice, overflowChoiceComplete: true }, controller.snapshot());
}

GameController.prototype.act = function overflowChoiceAct(action, token = this.snapshot()) {
  const actor = this.state?.players.find(player => player.playerId === action?.playerId);
  const card = actor?.hand.find(item => item.instanceId === action?.instanceId);
  if (
    action?.type !== 'playMiracle'
    || action?.noEffect
    || card?.definitionId !== 'miracle-14'
    || actor.type !== 'human'
    || action.overflowChoiceComplete
  ) return originalAct.call(this, action, token);

  if (!panel) {
    panel = document.createElement('div');
    panel.id = 'overflow-choice';
    panel.hidden = true;
    document.body.append(panel);
  }

  const choices = getNormalActions(this.state).filter(candidate => candidate.instanceId === card.instanceId);
  const candidateFor = playerId => choices.find(candidate => candidate.targetPlayerId === playerId);
  const showEffects = candidate => {
    if (!candidate) return;
    const target = this.state.players.find(player => player.playerId === candidate.targetPlayerId);
    const name = target?.displayName ?? `玩家 ${target?.seat}`;
    renderOptions(panel, '杯滿盈溢', `對象：${name}。選擇你獲得的效果。`, [
      { label: '我抽 1 張牌，對方使命 +3', onSelect: () => finishChoice(this, { ...candidate, choice: 'draw' }) },
      {
        label: '我使命 +3，對方抽 1 張牌',
        disabled: !['sheep', 'food', 'money'].some(type => (actor.missionProgress?.[type] ?? 0) < (actor.mission?.[type] ?? 0)),
        onSelect: () => finishChoice(this, { ...candidate, choice: 'mission' }),
      },
    ]);
  };

  panel.dataset.decisionCardId = action.instanceId;
  if (action.glassRandom) showEffects(candidateFor(action.targetPlayerId) ?? action);
  else {
    const targets = choices
      .map(choice => this.state.players.find(player => player.playerId === choice.targetPlayerId))
      .filter(Boolean);
    renderPlayers(
      panel,
      '杯滿盈溢',
      '選擇 1 名其他玩家。',
      targets,
      target => showEffects(candidateFor(target.playerId)),
      target => `火種 ${target.fire} · 手牌 ${target.hand.length}`,
    );
  }
  return { ok: false, pendingChoice: true, state: this.state };
};

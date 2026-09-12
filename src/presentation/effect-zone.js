import { GameController } from '../controller/game-controller.js';
import { getDefinition } from '../game/cards.js';

const originalSafeRender = GameController.prototype.safeRender;
const seatByPlayer = {
  'player-1': '#seat-human',
  'player-2': '#seat-left',
  'player-3': '#seat-top',
  'player-4': '#seat-right',
};

function renderEffectZone(player) {
  const seat = document.querySelector(seatByPlayer[player.playerId]);
  if (!seat) return;
  seat.querySelector('.player-effect-zone')?.remove();
  if (!player.effects?.length) return;

  const zone = document.createElement('div');
  zone.className = 'player-effect-zone';
  zone.setAttribute('aria-label', `Player ${player.seat} 效果區，共 ${player.effects.length} 張`);

  const visible = player.effects.slice(0, 3);
  for (const card of visible) {
    const definition = getDefinition(card);
    const item = document.createElement('button');
    item.className = `effect-mini effect-${definition.kind}`;
    item.type = 'button';
    item.title = `${definition.name}\n${definition.text ?? ''}`;
    item.setAttribute('aria-label', `${definition.name}：${definition.text ?? ''}`);
    item.innerHTML = `<img src="${definition.image}" alt="${definition.name}"><span>${definition.name}</span>${definition.definitionId === 'miracle-02' ? '<b>+3</b>' : ''}`;
    zone.append(item);
  }

  if (player.effects.length > 3) {
    const more = document.createElement('span');
    more.className = 'effect-more';
    more.textContent = `+${player.effects.length - 3}`;
    zone.append(more);
  }
  seat.append(zone);
}

function renderEffectZones(state) {
  if (!state?.players) return;
  state.players.forEach(renderEffectZone);
}

GameController.prototype.safeRender = function patchedSafeRender(available) {
  const rendered = originalSafeRender.call(this, available);
  try {
    renderEffectZones(this.state);
  } catch (error) {
    console.error('[Shepherd] effect-zone render failed; gameplay will continue.', error);
  }
  return rendered;
};

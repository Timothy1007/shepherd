import { GameController } from '../controller/game-controller.js';
import { getDefinition } from '../game/cards.js';

const originalSafeRender = GameController.prototype.safeRender;
const seatByPlayer = {
  'player-1': '#seat-human',
  'player-2': '#seat-left',
  'player-3': '#seat-top',
  'player-4': '#seat-right',
};

function openEffectDetail(card) {
  const definition = getDefinition(card);
  const overlay = document.querySelector('#detail-overlay');
  const content = document.querySelector('#detail-content');
  if (!overlay || !content) return;
  const tags = definition.tags?.length ? definition.tags.join('｜') : '持續效果';
  content.innerHTML = `
    <img class="detail-card-art" src="${definition.image}" alt="${definition.name}">
    <div class="detail-copy">
      <span class="eyebrow">神蹟 · ${tags}</span>
      <h2>${definition.name}</h2>
      <p>此牌目前正在效果區持續生效。</p>
      <p class="detail-note">${definition.text ?? ''}</p>
    </div>`;
  overlay.hidden = false;
}

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
    item.addEventListener('click', (event) => {
      event.preventDefault();
      event.stopPropagation();
      openEffectDetail(card);
    });
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

function renderModifiedCurrentResource(state) {
  const current = state?.currentResource;
  if (!current?.numberBonus) return;
  const tableCard = document.querySelector('#table-card');
  const visual = tableCard?.querySelector('.table-card-visual');
  if (!tableCard || !visual) return;

  let badge = visual.querySelector('.resource-modifier-badge');
  if (!badge) {
    badge = document.createElement('b');
    badge.className = 'resource-modifier-badge';
    visual.append(badge);
  }
  badge.textContent = `+${current.numberBonus}`;

  let effective = tableCard.querySelector('.effective-resource-number');
  if (!effective) {
    effective = document.createElement('span');
    effective.className = 'effective-resource-number';
    tableCard.append(effective);
  }
  effective.textContent = `實際數字 ${current.number}`;
}

GameController.prototype.safeRender = function patchedSafeRender(available) {
  const rendered = originalSafeRender.call(this, available);
  try {
    renderEffectZones(this.state);
    renderModifiedCurrentResource(this.state);
  } catch (error) {
    console.error('[Shepherd] effect-zone render failed; gameplay will continue.', error);
  }
  return rendered;
};

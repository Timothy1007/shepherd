import { GameController } from '../controller/game-controller.js';
import { getDefinition } from '../game/cards.js';
import { getNormalActions } from '../game/game.js';

const originalSafeRender = GameController.prototype.safeRender;
const originalStart = GameController.prototype.start;
const seatByPlayer = {
  'player-1': '#seat-human',
  'player-2': '#seat-left',
  'player-3': '#seat-top',
  'player-4': '#seat-right',
};
const FACE_SCOPE_TAGS = new Set(['應對', '單體', '多體', '群體']);
const FACE_TAG_FILES = Object.freeze({'新生':'Rebirth','流轉':'Flux','洞察':'Insight','庇護':'Sanctuary','共鳴':'Resonance','火種':'Ember','使命':'Mission','重構':'Reforge','掠奪':'Plunder','熄滅':'Extinction','蔓延':'Spread','崩解':'Collapse'});
const presentationImage = definition => { const tag=definition.tags?.find(item=>!FACE_SCOPE_TAGS.has(item)&&FACE_TAG_FILES[item]),file=FACE_TAG_FILES[tag]; if(!file)return definition.image;const response=definition.tags?.includes('應對')?'-Response':'';return definition.kind==='miracle'?`assets/miracles/miracal-${file}${response}.png`:`assets/disasters/disater-${file}${response}.png`; };

// app.js keeps a stable manual-preview seed. Route that seed to the card that
// is currently under manual QA so older fixtures do not keep reappearing.
GameController.prototype.start = function startCurrentManualQa(seed) {
  const qa = new URLSearchParams(globalThis.location?.search ?? '').get('qa');
  const state = originalStart.call(this, seed === 'recovery-preview' ? (qa === 'insect' ? 'insect-preview' : 'victory-preview') : seed);
  if (qa === 'insect' && state?.round === 1) {
    const human = state.players.find(player => player.type === 'human');
    let changed = false;
    for (const definitionId of ['miracle-06', 'miracle-22', 'disaster-09', 'miracle-04']) {
      if (human?.hand.some(card => card.definitionId === definitionId)) continue;
      const index = state.deck.findIndex(card => card.definitionId === definitionId);
      if (index >= 0) { human.hand.push(state.deck.splice(index, 1)[0]); changed = true; }
    }
    if (changed) this.safeRender(getNormalActions(state));
  }
  return state;
};

function openEffectDetail(card) {
  if (typeof globalThis.__shepherdOpenCardInspector === 'function') {
    globalThis.__shepherdOpenCardInspector(card, { inEffectZone: true });
    return;
  }
  const definition = getDefinition(card);
  const overlay = document.querySelector('#detail-overlay');
  const content = document.querySelector('#detail-content');
  if (!overlay || !content) return;
  const tags = definition.tags?.length ? definition.tags.join('｜') : '持續效果';
  const kind = definition.kind === 'disaster' ? '災難' : '神蹟';
  content.innerHTML = `
    <img class="detail-card-art" src="${presentationImage(definition)}" alt="${definition.name}">
    <div class="detail-copy">
      <span class="eyebrow">${kind} · ${tags}${definition.negative ? '｜負面' : ''}</span>
      <h2>${definition.name}</h2>
      <p>此牌目前正在效果區持續生效。</p>
      <p class="detail-note">${definition.effectText ?? definition.text ?? ''}</p>
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
    item.className = `effect-mini effect-${definition.kind}${definition.negative ? ' effect-negative' : ''}`;
    item.type = 'button';
    item.title = `${definition.name}\n${definition.effectText ?? definition.text ?? ''}`;
    item.setAttribute('aria-label', `${definition.name}：${definition.effectText ?? definition.text ?? ''}`);
    const badge = definition.definitionId === 'miracle-02' ? '<b>+3</b>' : definition.definitionId === 'miracle-09' ? '<b>使命+2</b>' : definition.definitionId === 'miracle-13' ? '<b>抽+1</b>' : ['disaster-06','disaster-09'].includes(definition.definitionId) ? '<b>-2</b>' : '';
    item.innerHTML = `<img src="${presentationImage(definition)}" alt="${definition.name}">${globalThis.__shepherdCardFaceGlyph?.(definition)??''}<span class="effect-mini-name">${definition.name}</span>${badge}`;
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
  const modifier = current?.numberModifier ?? current?.numberBonus ?? 0;
  if (!modifier) return;
  const tableCard = document.querySelector('#table-card');
  const visual = tableCard?.querySelector('.table-card-visual');
  if (!tableCard || !visual) return;

  let badge = visual.querySelector('.resource-modifier-badge');
  if (!badge) {
    badge = document.createElement('b');
    badge.className = 'resource-modifier-badge';
    visual.append(badge);
  }
  badge.textContent = modifier > 0 ? `+${modifier}` : `${modifier}`;

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

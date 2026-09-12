import { GameController, aiDelay } from '../controller/game-controller.js';
import { getDefinition, isLegalResourcePlay, RESOURCE_TYPES } from '../game/cards.js';

const names = { sheep: '群羊', food: '糧食', money: '金錢', grace: '恩典', miracle: '神蹟', disaster: '災難' };
const $ = (selector) => document.querySelector(selector);
let actions = [];
let token = null;
let currentState = null;
let selectedInstanceId = null;
let drag = null;
let gracePending = null;
let lastPlayedId = null;

const LONG_PRESS_MS = 480;
const DRAG_THRESHOLD = 6;

function actionsForCard(card) {
  return actions.filter((action) => ['playResource', 'playMiracle'].includes(action.type) && action.instanceId === card.instanceId);
}

function definitionTitle(definition) {
  if (definition.kind === 'miracle' || definition.kind === 'disaster') return definition.name;
  return `${names[definition.type]} ${definition.number}`;
}

function transformedImage(definition, declaredType) {
  if (definition.kind !== 'resource' || definition.type !== 'grace' || !declaredType) return definition.image;
  return `assets/cards/${declaredType}-${definition.number}.png`;
}

function cardStatus(card, state) {
  const definition = getDefinition(card);
  const playableActions = actionsForCard(card);
  const human = state.players[0];
  const humanTurn = state.currentPlayer === human.playerId;
  if (definition.kind === 'miracle') {
    return { definition, ruleLegal: true, playableActions, humanTurn, actionable: playableActions.length > 0 };
  }
  const possible = definition.type === 'grace' ? RESOURCE_TYPES : [definition.type];
  const ruleLegal = possible.some((type) => isLegalResourcePlay(card, state.currentResource, type));
  return { definition, ruleLegal, playableActions, humanTurn, actionable: playableActions.length > 0 };
}

function showToast(message, tone = '') {
  const toast = $('#toast');
  toast.textContent = message;
  toast.className = `toast show ${tone}`;
  clearTimeout(showToast.timer);
  showToast.timer = setTimeout(() => { toast.className = 'toast'; }, 1400);
}

function showDetail(card) {
  const definition = getDefinition(card);
  const playable = actionsForCard(card);
  const legalText = playable.length ? '目前可進入爭局。' : '目前不能打出，但你仍可查看它。';
  const title = definitionTitle(definition);
  const kindLabel = definition.kind === 'resource' ? names[definition.type] : names[definition.kind];
  const effectText = definition.text ?? '物資牌依目前物資與克制規則判定。';
  $('#detail-content').innerHTML = `
    <img class="detail-card-art" src="${definition.image}" alt="${title}">
    <div class="detail-copy">
      <span class="eyebrow">${kindLabel}</span>
      <h2>${title}</h2>
      <p>${legalText}</p>
      <p class="detail-note">${effectText}</p>
    </div>`;
  $('#detail-overlay').hidden = false;
}

function hideDetail() {
  $('#detail-overlay').hidden = true;
}

function closeGraceChoice() {
  gracePending = null;
  $('#grace-overlay').hidden = true;
}

function openGraceChoice(card) {
  const definition = getDefinition(card);
  const playable = actionsForCard(card);
  gracePending = { card, playable };
  const host = $('#grace-choices');
  host.replaceChildren();

  for (const type of RESOURCE_TYPES) {
    const action = playable.find((candidate) => candidate.declaredType === type);
    const button = document.createElement('button');
    button.className = `grace-choice ${action ? 'legal-choice' : 'illegal-choice'}`;
    button.disabled = !action;
    button.innerHTML = `
      <span class="choice-label">${names[type]}</span>
      <span class="choice-card grace-born-preview">
        <img src="assets/cards/${type}-${definition.number}.png" alt="${names[type]} ${definition.number}">
      </span>
      <small>${action ? '可化為此物資' : '目前無法如此化形'}</small>`;
    if (action) button.addEventListener('click', () => {
      closeGraceChoice();
      selectedInstanceId = null;
      controller.act(action, token);
    });
    host.append(button);
  }
  $('#grace-overlay').hidden = false;
}

function playOrChoose(card) {
  const playable = actionsForCard(card);
  const definition = getDefinition(card);
  if (!playable.length) {
    rejectCard(card.instanceId, '這張牌現在無法進入爭局');
    return;
  }
  if (definition.kind === 'resource' && definition.type === 'grace') {
    openGraceChoice(card);
    return;
  }
  selectedInstanceId = null;
  controller.act(playable[0], token);
}

function rejectCard(instanceId, message = '目前無法打出') {
  const el = document.querySelector(`[data-card-id="${CSS.escape(instanceId)}"]`);
  if (el) {
    el.classList.remove('reject');
    void el.offsetWidth;
    el.classList.add('reject');
    setTimeout(() => el.classList.remove('reject'), 520);
  }
  $('#drop-zone').classList.remove('accepting');
  $('#drop-zone').classList.add('rejecting');
  setTimeout(() => $('#drop-zone').classList.remove('rejecting'), 420);
  showToast(message, 'warning');
}

function dropZoneHit(x, y, startY) {
  const rect = $('#drop-zone').getBoundingClientRect();
  const direct = x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom;
  const broadCenter = x > innerWidth * .16 && x < innerWidth * .84 && y > innerHeight * .15 && y < innerHeight * .69;
  const fling = y < startY - 70 && x > innerWidth * .18 && x < innerWidth * .82;
  return direct || broadCenter || fling;
}

function stopLongPress() {
  if (drag?.longPressTimer) clearTimeout(drag.longPressTimer);
}

function beginPointer(card, button, event) {
  if (event.button !== undefined && event.button !== 0) return;
  event.preventDefault();
  const rect = button.getBoundingClientRect();
  drag = {
    card,
    button,
    pointerId: event.pointerId,
    startX: event.clientX,
    startY: event.clientY,
    x: event.clientX,
    y: event.clientY,
    dragging: false,
    longPressed: false,
    ghost: null,
    originRect: rect,
    offsetX: event.clientX - rect.left,
    offsetY: event.clientY - rect.top,
  };
  drag.longPressTimer = setTimeout(() => {
    if (!drag || drag.dragging) return;
    drag.longPressed = true;
    showDetail(card);
    button.classList.add('inspecting');
    if (navigator.vibrate) navigator.vibrate(18);
  }, LONG_PRESS_MS);
}

function startDragging() {
  if (!drag || drag.dragging || drag.longPressed) return;
  drag.dragging = true;
  selectedInstanceId = drag.card.instanceId;
  drag.button.classList.add('drag-source');
  const ghost = drag.button.cloneNode(true);
  ghost.className = `drag-ghost ${actionsForCard(drag.card).length ? 'drag-valid' : 'drag-invalid'}`;
  ghost.style.width = `${drag.originRect.width}px`;
  ghost.style.height = `${drag.originRect.height}px`;
  ghost.style.left = `${drag.originRect.left}px`;
  ghost.style.top = `${drag.originRect.top}px`;
  document.body.append(ghost);
  drag.ghost = ghost;
}

function movePointer(event) {
  if (!drag) return;
  if (drag.pointerId != null && event.pointerId != null && event.pointerId !== drag.pointerId) return;
  event.preventDefault();
  drag.x = event.clientX;
  drag.y = event.clientY;
  const distance = Math.hypot(drag.x - drag.startX, drag.y - drag.startY);
  if (!drag.dragging && distance > DRAG_THRESHOLD) {
    stopLongPress();
    startDragging();
  }
  if (!drag?.dragging || !drag.ghost) return;
  drag.ghost.style.left = `${drag.x - drag.offsetX}px`;
  drag.ghost.style.top = `${drag.y - drag.offsetY}px`;
  const over = dropZoneHit(drag.x, drag.y, drag.startY);
  $('#drop-zone').classList.toggle('accepting', over);
}

function animateGhost(ghost, fromRect, toRect, options = {}) {
  if (!ghost) return Promise.resolve();
  const fromLeft = Number.parseFloat(ghost.style.left) || fromRect.left;
  const fromTop = Number.parseFloat(ghost.style.top) || fromRect.top;
  const toLeft = toRect.left + (toRect.width - fromRect.width) / 2;
  const toTop = toRect.top + (toRect.height - fromRect.height) / 2;
  const animation = ghost.animate([
    { left: `${fromLeft}px`, top: `${fromTop}px`, transform: 'rotate(-2deg) scale(1.1)', opacity: 1 },
    { left: `${toLeft}px`, top: `${toTop}px`, transform: options.reject ? 'rotate(1deg) scale(.96)' : 'rotate(3deg) scale(.76)', opacity: options.reject ? 1 : .08 },
  ], {
    duration: options.reject ? 260 : 160,
    easing: options.reject ? 'cubic-bezier(.22,1.35,.36,1)' : 'cubic-bezier(.2,.8,.2,1)',
    fill: 'forwards',
  });
  return animation.finished.catch(() => {}).then(() => ghost.remove());
}

async function endPointer(event) {
  if (!drag) return;
  if (drag.pointerId != null && event?.pointerId != null && event.pointerId !== drag.pointerId) return;
  event?.preventDefault?.();
  stopLongPress();
  const finished = drag;
  drag = null;
  finished.button.classList.remove('drag-source', 'inspecting');
  $('#drop-zone').classList.remove('accepting');

  if (finished.dragging && finished.ghost) {
    const shouldDrop = dropZoneHit(finished.x, finished.y, finished.startY);
    if (!shouldDrop) {
      await animateGhost(finished.ghost, finished.originRect, finished.originRect, { reject: true });
      selectedInstanceId = null;
      if (currentState) {
        renderHand(currentState);
        renderSelectionActions(currentState);
      }
      return;
    }

    const playable = actionsForCard(finished.card);
    if (!playable.length) {
      await animateGhost(finished.ghost, finished.originRect, finished.originRect, { reject: true });
      rejectCard(finished.card.instanceId, '局勢拒絕了這張牌');
      return;
    }

    const target = $('#drop-zone').getBoundingClientRect();
    await animateGhost(finished.ghost, finished.originRect, target);
    playOrChoose(finished.card);
    return;
  }

  if (finished.longPressed) return;
  selectedInstanceId = selectedInstanceId === finished.card.instanceId ? null : finished.card.instanceId;
  if (currentState) {
    renderHand(currentState);
    renderSelectionActions(currentState);
  }
}

function cardButton(card, state) {
  const { definition, ruleLegal, humanTurn, actionable } = cardStatus(card, state);
  const button = document.createElement('button');
  button.dataset.cardId = card.instanceId;
  button.className = `card ${actionable ? 'legal' : 'illegal'} ${selectedInstanceId === card.instanceId ? 'selected' : ''}`;
  const statusText = actionable ? '可出牌' : (ruleLegal && !humanTurn ? '等待你的行動' : '目前不可出');
  const title = definitionTitle(definition);
  const fallbackMain = definition.kind === 'resource' ? definition.number : '✦';
  const fallbackType = definition.kind === 'resource' ? names[definition.type] : names[definition.kind];
  button.setAttribute('aria-label', `${title}，${statusText}`);
  button.innerHTML = `<img class="card-art" draggable="false" src="${definition.image}" alt="${title}"><span class="card-fallback"><span class="number">${fallbackMain}</span><span class="type">${fallbackType}</span></span><small>${statusText}</small>`;
  button.querySelector('.card-art').addEventListener('error', () => button.classList.add('image-missing'), { once: true });
  button.addEventListener('pointerdown', (event) => beginPointer(card, button, event));
  return button;
}

function renderHand(state) {
  const human = state.players[0];
  if (!human.hand.some((card) => card.instanceId === selectedInstanceId)) selectedInstanceId = null;
  $('#hand').replaceChildren(...human.hand.map((card) => cardButton(card, state)));
}

function renderSelectionActions(state) {
  const host = $('#selection-actions');
  host.replaceChildren();
  if (!selectedInstanceId) return;
  const card = state.players[0].hand.find((candidate) => candidate.instanceId === selectedInstanceId);
  if (!card) return;
  const definition = getDefinition(card);
  const playable = actionsForCard(card);
  if (!playable.length) {
    const button = document.createElement('button');
    button.textContent = state.currentPlayer === state.players[0].playerId ? '目前不可打出' : '等待你的行動';
    button.disabled = true;
    host.append(button);
  } else {
    const button = document.createElement('button');
    button.className = 'primary';
    if (definition.kind === 'miracle') button.textContent = `打出神蹟 · ${definition.name}`;
    else button.textContent = definition.type === 'grace' ? '選擇恩典化形' : `打出 ${names[definition.type]} ${definition.number}`;
    button.addEventListener('click', () => playOrChoose(card));
    host.append(button);
  }
  const inspect = document.createElement('button');
  inspect.textContent = '查看卡牌';
  inspect.addEventListener('click', () => showDetail(card));
  host.append(inspect);
  const cancel = document.createElement('button');
  cancel.textContent = '放回手牌';
  cancel.addEventListener('click', () => {
    selectedInstanceId = null;
    renderHand(state);
    renderSelectionActions(state);
  });
  host.append(cancel);
}

function renderCurrentCard(state) {
  const currentPlayed = state.playedArea.at(-1);
  const tableCard = $('#table-card');
  if (!currentPlayed) {
    tableCard.innerHTML = '<span>等待本輪第一張牌</span>';
    tableCard.classList.remove('has-card', 'grace-born');
    return;
  }
  const definition = getDefinition(currentPlayed);
  if (definition.kind === 'miracle' || definition.kind === 'disaster') {
    const title = definitionTitle(definition);
    tableCard.innerHTML = `<span class="table-card-visual"><img src="${definition.image}" alt="目前出牌：${title}"></span><span>${title}</span>`;
    tableCard.classList.add('has-card');
    tableCard.classList.remove('grace-born');
  } else {
    const effectiveType = definition.type === 'grace' ? currentPlayed.declaredType : definition.type;
    const graceBorn = definition.type === 'grace' && effectiveType;
    const image = transformedImage(definition, effectiveType);
    tableCard.innerHTML = `<span class="table-card-visual"><img src="${image}" alt="目前出牌：${names[effectiveType]} ${definition.number}"></span><span>${names[effectiveType]} ${definition.number}</span>`;
    tableCard.classList.add('has-card');
    tableCard.classList.toggle('grace-born', graceBorn);
  }
  if (currentPlayed.instanceId !== lastPlayedId) {
    lastPlayedId = currentPlayed.instanceId;
    tableCard.classList.remove('impact');
    void tableCard.offsetWidth;
    tableCard.classList.add('impact');
  }
}

function playerMarkup(player) {
  return `<div class="name">Player ${player.seat} · ${player.type === 'human' ? '牧羊人' : 'AI'}</div><div class="stats">🔥 ${player.fire}　手牌 ${player.hand.length}　低溫火種 ${player.lowTemperatureFire}</div>`;
}

function renderSeats(state) {
  const human = state.players[0];
  const opponents = state.players.slice(1);
  const targets = ['#seat-left', '#seat-top', '#seat-right'];
  opponents.forEach((player, index) => {
    const el = $(targets[index]);
    el.className = `seat ${targets[index].slice(1)} ${state.currentPlayer === player.playerId ? 'current' : ''} ${player.hasNormalAction ? '' : 'inactive'}`;
    el.innerHTML = playerMarkup(player);
  });
  const humanEl = $('#seat-human');
  humanEl.className = `human-meta ${state.currentPlayer === human.playerId ? 'current' : ''}`;
  humanEl.innerHTML = playerMarkup(human);
}

function render(state, nextActions, nextToken) {
  currentState = state;
  actions = nextActions;
  token = nextToken;
  const human = state.players[0];
  const resource = state.currentResource ? `${names[state.currentResource.type]} ${state.currentResource.number}` : '自由出牌';
  $('#round-display').textContent = `第 ${state.round} / 7 輪`;
  $('#status').innerHTML = `<span>牌庫 <strong>${state.deck.length}</strong></span><span>棄牌 <strong>${state.discardPile.length}</strong></span><span>場內 <strong>${state.playedArea.length}</strong></span><span>目前物資 <strong>${resource}</strong></span>`;
  $('#apostle-display').textContent = `使徒 · ${state.currentApostle ?? '尚無'}`;
  renderCurrentCard(state);
  renderSeats(state);
  renderHand(state);
  renderSelectionActions(state);
  const redrawAction = nextActions.find((action) => action.type === 'redraw');
  const endAction = nextActions.find((action) => action.type === 'endParticipation');
  if (state.gameOver) $('#instruction').textContent = `爭局結束 · 勝者 ${state.winner.join('、')}`;
  else if (redrawAction && state.currentPlayer === human.playerId) $('#instruction').textContent = '沒有可出的牌。你本輪仍有一次重新整備手牌的機會。';
  else if (endAction && state.currentPlayer === human.playerId) $('#instruction').textContent = '重整後仍沒有可出的牌。你將退出本輪爭局。';
  else if (state.currentPlayer === human.playerId) $('#instruction').textContent = '輪到你。短按拿牌、長按查看、直接把牌拖向中央。';
  else $('#instruction').textContent = `等待 ${state.currentPlayer} 行動…`;
  $('#actions').replaceChildren();
  if (!state.gameOver && state.currentPlayer === human.playerId) renderHumanActions(state);
  $('#round-result').textContent = state.lastRoundResult ? `第 ${state.lastRoundResult.round} 輪 · 使徒 ${state.lastRoundResult.apostle ?? '無'} · +${state.lastRoundResult.reward} 火種` : '火種仍在等待新的使徒';
}

function actionButton(label, action, emphasized = false) {
  const button = document.createElement('button');
  button.textContent = label;
  if (emphasized) button.classList.add('primary-action');
  button.addEventListener('click', () => {
    selectedInstanceId = null;
    if (action.type === 'redraw') {
      $('#hand').classList.add('redealing');
      setTimeout(() => controller.act(action, token), 230);
    } else controller.act(action, token);
  });
  $('#actions').append(button);
}

function renderHumanActions() {
  const automatic = actions.find((action) => !['playResource', 'playMiracle'].includes(action.type));
  if (!automatic) return;
  const labels = {
    redraw: '重新整備手牌',
    endParticipation: '退出本輪爭局',
    endEmptyHand: '空手：結束參與',
  };
  actionButton(labels[automatic.type], automatic, automatic.type === 'redraw');
}

window.addEventListener('pointermove', movePointer, { passive: false });
window.addEventListener('pointerup', endPointer, { passive: false });
window.addEventListener('pointercancel', endPointer, { passive: false });

window.addEventListener('keydown', (event) => {
  if (event.key !== 'Escape') return;
  if (!$('#grace-overlay').hidden) return closeGraceChoice();
  if (!$('#detail-overlay').hidden) return hideDetail();
  if (selectedInstanceId && currentState) {
    selectedInstanceId = null;
    renderHand(currentState);
    renderSelectionActions(currentState);
  }
});

document.querySelectorAll('[data-close="detail"]').forEach((element) => element.addEventListener('click', hideDetail));
$('#grace-cancel').addEventListener('click', () => {
  closeGraceChoice();
  if (currentState) renderSelectionActions(currentState);
});

const controller = new GameController({ render, delay: aiDelay });
$('#restart').addEventListener('click', () => {
  selectedInstanceId = null;
  closeGraceChoice();
  hideDetail();
  controller.restart($('#restart').dataset.seed = `${Date.now()}`);
});
controller.start('recovery-preview');
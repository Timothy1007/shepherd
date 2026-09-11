import { GameController, aiDelay } from '../controller/game-controller.js';
import { getDefinition, isLegalResourcePlay, RESOURCE_TYPES } from '../game/cards.js';

const names = { sheep: '群羊', food: '糧食', money: '金錢', grace: '恩典' };
const $ = (selector) => document.querySelector(selector);
let actions = [];
let token = null;
let selectedInstanceId = null;

function actionsForCard(card) {
  return actions.filter((action) => action.type === 'playResource' && action.instanceId === card.instanceId);
}

function cardButton(card, state) {
  const definition = getDefinition(card);
  const possible = definition.type === 'grace' ? RESOURCE_TYPES : [definition.type];
  const ruleLegal = possible.some((type) => isLegalResourcePlay(card, state.currentResource, type));
  const playableActions = actionsForCard(card);
  const actionable = playableActions.length > 0;
  const human = state.players[0];
  const humanTurn = state.currentPlayer === human.playerId;
  const button = document.createElement('button');
  button.className = `card ${actionable ? 'legal' : 'illegal'} ${selectedInstanceId === card.instanceId ? 'selected' : ''}`;
  const statusText = actionable ? '可出牌' : (ruleLegal && !humanTurn ? '等待你的回合' : '目前不可出');
  button.setAttribute('aria-label', `${names[definition.type]} ${definition.number}，${statusText}`);
  button.innerHTML = `<img class="card-art" src="${definition.image}" alt="${names[definition.type]} ${definition.number}"><span class="card-fallback"><span class="number">${definition.number}</span><span class="type">${names[definition.type]}</span></span><small>${statusText}</small>`;
  button.querySelector('.card-art').addEventListener('error', () => button.classList.add('image-missing'), { once: true });
  button.addEventListener('click', () => {
    selectedInstanceId = selectedInstanceId === card.instanceId ? null : card.instanceId;
    renderHand(state);
    renderSelectionActions(state);
  });
  button.addEventListener('dblclick', () => {
    if (playableActions.length === 1) controller.act(playableActions[0], token);
  });
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
    button.textContent = state.currentPlayer === state.players[0].playerId ? '目前不可打出' : '等待你的回合';
    button.disabled = true;
    host.append(button);
    return;
  }
  for (const action of playable) {
    const button = document.createElement('button');
    button.className = 'primary';
    const suffix = definition.type === 'grace' ? ` · 作為${names[action.declaredType]}` : '';
    button.textContent = `打出 ${names[definition.type]} ${definition.number}${suffix}`;
    button.addEventListener('click', () => {
      selectedInstanceId = null;
      controller.act(action, token);
    });
    host.append(button);
  }
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
    tableCard.innerHTML = '<span>等待本輪第一張物資牌</span>';
    tableCard.classList.remove('has-card');
    return;
  }
  const definition = getDefinition(currentPlayed);
  tableCard.innerHTML = `<img src="${definition.image}" alt="目前出牌：${names[definition.type]} ${definition.number}"><span>${names[definition.type]} ${definition.number}</span>`;
  tableCard.classList.add('has-card');
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
  $('#instruction').textContent = state.gameOver ? `爭局結束 · 勝者 ${state.winner.join('、')}` : state.currentPlayer === human.playerId ? '輪到你。選擇一張牌，將它拿起。' : `等待 ${state.currentPlayer} 行動…`;
  $('#actions').replaceChildren();
  if (!state.gameOver && state.currentPlayer === human.playerId) renderHumanActions(state);
  $('#round-result').textContent = state.lastRoundResult ? `第 ${state.lastRoundResult.round} 輪 · 使徒 ${state.lastRoundResult.apostle ?? '無'} · +${state.lastRoundResult.reward} 火種` : '火種仍在等待新的使徒';
}

function actionButton(label, action) {
  const button = document.createElement('button');
  button.textContent = label;
  button.addEventListener('click', () => {
    selectedInstanceId = null;
    controller.act(action, token);
  });
  $('#actions').append(button);
}

function renderHumanActions() {
  const automatic = actions.find((action) => action.type !== 'playResource');
  if (automatic) actionButton({ redraw: '重洗手牌', endParticipation: '結束本輪參與', endEmptyHand: '空手：結束參與' }[automatic.type], automatic);
}

window.addEventListener('keydown', (event) => {
  if (event.key === 'Escape' && selectedInstanceId) {
    selectedInstanceId = null;
    const state = controller.getState?.();
    if (state) {
      renderHand(state);
      renderSelectionActions(state);
    }
  }
});

const controller = new GameController({ render, delay: aiDelay });
$('#restart').addEventListener('click', () => {
  selectedInstanceId = null;
  controller.restart($('#restart').dataset.seed = `${Date.now()}`);
});
controller.start('recovery-preview');

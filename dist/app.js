import { GameController, aiDelay } from '../controller/game-controller.js';
import { getDefinition, isLegalResourcePlay, RESOURCE_TYPES } from '../game/cards.js';

const names = { sheep: '群羊', food: '糧食', money: '金錢', grace: '恩典' };
const $ = (selector) => document.querySelector(selector);
let actions = [];
let token = null;

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
  button.className = `card ${actionable ? 'legal' : 'illegal'}`;
  const statusText = actionable ? '可出牌' : (ruleLegal && !humanTurn ? '等待你的回合' : '目前不可出');
  button.setAttribute('aria-label', `${names[definition.type]} ${definition.number}，${statusText}`);
  button.innerHTML = `<img class="card-art" src="${definition.image}" alt="${names[definition.type]} ${definition.number}"><span class="card-fallback"><span class="number">${definition.number}</span><span class="type">${names[definition.type]}</span></span><small>${statusText}</small>`;
  button.querySelector('.card-art').addEventListener('error', () => button.classList.add('image-missing'), { once: true });
  button.addEventListener('click', () => showCard(card, { ruleLegal, humanTurn }));
  return button;
}

function showCard(card, { ruleLegal, humanTurn }) {
  const definition = getDefinition(card);
  const playableActions = actionsForCard(card);
  const statusText = playableActions.length
    ? '此牌目前可以打出。'
    : (ruleLegal && !humanTurn ? '牌型合法，但目前不是你的回合。' : '此牌目前不可打出，但仍可查看。');
  $('#card-detail').innerHTML = `<img class="detail-art" src="${definition.image}" alt="${names[definition.type]} ${definition.number}"><h2>${names[definition.type]} ${definition.number}</h2><p>實體牌：${card.instanceId}</p><p>${statusText}</p><div class="detail-actions" id="detail-actions"></div>`;
  const detailActions = $('#detail-actions');
  for (const action of playableActions) {
    const button = document.createElement('button');
    const suffix = definition.type === 'grace' ? `作為${names[action.declaredType]}` : '';
    button.textContent = `出牌${suffix ? `：${suffix}` : ''}`;
    button.addEventListener('click', () => {
      $('#card-dialog').close();
      controller.act(action, token);
    });
    detailActions.append(button);
  }
  if (!playableActions.length) {
    const note = document.createElement('span');
    note.className = 'detail-action-note';
    note.textContent = ruleLegal && !humanTurn ? '請等待輪到 Player 1。' : '此牌目前不可出。';
    detailActions.append(note);
  }
  $('#card-dialog').showModal();
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

function render(state, nextActions, nextToken) {
  actions = nextActions;
  token = nextToken;
  const resource = state.currentResource ? `${names[state.currentResource.type]} ${state.currentResource.number}` : '自由出牌';
  $('#status').innerHTML = `<span>輪次 <strong>${state.round}/7</strong></span><span>牌庫 <strong>${state.deck.length}</strong></span><span>棄牌 <strong>${state.discardPile.length}</strong></span><span>已出牌 <strong>${state.playedArea.length}</strong></span><span>目前物資 <strong>${resource}</strong></span><span>使徒 <strong>${state.currentApostle ?? '尚無'}</strong></span>`;
  renderCurrentCard(state);
  $('#players').replaceChildren(...state.players.map((player) => {
    const element = document.createElement('article');
    element.className = `player ${state.currentPlayer === player.playerId ? 'current' : ''} ${player.hasNormalAction ? '' : 'inactive'}`;
    element.innerHTML = `<strong>Player ${player.seat} · ${player.type === 'human' ? '真人' : 'AI'}</strong><div>手牌 ${player.hand.length}</div><div>火種 ${player.fire}／低溫火種 ${player.lowTemperatureFire}</div><div>連敗 ${player.losingStreak}／行動 ${player.hasNormalAction ? '有' : '無'}</div>`;
    return element;
  }));
  const human = state.players[0];
  $('#hand').replaceChildren(...human.hand.map((card) => cardButton(card, state)));
  $('#instruction').textContent = state.gameOver ? `勝者：${state.winner.join('、')}` : state.currentPlayer === human.playerId ? '輪到你' : `等待 ${state.currentPlayer} 行動…`;
  $('#actions').replaceChildren();
  if (!state.gameOver && state.currentPlayer === human.playerId) renderHumanActions(state);
  $('#round-result').textContent = state.lastRoundResult ? `第 ${state.lastRoundResult.round} 輪：使徒 ${state.lastRoundResult.apostle ?? '無'}，獲得 ${state.lastRoundResult.reward} 火種。` : '尚未結算';
}

function actionButton(label, action) {
  const button = document.createElement('button');
  button.textContent = label;
  button.addEventListener('click', () => controller.act(action, token));
  $('#actions').append(button);
}

function renderHumanActions(state) {
  const plays = actions.filter((action) => action.type === 'playResource');
  for (const action of plays) {
    const card = state.players[0].hand.find((candidate) => candidate.instanceId === action.instanceId);
    const definition = getDefinition(card);
    const suffix = definition.type === 'grace' ? ` → ${names[action.declaredType]}` : '';
    actionButton(`打出 ${names[definition.type]} ${definition.number}${suffix}`, action);
  }
  const automatic = actions.find((action) => action.type !== 'playResource');
  if (automatic) actionButton({ redraw: '重洗手牌', endParticipation: '結束本輪參與', endEmptyHand: '空手：自動結束參與' }[automatic.type], automatic);
}

const controller = new GameController({ render, delay: aiDelay });
$('#restart').addEventListener('click', () => controller.restart($('#restart').dataset.seed = `${Date.now()}`));
$('#card-dialog .close').addEventListener('click', () => $('#card-dialog').close());
controller.start('recovery-preview');

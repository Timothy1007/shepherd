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
  const ruleLegal = definition.kind === 'miracle' || definition.kind === 'disaster' || isLegalResourcePlay(state, card, playableActions[0]?.declaredType);
  return { definition, playableActions, humanTurn, ruleLegal, actionable: playableActions.length > 0 && humanTurn };
}

function showDetail(card) {
  const definition = getDefinition(card);
  const host = $('#detail-content');
  const title = definitionTitle(definition);
  const kind = definition.kind === 'resource' ? names[definition.type] : names[definition.kind];
  host.innerHTML = `<img class="detail-card-art" src="${definition.image}" alt="${title}"><div class="detail-copy"><span class="eyebrow">${kind}</span><h2>${title}</h2><p>${definition.text ?? '物資牌依目前物資與克制規則判定。'}</p></div>`;
  $('#detail-overlay').hidden = false;
}

function hideDetail() { $('#detail-overlay').hidden = true; }
function closeGraceChoice() { gracePending = null; $('#grace-overlay').hidden = true; }

function rejectCard(instanceId, message) {
  const button = document.querySelector(`[data-card-id="${instanceId}"]`);
  if (button) { button.classList.remove('rejected'); void button.offsetWidth; button.classList.add('rejected'); }
  const toast = $('#toast'); toast.textContent = message; toast.classList.add('show'); setTimeout(() => toast.classList.remove('show'), 1100);
}

function playOrChoose(card) {
  const definition = getDefinition(card);
  const candidates = actionsForCard(card);
  if (!candidates.length) return rejectCard(card.instanceId, '目前不能打出這張牌');
  if (definition.kind === 'resource' && definition.type === 'grace') {
    gracePending = { card, candidates };
    $('#grace-choices').replaceChildren(...RESOURCE_TYPES.map((type) => {
      const action = candidates.find((candidate) => candidate.declaredType === type);
      const button = document.createElement('button');
      button.className = 'grace-choice'; button.disabled = !action;
      button.innerHTML = `<img src="${transformedImage(definition, type)}" alt="${names[type]} ${definition.number}"><strong>${names[type]}</strong><span>${action ? '可化形' : '目前不可化形'}</span>`;
      if (action) button.addEventListener('click', () => { closeGraceChoice(); selectedInstanceId = null; controller.act(action, token); });
      return button;
    }));
    $('#grace-overlay').hidden = false; return;
  }
  selectedInstanceId = null; controller.act(candidates[0], token);
}

function beginPointer(card, button, event) {
  if (event.button !== 0) return;
  const rect = button.getBoundingClientRect();
  drag = { card, button, pointerId:event.pointerId, startX:event.clientX, startY:event.clientY, lastX:event.clientX, lastY:event.clientY, startTime:performance.now(), originRect:rect, dragging:false, longPressed:false, ghost:null };
  button.setPointerCapture?.(event.pointerId);
  drag.longPressTimer = setTimeout(() => { if (!drag || drag.dragging) return; drag.longPressed=true; showDetail(card); }, LONG_PRESS_MS);
}

function makeGhost(button, rect) {
  const ghost = button.cloneNode(true); ghost.classList.add('drag-ghost'); ghost.style.width=`${rect.width}px`; ghost.style.height=`${rect.height}px`; document.body.append(ghost); return ghost;
}

function moveGhost(ghost,x,y,rect,rotation=0){ ghost.style.transform=`translate3d(${x-rect.width/2}px,${y-rect.height/2}px,0) rotate(${rotation}deg)`; }
function movePointer(event){ if(!drag||drag.pointerId!==event.pointerId)return; drag.lastX=event.clientX;drag.lastY=event.clientY;const dx=event.clientX-drag.startX,dy=event.clientY-drag.startY;if(!drag.dragging&&Math.hypot(dx,dy)>DRAG_THRESHOLD){clearTimeout(drag.longPressTimer);drag.dragging=true;drag.ghost=makeGhost(drag.button,drag.originRect);drag.button.classList.add('drag-source');}if(drag.dragging){event.preventDefault();moveGhost(drag.ghost,event.clientX,event.clientY,drag.originRect,Math.max(-10,Math.min(10,dx/18)));}}

function rectCenter(rect){return{x:rect.left+rect.width/2,y:rect.top+rect.height/2};}
function animateGhost(ghost,fromRect,toRect,{reject=false}={}){return new Promise(resolve=>{if(!ghost)return resolve();const from=rectCenter(fromRect),to=rectCenter(toRect);ghost.style.transition='transform .24s cubic-bezier(.2,.75,.3,1), opacity .24s';requestAnimationFrame(()=>{moveGhost(ghost,to.x,to.y,fromRect,reject?-5:4);ghost.style.opacity=reject?'1':'.2';});setTimeout(()=>{ghost.remove();resolve();},250);});}

async function endPointer(event){if(!drag||drag.pointerId!==event.pointerId)return;const finished=drag;drag=null;clearTimeout(finished.longPressTimer);finished.button.classList.remove('drag-source');finished.button.releasePointerCapture?.(event.pointerId);if(finished.dragging){const drop=$('#drop-zone').getBoundingClientRect();const inside=event.clientX>=drop.left&&event.clientX<=drop.right&&event.clientY>=drop.top&&event.clientY<=drop.bottom;const candidates=actionsForCard(finished.card);if(!inside||!candidates.length){await animateGhost(finished.ghost,finished.originRect,finished.originRect,{reject:true});rejectCard(finished.card.instanceId,'局勢拒絕了這張牌');return;}const target=$('#drop-zone').getBoundingClientRect();await animateGhost(finished.ghost,finished.originRect,target);playOrChoose(finished.card);return;}if(finished.longPressed)return;selectedInstanceId=selectedInstanceId===finished.card.instanceId?null:finished.card.instanceId;if(currentState){renderHand(currentState);renderSelectionActions(currentState);}}

function cardButton(card,state){const{definition,ruleLegal,humanTurn,actionable}=cardStatus(card,state);const button=document.createElement('button');button.dataset.cardId=card.instanceId;button.className=`card ${actionable?'legal':'illegal'} ${selectedInstanceId===card.instanceId?'selected':''}`;const statusText=actionable?'可出牌':(ruleLegal&&!humanTurn?'等待你的行動':'目前不可出');const title=definitionTitle(definition);const fallbackMain=definition.kind==='resource'?definition.number:'✦';const fallbackType=definition.kind==='resource'?names[definition.type]:names[definition.kind];button.setAttribute('aria-label',`${title}，${statusText}`);button.innerHTML=`<img class="card-art" draggable="false" src="${definition.image}" alt="${title}"><span class="card-fallback"><span class="number">${fallbackMain}</span><span class="type">${fallbackType}</span></span><small>${statusText}</small>`;button.querySelector('.card-art').addEventListener('error',()=>button.classList.add('image-missing'),{once:true});button.addEventListener('pointerdown',event=>beginPointer(card,button,event));return button;}
function renderHand(state){const human=state.players[0];if(!human.hand.some(card=>card.instanceId===selectedInstanceId))selectedInstanceId=null;$('#hand').replaceChildren(...human.hand.map(card=>cardButton(card,state)));}
function renderSelectionActions(state){const host=$('#selection-actions');host.replaceChildren();if(!selectedInstanceId)return;const card=state.players[0].hand.find(candidate=>candidate.instanceId===selectedInstanceId);if(!card)return;const definition=getDefinition(card);const playable=actionsForCard(card);if(!playable.length){const button=document.createElement('button');button.textContent=state.currentPlayer===state.players[0].playerId?'目前不可打出':'等待你的行動';button.disabled=true;host.append(button);}else{const button=document.createElement('button');button.className='primary';button.textContent=definition.kind==='miracle'?`打出神蹟 · ${definition.name}`:(definition.type==='grace'?'選擇恩典化形':`打出 ${names[definition.type]} ${definition.number}`);button.addEventListener('click',()=>playOrChoose(card));host.append(button);}const inspect=document.createElement('button');inspect.textContent='查看卡牌';inspect.addEventListener('click',()=>showDetail(card));host.append(inspect);const cancel=document.createElement('button');cancel.textContent='放回手牌';cancel.addEventListener('click',()=>{selectedInstanceId=null;renderHand(state);renderSelectionActions(state);});host.append(cancel);}
function renderCurrentCard(state){const currentPlayed=state.playedArea.at(-1),tableCard=$('#table-card');if(!currentPlayed){tableCard.innerHTML='<span>等待本輪第一張牌</span>';tableCard.classList.remove('has-card','grace-born');return;}const definition=getDefinition(currentPlayed);if(definition.kind==='miracle'||definition.kind==='disaster'){const title=definitionTitle(definition);tableCard.innerHTML=`<span class="table-card-visual"><img src="${definition.image}" alt="目前出牌：${title}"></span><span>${title}</span>`;tableCard.classList.add('has-card');tableCard.classList.remove('grace-born');}else{const effectiveType=definition.type==='grace'?currentPlayed.declaredType:definition.type;const graceBorn=definition.type==='grace'&&effectiveType;const image=transformedImage(definition,effectiveType);tableCard.innerHTML=`<span class="table-card-visual"><img src="${image}" alt="目前出牌：${names[effectiveType]} ${definition.number}"></span><span>${names[effectiveType]} ${definition.number}</span>`;tableCard.classList.add('has-card');tableCard.classList.toggle('grace-born',graceBorn);}if(currentPlayed.instanceId!==lastPlayedId){lastPlayedId=currentPlayed.instanceId;tableCard.classList.remove('impact');void tableCard.offsetWidth;tableCard.classList.add('impact');}}
function playerMarkup(player){return `<div class="name">Player ${player.seat} · ${player.type==='human'?'牧羊人':'AI'}</div><div class="stats">🔥 ${player.fire}　手牌 ${player.hand.length}　低溫火種 ${player.lowTemperatureFire}</div>`;}
function renderSeats(state){const human=state.players[0],opponents=state.players.slice(1),targets=['#seat-left','#seat-top','#seat-right'];opponents.forEach((player,index)=>{const el=$(targets[index]);el.className=`seat ${targets[index].slice(1)} ${state.currentPlayer===player.playerId?'current':''} ${player.hasNormalAction?'':'inactive'}`;el.innerHTML=playerMarkup(player);});const humanEl=$('#seat-human');humanEl.className=`human-meta ${state.currentPlayer===human.playerId?'current':''}`;humanEl.innerHTML=playerMarkup(human);}

function render(state,nextActions,nextToken){currentState=state;actions=nextActions;token=nextToken;window.dispatchEvent(new CustomEvent('shepherd:state',{detail:{state}}));const human=state.players[0];const resource=state.currentResource?`${names[state.currentResource.type]} ${state.currentResource.number}`:'自由出牌';$('#round-display').textContent=`第 ${state.round} / 7 輪`;$('#status').innerHTML=`<span>牌庫 <strong>${state.deck.length}</strong></span><span>棄牌 <strong>${state.discardPile.length}</strong></span><span>場內 <strong>${state.playedArea.length}</strong></span><span>目前物資 <strong>${resource}</strong></span>`;$('#apostle-display').textContent=`使徒 · ${state.currentApostle??'尚無'}`;renderCurrentCard(state);renderSeats(state);renderHand(state);renderSelectionActions(state);const redrawAction=nextActions.find(action=>action.type==='redraw');const endAction=nextActions.find(action=>action.type==='endParticipation');if(state.gameOver)$('#instruction').textContent=`爭局結束 · 勝者 ${state.winner.join('、')}`;else if(redrawAction&&state.currentPlayer===human.playerId)$('#instruction').textContent='沒有可出的牌。你本輪仍有一次重新整備手牌的機會。';else if(endAction&&state.currentPlayer===human.playerId)$('#instruction').textContent='重整後仍沒有可出的牌。你將退出本輪爭局。';else if(state.currentPlayer===human.playerId)$('#instruction').textContent='輪到你。短按拿牌、長按查看、直接把牌拖向中央。';else $('#instruction').textContent=`等待 ${state.currentPlayer} 行動…`;$('#actions').replaceChildren();if(!state.gameOver&&state.currentPlayer===human.playerId)renderHumanActions(state);$('#round-result').textContent=state.lastRoundResult?`第 ${state.lastRoundResult.round} 輪 · 使徒 ${state.lastRoundResult.apostle??'無'} · +${state.lastRoundResult.reward} 火種`:'火種仍在等待新的使徒';}
function actionButton(label,action,emphasized=false){const button=document.createElement('button');button.textContent=label;if(emphasized)button.classList.add('primary-action');button.addEventListener('click',()=>{selectedInstanceId=null;if(action.type==='redraw'){$('#hand').classList.add('redealing');setTimeout(()=>controller.act(action,token),230);}else controller.act(action,token);});$('#actions').append(button);}
function renderHumanActions(){const automatic=actions.find(action=>!['playResource','playMiracle'].includes(action.type));if(!automatic)return;const labels={redraw:'重新整備手牌',endParticipation:'退出本輪爭局',endEmptyHand:'空手：結束參與'};actionButton(labels[automatic.type],automatic,automatic.type==='redraw');}
window.addEventListener('pointermove',movePointer,{passive:false});window.addEventListener('pointerup',endPointer,{passive:false});window.addEventListener('pointercancel',endPointer,{passive:false});
window.addEventListener('keydown',event=>{if(event.key!=='Escape')return;if(!$('#grace-overlay').hidden)return closeGraceChoice();if(!$('#detail-overlay').hidden)return hideDetail();if(selectedInstanceId&&currentState){selectedInstanceId=null;renderHand(currentState);renderSelectionActions(currentState);}});
document.querySelectorAll('[data-close="detail"]').forEach(element=>element.addEventListener('click',hideDetail));$('#grace-cancel').addEventListener('click',()=>{closeGraceChoice();if(currentState)renderSelectionActions(currentState);});
const controller=new GameController({render,delay:aiDelay});$('#restart').addEventListener('click',()=>{selectedInstanceId=null;closeGraceChoice();hideDetail();controller.restart($('#restart').dataset.seed=`${Date.now()}`);});controller.start('recovery-preview');
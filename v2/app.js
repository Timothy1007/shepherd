import {
  createPlayableGame, legalActions, playCard, prepare, skipNoLegalAction,
  runAiTurn, resolveAuction, activeJudgments, CHARACTER_POOL, RESOURCE_TYPES, LABEL
} from './src/playable/game.js';

const $=s=>document.querySelector(s);
let state=null,aiTimer=null,forcedSkipTimer=null,audioStarted=false;

const el={
  status:$('#status'), round:$('#round-display'), instruction:$('#instruction'),
  top:$('#seat-top'), left:$('#seat-left'), right:$('#seat-right'), human:$('#seat-human'),
  hand:$('#hand'), actions:$('#actions'), selectionActions:$('#selection-actions'),
  pile:$('#played-pile'), currentLabel:$('#current-label'), apostle:$('#apostle-display'),
  roundResult:$('#round-result'), toast:$('#toast'), bgm:$('#bgm'),
  detailOverlay:$('#detail-overlay'), detail:$('#detail-content'),
  decisionOverlay:$('#decision-overlay'), decision:$('#decision-content'),
  historyOverlay:$('#history-overlay'), historyGrid:$('#played-history-grid')
};

function esc(s=''){return String(s).replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[m]));}
function toast(msg){el.toast.textContent=msg;el.toast.classList.add('show');setTimeout(()=>el.toast.classList.remove('show'),1400);}
function pct(n,d){return d?Math.min(100,Math.round(n/d*100)):0;}
function playerById(id){return state?.players?.find(p=>p.id===id)||null;}
function cardById(id){return state?.players?.[0]?.hand?.find(c=>c.id===id)||null;}
function isBlind(){return activeJudgments(state).find(j=>j.id==='blindness-1'||j.id==='blindness-2');}
function playableIds(){return new Set(legalActions(state,'p1').filter(a=>a.type==='play').map(a=>a.cardId));}

function missionHtml(p){
  if(!p.mission)return '<div class="seat-char">最終輪 · 無一般使命</div>';
  return `<div class="mission-row">${RESOURCE_TYPES.map(t=>`
    <div class="mission-bit">${LABEL[t]} ${p.missionProgress[t]}/${p.mission[t]}
      <div class="mission-bar"><i style="width:${pct(p.missionProgress[t],p.mission[t])}%"></i></div>
    </div>`).join('')}</div>`;
}
function seatHtml(p){
  const current=state.currentPlayer===p.id;
  return `<span class="name">${esc(p.name)}</span>
    <div class="stats">🔥 ${p.fire}　🃏 ${p.hand.length}　使命 ${p.completedMissions}${p.qualified?' ✓':''}</div>
    <div class="seat-char">${esc(p.character?.faction||'')} · ${esc(p.character?.name||'')}</div>
    ${missionHtml(p)}
    <div class="seat-foot">${p.blessings.map(b=>`<span class="mini-chip">${esc(b.name)}</span>`).join('')}${p.effects.map(e=>`<span class="mini-chip">${esc(e.id)}</span>`).join('')}</div>`;
}
function renderSeats(){
  const [human,p2,p3,p4]=state.players;
  el.human.innerHTML=seatHtml(human);el.top.innerHTML=seatHtml(p2);el.left.innerHTML=seatHtml(p3);el.right.innerHTML=seatHtml(p4);
  for(const [node,p] of [[el.human,human],[el.top,p2],[el.left,p3],[el.right,p4]])node.classList.toggle('current',state.currentPlayer===p.id);
}

function renderStatus(){
  const judgments=activeJudgments(state);
  const phase=state.gameOver?'爭局結束':state.phase==='auction'?'祝福競拍':state.phase==='death-round'?'死亡輪':state.round<=6?'使命試煉':'最終使徒';
  el.status.innerHTML=`
    <span class="v2-status-item"><span>階段</span><strong>${phase}</strong></span>
    <span class="v2-status-item"><span>崩壞</span><strong>${state.corruption}/${state.threshold}</strong><span class="v2-corruption-track"><i style="width:${pct(state.corruption,state.threshold)}%"></i></span></span>
    <span class="v2-status-item"><span>場內</span><strong>${state.played.length}</strong></span>
    <span class="v2-status-item"><span>牌庫</span><strong>${state.deck.length}</strong></span>
    ${state.death.triggered?'<span class="v2-status-item"><strong>死亡已揭示</strong></span>':''}
    ${judgments.map(j=>`<span class="v2-judgment" title="${esc(j.text)}">${esc(j.name)}</span>`).join('')}
  `;
}

const pileRot=[-8,5,-3], pileOffsets=[[-26,12],[18,7],[-6,-4]];
function renderPile(){
  const recent=state.played.slice(-3);
  el.pile.replaceChildren(...recent.map((c,index)=>{
    const node=document.createElement('div');node.className='pile-card'+(index===recent.length-1?' pile-latest':'');
    const [x,y]=pileOffsets[index]||[0,0];
    node.style.setProperty('--pile-x',`${x}px`);node.style.setProperty('--pile-y',`${y}px`);
    node.style.setProperty('--pile-r',`${pileRot[index]||0}deg`);node.style.setProperty('--pile-depth',String(index));
    node.innerHTML=`<img src="${c.image||''}" alt="${esc(c.name||'已出牌')}">`;
    return node;
  }));
  const resource=state.currentResource;
  el.currentLabel.textContent=resource?`當前物資 · ${LABEL[resource.type]||resource.type} ${resource.number}`:'當前物資 · 尚無';
  const apostle=state.currentApostle?playerById(state.currentApostle)?.name:'尚無';
  el.apostle.textContent=`使徒 · ${apostle}`;
}

function renderHand(){
  const p=state.players[0], legal=playableIds(), blind=isBlind();
  el.hand.innerHTML='';
  p.hand.forEach((c,i)=>{
    const b=document.createElement('button');b.type='button';b.dataset.cardId=c.id;
    b.className=`card ${legal.has(c.id)?'legal':'illegal'}`;
    b.style.setProperty('--r',`${(i-(p.hand.length-1)/2)*1.1}deg`);
    const hide=blind&&c.type!=='resource';
    if(hide){
      const label=blind.id==='blindness-2'?'未知功能牌':c.type==='miracle'?'未知神蹟':'未知災難';
      b.innerHTML=`<div class="card-back">${label}</div><span class="card-corruption">?</span>`;
    }else{
      b.innerHTML=`<img class="card-art" src="${c.image}" alt="${esc(c.name)}"><span class="card-corruption">${c.corruption||0}</span>`;
    }
    el.hand.appendChild(b);
  });
  el.hand.dataset.count=String(p.hand.length);
}

function renderActions(){
  el.actions.innerHTML='';el.selectionActions.innerHTML='';
  const canPrepare=state.currentPlayer==='p1'&&state.phase==='actions'&&legalActions(state,'p1').some(a=>a.type==='prepare');
  if(canPrepare){
    const b=document.createElement('button');b.textContent='整備';b.className='primary-action';
    b.onclick=()=>{try{prepare(state,'p1');render();scheduleAI();}catch(e){toast(e.message);}};
    el.actions.appendChild(b);
  }
}

function render(){
  if(!state)return;
  el.round.textContent=`第 ${state.round} / 7 輪`;
  renderSeats();renderStatus();renderPile();renderHand();renderActions();
  el.roundResult.textContent=state.phase==='death-round'?'死亡輪 · 每人最後一次正常行動':'';
  scheduleForcedHumanSkip();
  if(state.phase==='auction')renderAuction();
  if(state.gameOver)renderFinal();
}

function cardDetailHtml(c){
  const blind=isBlind();
  if(blind&&c.type!=='resource')return `<div class="detail-copy"><h2>${blind.id==='blindness-2'?'未知功能牌':c.type==='miracle'?'未知神蹟':'未知災難'}</h2><p>【蒙蔽】使你無法確認這張牌的具體內容。</p></div>`;
  return `<img class="detail-card-art" src="${c.image}" alt=""><div class="detail-copy"><h2>${esc(c.name)}</h2><p>${esc(c.text||`${LABEL[c.resourceType]||'物資'} · 原始數字 ${c.printedNumber}`)}</p><div class="detail-meta"><span>${c.type==='resource'?'物資':c.type==='miracle'?'神蹟':'災難'}</span><span>崩壞 ${c.corruption||0}</span>${c.printedNumber?`<span>原始數字 ${c.printedNumber}</span>`:''}</div></div>`;
}
function openDetail(c){
  el.detail.innerHTML=cardDetailHtml(c);
  el.detailOverlay.hidden=false;
}
function closeDetail(){el.detailOverlay.hidden=true;}

function openChoice(title,copy,choices,done,{cancel=true}={}){
  el.decision.innerHTML=`<span class="eyebrow">V2 DECISION</span><h2>${esc(title)}</h2><p>${esc(copy)}</p>
    <div class="v2-choice-grid">${choices.map(x=>`<button class="v2-choice" data-id="${esc(x.id)}"><strong>${esc(x.name)}</strong>${x.sub?`<small>${esc(x.sub)}</small>`:''}</button>`).join('')}</div>
    <div class="v2-modal-actions">${cancel?'<button data-cancel>取消</button>':''}</div>`;
  el.decisionOverlay.hidden=false;
  el.decision.querySelectorAll('[data-id]').forEach(b=>b.onclick=()=>{el.decisionOverlay.hidden=true;done(b.dataset.id);});
  el.decision.querySelector('[data-cancel]')?.addEventListener('click',()=>el.decisionOverlay.hidden=true);
}
function randomBlindCard(c){
  const blind=isBlind();if(!blind||c.type==='resource')return c;
  const p=state.players[0],pool=blind.id==='blindness-2'?p.hand.filter(x=>x.type!=='resource'):p.hand.filter(x=>x.type===c.type);
  return pool[Math.floor(Math.random()*pool.length)]||c;
}
function requiresTarget(c){return c.target==='single'||c.target==='multi'||['victory','bird','cup','overflow'].includes(c.effect);}
function chooseTarget(c,done){
  const others=state.players.filter(p=>p.id!=='p1'),multi=c.target==='multi';
  openChoice(multi?'選擇兩名目標':'選擇目標',c.name,others.map(p=>({id:p.id,name:p.name,sub:`🔥 ${p.fire} · 手牌 ${p.hand.length}`})),id=>{
    if(!multi)done(id);else{const second=others.find(p=>p.id!==id)?.id;done([id,second].filter(Boolean));}
  });
}
function commitPlay(c,choice){
  try{playCard(state,'p1',c.id,choice);render();scheduleAI();}catch(e){toast(e.message);render();}
}
function requestPlay(raw){
  if(state.currentPlayer!=='p1'){toast('還沒輪到你');return;}
  if(!playableIds().has(raw.id)){toast('這張牌目前不能打出');return;}
  const c=randomBlindCard(raw);
  if(c.type==='resource'&&c.resourceType==='grace'){openChoice('恩典化為物資','選擇本次恩典視為哪種物資。',RESOURCE_TYPES.map(t=>({id:t,name:LABEL[t]})),id=>commitPlay(c,{declared:id}));return;}
  if(c.effect==='wind'){openChoice('如風吹來','選擇本次效果。',[{id:'fire',name:'獲得 4 火種'},{id:'cycle',name:'棄 1 張並抽 1 張'}],id=>commitPlay(c,{mode:id,count:1}));return;}
  if(c.effect==='overflow'){chooseTarget(c,targetId=>openChoice('杯滿盈溢','你要取得哪一項？',[{id:'mission',name:'使命 +3'},{id:'draw',name:'抽 1 張'}],mode=>commitPlay(c,{targetId,mode})));return;}
  if(requiresTarget(c)){chooseTarget(c,choice=>commitPlay(c,Array.isArray(choice)?{targetIds:choice}:{targetId:choice}));return;}
  commitPlay(c,{});
}

function renderHistory(){
  el.historyGrid.innerHTML=state.played.map(c=>`<button class="played-history-card" data-history-card="${esc(c.id)}"><img src="${c.image||''}" alt=""><span>${esc(c.name||'已出牌')}</span></button>`).join('');
  el.historyGrid.querySelectorAll('[data-history-card]').forEach(b=>b.onclick=()=>{
    const c=state.played.find(x=>x.id===b.dataset.historyCard);if(c){el.historyOverlay.hidden=true;openDetail(c);}
  });
  el.historyOverlay.hidden=false;
}

function renderAuction(){
  if(!state.auction)return;
  const h=state.players[0],opts=state.auction.options;let target=opts[0]?.id||'';
  el.decision.innerHTML=`<span class="eyebrow">火種精靈</span><h2>祝福競拍</h2><p>選擇一張祝福並決定出價。</p>
    <div class="v2-choice-grid">${opts.map((b,i)=>`<button class="v2-choice ${i===0?'selected':''}" data-bid-target="${b.id}"><strong>${esc(b.name)}</strong><small>${esc(b.text)}</small></button>`).join('')}</div>
    <div class="bid-row"><span>出價</span><input id="bid" type="range" min="0" max="${h.fire}" value="${Math.min(4,h.fire)}"><strong id="bid-value">${Math.min(4,h.fire)}</strong></div>
    <div class="v2-modal-actions"><button id="bid-submit" class="primary">提交競拍</button></div>`;
  el.decisionOverlay.hidden=false;
  el.decision.querySelectorAll('[data-bid-target]').forEach(b=>b.onclick=()=>{target=b.dataset.bidTarget;el.decision.querySelectorAll('[data-bid-target]').forEach(x=>x.classList.toggle('selected',x===b));});
  const range=$('#bid'),value=$('#bid-value');range.oninput=()=>value.textContent=range.value;
  $('#bid-submit').onclick=()=>{el.decisionOverlay.hidden=true;resolveAuction(state,{blessingId:target,bid:Number(range.value)});render();scheduleAI();};
}

function renderFinal(){
  const w=playerById(state.final?.winnerId);
  el.decision.innerHTML=`<span class="eyebrow">FINAL</span><h2>${w?.id==='p1'?'你贏得了荒野爭局':'荒野爭局結束'}</h2><p>${esc(state.final?.reason||'')}</p>
    <div class="v2-choice-grid">${state.players.map(p=>`<div class="v2-choice ${p.id===w?.id?'selected':''}"><strong>${esc(p.name)}</strong><small>使命 ${p.completedMissions} · 火種 ${p.fire} · ${p.qualified?'已取得資格':'未取得資格'}</small></div>`).join('')}</div>
    <div class="v2-modal-actions"><button id="again" class="primary">再來一局</button></div>`;
  el.decisionOverlay.hidden=false;$('#again').onclick=setupModal;
}

function scheduleForcedHumanSkip(){
  clearTimeout(forcedSkipTimer);
  if(!state||state.gameOver||state.currentPlayer!=='p1'||state.phase!=='death-round')return;
  if(legalActions(state,'p1').some(a=>a.type==='play'))return;
  forcedSkipTimer=setTimeout(()=>{
    if(!state||state.gameOver||state.currentPlayer!=='p1'||state.phase!=='death-round'||legalActions(state,'p1').some(a=>a.type==='play'))return;
    toast('死亡輪沒有合法牌，最後行動自動略過');
    skipNoLegalAction(state,'p1');render();scheduleAI();
  },500);
}
function scheduleAI(){
  clearTimeout(aiTimer);
  if(!state||state.gameOver||state.phase==='auction')return;
  const p=playerById(state.currentPlayer);if(p?.human)return;
  aiTimer=setTimeout(()=>{try{runAiTurn(state);}catch(e){console.error(e);toast('AI 行動錯誤：'+e.message);}render();scheduleAI();},420);
}

function setupModal(){
  clearTimeout(aiTimer);clearTimeout(forcedSkipTimer);
  const chars=CHARACTER_POOL.map((c,i)=>`<button class="v2-choice ${i===0?'selected':''}" data-character="${c.id}"><strong>${esc(c.name)}</strong><small>${esc(c.faction)}</small></button>`).join('');
  el.decision.innerHTML=`<span class="eyebrow">PVP V2</span><h2>建立測試爭局</h2><p>真人 1 名＋AI ×3。畫面與操作以 V1 最後優化版為基底，只在上面加入 V2 系統。</p>
    <div class="setup-character-grid">${chars}</div>
    <div class="v2-modal-actions"><label>崩壞臨界值 <input id="threshold" type="number" min="15" max="60" value="30"></label><label>Seed <input id="seed" value="v2-${Date.now().toString().slice(-5)}"></label><button id="start" class="primary">開始爭局</button></div>`;
  el.decisionOverlay.hidden=false;let character=CHARACTER_POOL[0].id;
  el.decision.querySelectorAll('[data-character]').forEach(b=>b.onclick=()=>{character=b.dataset.character;el.decision.querySelectorAll('[data-character]').forEach(x=>x.classList.toggle('selected',x===b));});
  $('#start').onclick=()=>{state=createPlayableGame({seed:$('#seed').value||'v2',threshold:Number($('#threshold').value)||30,humanCharacter:character});el.decisionOverlay.hidden=true;render();scheduleAI();startAudio();};
}
function startAudio(){if(audioStarted)return;audioStarted=true;el.bgm.volume=.34;el.bgm.play().catch(()=>{audioStarted=false;});}

window.addEventListener('shepherd:v2-card-preview',e=>{
  const id=e.detail?.cardId,cardEl=id?el.hand.querySelector(`.card[data-card-id="${CSS.escape(id)}"]`):null;if(!cardEl)return;
  const was=cardEl.classList.contains('click-preview');el.hand.querySelectorAll('.click-preview').forEach(x=>x.classList.remove('click-preview'));if(!was)cardEl.classList.add('click-preview');
});
window.addEventListener('shepherd:v2-card-detail',e=>{const c=cardById(e.detail?.cardId);if(c)openDetail(c);});
window.addEventListener('shepherd:v2-play-card',e=>{const c=cardById(e.detail?.cardId);if(c)requestPlay(c);});

el.pile.addEventListener('click',renderHistory);
el.pile.addEventListener('keydown',e=>{if(e.key==='Enter'||e.key===' '){e.preventDefault();renderHistory();}});
document.querySelectorAll('[data-close="detail"]').forEach(x=>x.addEventListener('click',closeDetail));
document.querySelectorAll('[data-close="history"]').forEach(x=>x.addEventListener('click',()=>el.historyOverlay.hidden=true));
$('#restart').onclick=setupModal;
$('#audio-toggle').onclick=()=>{if(el.bgm.paused){el.bgm.play();$('#audio-toggle').textContent='♫ 音樂';}else{el.bgm.pause();$('#audio-toggle').textContent='♫ 靜音';}};
document.addEventListener('keydown',e=>{if(e.key==='Escape'){el.detailOverlay.hidden=true;el.historyOverlay.hidden=true;if(state?.phase!=='auction')el.decisionOverlay.hidden=true;}});

setupModal();

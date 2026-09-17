const hand=()=>document.querySelector('#hand');
const dropZone=()=>document.querySelector('#drop-zone');
const humanCanAct=()=>document.querySelector('#seat-human')?.classList.contains('current')===true;
let gesture=null;
const START_DISTANCE=8;
const LONG_PRESS_MS=430;
const THROW_DISTANCE=115;
const THROW_VELOCITY=-0.42;

function emit(name,card){window.dispatchEvent(new CustomEvent(name,{detail:{cardId:card?.dataset?.cardId??null}}));}
function cancelLongPress(){if(gesture?.longTimer){clearTimeout(gesture.longTimer);gesture.longTimer=null;}}
function expandedContains(x,y,rect,pad=70){return x>=rect.left-pad&&x<=rect.right+pad&&y>=rect.top-pad&&y<=rect.bottom+pad;}
function cleanup(){
  if(!gesture)return;
  cancelLongPress();
  gesture.ghost?.remove();
  gesture.card?.classList.remove('drag-source');
  dropZone()?.classList.remove('accepting','rejecting');
  gesture=null;
}
function startGhost(){
  if(!gesture||gesture.ghost||gesture.longPressed||!humanCanAct())return;
  cancelLongPress();
  const rect=gesture.card.getBoundingClientRect();
  const ghost=gesture.card.cloneNode(true);
  ghost.classList.remove('selected','hand-hover','click-preview','illegal');
  ghost.classList.add('v2-drag-ghost');
  ghost.style.width=`${rect.width}px`;
  document.body.appendChild(ghost);
  gesture.ghost=ghost;
  gesture.card.classList.add('drag-source');
  dropZone()?.classList.add('accepting');
}
function moveGhost(x,y){
  if(!gesture?.ghost)return;
  gesture.ghost.style.transform=`translate(${x-gesture.offsetX}px,${y-gesture.offsetY}px) rotate(-3deg) scale(1.08)`;
}
function isThrow(event){
  if(!gesture)return false;
  const dy=event.clientY-gesture.startY;
  const dt=Math.max(1,event.timeStamp-gesture.lastT);
  const vy=(event.clientY-gesture.lastY)/dt;
  return dy<=-THROW_DISTANCE||(dy<=-70&&vy<=THROW_VELOCITY);
}

document.addEventListener('pointerdown',event=>{
  const card=event.target.closest?.('.card');
  if(!card||!hand()?.contains(card)||event.button!==0)return;
  const rect=card.getBoundingClientRect();
  gesture={
    card,pointerId:event.pointerId,
    startX:event.clientX,startY:event.clientY,
    lastX:event.clientX,lastY:event.clientY,lastT:event.timeStamp,
    offsetX:event.clientX-rect.left,offsetY:event.clientY-rect.top,
    ghost:null,longPressed:false,longTimer:null,
  };
  gesture.longTimer=setTimeout(()=>{
    if(!gesture||gesture.ghost)return;
    gesture.longPressed=true;
    gesture.card.classList.remove('click-preview');
    emit('shepherd:v2-card-detail',gesture.card);
  },LONG_PRESS_MS);
},{capture:true});

document.addEventListener('pointermove',event=>{
  if(!gesture||event.pointerId!==gesture.pointerId)return;
  const distance=Math.hypot(event.clientX-gesture.startX,event.clientY-gesture.startY);
  if(distance>=START_DISTANCE&&!gesture.longPressed)startGhost();
  if(gesture.ghost){
    moveGhost(event.clientX,event.clientY);
    event.preventDefault();
  }
  gesture.lastX=event.clientX;gesture.lastY=event.clientY;gesture.lastT=event.timeStamp;
},{capture:true});

document.addEventListener('pointerup',event=>{
  if(!gesture||event.pointerId!==gesture.pointerId)return;
  const {card,ghost,longPressed}=gesture;
  let play=false;
  if(ghost){
    const rect=dropZone()?.getBoundingClientRect();
    play=(rect?expandedContains(event.clientX,event.clientY,rect):false)||isThrow(event);
  }
  cleanup();
  if(longPressed)return;
  if(ghost){
    if(play)emit('shepherd:v2-play-card',card);
    return;
  }
  emit('shepherd:v2-card-preview',card);
},{capture:true});

document.addEventListener('click',event=>{
  if(event.target.closest?.('.card'))event.preventDefault();
},{capture:true});
window.addEventListener('pointercancel',cleanup,{capture:true});
window.addEventListener('blur',cleanup);

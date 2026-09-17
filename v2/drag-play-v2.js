const hand=()=>document.querySelector('#hand');
const stage=()=>document.querySelector('.center-stage');
let drag=null;
const START_DISTANCE=8;

function pointInExpandedRect(x,y,rect,pad=70){return x>=rect.left-pad&&x<=rect.right+pad&&y>=rect.top-pad&&y<=rect.bottom+pad;}
function cleanup(){
  if(!drag)return;
  drag.ghost?.remove();
  drag.card?.classList.remove('drag-source');
  stage()?.classList.remove('drop-ready');
  drag=null;
}
function startGhost(){
  if(!drag||drag.ghost)return;
  const rect=drag.card.getBoundingClientRect();
  const ghost=drag.card.cloneNode(true);
  ghost.classList.remove('selected','hand-hover','illegal');
  ghost.classList.add('drag-ghost-v2');
  ghost.style.width=`${rect.width}px`;ghost.style.height=`${rect.height}px`;
  document.body.appendChild(ghost);
  drag.ghost=ghost;drag.card.classList.add('drag-source');stage()?.classList.add('drop-ready');
}
function moveGhost(x,y){if(!drag?.ghost)return;drag.ghost.style.transform=`translate(${x-drag.offsetX}px,${y-drag.offsetY}px) rotate(-3deg) scale(1.06)`;}

document.addEventListener('pointerdown',event=>{
  const card=event.target.closest?.('.game-card');
  if(!card||!hand()?.contains(card)||event.button!==0)return;
  const rect=card.getBoundingClientRect();
  drag={card,startX:event.clientX,startY:event.clientY,offsetX:event.clientX-rect.left,offsetY:event.clientY-rect.top,ghost:null,pointerId:event.pointerId};
},{capture:true});

document.addEventListener('pointermove',event=>{
  if(!drag||event.pointerId!==drag.pointerId)return;
  const distance=Math.hypot(event.clientX-drag.startX,event.clientY-drag.startY);
  if(distance<START_DISTANCE&&!drag.ghost)return;
  startGhost();moveGhost(event.clientX,event.clientY);
  event.preventDefault();
},{capture:true});

document.addEventListener('pointerup',event=>{
  if(!drag||event.pointerId!==drag.pointerId)return;
  const {card,ghost}=drag;
  let shouldPlay=false;
  if(ghost){const rect=stage()?.getBoundingClientRect();shouldPlay=rect?pointInExpandedRect(event.clientX,event.clientY,rect):false;}
  cleanup();
  if(!ghost)return;
  if(shouldPlay){
    card.click();
    requestAnimationFrame(()=>document.querySelector('#play-selected:not(:disabled)')?.click());
  }
},{capture:true});

window.addEventListener('pointercancel',cleanup,{capture:true});
window.addEventListener('blur',cleanup);

const hand=()=>document.querySelector('#hand');
const humanMeta=()=>document.querySelector('#seat-human');
const humanCanAct=()=>humanMeta()?.classList.contains('current')===true;
let hoveredCard=null;
let hoverCandidate=null;
let hoverTimer=null;
const SWITCH_DELAY_MS=40;
const CLEAR_DELAY_MS=55;
const HYSTERESIS_PX=14;

function setHover(card){
  if(hoveredCard===card)return;
  hoveredCard?.classList.remove('hand-hover');
  hoveredCard=card??null;
  hoveredCard?.classList.add('hand-hover');
}
function clearHover(delay=0){
  clearTimeout(hoverTimer);
  hoverCandidate=null;
  if(!delay){setHover(null);return;}
  hoverTimer=setTimeout(()=>setHover(null),delay);
}
function nearestCardForX(x){
  const cards=[...hand()?.querySelectorAll('.card:not(.drag-source)')??[]];
  if(!cards.length)return null;
  return cards.reduce((best,card)=>{
    const rect=card.getBoundingClientRect(),distance=Math.abs(x-(rect.left+rect.width/2));
    return !best||distance<best.distance?{card,distance}:best;
  },null);
}
function requestHover(candidate){
  if(!candidate?.card)return clearHover(CLEAR_DELAY_MS);
  if(hoveredCard===candidate.card){clearTimeout(hoverTimer);hoverCandidate=null;return;}
  if(hoveredCard){
    const rect=hoveredCard.getBoundingClientRect();
    const currentDistance=Math.abs(candidate.x-(rect.left+rect.width/2));
    if(candidate.distance+HYSTERESIS_PX>=currentDistance)return;
  }
  if(hoverCandidate===candidate.card)return;
  hoverCandidate=candidate.card;
  clearTimeout(hoverTimer);
  hoverTimer=setTimeout(()=>{
    setHover(hoverCandidate);
    hoverCandidate=null;
  },SWITCH_DELAY_MS);
}

function settleHand(){
  const host=hand();
  if(!host)return;
  document.querySelectorAll('.drag-ghost').forEach(node=>node.remove());
  document.querySelector('#drop-zone')?.classList.remove('accepting','rejecting');
  host.querySelectorAll('.card.drag-source,.card.inspecting').forEach(card=>card.classList.remove('drag-source','inspecting'));
  clearHover();
  host.classList.remove('hand-settling');
  void host.offsetWidth;
  host.classList.add('hand-settling');
  clearTimeout(settleHand.timer);
  settleHand.timer=setTimeout(()=>host.classList.remove('hand-settling'),180);
}

function cancelSelectionWhenIdle(){
  if(humanCanAct())return;
  settleHand();
  // Reuse the app's Escape path so its private selected-card state is cleared too.
  window.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape'}));
}

// Use horizontal proximity plus a short switch delay/hysteresis instead of raw
// :hover. This stops two overlapping cards from stealing hover from each other
// while keeping transitions quick enough to feel direct. During a legal drag,
// the dragged source is excluded but neighbouring cards can still lift normally.
document.addEventListener('pointermove',event=>{
  const host=hand();
  if(!host||!host.matches(':hover'))return;
  const nearest=nearestCardForX(event.clientX);
  requestHover(nearest?{...nearest,x:event.clientX}:null);
},{passive:true});
document.addEventListener('pointerout',event=>{
  const host=hand();
  if(!host||event.target!==host)return;
  if(event.relatedTarget&&host.contains(event.relatedTarget))return;
  clearHover(CLEAR_DELAY_MS);
},{capture:true});

// AI turns remain visually interactive, but accidental pointer-down must never
// begin a drag or leave a card half-raised.
document.addEventListener('pointerdown',event=>{
  const card=event.target.closest?.('#hand .card');
  if(!card||humanCanAct())return;
  event.preventDefault();
  event.stopImmediatePropagation();
  clearTimeout(hoverTimer);
  setHover(card);
},{capture:true});

// Recover from browser focus loss / pointer cancellation cases where pointerup
// may never reach the game and a card could otherwise remain half-raised.
window.addEventListener('blur',settleHand);
document.addEventListener('visibilitychange',()=>{if(document.hidden)settleHand();});
window.addEventListener('pointercancel',settleHand,{capture:true});

// Rendering changes #seat-human's class every turn. Detect the transition into
// an idle turn and clear active interaction state, while hover remains available.
const observer=new MutationObserver(()=>cancelSelectionWhenIdle());
const start=()=>{
  const meta=humanMeta();
  if(meta)observer.observe(meta,{attributes:true,attributeFilter:['class']});
  cancelSelectionWhenIdle();
};
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();

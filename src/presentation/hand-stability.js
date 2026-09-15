const hand=()=>document.querySelector('#hand');
const humanMeta=()=>document.querySelector('#seat-human');
const humanCanAct=()=>humanMeta()?.classList.contains('current')===true;

function settleHand(){
  const host=hand();
  if(!host)return;
  document.querySelectorAll('.drag-ghost').forEach(node=>node.remove());
  document.querySelector('#drop-zone')?.classList.remove('accepting','rejecting');
  host.querySelectorAll('.card.drag-source,.card.inspecting').forEach(card=>card.classList.remove('drag-source','inspecting'));
  host.classList.remove('hand-settling');
  void host.offsetWidth;
  host.classList.add('hand-settling');
  clearTimeout(settleHand.timer);
  settleHand.timer=setTimeout(()=>host.classList.remove('hand-settling'),190);
}

function cancelSelectionWhenIdle(){
  if(humanCanAct())return;
  settleHand();
  // Reuse the app's Escape path so its private selected-card state is cleared too.
  window.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape'}));
}

// Block accidental drag starts while AI/other players are acting. Capture phase
// runs before the per-card pointerdown handler in app.js.
document.addEventListener('pointerdown',event=>{
  const card=event.target.closest?.('#hand .card');
  if(!card||humanCanAct())return;
  event.preventDefault();
  event.stopImmediatePropagation();
  settleHand();
},{capture:true});

// Recover from browser focus loss / pointer cancellation cases where pointerup
// may never reach the game and a card could otherwise remain half-raised.
window.addEventListener('blur',settleHand);
document.addEventListener('visibilitychange',()=>{if(document.hidden)settleHand();});
window.addEventListener('pointercancel',settleHand,{capture:true});

// Rendering changes #seat-human's class every turn. Detect the transition into
// an idle turn and force the whole hand back to its canonical fan position.
const observer=new MutationObserver(()=>cancelSelectionWhenIdle());
const start=()=>{
  const meta=humanMeta();
  if(meta)observer.observe(meta,{attributes:true,attributeFilter:['class']});
  cancelSelectionWhenIdle();
};
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();

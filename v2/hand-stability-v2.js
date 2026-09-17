const hand=()=>document.querySelector('#hand');
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
  const cards=[...(hand()?.querySelectorAll('.game-card')??[])];
  if(!cards.length)return null;
  return cards.reduce((best,card)=>{
    const rect=card.getBoundingClientRect();
    const distance=Math.abs(x-(rect.left+rect.width/2));
    return !best||distance<best.distance?{card,distance}:best;
  },null);
}
function requestHover(candidate,x){
  if(!candidate?.card)return clearHover(CLEAR_DELAY_MS);
  if(hoveredCard===candidate.card){clearTimeout(hoverTimer);hoverCandidate=null;return;}
  if(hoveredCard){
    const rect=hoveredCard.getBoundingClientRect();
    const currentDistance=Math.abs(x-(rect.left+rect.width/2));
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
  clearHover();
  host.classList.remove('hand-settling');
  void host.offsetWidth;
  host.classList.add('hand-settling');
  clearTimeout(settleHand.timer);
  settleHand.timer=setTimeout(()=>host.classList.remove('hand-settling'),180);
}
function syncHandCount(){
  const host=hand();
  if(!host)return;
  host.dataset.count=String(host.querySelectorAll('.game-card').length);
}
function syncPhaseAccent(){
  const shell=document.querySelector('.app-shell');
  const summary=document.querySelector('#round-summary');
  const phase=document.querySelector('#phase-copy');
  if(!shell)return;
  const text=`${summary?.textContent??''} ${phase?.textContent??''}`;
  shell.classList.toggle('death-active',text.includes('死亡'));
}

// Horizontal proximity + hysteresis prevents the old overlapped-hand problem
// where two neighbouring cards repeatedly steal :hover from each other.
document.addEventListener('pointermove',event=>{
  const host=hand();
  if(!host||!host.matches(':hover')){clearHover(CLEAR_DELAY_MS);return;}
  const nearest=nearestCardForX(event.clientX);
  requestHover(nearest,event.clientX);
},{passive:true});
document.addEventListener('pointerleave',event=>{
  if(event.target===hand())clearHover(CLEAR_DELAY_MS);
},{capture:true});
window.addEventListener('blur',settleHand);
window.addEventListener('pointercancel',settleHand,{capture:true});
document.addEventListener('visibilitychange',()=>{if(document.hidden)settleHand();});

// Keep compression rules and phase accents in sync even though app.js fully
// rerenders the hand after most actions.
const observer=new MutationObserver(()=>{syncHandCount();syncPhaseAccent();});
function start(){
  const host=hand();
  if(host)observer.observe(host,{childList:true,subtree:false});
  const summary=document.querySelector('#round-summary');
  if(summary)observer.observe(summary,{childList:true,subtree:true,characterData:true});
  const phase=document.querySelector('#phase-copy');
  if(phase)observer.observe(phase,{childList:true,subtree:true,characterData:true});
  syncHandCount();syncPhaseAccent();
}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();

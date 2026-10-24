import { renderCardRow } from './choice-card-row.js';

// Shared motion for choice tiles; reduced-motion and touch preferences are respected.
export function addChoiceMotion(button){
  button.style.transition='transform .12s ease-out';
  button.addEventListener('pointermove',event=>{
    if(button.disabled||event.buttons||event.pointerType==='touch'||globalThis.matchMedia?.('(prefers-reduced-motion: reduce)').matches)return;
    const rect=button.getBoundingClientRect();if(!rect.width||!rect.height)return;
    const x=Math.min(1,Math.max(0,(event.clientX-rect.left)/rect.width));
    const y=Math.min(1,Math.max(0,(event.clientY-rect.top)/rect.height));
    button.style.transform=`perspective(900px) rotateX(${(.5-y)*18}deg) rotateY(${(x-.5)*22}deg)`;
  });
  button.addEventListener('pointerleave',()=>{button.style.transform='';});
}

// One shared, replaceable portrait placeholder for player-selection screens.
export function renderPlayers(overlay, title, note, targets, onSelect, describe=()=> '') {
  const host=renderCardRow(overlay,title,note);
  for(const player of targets){
    const button=document.createElement('button');
    button.type='button';button.className='choice-row-card player-choice-placeholder';
    button.style.cssText='display:flex;flex-direction:column;justify-content:center;align-items:center;gap:12px;color:#f5eee0;background:linear-gradient(155deg,#43504b,#202c29);border:1px solid #d8c79588;box-shadow:0 8px 24px #0009;';
    const portrait=document.createElement('span');
    portrait.setAttribute('aria-hidden','true');portrait.textContent='✦';
    portrait.style.cssText='display:grid;place-items:center;width:50%;aspect-ratio:1;border-radius:50%;background:#ffffff18;color:#ddd1ae;font-size:36px';
    const name=document.createElement('strong');name.textContent=player.type==='human'?'你':(player.displayName??`玩家 ${player.seat}`);
    const detail=document.createElement('small');detail.textContent=describe(player);
    detail.style.cssText='max-width:90%;line-height:1.35;color:#e2d9c7';
    addChoiceMotion(button);
    button.append(portrait,name,detail);button.onclick=()=>onSelect(player);host.append(button);
  }
  return host;
}
export function renderOptions(overlay,title,note,options) {
  const host=renderCardRow(overlay,title,note);
  for(const option of options){
    const button=document.createElement('button');button.type='button';button.className='choice-row-card';
    button.style.cssText='height:auto;min-height:95px;padding:16px;aspect-ratio:auto;color:#f5eee0;background:#26332de8;border:1px solid #d8c79588;line-height:1.5';
    button.textContent=option.label;button.disabled=Boolean(option.disabled);addChoiceMotion(button);button.onclick=option.onSelect;host.append(button);
  }
  return host;
}

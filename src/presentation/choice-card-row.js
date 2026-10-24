import { getDefinition } from '../game/cards.js';

let choicePreview=null;
function closeChoicePreview(){choicePreview?.remove();choicePreview=null;}
function showChoicePreview(card){
  closeChoicePreview();
  const d=getDefinition(card), preview=document.createElement('div');
  preview.className='choice-card-preview';
  const article=document.createElement('article');article.className='choice-card-preview-content';
  const image=document.createElement('div');image.className='choice-card-preview-face';
  const src=globalThis.__shepherdCardFaceAsset?.(d)??d.image;
  image.innerHTML=`<img draggable="false" alt="" src="${src}">${globalThis.__shepherdCardFaceGlyph?.(d)??''}`;
  const text=document.createElement('div');text.className='choice-card-preview-text';
  const name=document.createElement('h2');name.textContent=d.name??'';
  const effect=document.createElement('p');effect.textContent=d.text??'暫無效果說明。';
  const close=document.createElement('button');close.type='button';close.textContent='返回選擇';close.onclick=closeChoicePreview;
  text.append(name,effect,close);article.append(image,text);preview.append(article);document.body.append(preview);choicePreview=preview;
}
if(typeof document!=='undefined')document.addEventListener('pointerdown',event=>{
  if(!choicePreview||event.target.closest?.('.choice-card-preview-content'))return;
  closeChoicePreview();event.preventDefault();event.stopImmediatePropagation();
},true);

// Shared presentation only: actions and their legality remain with each card.
export function prepareCardRow(overlay) {
  if (!document.querySelector('#choice-card-row-style')) {
    const style = document.createElement('style');
    style.id = 'choice-card-row-style';
    style.textContent = `
      .choice-row-overlay[hidden]{display:none!important}
      .choice-row-overlay{position:fixed!important;inset:0!important;z-index:42500!important;display:block!important;padding:0!important;background:transparent!important;backdrop-filter:none!important;pointer-events:none!important;color:#eee8d8}
      .choice-row-panel{position:absolute;inset:0;text-align:center;pointer-events:none;text-shadow:0 2px 6px #000,0 0 12px #000}
      .choice-row-heading{position:absolute;left:50%;bottom:calc(50% + min(120px,18vh) + 24px);transform:translateX(-50%);width:min(850px,90vw)}
      .choice-row-heading h2{margin:0 0 12px;font-size:22px}.choice-row-heading p{margin:0;line-height:1.5}
      .choice-row-cards{position:absolute;left:50%;top:50%;transform:translate(-50%,-50%);display:flex;align-items:center;gap:14px;width:min(994px,96vw);height:min(265px,38vh);padding:14px 12px;box-sizing:border-box;overflow-x:auto;overflow-y:hidden;scrollbar-width:none;touch-action:none;user-select:none;pointer-events:auto}
      .choice-row-cards::-webkit-scrollbar{display:none}.choice-row-cards::before,.choice-row-cards::after{content:'';margin:auto}
      .choice-row-card{position:relative;flex:0 0 auto;width:min(150px,21vh,calc(16vw - 16px));aspect-ratio:63/88;padding:0;border:0;border-radius:9px;background:transparent;cursor:pointer;perspective:900px}
      .choice-row-tilt{position:relative;display:block;width:100%;height:100%;overflow:hidden;border-radius:9px;box-shadow:0 8px 24px #0009;transform:perspective(900px) rotateX(var(--rx,0deg)) rotateY(var(--ry,0deg));transition:transform .12s ease-out}
      .choice-row-tilt img{display:block;width:100%;height:100%;object-fit:cover}.choice-row-tilt .runtime-tag-glyph{z-index:2}
      .choice-row-name{position:absolute;top:calc(100% + 5px);left:-12px;right:-12px;color:#fff;font-size:14px;line-height:1.2;white-space:nowrap;text-shadow:0 1px 5px #000,0 0 8px #000}.choice-row-card.selected{outline:3px solid #ffe190;outline-offset:4px}.choice-row-card:focus-visible{outline:3px solid #ffe190;outline-offset:4px}
      .choice-card-preview{position:fixed;inset:0;z-index:45000;display:grid;place-items:center;pointer-events:none;color:#eee8d8}
      .choice-card-preview-content{display:flex;align-items:center;gap:24px;max-width:min(700px,94vw);padding:18px;pointer-events:auto;text-shadow:0 2px 6px #000,0 0 12px #000}
      .choice-card-preview-face{position:relative;flex:0 0 auto;width:min(210px,28vw,35vh);aspect-ratio:63/88;overflow:hidden;border-radius:10px;box-shadow:0 8px 24px #0009}
      .choice-card-preview-face img{display:block;width:100%;height:100%;object-fit:cover}.choice-card-preview-face .runtime-tag-glyph{z-index:2}
      .choice-card-preview-text{width:min(320px,45vw);max-height:70vh;overflow:auto;text-align:left;line-height:1.6}.choice-card-preview-text h2{margin:0 0 12px;font-size:22px}.choice-card-preview-text p{margin:0 0 16px;white-space:pre-wrap}.choice-card-preview-text button{padding:9px 15px;border:1px solid #e1cf9b88;border-radius:10px;background:#14201be8;color:#fff;cursor:pointer;font:inherit}
      @media(prefers-reduced-motion:reduce){.choice-row-tilt{transform:none!important;transition:none!important}}
    `;
    document.head.append(style);
  }
  overlay.classList.remove('decision-floating','decision-panel');
  overlay.classList.add('choice-row-overlay');
}
export function renderCardRow(overlay, name, note) {
  globalThis.__shepherdCloseCardInspector?.();
  prepareCardRow(overlay);
  overlay.innerHTML = '<article class="choice-row-panel"><div class="choice-row-heading"><h2></h2><p></p></div><div class="choice-row-cards" aria-label="可選擇的牌"></div></article>';
  overlay.querySelector('h2').textContent=name;
  overlay.querySelector('p').textContent=note;
  const host=overlay.querySelector('.choice-row-cards');
  host.addEventListener('wheel',event=>{if(event.deltaY && host.scrollWidth>host.clientWidth){event.preventDefault();host.scrollLeft+=event.deltaY;}},{passive:false});
  let start=null, suppressClick=false;
  const clearHold=()=>{if(start?.timer){clearTimeout(start.timer);start.timer=null;}};
  host.addEventListener('pointerdown',event=>{
    if(!event.isPrimary||(event.pointerType==='mouse'&&event.button!==0))return;
    const button=event.target.closest?.('.choice-row-card'),card=button?.choiceCard;
    start={id:event.pointerId,x:event.clientX,y:event.clientY,left:host.scrollLeft,time:Date.now(),dragged:false,moved:false,longPressed:false,timer:null};suppressClick=false;
    if(card&&['miracle','disaster'].includes(getDefinition(card).kind)){
      const press=start;press.timer=setTimeout(()=>{if(start!==press||press.dragged)return;press.timer=null;press.longPressed=true;showChoicePreview(card);},500);
    }
  });
  host.addEventListener('pointermove',event=>{
    if(!start||event.pointerId!==start.id)return;
    const delta=event.clientX-start.x;
    if(Math.hypot(delta,event.clientY-start.y)>5){start.moved=true;clearHold();}
    if(start.longPressed)return;
    if(Math.abs(delta)>5&&host.scrollWidth>host.clientWidth){if(!start.dragged){start.dragged=true;host.setPointerCapture(event.pointerId);}host.scrollLeft=start.left-delta;event.preventDefault();}
  });
  host.addEventListener('pointerup',event=>{if(!start||event.pointerId!==start.id)return;clearHold();suppressClick=start.longPressed||start.moved||Date.now()-start.time>500;start=null;});
  host.addEventListener('pointercancel',()=>{clearHold();start=null;suppressClick=true;});
  host.addEventListener('click',event=>{if(suppressClick){event.preventDefault();event.stopPropagation();suppressClick=false;}},true);
  overlay.hidden=false;
  return host;
}
export function appendChoiceCard(host,card,label,onSelect){
  const d=getDefinition(card),button=document.createElement('button');
  button.type='button';button.className='choice-row-card';button.choiceCard=card;button.setAttribute('aria-label',label);
  if (d.kind==='miracle'||d.kind==='disaster') button.addEventListener('contextmenu',event=>event.preventDefault());
  const face=globalThis.__shepherdCardFaceAsset?.(d)??d.image;
  const glyph=globalThis.__shepherdCardFaceGlyph?.(d)??'';
  button.innerHTML=`<span class="choice-row-tilt"><img draggable="false" src="${face}" alt="">${glyph}</span>`;
  const tilt=button.querySelector('.choice-row-tilt');
  button.addEventListener('pointermove',event=>{if(event.buttons||event.pointerType==='touch')return;const rect=button.getBoundingClientRect();if(!rect.width||!rect.height)return;tilt.style.setProperty('--rx',`${(.5-(event.clientY-rect.top)/rect.height)*18}deg`);tilt.style.setProperty('--ry',`${((event.clientX-rect.left)/rect.width-.5)*22}deg`);});
  button.addEventListener('pointerleave',()=>{tilt.style.setProperty('--rx','0deg');tilt.style.setProperty('--ry','0deg');});
  if (d.kind==='miracle'||d.kind==='disaster') {const name=document.createElement('span');name.className='choice-row-name';name.textContent=label;button.append(name);}
  button.onclick=onSelect;host.append(button);return button;
}

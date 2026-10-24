// Keep every pending choice intact while the player inspects the table.
(() => {
  if(typeof document==='undefined'||typeof MutationObserver==='undefined'||!document.body)return;
  const selectors=['#grace-overlay','#target-choice-overlay','#ark-choice-overlay','#reaction-overlay','#empty-tomb-overlay','#stronghold-overlay','#overcome-death-overlay','#lies-choice-overlay','#mission-reward-overlay','#our-victory-overlay','.war-discard-overlay','#rope-choice','#rope-reveal-choice','#cup-choice','#narrow-choice','#overflow-choice','#aftermath-choice','#false-tongue-choice','#desire-seed-choice','.choice-row-overlay'];
  const selector=selectors.join(',');
  const style=document.createElement('style');
  style.textContent=`.decision-floating{background:transparent!important;backdrop-filter:none!important;-webkit-backdrop-filter:none!important;pointer-events:none!important;place-items:center end!important;padding:12px!important}.decision-floating>.overlay-backdrop{display:none!important}.decision-floating>article,.decision-floating>.decision-battlefield-button,.decision-floating:not(.overlay)>div:not(.overlay-backdrop){pointer-events:auto}.decision-floating>article{max-width:min(520px,92vw)!important;max-height:70vh!important;overflow:auto!important}.decision-floating[role=dialog],.decision-floating>article[role=dialog]{backdrop-filter:none!important}.decision-floating>.decision-battlefield-button,.decision-panel>.decision-battlefield-button{position:fixed;right:16px;top:16px;z-index:1}.decision-battlefield-hidden.decision-battlefield-hidden{display:none!important}.decision-battlefield-button,.decision-battlefield-return{padding:9px 14px;border:1px solid #d7bd7a;border-radius:10px;background:#17201f;color:#fff;cursor:pointer;font-weight:700}.decision-battlefield-return{position:fixed;right:16px;bottom:16px;z-index:99999}`;
  document.head.append(style);
  let hidden=null,back=null;
  const openStates=new WeakMap();
  const isPending=el=>el.isConnected&&!el.hidden;
  globalThis.__shepherdDecisionPending=()=>Array.from(document.querySelectorAll(selector)).some(isPending);
  function restore(){if(hidden?.isConnected&&!hidden.hidden){hidden.classList.remove('decision-battlefield-hidden');globalThis.__shepherdCloseCardInspector?.();}hidden=null;back?.remove();back=null;}
  function suspend(el){if(hidden===el)return;restore();hidden=el;el.classList.add('decision-battlefield-hidden');back=document.createElement('button');back.type='button';back.className='decision-battlefield-return';back.textContent='返回選擇';back.addEventListener('click',restore);document.body.append(back);}
  function add(el){
    if(el.classList.contains('choice-row-overlay'))return;
    el.classList.add(el.tagName==='SECTION'?'decision-panel':'decision-floating');el.removeAttribute('aria-modal');
    const article=el.querySelector(':scope > article');if(article){article.removeAttribute('aria-modal');if(article.getAttribute('role')==='dialog')article.removeAttribute('role');}
    if(el.querySelector(':scope > .decision-battlefield-button'))return;
    const button=document.createElement('button');button.type='button';button.className='decision-battlefield-button';button.textContent='查看戰場';button.addEventListener('click',()=>suspend(el));el.append(button);
  }
  function refresh(){
    if(hidden&&(!hidden.isConnected||hidden.hidden))restore();
    document.querySelectorAll(selector).forEach(el=>{
      add(el);
      const open=isPending(el);
      if(open&&!openStates.get(el))globalThis.__shepherdCloseCardInspector?.();
      openStates.set(el,open);
    });
  }
  document.addEventListener('pointerdown',event=>{
    const target=event.target;
    if(target.closest?.('.decision-battlefield-return'))return;
    const sourceCard=target.closest?.('.card[data-card-id]');
    if(hidden&&sourceCard&&hidden.dataset.decisionCardId&&sourceCard.dataset.cardId===hidden.dataset.decisionCardId){
      event.preventDefault();event.stopImmediatePropagation();restore();return;
    }
    if(globalThis.__shepherdDecisionPending()&&target.closest?.('#actions, #selection-actions')){
      event.preventDefault();event.stopImmediatePropagation();return;
    }
    const open=Array.from(document.querySelectorAll(selector)).filter(el=>isPending(el)&&!el.classList.contains('decision-battlefield-hidden')).at(-1);
    if(!open)return;
    const inside=open.classList.contains('choice-row-overlay')?target.closest?.('.choice-row-card, .choice-row-panel button, .choice-card-preview-content'):open.contains(target);
    if(!inside)suspend(open);
  },true);
  new MutationObserver(refresh).observe(document.body,{childList:true,subtree:true,attributes:true,attributeFilter:['hidden']});refresh();
})();

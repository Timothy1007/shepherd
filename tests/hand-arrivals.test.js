import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';

test('deal waits for images, and repeated restart isolates earlier animations', async () => {
  const src=fs.readFileSync(new URL('../src/presentation/app.js',import.meta.url),'utf8');
  const code=src.slice(src.indexOf('let animatedHandIds = null;'),src.indexOf('function renderHand(state)'));
  const animations=[],callbacks={},timers=new Map(); let nextTimer=0;
  const rect={left:100,top:100,width:60,height:80};
  function makeImage(){const listeners=new Map();return {complete:false,naturalWidth:0,style:{},addEventListener(n,f){listeners.set(n,f)},removeEventListener(n){listeners.delete(n)},load(){this.complete=true;this.naturalWidth=60;listeners.get('load')?.()}};}
  function element(id){const image=makeImage();return {dataset:{cardId:id},image,style:{setProperty(k,v){this[k]=v}},classList:{add(){}},removeAttribute(){},setAttribute(){},getBoundingClientRect(){return rect},cloneNode(){return element()},querySelector(){return image},animate(){const animation={};animations.push(animation);return animation},remove(){}};}
  const cards=[element('a'),element('b')];
  const document={hidden:false,body:{append(){}},addEventListener(name,fn){callbacks[name]=fn},querySelector(){return {getBoundingClientRect(){return rect}}},querySelectorAll(query){return query.includes('#hand')?cards:[]}};
  const context={document,window:{innerWidth:1000},performance:{now:()=>10},requestAnimationFrame:fn=>fn(),setTimeout(fn){const id=++nextTimer;timers.set(id,fn);return id},clearTimeout(id){timers.delete(id)},console};
  vm.createContext(context);vm.runInContext(code,context);
  const state={round:1,players:[{hand:[{instanceId:'a'},{instanceId:'b'}]}]};
  context.animateHandArrivals(state);
  assert.equal(cards[0].style.visibility,'hidden');
  assert.equal(animations.length,0);
  cards.forEach(card=>card.image.load());
  await Promise.resolve();await Promise.resolve();
  assert.equal(animations.length,2,'loaded faces start their deal animations');
  animations[0].onfinish();
  assert.equal(cards[0].style.visibility,'visible');
  assert.equal(cards[1].style.visibility,'hidden');
  vm.runInContext('animatedHandIds=null;animatedHandRound=null;clearHandArrivals()',context);
  context.animateHandArrivals(state);
  await Promise.resolve();await Promise.resolve();
  assert.equal(animations.length,4,'restart deals again');
  animations[1].oncancel();
  assert.equal(cards[1].style.visibility,'hidden','old callbacks cannot reveal the new deal');
  animations[2].onfinish();animations[3].onfinish();
  assert.equal(cards[0].style.visibility,'visible');
  assert.equal(cards[1].style.visibility,'visible');
  vm.runInContext('animatedHandIds=null;animatedHandRound=null;clearHandArrivals()',context);
  context.animateHandArrivals(state);
  await Promise.resolve();await Promise.resolve();
  document.hidden=true;callbacks.visibilitychange();
  assert.equal(cards[0].style.visibility,'visible');
  assert.equal(cards[1].style.visibility,'visible');
});

import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';

test('hand cards recover when deal animation is cancelled, interrupted, or cannot load', () => {
  const src=fs.readFileSync(new URL('../src/presentation/app.js',import.meta.url),'utf8');
  const code=src.slice(src.indexOf('let animatedHandIds = null;'),src.indexOf('function renderHand(state)'));
  const animations=[],callbacks={};
  let imageReady=true;
  const rect={left:100,top:100,width:60,height:80};
  function element(id){return {dataset:{cardId:id},style:{setProperty(k,v){this[k]=v}},classList:{add(){}},removeAttribute(){},setAttribute(){},getBoundingClientRect(){return rect},cloneNode(){return element()},querySelector(){return {complete:imageReady,naturalWidth:imageReady?60:0,style:{}}},animate(){const animation={};animations.push(animation);return animation},remove(){}};}
  const cards=[element('a'),element('b')];
  const document={hidden:false,body:{append(){}},addEventListener(name,fn){callbacks[name]=fn},querySelector(){return {getBoundingClientRect(){return rect}}},querySelectorAll(query){return query.includes('#hand')?cards:[]}};
  const timers=[];
  const context={document,window:{innerWidth:1000},performance:{now:()=>10},requestAnimationFrame:fn=>fn(),setTimeout(fn){timers.push(fn);return timers.length},clearTimeout(){},console};
  vm.createContext(context);
  vm.runInContext(code,context);
  const players=[{hand:[{instanceId:'a'},{instanceId:'b'}]}];
  context.animateHandArrivals({round:1,players});
  assert.equal(cards[0].style.visibility,'hidden');
  animations[0].oncancel();
  assert.equal(cards[0].style.visibility,'visible');
  assert.equal(cards[1].style.visibility,'hidden');
  document.hidden=true;
  callbacks.visibilitychange();
  assert.equal(cards[1].style.visibility,'visible');
  document.hidden=false;
  imageReady=false;
  context.animateHandArrivals({round:2,players});
  assert.equal(cards[0].style.visibility,'visible');
  assert.equal(cards[1].style.visibility,'visible');
});

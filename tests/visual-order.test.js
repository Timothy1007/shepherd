import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';

test('new hand cards wait for discard burn before flying in',async()=>{
  const src=fs.readFileSync(new URL('../src/presentation/app.js',import.meta.url),'utf8');
  const code=src.slice(src.indexOf('let animatedHandIds = null;'),src.indexOf('function renderHand(state)'));
  const flights=[];let releaseBurn;
  const rect={left:100,top:100,width:60,height:80};
  function card(id){return {dataset:{cardId:id},style:{setProperty(k,v){this[k]=v}},classList:{add(){}},removeAttribute(){},setAttribute(){},getBoundingClientRect(){return rect},cloneNode(){return card()},querySelector(){return {complete:true,naturalWidth:60,style:{}}},animate(){const a={};flights.push(a);return a},remove(){}};}
  const cards=[card('a')],document={hidden:false,body:{append(){}},addEventListener(){},querySelector(){return{getBoundingClientRect(){return rect}}},querySelectorAll(q){return q.includes('#hand')?cards:[]}};
  const context={document,window:{innerWidth:1000},performance:{now:()=>10},requestAnimationFrame:fn=>fn(),setTimeout(){return 1},clearTimeout(){},console};
  vm.createContext(context);vm.runInContext(code,context);
  context.__shepherdDiscardBurnDone=new Promise(resolve=>{releaseBurn=resolve;});
  context.animateHandArrivals({round:1,players:[{hand:[{instanceId:'a'}]}]});
  await Promise.resolve();await Promise.resolve();
  assert.equal(cards[0].style.visibility,'hidden');assert.equal(flights.length,0);
  releaseBurn();await new Promise(resolve=>setImmediate(resolve));
  assert.equal(flights.length,1);
  let arrived=false;context.__shepherdHandArrivalsDone.then(()=>{arrived=true;});
  await Promise.resolve();assert.equal(arrived,false);
  flights[0].onfinish();await new Promise(resolve=>setImmediate(resolve));
  assert.equal(cards[0].style.visibility,'visible');assert.equal(arrived,true);
});

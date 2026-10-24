import { test } from 'node:test';
import assert from 'node:assert/strict';

// The inspector module loads after the game has started, so no safeRender has
// necessarily been observed yet. Exercise both gestures against live state.
test('pile short press and long press read the live game before the first observed render',async()=>{
  const handlers={},elements=new Map(),host={children:[],append(node){this.children.push(node)},addEventListener(){}};
  const heading={textContent:''},note={textContent:''};
  const pile={classList:{contains:name=>name==='has-cards'},getBoundingClientRect:()=>({left:0,top:0,right:100,bottom:100}),closest:()=>pile};
  function node(tag){return {tagName:tag.toUpperCase(),style:{setProperty(){}},dataset:{},children:[],classList:{add(){},remove(){},contains(){return false}},hidden:false,append(...children){this.children.push(...children)},addEventListener(){},setAttribute(){},querySelector(selector){if(selector==='h2')return heading;if(selector==='p')return note;if(selector==='.choice-row-cards')return host;if(selector==='.choice-row-tilt')return {style:{setProperty(){}}};return null;},set innerHTML(value){this._html=value;},get innerHTML(){return this._html??'';}};}
  globalThis.document={body:{append(el){if(el.id)elements.set(`#${el.id}`,el);}},head:{append(el){if(el.id)elements.set(`#${el.id}`,el);}},createElement:node,addEventListener(){},querySelector(selector){if(selector==='.tabletop-pile')return pile;return elements.get(selector)??null;},querySelectorAll(){return[]},elementFromPoint(){return pile}};
  globalThis.window={addEventListener(type,fn){handlers[type]=fn}};
  Object.defineProperty(globalThis,'navigator',{value:{},configurable:true});
  const cards=[{definitionId:'miracle-01',instanceId:'m1'},{definitionId:'disaster-07',instanceId:'d1'}];
  globalThis.__shepherdController={state:{playedArea:cards}};
  let inspected=null;globalThis.__shepherdOpenCardInspector=card=>{inspected=card;};
  await import('../src/presentation/qa-inspector.js');
  const event={button:0,pointerId:1,clientX:20,clientY:20};
  handlers.pointerdown(event);handlers.pointerup(event);
  assert.equal(inspected,cards[1],'short press opens the latest card with the shared inspector');
  handlers.pointerdown(event);await new Promise(resolve=>setTimeout(resolve,520));handlers.pointerup(event);
  assert.equal(elements.get('#played-history-overlay').hidden,false,'long press opens history');
  assert.equal(host.children.length,2,'history contains every played card');
  host.children[0].onclick();
  assert.equal(inspected,cards[0],'choosing a history card opens the same shared inspector');
  assert.equal(elements.get('#played-history-overlay').hidden,true);
  const restartedCard={definitionId:'miracle-05',instanceId:'restart'};
  globalThis.__shepherdController.state={playedArea:[restartedCard]};
  handlers.pointerdown(event);handlers.pointerup(event);
  assert.equal(inspected,restartedCard,'short press follows the replacement state after a restart');
});

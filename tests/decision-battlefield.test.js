import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

test('pending card choice can be hidden and restored without resolving it', () => {
  const classes = new Set(['choice-row-overlay']);
  const overlay = { isConnected:true, hidden:false, dataset:{decisionCardId:'played'}, classList:{contains:x=>classes.has(x),add:x=>classes.add(x),remove:x=>classes.delete(x)} };
  const events = {}; const bodyChildren = [];
  const document = {
    body:{append:x=>bodyChildren.push(x)}, head:{append(){}},
    createElement:()=>({className:'',addEventListener(type,fn){this[type]=fn},remove(){this.removed=true}}),
    querySelectorAll:()=>[overlay],
    addEventListener(type,fn){events[type]=fn}
  };
  const context={document,MutationObserver:class {observe(){}},globalThis:null};context.globalThis=context;
  vm.runInNewContext(readFileSync('src/presentation/decision-battlefield.js','utf8'),context);
  assert.equal(context.__shepherdDecisionPending(),true);
  events.pointerdown({target:{closest:()=>null}});
  assert.equal(classes.has('decision-battlefield-hidden'),true);
  assert.equal(context.__shepherdDecisionPending(),true);
  assert.equal(bodyChildren.at(-1).textContent,'返回選擇');
  bodyChildren.at(-1).click();
  assert.equal(classes.has('decision-battlefield-hidden'),false);
  assert.equal(context.__shepherdDecisionPending(),true);
  events.pointerdown({target:{closest:()=>null}});
  let prevented=false;
  const source={dataset:{cardId:'played'}};
  events.pointerdown({target:{closest:q=>q==='.card[data-card-id]'?source:null},preventDefault(){prevented=true},stopImmediatePropagation(){}});
  assert.equal(prevented,true);
  assert.equal(classes.has('decision-battlefield-hidden'),false);
});

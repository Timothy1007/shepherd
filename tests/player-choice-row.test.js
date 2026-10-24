import { test } from 'node:test';
import assert from 'node:assert/strict';
import { renderPlayers, renderOptions } from '../src/presentation/player-choice-row.js';

test('player placeholders and effect choices preserve the selected target', () => {
  const host={children:[],append(node){this.children.push(node)},addEventListener(){}};
  const overlay={dataset:{},hidden:true,classList:{add(){},remove(){}},querySelector(sel){return sel==='.choice-row-cards'?host:{textContent:''}}};
  const previousDocument=globalThis.document;
  globalThis.document={querySelector(){return {}},createElement(){return {style:{},children:[],listeners:{},addEventListener(type,fn){this.listeners[type]=fn},getBoundingClientRect(){return {left:0,top:0,width:100,height:100}},setAttribute(){},append(...items){this.children.push(...items)}}}};
  try {
    const players=[{playerId:'p1',type:'human'},{playerId:'p2',type:'ai',displayName:'旅人'}];
    let selected=null;renderPlayers(overlay,'選玩家','選一位',players,p=>selected=p.playerId);
    assert.equal(host.children.length,2);assert.equal(host.children[0].children[1].textContent,'你');
    host.children[1].listeners.pointermove({buttons:0,pointerType:'mouse',clientX:80,clientY:20});
    assert.match(host.children[1].style.transform,/rotateX/);
    host.children[1].listeners.pointerleave();assert.equal(host.children[1].style.transform,'');
    host.children[1].onclick();assert.equal(selected,'p2');assert.equal(overlay.hidden,false);
    host.children=[];let choice=null;renderOptions(overlay,'選效果','',[
      {label:'抽牌',onSelect:()=>{choice='draw'}},{label:'使命',disabled:true,onSelect:()=>{choice='mission'}}]);
    assert.equal(host.children[1].disabled,true);host.children[0].listeners.pointermove({buttons:0,pointerType:'mouse',clientX:80,clientY:20});assert.match(host.children[0].style.transform,/rotateX/);host.children[0].listeners.pointerleave();assert.equal(host.children[0].style.transform,'');host.children[0].onclick();assert.equal(choice,'draw');
  } finally {globalThis.document=previousDocument;}
});

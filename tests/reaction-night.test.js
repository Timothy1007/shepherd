import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, getNormalActions, listCardLocations } from '../src/game/game.js';
import { getDefinition } from '../src/game/cards.js';

function moveReactionToHuman(state){
  let card;
  for(const player of state.players){
    const index=player.hand.findIndex(c=>c.definitionId==='miracle-16');
    if(index>=0){card=player.hand.splice(index,1)[0];break;}
  }
  if(!card){
    const index=state.deck.findIndex(c=>c.definitionId==='miracle-16');
    if(index>=0)card=state.deck.splice(index,1)[0];
  }
  if(!card)throw new Error('越過長夜 missing from registry.');
  state.players[0].hand.push(card);
  return card;
}

test('越過長夜 is registered as a miracle reaction with official artwork',()=>{
  const state=createGame({seed:'reaction-registry'});
  const card=Object.values(state.cardRegistry).includes('miracle-16');
  assert.equal(card,true);
  const instance=state.deck.find(c=>c.definitionId==='miracle-16')??state.players.flatMap(p=>p.hand).find(c=>c.definitionId==='miracle-16');
  const definition=getDefinition(instance);
  assert.equal(definition.name,'越過長夜');
  assert.equal(definition.cardType,'miracle');
  assert.equal(definition.kind,'miracle');
  assert.equal(definition.reaction,true);
  assert.equal(definition.image,'assets/miracles/miracle-16.png');
});

test('越過長夜 remains available as a normal-turn miracle outside reaction timing',()=>{
  const state=createGame({seed:'reaction-normal-action'});
  const reaction=moveReactionToHuman(state);
  state.currentPlayer='player-1';
  const actions=getNormalActions(state);
  assert.equal(actions.some(a=>a.instanceId===reaction.instanceId&&a.type==='playMiracle'),true);
});

test('reaction card remains inside card location invariant before use',()=>{
  const state=createGame({seed:'reaction-invariant'});
  moveReactionToHuman(state);
  const locations=listCardLocations(state);
  assert.equal(new Set(locations).size,locations.length);
  assert.equal(locations.length,Object.keys(state.cardRegistry).length);
});

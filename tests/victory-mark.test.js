import { test } from 'node:test';
import assert from 'node:assert/strict';
import { isHumanVictoryTarget } from '../src/presentation/victory-mark.js';

test('marker follows the human victory effect and disappears when it leaves',()=>{
  const human={type:'human',effects:[{definitionId:'miracle-09',targetPlayerId:'player-3'}]};
  const state={players:[human,{type:'ai',effects:[{definitionId:'miracle-09',targetPlayerId:'player-2'}]}]};
  assert.equal(isHumanVictoryTarget(state,'player-3'),true);
  assert.equal(isHumanVictoryTarget(state,'player-2'),false);
  human.effects=[];
  assert.equal(isHumanVictoryTarget(state,'player-3'),false);
});

import { simulate } from './simulate.js';
import { MIRACLE_DEFINITIONS, DISASTER_DEFINITIONS, JUDGMENT_DEFINITIONS } from '../src/game/cards.js';

const count = Number(process.argv[2] ?? 150);
if (!Number.isInteger(count) || count < 1) throw new Error('Expected a positive game count.');
const cards = new Set(), judgments = new Set(), pairs = new Set(), choices = new Set();
let actions = 0;
for (let i = 0; i < count; i++) {
  const seed = `audit-${i}`;
  const game = simulate(seed, { preferReaction: i % 2 === 1, observe: ({state, card, action}) => {
    const active = Object.values(state.activeJudgments ?? {});
    for (const id of active) judgments.add(id);
    if (action.type === 'playMiracle' && card && /^(miracle|disaster)-/.test(card.definitionId)) {
      cards.add(card.definitionId);
      for (const id of active) pairs.add(`${card.definitionId}|${id}`);
    }
    if (action.type.startsWith('resolve') && action.choice) choices.add(`${action.type}:${action.choice}`);
  }});
  actions += game.actions;
}
const allCards = [...MIRACLE_DEFINITIONS, ...DISASTER_DEFINITIONS].map(c => c.definitionId);
const allJudgments = JUDGMENT_DEFINITIONS.map(j => j.id ?? j.judgmentId);
const missingPairs = allCards.flatMap(card => allJudgments.map(judgment => `${card}|${judgment}`)).filter(pair => !pairs.has(pair));
console.log(JSON.stringify({ games: count, actions, cards: [...cards].sort(), missingCards: allCards.filter(x => !cards.has(x)), judgments: [...judgments].sort(), missingJudgments: allJudgments.filter(x => !judgments.has(x)), observedCardJudgmentPairs: [...pairs].sort(), missingCardJudgmentPairs: missingPairs, observedChoices: [...choices].sort() }, null, 2));

import { JUDGMENT_DOMAINS } from './constants.js';

export const JUDGMENTS = Object.freeze({
  DISORDER_I: {
    id: 'disorder-1',
    name: '失序 I',
    domain: JUDGMENT_DOMAINS.ORDER,
    tier: 1,
    upgradesTo: 'disorder-2',
    text: '本場物資克制關係顛倒為：群羊＜糧食＜金錢＜群羊。此效果僅影響物資的合法出牌與比較判定，不改變物資原始數字與使命進度。',
  },
  DISORDER_II: {
    id: 'disorder-2',
    name: '失序 II',
    domain: JUDGMENT_DOMAINS.ORDER,
    tier: 2,
    replaces: 'disorder-1',
    text: '在【失序 I】基礎上，物資數字大小關係亦顛倒：數字較小者視為較大。此效果不改變物資原始數字與使命進度。',
  },
  BLINDNESS_I: {
    id: 'blindness-1',
    name: '蒙蔽 I',
    domain: JUDGMENT_DOMAINS.INFORMATION,
    tier: 1,
    upgradesTo: 'blindness-2',
    text: '本場所有玩家手牌中的神蹟與災難皆保持覆蓋。玩家於正常行動欲打出神蹟或災難時，先選擇牌型，再從手牌中該牌型的牌隨機打出1張。新抽到或取得的神蹟與災難亦直接以覆蓋狀態加入手牌。',
  },
  BLINDNESS_II: {
    id: 'blindness-2',
    name: '蒙蔽 II',
    domain: JUDGMENT_DOMAINS.INFORMATION,
    tier: 2,
    replaces: 'blindness-1',
    text: '在【蒙蔽 I】基礎上，神蹟與災難不再區分牌型，統一視為未知功能牌。玩家於正常行動欲打出功能牌時，從所有未知功能牌中隨機打出1張。',
  },
  SINKING_I: {
    id: 'sinking-1',
    name: '沉淪 I',
    domain: JUDGMENT_DOMAINS.CALAMITY,
    tier: 1,
    text: '本場每當一張單體災難對其原目標實際生效時，該玩家左右相鄰的玩家亦受到相同效果。因【沉淪】受到效果的玩家不視為該災難的目標，且不會再次觸發【沉淪】。',
  },
  WAR_I: {
    id: 'war-1',
    name: '爭戰 I',
    domain: JUDGMENT_DOMAINS.BATTLEFIELD,
    tier: 1,
    upgradesTo: 'war-2',
    text: '每名玩家每輪1次，在自己的正常行動開始時，可以棄置1張手牌；若如此做，抽1張牌。',
  },
  WAR_II: {
    id: 'war-2',
    name: '爭戰 II',
    domain: JUDGMENT_DOMAINS.BATTLEFIELD,
    tier: 2,
    replaces: 'war-1',
    text: '每名玩家每輪1次，在自己的正常行動開始時，可以棄置至多2張手牌；若如此做，抽取等量的牌。',
  },
  OVERTURN_I: {
    id: 'overturn-1',
    name: '傾覆 I',
    domain: JUDGMENT_DOMAINS.MUTATION,
    tier: 1,
    upgradesTo: 'overturn-2',
    text: '本場所有位於效果區的神蹟、災難、【增益】與【負面】，若造成單次手牌數或火種數的增加或減少，其絕對值最多為3。',
  },
  OVERTURN_II: {
    id: 'overturn-2',
    name: '傾覆 II',
    domain: JUDGMENT_DOMAINS.MUTATION,
    tier: 2,
    replaces: 'overturn-1',
    text: '在【傾覆 I】基礎上，所有位於效果區的神蹟、災難、【增益】與【負面】，其造成的單次手牌數或火種數變動值固定為1。',
  },
  DIVISION_I: {
    id: 'division-1',
    name: '分裂 I',
    domain: JUDGMENT_DOMAINS.RESOURCE,
    tier: 1,
    upgradesTo: 'division-2',
    text: '本場每當一名玩家於行動或效果結算中一次獲得5點及以上火種時，當前火種最少的玩家獲得2點火種。若有多人並列最少，由該次獲得火種的玩家選擇其中1名。輪末使徒 Jackpot 不觸發此效果。',
  },
  DIVISION_II: {
    id: 'division-2',
    name: '分裂 II',
    domain: JUDGMENT_DOMAINS.RESOURCE,
    tier: 2,
    replaces: 'division-1',
    text: '在【分裂 I】基礎上，火種最少的玩家改為獲得3點火種。若該次獲得5點及以上火種的玩家自己就是火種最少者，則改由火種次少的玩家獲得；若多人並列，仍由該玩家選擇其中1名。',
  },
  REVELATION_I: {
    id: 'revelation-1',
    name: '揭露 I',
    domain: JUDGMENT_DOMAINS.INFORMATION,
    tier: 1,
    upgradesTo: 'revelation-2',
    text: '本場每當任意玩家透過抽牌、取得、交換或其他方式，使正常牌加入手牌時，須先向所有玩家公開該牌，再加入手牌。',
  },
  REVELATION_II: {
    id: 'revelation-2',
    name: '揭露 II',
    domain: JUDGMENT_DOMAINS.INFORMATION,
    tier: 2,
    replaces: 'revelation-1',
    text: '本場所有玩家的全部手牌皆保持公開。',
  },
});

const BY_ID = Object.freeze(
  Object.fromEntries(Object.values(JUDGMENTS).map((judgment) => [judgment.id, judgment])),
);

export function getJudgment(id) {
  const judgment = BY_ID[id];
  if (!judgment) throw new Error(`Unknown judgment: ${id}`);
  return judgment;
}

export function activateJudgment(state, judgmentId) {
  const incoming = getJudgment(judgmentId);
  const previousId = state.judgments.activeByDomain[incoming.domain] ?? null;
  const previous = previousId ? getJudgment(previousId) : null;

  // Different judgments in the same domain replace one another. A tier-II
  // judgment also naturally replaces its tier-I predecessor in that domain.
  state.judgments.activeByDomain[incoming.domain] = incoming.id;
  state.judgments.history.push({
    judgmentId: incoming.id,
    domain: incoming.domain,
    replacedJudgmentId: previous?.id ?? null,
    round: state.round,
  });

  if (incoming.upgradesTo && !state.judgments.deck.includes(incoming.upgradesTo)) {
    state.judgments.deck.push(incoming.upgradesTo);
  }

  // Once a judgment appears, that exact card is removed from the future deck.
  state.judgments.deck = state.judgments.deck.filter((id) => id !== incoming.id);
  return { active: incoming, replaced: previous };
}

export function getActiveJudgments(state) {
  return Object.values(state.judgments.activeByDomain).map(getJudgment);
}

export function createBaseJudgmentDeck() {
  return [
    'disorder-1',
    'blindness-1',
    'sinking-1',
    'war-1',
    'overturn-1',
    'division-1',
    'revelation-1',
  ];
}

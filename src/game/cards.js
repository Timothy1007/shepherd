export const RESOURCE_TYPES = Object.freeze(['sheep', 'food', 'money']);

const RESOURCE_DEFINITIONS = ['sheep', 'food', 'money', 'grace'].flatMap((type) =>
  Array.from({ length: 9 }, (_, index) => Object.freeze({
    definitionId: `${type}-${index + 1}`,
    kind: 'resource',
    type,
    number: index + 1,
    name: type === 'grace' ? `恩典 ${index + 1}` : undefined,
    image: `assets/cards/${type}-${index + 1}.png`,
    copies: type === 'grace' ? 1 : 3,
  })),
);

export const MIRACLE_DEFINITIONS = Object.freeze([
  Object.freeze({
    definitionId: 'miracle-01',
    kind: 'miracle',
    name: '回轉歸向',
    tags: Object.freeze(['新生']),
    text: '改變出牌方向，然後你抽1張牌',
    image: 'assets/miracles/miracle-01.png',
    copies: 1,
  }),
  Object.freeze({
    definitionId: 'miracle-05',
    kind: 'miracle',
    name: '如風吹來',
    tags: Object.freeze(['流轉', '火種']),
    text: '你選擇：棄置1至3張手牌，然後抽取等量的牌 或 不棄置手牌，獲得3點火種',
    image: 'assets/miracles/miracle-05.png',
    copies: 1,
  }),
]);

export const CARD_DEFINITIONS = Object.freeze([
  ...RESOURCE_DEFINITIONS,
  ...MIRACLE_DEFINITIONS,
]);

export const DEFINITION_BY_ID = new Map(CARD_DEFINITIONS.map((definition) => [definition.definitionId, definition]));

export function createResourceDeck() {
  const cards = [];
  for (const definition of CARD_DEFINITIONS) {
    for (let copy = 1; copy <= definition.copies; copy += 1) {
      cards.push({ instanceId: `${definition.definitionId}#${copy}`, definitionId: definition.definitionId });
    }
  }
  return cards;
}

export function getDefinition(card) {
  const definition = DEFINITION_BY_ID.get(card?.definitionId);
  if (!definition) throw new Error(`Unknown card definition: ${card?.definitionId}`);
  return definition;
}

export function effectiveType(card, declaredType = card?.declaredType) {
  const definition = getDefinition(card);
  if (definition.kind !== 'resource') return null;
  return definition.type === 'grace' ? declaredType : definition.type;
}

export function beats(challenger, current) {
  return (challenger === 'sheep' && current === 'food')
    || (challenger === 'food' && current === 'money')
    || (challenger === 'money' && current === 'sheep');
}

export function isLegalResourcePlay(card, currentResource, declaredType) {
  const definition = getDefinition(card);
  if (definition.kind !== 'resource') return false;
  const type = definition.type === 'grace' ? declaredType : definition.type;
  if (!RESOURCE_TYPES.includes(type)) return false;
  if (!currentResource) return true;
  if (type === currentResource.type) return definition.number >= currentResource.number;
  return beats(type, currentResource.type);
}

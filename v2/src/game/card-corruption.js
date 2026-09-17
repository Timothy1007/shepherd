export const RESOURCE_CORRUPTION = Object.freeze({
  grace: 0,
  low: 0,
  high: 1,
});

export function getResourceCorruption({ resourceType, printedNumber }) {
  if (resourceType === 'grace') return RESOURCE_CORRUPTION.grace;
  if (!Number.isInteger(printedNumber) || printedNumber < 1 || printedNumber > 9) {
    throw new RangeError('Resource printedNumber must be an integer from 1 to 9');
  }
  return printedNumber <= 4 ? RESOURCE_CORRUPTION.low : RESOURCE_CORRUPTION.high;
}

export const MIRACLE_CORRUPTION = Object.freeze({
  '回轉歸向': 2,
  '行曠野之路': 2,
  '荊棘冠冕': 3,
  '於水中重生': 3,
  '如風吹來': 1,
  '所望之實底': 1,
  '行向水深之處': 3,
  '拆毀後重建': 2,
  '勝利歸於我們': 3,
  '恰如飛鳥經過': 2,
  '窄門與窄路': 3,
  '分杯之火': 1,
  '在黎明前叩門': 1,
  '杯滿盈溢': 2,
  '三股合成繩': 3,
  '越過長夜': 1,
  '替罪羊': 3,
  '空墳墓': 2,
  '拆毀堅固營壘': 2,
  '勝過死亡': 2,
  '焚而不毀荊棘': 2,
  '雨幕之下': 1,
  '第二次生命': 2,
  '劫後餘生': 3,
});

export const DISASTER_CORRUPTION = Object.freeze({
  '方舟之外': 3,
  '謊言與試探': 2,
  '告別舊時代': 3,
  '半朽蜜果': 2,
  '瞳中倒影': 2,
  '蟲災': 2,
  '灰與燼': 3,
  '積財寶在地上': 2,
  '瘟疫': 4,
  '染血銀幣': 2,
  '盜火': 2,
  '哈米吉多頓': 4,
  '破碎玻璃海': 4,
  '愛慾之種': 3,
  '焚城之火': 3,
  '虛謊之舌': 2,
  '三分之一的星辰': 4,
  '倒塌帳幕': 3,
});

export function getCardCorruption(card) {
  if (!card || typeof card !== 'object') {
    throw new TypeError('card is required');
  }

  if (card.type === 'resource') {
    return getResourceCorruption({
      resourceType: card.resourceType,
      printedNumber: card.printedNumber,
    });
  }

  if (card.type === 'miracle') {
    if (!(card.name in MIRACLE_CORRUPTION)) {
      throw new Error(`Unknown V2 miracle corruption value: ${card.name}`);
    }
    return MIRACLE_CORRUPTION[card.name];
  }

  if (card.type === 'disaster') {
    if (!(card.name in DISASTER_CORRUPTION)) {
      throw new Error(`Unknown V2 disaster corruption value: ${card.name}`);
    }
    return DISASTER_CORRUPTION[card.name];
  }

  return 0;
}

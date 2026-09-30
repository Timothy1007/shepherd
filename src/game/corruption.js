export const MIRACLE_CORRUPTION = Object.freeze({
  '回轉歸向': 2, '行曠野之路': 2, '荊棘冠冕': 3, '於水中重生': 3,
  '如風吹來': 1, '所望之實底': 1, '行向水深之處': 3, '拆毀後重建': 2,
  '勝利歸於我們': 3, '恰如飛鳥經過': 2, '窄門與窄路': 3, '分杯之火': 1,
  '在黎明前叩門': 1, '杯滿盈溢': 2, '三股合成繩': 3, '越過長夜': 1,
  '替罪羊': 3, '空墳墓': 2, '拆毀堅固營壘': 2, '勝過死亡': 2,
  '焚而不毀荊棘': 2, '雨幕之下': 1, '第二次生命': 2, '劫後餘生': 3,
});

export const DISASTER_CORRUPTION = Object.freeze({
  '方舟之外': 3, '謊言與試探': 2, '告別舊時代': 3, '半朽蜜果': 2,
  '瞳中倒影': 2, '蟲災': 2, '灰與燼': 3, '積財寶在地上': 2,
  '瘟疫': 4, '染血銀幣': 2, '盜火': 2, '哈米吉多頓': 4,
  '破碎玻璃海': 4, '愛慾之種': 3, '焚城之火': 3, '虛謊之舌': 2,
  '三分之一的星辰': 4, '倒塌帳幕': 3,
});

export function getCardCorruption(definition) {
  if (!definition || typeof definition !== 'object') throw new TypeError('A card definition is required.');
  if (definition.kind === 'resource') return definition.type === 'grace' ? 0 : definition.number >= 5 ? 1 : 0;
  if (definition.kind === 'miracle') return MIRACLE_CORRUPTION[definition.name] ?? 0;
  if (definition.kind === 'disaster') return DISASTER_CORRUPTION[definition.name] ?? 0;
  return 0;
}

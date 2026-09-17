export const RESOURCE_TYPES = ['sheep','food','money'];
export const RESOURCE_LABELS = { sheep:'群羊', food:'糧食', money:'金錢', grace:'恩典' };

const res = [];
for (const type of ['sheep','food','money']) {
  for (let n=1;n<=9;n++) {
    for (let copy=1;copy<=3;copy++) res.push({
      id:`${type}-${n}-${copy}`, defId:`${type}-${n}`, type:'resource', resourceType:type, printedNumber:n,
      name:`${RESOURCE_LABELS[type]} ${n}`, corruption:n<=4?0:1, image:`../public/assets/cards/${type}-${n}.png`
    });
  }
}
for (let n=1;n<=9;n++) res.push({
  id:`grace-${n}-1`, defId:`grace-${n}`, type:'resource', resourceType:'grace', printedNumber:n,
  name:`恩典 ${n}`, corruption:0, image:`../public/assets/cards/grace-${n}.png`
});

export const MIRACLES = [
['miracle-01','回轉歸向',2,'改變出牌方向，然後你抽1張牌。','direction'],
['miracle-02','行曠野之路',2,'你下一張成功打出的物資牌在合法性判定時數字+3，可超過一般上限；使命仍計原始數字。','road'],
['miracle-03','荊棘冠冕',3,'你下一張成功打出的物資牌無視出牌限制。','crown'],
['miracle-04','於水中重生',3,'棄置全部手牌，然後抽取等量的正常牌。','rebirth'],
['miracle-05','如風吹來',1,'選擇：棄置1至3張手牌並抽取等量；或不棄置，獲得4點火種。','wind'],
['miracle-06','所望之實底',1,'查看牌庫頂3張，選1張正常牌加入手牌，其餘隨機置於牌庫底。','hope'],
['miracle-07','行向水深之處',3,'依序展示牌庫頂，直到3種牌型、5張正常牌或死亡；死亡會觸發。','deep'],
['miracle-08','拆毀後重建',2,'棄1張手牌；查看牌庫頂5張，選1張正常牌加入手牌；若選神蹟或災難再棄1張。','rebuild'],
['miracle-09','勝利歸於我們',3,'選1名其他玩家。輪末若你或該玩家成為使徒，雙方各選1個未完成使命資源+2。','victory'],
['miracle-10','恰如飛鳥經過',2,'與1名其他玩家各展示1張手牌並交換；若牌型不同，雙方各選1個未完成使命+2。','bird'],
['miracle-11','窄門與窄路',3,'將共3張手牌分配給其他玩家；你抽2張並獲得5點火種。','narrow'],
['miracle-12','分杯之火',1,'支付6點火種；另一名玩家獲得3點火種；你抽1張牌。','cup'],
['miracle-13','在黎明前叩門',1,'你下一次因效果抽牌時額外抽1張；之後棄1張手牌。','dawn'],
['miracle-14','杯滿盈溢',2,'選1名其他玩家；你在「抽1張」與「任一未完成使命+3」中選1項，對方獲得另一項。','overflow'],
['miracle-15','三股合成繩',3,'選至多2名其他玩家，各展示1張手牌；依牌型相同/不同給予使命進度或抽牌。','cord'],
['miracle-16','越過長夜',1,'【應對】免疫下一次災難效果。快速測試版亦可於正常行動打出，獲得本輪災難護盾。','night'],
['miracle-17','替罪羊',3,'【應對】替另一名玩家承受災難；若實際受損，下一次災難少失4火或少棄1張。','scapegoat'],
['miracle-18','空墳墓',2,'【應對】因災難失去火種後，每實際失去3火抽1張，最多2張。','tomb'],
['miracle-19','拆毀堅固營壘',2,'【應對】災難具有2項以上效果時，使其中1項對你無效；之後棄1抽1。','stronghold'],
['miracle-20','勝過死亡',2,'【應對】手牌或火種因效果將由正數降至0時，可保留1。','overcome'],
['miracle-21','焚而不毀荊棘',2,'下一次因災難失去火種後，返還實際損失的一半（向下取整），最多4點。','burning'],
['miracle-22','雨幕之下',1,'選1名玩家，清除其1個持續災難或負面效果；成功則該玩家+2火。','rain'],
['miracle-23','第二次生命',2,'下一次於玩家行動/牌效中獲得火種時，改為抽1張；若抽到物資，再+2火。','second'],
['miracle-24','劫後餘生',3,'依出牌方向，每名玩家可支付4火抽1，或棄1張獲3火；第一名成功執行者額外+2火。','aftermath'],
].map(([defId,name,corruption,text,effect])=>({defId,type:'miracle',name,corruption,text,effect,image:`../public/assets/miracles/${defId}.png`,id:defId}));

export const DISASTERS = [
['disaster-01','方舟之外',3,'每名玩家保留1種物資，從其他種類物資中棄2張，不足則全棄。','ark','group'],
['disaster-02','謊言與試探',2,'選2名其他玩家，各展示1張手牌；你取得其中1張。','lies','multi'],
['disaster-03','告別舊時代',3,'有至少2張手牌的玩家各隨機傳1張給出牌方向下一名符合者。','old','group'],
['disaster-04','半朽蜜果',2,'目標棄牌直到棄2張或僅剩1張；每少棄1張失去3火。','fruit','single'],
['disaster-05','瞳中倒影',2,'手牌最多與次多者各棄1張；並列者皆受影響。','mirror','group'],
['disaster-06','蟲災',2,'目標本輪糧食牌合法性數字-2，最低0；使命仍計原始數字。','locust','single'],
['disaster-07','灰與燼',3,'所有玩家失去4火；本輪因神蹟獲得火種-2，最低0。','ashes','group'],
['disaster-08','積財寶在地上',2,'目標每持有5火失去2火，最多12火。','treasure','single'],
['disaster-09','瘟疫',4,'目標本輪群羊合法性數字-2；成功打出群羊後，移至出牌方向下一名玩家。','plague','single'],
['disaster-10','染血銀幣',2,'目標本輪每次獲得火種時，其中1點給另一名玩家。','coin','single'],
['disaster-11','盜火',2,'目標選擇：失去7火（來源獲得實際損失）或給來源1張手牌。','steal','single'],
['disaster-12','哈米吉多頓',4,'每名有物資手牌者棄最高數字物資；再隨機棄置已出牌區5張。','armageddon','group'],
['disaster-13','破碎玻璃海',4,'直到輪末，單體/多體神蹟與災難的玩家目標改為隨機合法目標。','glass','group'],
['disaster-14','愛慾之種',3,'目標本輪每次因同一效果獲得2張以上正常牌時，須給另一名玩家其中1張。','desire','single'],
['disaster-15','焚城之火',3,'選2名其他玩家；各棄1個增益/持續神蹟，否則失去6火。','cityfire','multi'],
['disaster-16','虛謊之舌',2,'【應對】玩家即將從神蹟獲得抽牌/火種/使命時，削弱其中1項。','tongue','single'],
['disaster-17','三分之一的星辰',4,'依序移出最多15張正常牌至輪末；若死亡成為牌庫頂則停止。','stars','group'],
['disaster-18','倒塌帳幕',3,'所有玩家棄置效果區中的神蹟；有棄到者獲得5火。','tent','group'],
].map(([defId,name,corruption,text,effect,target])=>({defId,type:'disaster',name,corruption,text,effect,target,image:`../public/assets/disasters/${defId}.png`,id:defId}));

export const SPECIALS = [...MIRACLES,...DISASTERS];
export const SPECIAL_BY_ID = Object.fromEntries(SPECIALS.map(c=>[c.defId,c]));

export function createFullDeck() {
  const cards = res.map(c=>({...c}));
  for (const d of SPECIALS) cards.push({...d,id:`${d.defId}-1`});
  return cards;
}

export function beats(challenger,current,{reversed=false}={}) {
  const normal=(challenger==='sheep'&&current==='food')||(challenger==='food'&&current==='money')||(challenger==='money'&&current==='sheep');
  if (!reversed) return normal;
  return (current==='sheep'&&challenger==='food')||(current==='food'&&challenger==='money')||(current==='money'&&challenger==='sheep');
}

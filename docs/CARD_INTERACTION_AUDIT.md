# 神蹟／災難交互盤點（初版，尚未完成）

基準：main 7c8fb0a1d3f3e60373090e804a33ee1f686b9ea6；以 src/game/cards.js、docs/RULES.md 為準。

此表是待人工核對的候選清單，不是已確認適用的完整組合；檔名只代表文字或卡號出現，不能證明測試有斷言該交互。數值和動作的邊界、連鎖順序、各審判同時成立的可達性均需另行核對。

全部功能牌均有蒙蔽 I／II 出牌情境；其餘候選審判依卡面文字作初步標記，不能由缺少標記推定不會受影響。揭露 I／II 對公開資訊或加入手牌、傾覆 I／II 對效果數值、分裂 I／II 對獲得火種、沉淪 I 對單體災難等，均需核對觸發事件與時機。失序及爭戰可能透過持續效果、物資出牌或抽牌間接作用。

| 卡牌 | 卡面效果 | 候選交互（須核實） | 可能相關測試檔（待逐項核實） |
|---|---|---|---|
| 方舟之外（`disaster-01`） | 每名玩家選擇自己手中的1種物資種類並保留該種類的物資牌，然後從其他種類的物資牌中棄置2張；若不足2張，則全部棄置。 | 蒙蔽 I／II；傾覆 I／II（效果數值）；越過長夜（受災難時）；拆毀堅固營壘（須先核對是否有獨立多項效果）；勝過死亡（非支付或主動消耗） | `deep-insight.test.js`, `game.test.js`, `group-disaster.test.js`, `rope-qa.test.js` |
| 謊言與試探（`disaster-02`） | 你選擇2名其他玩家。這2名玩家各自選擇1張手牌並展示，你選擇其中1張取得；另一張由原持有者收回手牌。 | 蒙蔽 I／II；揭露 I／II（資訊）；越過長夜（受災難時）；替罪羊（未受影響時）；拆毀堅固營壘（須先核對是否有獨立多項效果） | `glass-sea.test.js`, `judgment-blindness.test.js`, `multi-target-plunder.test.js`, `reaction-engine.test.js` |
| 告別舊時代（`disaster-03`） | 所有有手牌且未免疫此災難的玩家，各隨機選擇1張手牌，並依出牌方向同時給予下一名符合條件的玩家。若符合條件的玩家不足2名，此效果不生效。 | 蒙蔽 I／II；揭露 I／II（資訊）；傾覆 I／II（效果數值）；越過長夜（受災難時）；拆毀堅固營壘（須先核對是否有獨立多項效果）；勝過死亡（非支付或主動消耗） | `farewell-qa.test.js`, `glass-sea.test.js`, `reaction-engine.test.js` |
| 半朽蜜果（`disaster-04`） | 你選擇1名其他玩家。該玩家棄置手牌，直到棄置2張或只剩1張手牌；每少棄置1張，該玩家失去3點火種。 | 蒙蔽 I／II；揭露 I／II（資訊）；傾覆 I／II（效果數值）；沉淪 I（擴散）；越過長夜（受災難時）；替罪羊（未受影響時）；空墳墓（災難失火）；拆毀堅固營壘不適用（失火為未棄足的替代後果）；勝過死亡（非支付或主動消耗） | `ai-resolution-timing.test.js`, `false-tongue-game.test.js`, `fruit-immunity.test.js`, `fruit-qa.test.js`, `glass-sea.test.js`, `judgment-reaction-combinations.test.js` |
| 瞳中倒影（`disaster-05`） | 手牌數量最高與次高的玩家，各棄置1張手牌；若有並列，並列玩家皆棄置。 | 蒙蔽 I／II；揭露 I／II（資訊）；傾覆 I／II（效果數值）；越過長夜（受災難時）；替罪羊（未受影響時）；拆毀堅固營壘（須先核對是否有獨立多項效果）；勝過死亡（非支付或主動消耗） | `reflection-qa.test.js` |
| 蟲災（`disaster-06`） | 你選擇1名其他玩家，將此牌置於該玩家的效果區。本輪內，該玩家打出的糧食牌在出牌判定時數字-2，最低為0。本輪結束時，棄置此牌。 | 蒙蔽 I／II；傾覆 I／II（效果數值）；沉淪 I（擴散）；越過長夜（受災難時）；替罪羊（未受影響時）；拆毀堅固營壘（須先核對是否有獨立多項效果）；勝過死亡（非支付或主動消耗） | `burning-city.test.js`, `glass-sea.test.js`, `plague.test.js` |
| 灰與燼（`disaster-07`） | 每名玩家失去4點火種。直到本輪結束，所有玩家因神蹟效果獲得的火種減少2點，最低為0。 | 蒙蔽 I／II；傾覆 I／II（效果數值）；分裂 I／II（火種）；越過長夜（受災難時）；空墳墓（災難失火）；拆毀堅固營壘（須先核對是否有獨立多項效果）；勝過死亡（非支付或主動消耗） | `aftermath-qa.test.js`, `cards.test.js`, `cup-qa.test.js`, `game.test.js`, `judgment-revelation.test.js`, `multi-effect.test.js`, `narrow-qa.test.js`, `rain-cover.test.js`, `reaction-engine.test.js`, `steal-fire.test.js`, `tabernacle-qa.test.js` |
| 積財寶在地上（`disaster-08`） | 你選擇1名其他玩家。該玩家每持有5點火種，則失去2點火種，最多失去12點。若該玩家持有的火種不足5點，則不受此效果影響。 | 蒙蔽 I／II；傾覆 I／II（效果數值）；沉淪 I（擴散）；越過長夜（受災難時）；替罪羊（未受影響時）；空墳墓（災難失火）；拆毀堅固營壘（須先核對是否有獨立多項效果）；勝過死亡（非支付或主動消耗） | `cards.test.js`, `disaster-target.test.js`, `game.test.js`, `glass-sea.test.js`, `judgment-blindness.test.js`, `judgment-sinking.test.js`, `reaction-engine.test.js`, `scapegoat.test.js` |
| 瘟疫（`disaster-09`） | 【負面】放置於任意玩家的效果區。本輪內，你成功打出的群羊牌數字-2，最低為0。成功打出群羊牌後，依出牌方向將此牌移至下一名玩家的效果區。本輪結束時，棄置此牌。 | 蒙蔽 I／II；傾覆 I／II（效果數值）；沉淪 I（擴散）；越過長夜（受災難時）；替罪羊（未受影響時）；拆毀堅固營壘（須先核對是否有獨立多項效果）；勝過死亡（非支付或主動消耗） | `game.test.js`, `glass-sea.test.js`, `judgment-blindness.test.js`, `plague.test.js`, `rain-cover.test.js` |
| 染血銀幣（`disaster-10`） | 【負面】你選擇1名其他玩家，將此牌置於其效果區。本輪內，該玩家每次獲得火種後，將其中1點火種給予1名其他玩家。因《染血銀幣》給予而獲得的火種，不會觸發任何《染血銀幣》的效果。本輪結束時，棄置此牌。 | 蒙蔽 I／II；傾覆 I／II（效果數值）；分裂 I／II（火種）；沉淪 I（擴散）；越過長夜（受災難時）；替罪羊（未受影響時）；拆毀堅固營壘（須先核對是否有獨立多項效果）；勝過死亡（非支付或主動消耗） | `bloodied-silver.test.js`, `false-tongue-game.test.js`, `glass-sea.test.js` |
| 盜火（`disaster-11`） | 你選擇1名其他玩家。該玩家選擇：失去7點火種，你獲得等同其實際失去數量的火種；或給予你1張手牌。此效果不影響出牌者。 | 蒙蔽 I／II；揭露 I／II（資訊）；傾覆 I／II（效果數值）；分裂 I／II（火種）；沉淪 I（擴散）；越過長夜（受災難時）；替罪羊（未受影響時）；空墳墓（災難失火）；拆毀堅固營壘（須先核對是否有獨立多項效果）；勝過死亡（非支付或主動消耗） | `glass-sea.test.js`, `steal-fire.test.js` |
| 哈米吉多頓（`disaster-12`） | 每名有物資牌的玩家棄置自己手中數字最高的1張物資牌。然後從已出牌區隨機棄置5張牌；若不足5張，則全部棄置。死亡牌不受此效果影響。 | 蒙蔽 I／II；傾覆 I／II（效果數值）；越過長夜（受災難時）；拆毀堅固營壘（須先核對是否有獨立多項效果）；勝過死亡（非支付或主動消耗） | `armageddon-qa.test.js` |
| 破碎玻璃海（`disaster-13`） | 將此牌置於你的效果區。直到本輪結束，所有單體與多體神蹟、災難在選擇玩家為目標時，改為從合法目標中隨機選擇。群體與擴散效果不受影響。 | 蒙蔽 I／II；越過長夜（受災難時）；拆毀堅固營壘（須先核對是否有獨立多項效果） | `glass-sea.test.js` |
| 愛慾之種（`disaster-14`） | 你選擇1名其他玩家，將此牌置於其效果區。本輪內，當2張以上正常牌因同一效果同時加入你的手牌時，你選擇其中1張給予另一名玩家，然後將此牌移至該玩家的效果區。本輪結束時，棄置此牌。 | 蒙蔽 I／II；揭露 I／II（資訊）；傾覆 I／II（效果數值）；沉淪 I（擴散）；越過長夜（受災難時）；替罪羊（未受影響時）；拆毀堅固營壘（須先核對是否有獨立多項效果）；勝過死亡（非支付或主動消耗） | `desire-seed-game.test.js`, `desire-seed-unit.test.js` |
| 焚城之火（`disaster-15`） | 你選擇2名其他玩家；若其他玩家不足2名，則選擇所有其他玩家。這些玩家各棄置其效果區中的1張【增益】或神蹟牌；若沒有可棄置的牌，則失去6點火種。 | 蒙蔽 I／II；傾覆 I／II（效果數值）；越過長夜（受災難時）；替罪羊（未受影響時）；空墳墓（災難失火）；拆毀堅固營壘（須先核對是否有獨立多項效果）；勝過死亡（非支付或主動消耗） | `burning-bush-qa.test.js`, `burning-city.test.js`, `glass-sea.test.js`, `judgment-reaction-combinations.test.js`, `reaction-engine.test.js` |
| 虛謊之舌（`disaster-16`） | 【應對】當一名玩家即將因神蹟效果抽牌、獲得火種或增加使命進度時打出：若其即將抽牌，則少抽1張；若其即將獲得火種，則少獲得5點；若其即將增加使命進度，則該次進度增加量-2，最低為0。若同時符合多項，由該玩家選擇其中1項受到影響。 | 蒙蔽 I／II；揭露 I／II（資訊）；傾覆 I／II（效果數值）；分裂 I／II（火種） | `false-tongue-game.test.js`, `false-tongue-unit.test.js`, `judgment-reaction-combinations.test.js`, `reaction-only-action.test.js` |
| 三分之一的星辰（`disaster-17`） | 依序將牌庫頂最多15張正常牌移出本輪。若死亡牌因此成為牌庫頂，立即停止此效果，不移動死亡牌。本輪結束時，將因此移出的牌洗回牌庫。 | 蒙蔽 I／II；越過長夜（受災難時）；拆毀堅固營壘（須先核對是否有獨立多項效果） | `stars-qa.test.js` |
| 倒塌的帳幕（`disaster-18`） | 所有玩家棄置自己效果區中的所有神蹟牌。若因此棄置至少1張神蹟牌，則獲得5點火種。 | 蒙蔽 I／II；傾覆 I／II（效果數值）；分裂 I／II（火種）；越過長夜（受災難時）；拆毀堅固營壘（須先核對是否有獨立多項效果）；勝過死亡（非支付或主動消耗） | `tabernacle-qa.test.js` |
| 回轉歸向（`miracle-01`） | 改變出牌方向，然後你抽1張牌。 | 蒙蔽 I／II；揭露 I／II（資訊）；傾覆 I／II（效果數值）；虛謊之舌（神蹟抽牌／火種／使命） | `crown-of-thorns.test.js`, `dawn-knock.test.js`, `deep-insight.test.js`, `desire-seed-game.test.js`, `false-tongue-game.test.js`, `game.test.js`, `hand-exchange.test.js`, `judgment-blindness.test.js`, `second-life-qa.test.js` |
| 行曠野之路（`miracle-02`） | 將此牌置於你的效果區。你下一張成功打出的物資牌數字+3，且可超過原本的數字上限。成功打出物資牌後，你棄置此牌。 | 蒙蔽 I／II；傾覆 I／II（效果數值）；勝過死亡（非支付或主動消耗） | `burning-city.test.js`, `crown-of-thorns.test.js`, `missions.test.js`, `plague.test.js`, `rain-cover.test.js`, `wilderness-road.test.js` |
| 荊棘冠冕（`miracle-03`） | 將此牌置於你的效果區。你下一張成功打出的物資牌無視出牌限制。成功打出物資牌後，你棄置此牌。 | 蒙蔽 I／II；傾覆 I／II（效果數值）；勝過死亡（非支付或主動消耗） | `crown-of-thorns.test.js` |
| 於水中重生（`miracle-04`） | 你棄置全部手牌，然後抽取等量的牌。 | 蒙蔽 I／II；揭露 I／II（資訊）；傾覆 I／II（效果數值）；勝過死亡（非支付或主動消耗）；虛謊之舌（神蹟抽牌／火種／使命） | `desire-seed-game.test.js`, `false-tongue-game.test.js`, `game.test.js`, `rebirth.test.js`, `second-life-qa.test.js` |
| 如風吹來（`miracle-05`） | 你選擇：棄置1至3張手牌，然後抽取等量的牌；或不棄置手牌，獲得4點火種。 | 蒙蔽 I／II；揭露 I／II（資訊）；傾覆 I／II（效果數值）；分裂 I／II（火種）；勝過死亡（非支付或主動消耗）；虛謊之舌（神蹟抽牌／火種／使命） | `bloodied-silver.test.js`, `false-tongue-game.test.js`, `game.test.js`, `judgment-reaction-combinations.test.js`, `miracle-wind.test.js`, `second-life-qa.test.js` |
| 所望之實底（`miracle-06`） | 你查看牌庫頂3張牌，選擇其中1張加入手牌，其餘牌以隨機順序置於牌庫底。 | 蒙蔽 I／II；揭露 I／II（資訊） | `dawn-knock.test.js`, `hope-insight.test.js` |
| 行向水深之處（`miracle-07`） | 你依序展示牌庫頂的牌，直到展示出3種不同牌型、共展示5張，或展示到死亡牌為止。若未展示死亡牌，選擇其中1張加入手牌，其餘牌以隨機順序置於牌庫底。若展示到死亡牌，依死亡規則處理，先前已展示的正常牌以隨機順序置於牌庫底，本效果不再取得牌。 | 蒙蔽 I／II；揭露 I／II（資訊） | `deep-insight.test.js` |
| 拆毀後重建（`miracle-08`） | 你棄置1張手牌。若如此做，你查看棄牌區最上方5張牌，選擇其中1張加入手牌。若你選擇的是神蹟或災難，你再棄置1張手牌。 | 蒙蔽 I／II；揭露 I／II（資訊）；傾覆 I／II（效果數值）；勝過死亡（非支付或主動消耗） | `rebuild-after-destruction.test.js` |
| 勝利歸於我們（`miracle-09`） | 你選擇1名其他玩家，並將此牌置於你的效果區。本輪結算時，若你或該玩家成為使徒，你與該玩家各選擇1種尚未完成的使命物資，使該物資的使命進度+2，然後你棄置此牌。 | 蒙蔽 I／II；傾覆 I／II（效果數值）；勝過死亡（非支付或主動消耗）；虛謊之舌（神蹟抽牌／火種／使命） | `cards.test.js`, `game.test.js`, `glass-sea.test.js`, `our-victory.test.js` |
| 恰如飛鳥經過（`miracle-10`） | 你選擇1名其他玩家。若你與該玩家皆有手牌，你們各選擇1張手牌並同時展示。若雙方皆同意，則交換這2張牌；若任一方不同意，則收回展示的牌。若交換的牌型不同，你與該玩家各選擇1種尚未完成的使命物資，使該物資的使命進度+2。 | 蒙蔽 I／II；揭露 I／II（資訊）；傾覆 I／II（效果數值）；勝過死亡（非支付或主動消耗）；虛謊之舌（神蹟抽牌／火種／使命） | `cards.test.js`, `game.test.js`, `glass-sea.test.js`, `hand-exchange.test.js`, `judgment-blindness.test.js` |
| 窄門與窄路（`miracle-11`） | 你將3張手牌任意分配給其他玩家；若不足3張，則將所有手牌分配出去。若你以此法給出至少1張牌，你抽2張牌並獲得5點火種。 | 蒙蔽 I／II；揭露 I／II（資訊）；傾覆 I／II（效果數值）；分裂 I／II（火種）；虛謊之舌（神蹟抽牌／火種／使命） | `desire-seed-game.test.js`, `false-tongue-game.test.js`, `game.test.js`, `narrow-qa.test.js` |
| 分杯之火（`miracle-12`） | 你支付6點火種。若如此做，選擇1名其他玩家，使其獲得3點火種，然後你抽1張牌。 | 蒙蔽 I／II；揭露 I／II（資訊）；傾覆 I／II（效果數值）；分裂 I／II（火種）；虛謊之舌（神蹟抽牌／火種／使命） | `cup-qa.test.js`, `false-tongue-game.test.js`, `glass-sea.test.js` |
| 在黎明前叩門（`miracle-13`） | 將此牌置於你的效果區。你下一次因效果抽牌時，抽牌數量+1。抽牌後，你棄置1張手牌，然後棄置此牌。 | 蒙蔽 I／II；揭露 I／II（資訊）；傾覆 I／II（效果數值）；勝過死亡（非支付或主動消耗）；虛謊之舌（神蹟抽牌／火種／使命） | `aftermath-qa.test.js`, `burning-city.test.js`, `cup-qa.test.js`, `dawn-knock.test.js`, `desire-seed-game.test.js`, `false-tongue-game.test.js`, `judgment-reaction-combinations.test.js`, `judgment-war.test.js`, `narrow-qa.test.js`, `overflow-qa.test.js`, `rebirth.test.js`, `rope-qa.test.js` |
| 杯滿盈溢（`miracle-14`） | 你選擇1名其他玩家。然後你選擇：抽1張牌；或選擇1種尚未完成的使命物資，使該物資的使命進度+3。該玩家獲得你未選擇的另一項效果。 | 蒙蔽 I／II；揭露 I／II（資訊）；傾覆 I／II（效果數值）；虛謊之舌（神蹟抽牌／火種／使命） | `false-tongue-game.test.js`, `glass-sea.test.js`, `overflow-qa.test.js` |
| 三股合成的繩子（`miracle-15`） | 你選擇2名其他玩家；若其他玩家不足2名，則選擇所有其他玩家。所有參與玩家各選擇1張手牌並同時展示。若所有展示牌的牌型皆相同，所有參與玩家各選擇1種尚未完成的使命物資，使該物資的使命進度+3；若所有展示牌的牌型皆不同，你抽1張牌，其他參與玩家各選擇1種尚未完成的使命物資，使該物資的使命進度+2。 | 蒙蔽 I／II；揭露 I／II（資訊）；傾覆 I／II（效果數值）；虛謊之舌（神蹟抽牌／火種／使命） | `glass-sea.test.js`, `rope-qa.test.js` |
| 越過長夜（`miracle-16`） | 【應對】當你即將受到災難效果時，你可以打出此牌：你免疫該次效果。 | 蒙蔽 I／II | `bloodied-silver.test.js`, `burning-bush-reaction-regression.test.js`, `burning-city.test.js`, `desire-seed-game.test.js`, `fruit-immunity.test.js`, `glass-sea.test.js`, `reaction-engine.test.js`, `reaction-night.test.js`, `reflection-qa.test.js`, `scapegoat.test.js`, `steal-fire.test.js` |
| 替罪羊（`miracle-17`） | 【應對】當另一名玩家成為災難目標，且你未受到該次災難影響時，你可以打出此牌：將該災難對他的效果轉移給你，且你不能免疫。若你因此實際失去火種或棄置至少1張手牌，將此牌置於你的效果區。你下一次受到災難效果時，你選擇：少失去4點火種，或少棄置1張手牌。成功後，棄置此牌。 | 蒙蔽 I／II；揭露 I／II（資訊）；傾覆 I／II（效果數值）；勝過死亡（非支付或主動消耗） | `armageddon-qa.test.js`, `burning-bush-reaction-regression.test.js`, `burning-city.test.js`, `fruit-immunity.test.js`, `glass-sea.test.js`, `reaction-engine.test.js`, `reflection-qa.test.js`, `scapegoat.test.js`, `steal-fire.test.js`, `tabernacle-qa.test.js` |
| 空墳墓（`miracle-18`） | 【應對】當你即將因災難效果失去火種時，你可以打出此牌：正常失去火種後，你每實際失去3點火種，你抽1張牌，最多抽2張。 | 蒙蔽 I／II；揭露 I／II（資訊）；傾覆 I／II（效果數值）；勝過死亡（非支付或主動消耗）；虛謊之舌（神蹟抽牌／火種／使命） | `burning-bush-reaction-regression.test.js`, `burning-city.test.js`, `false-tongue-game.test.js`, `farewell-qa.test.js`, `fruit-immunity.test.js`, `fruit-qa.test.js`, `judgment-reaction-combinations.test.js`, `reaction-engine.test.js`, `steal-fire.test.js` |
| 拆毀堅固營壘（`miracle-19`） | 【應對】當你即將受到具有2項及以上效果的災難時，你可以打出此牌：選擇其中1項效果，使其對你無效；其餘效果正常結算。結算後，你棄置1張手牌，然後抽1張牌。 | 蒙蔽 I／II；揭露 I／II（資訊）；傾覆 I／II（效果數值）；勝過死亡（非支付或主動消耗）；虛謊之舌（神蹟抽牌／火種／使命） | `burning-bush-reaction-regression.test.js`, `fruit-immunity.test.js`, `multi-effect.test.js`, `reaction-engine.test.js`, `reaction-only-action.test.js`, `reflection-qa.test.js` |
| 勝過死亡（`miracle-20`） | 【應對】當你的手牌數量或火種即將因其他牌或人物效果從1以上降至0時，你可以打出此牌：選擇其中一項，使其保留1。此效果不能用於支付代價、整備或其他由你主動選擇的消耗。 | 蒙蔽 I／II；揭露 I／II（資訊） | `burning-bush-reaction-regression.test.js`, `burning-city.test.js`, `fruit-immunity.test.js`, `judgment-reaction-combinations.test.js`, `reaction-engine.test.js` |
| 焚而不毀的荊棘（`miracle-21`） | 將此牌置於你的效果區。本輪你下一次因災難效果實際失去火種後，獲得等同此次實際失去火種一半的火種，向下取整，最多獲得4點，然後棄置此牌。 | 蒙蔽 I／II；傾覆 I／II（效果數值）；分裂 I／II（火種）；勝過死亡（非支付或主動消耗）；虛謊之舌（神蹟抽牌／火種／使命） | `burning-bush-qa.test.js`, `burning-bush-reaction-regression.test.js`, `false-tongue-game.test.js`, `fruit-qa.test.js`, `steal-fire.test.js` |
| 雨幕之下（`miracle-22`） | 你選擇1名玩家，棄置其效果區中的1張持續災難牌，或清除其1個【負面】。若成功，該玩家獲得2點火種。 | 蒙蔽 I／II；傾覆 I／II（效果數值）；分裂 I／II（火種）；勝過死亡（非支付或主動消耗）；虛謊之舌（神蹟抽牌／火種／使命） | `bloodied-silver.test.js`, `game.test.js`, `glass-sea.test.js`, `judgment-blindness.test.js`, `plague.test.js`, `rain-cover.test.js` |
| 第二次生命（`miracle-23`） | 將此牌置於你的效果區。你下一次於玩家行動或卡牌效果結算中獲得火種時，先棄置此牌，然後抽1張牌；若抽到物資牌，再獲得2點火種。 | 蒙蔽 I／II；揭露 I／II（資訊）；傾覆 I／II（效果數值）；分裂 I／II（火種）；勝過死亡（非支付或主動消耗）；虛謊之舌（神蹟抽牌／火種／使命） | `aftermath-qa.test.js`, `cup-qa.test.js`, `false-tongue-game.test.js`, `judgment-reaction-combinations.test.js`, `narrow-qa.test.js`, `second-life-qa.test.js`, `steal-fire.test.js` |
| 劫後餘生（`miracle-24`） | 從你開始，依目前出牌方向，每名玩家可選擇一項：支付4點火種，然後抽1張牌；或棄置1張手牌，然後獲得3點火種。無法支付代價者不能選擇該項。首名成功完成其中一項的玩家額外獲得2點火種。 | 蒙蔽 I／II；揭露 I／II（資訊）；傾覆 I／II（效果數值）；分裂 I／II（火種）；勝過死亡（非支付或主動消耗）；虛謊之舌（神蹟抽牌／火種／使命） | `aftermath-qa.test.js`, `false-tongue-game.test.js` |

## 可計數的初階對照表（仍未完成）

`docs/CARD_INTERACTION_MATRIX.csv` 固定列出 42 張牌 × 13 種單一審判（546 列）及 42 張牌 × 6 張應對牌（252 列），共 798 列。這些是**待審候選配對**，不是全部合法組合，也不涵蓋多重審判、不同參與者、連鎖順序、替代分支或邊界值。

截至本次：94 列可指向至少一個經核對的具體結果或選項斷言，2 列有規則及測試證據可排除（半朽蜜果、瞳中倒影／拆毀堅固營壘），另有 702 列仍須判斷是否適用、核查既有測試；這 702 列**不是確定缺少測試，也不是確定要新增 702 個測試**。已有局部結果斷言亦不代表所有分支已覆蓋；其中告別舊時代的三項應對只檢查了單一場景的選項，不代表應對後的完整結算已驗證。模擬出現欄只記錄指定牌與審判同時出現，不證明交互生效或結果正確。

這是一份可逐列審核的中間成果，尚不能以此計算進入下一階段前的全部剩餘工作。

## 已核對的指定情境

以下 9 個是測試案例數，不等於完整交互總數。其具體斷言見 `tests/judgment-reaction-combinations.test.js`：

- 2：半朽蜜果＋沉淪 I，分別搭配傾覆 I／II。
- 1：半朽蜜果失火→空墳墓→黎明加抽→虛謊之舌減抽→棄牌。
- 2：第二次生命＋分裂 I＋揭露 II，分別搭配傾覆 I／II。
- 4：焚城之火＋空墳墓／勝過死亡，傾覆 I／II 與出牌方向正／逆各一例。

## 已觀察但不能視為結果驗證

`scripts/audit-simulations.js` 在 150 場對局觀察到 42 張功能牌與 13 種單一審判的 546 種配對；此記錄代表當時有成功執行該行動，不代表逐項數值或應對結果正確。

## 依風險進行驗證（2026-10-23）

使用者確認不再以逐一測完候選配對作為目標。優先檢驗有數值依賴、應對、死亡觸發、連續選擇與多個效果互相暫停／恢復的流程。原始對照表保留供追查，但待審列數不等於需要新增的測試數。

- `tests/judgment-reaction-combinations.test.js`：9 個既有針對審判、應對與連鎖結算的案例，結果以各自斷言為準。
- `tests/risk-regression.test.js`：12 個固定種子完整對局，每一步檢查 `listCardLocations` 列出的牌實例沒有重複，並檢查到達第七輪與遊戲結束；包含選擇應對和一般選擇兩種模擬偏好。另檢查星辰停在死亡牌後，再由神蹟抽出才觸發死亡輪。
- 額外執行 100 個固定種子完整模擬（`risk-0` 至 `risk-99`），29,170 步均無卡住、失敗動作或已列牌區內重複實例。模擬器每次採用第一個合法選項，半數優先使用可用應對，不代表涵蓋所有選擇與數值。

牌區唯一性檢查不等於牌數守恆檢查：部分結算中的實體牌可能暫存在待處理狀態，而沉淪可產生虛擬持續效果。這些需要區分後才可逐步核對物理牌數。

## 未完成

1. 依風險核對間接觸發、負面狀態和同時觸發次序，優先補足可能出現的數值或結算問題。
2. 對高風險情境核對現有測試的 setup、行動與斷言；不得只憑檔名、關鍵字或模擬出現判定效果正確。
3. 依已確認的風險與實際測試缺口安排後續驗證，不以窮舉所有配對或計算理論總數作為完成條件。
4. 真人操作介面與視覺驗收不包含在此盤點。

目前無法從此初版給出有效組合總數或剩餘數。

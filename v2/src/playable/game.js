import { createFullDeck, SPECIAL_BY_ID, RESOURCE_TYPES, beats } from './cards.js';

function hashSeed(input='shepherd-v2') { let h=2166136261>>>0; for(const ch of String(input)){h^=ch.charCodeAt(0);h=Math.imul(h,16777619);} return h>>>0; }
function mulberry32(a){return()=>{let t=a+=0x6D2B79F5;t=Math.imul(t^t>>>15,t|1);t^=t+Math.imul(t^t>>>7,t|61);return((t^t>>>14)>>>0)/4294967296;};}
function clone(v){return structuredClone(v);}
function pick(rng,arr){return arr[Math.floor(rng()*arr.length)];}
function shuffle(rng,arr){const a=[...arr];for(let i=a.length-1;i>0;i--){const j=Math.floor(rng()*(i+1));[a[i],a[j]]=[a[j],a[i]];}return a;}
const LABEL={sheep:'群羊',food:'糧食',money:'金錢'};

const CHARACTER_POOL=[
  ['jeweler','珠寶商','富饒城邦'],['governor','統御官','富饒城邦'],['king','君王','富饒城邦'],['dragon','龍騎士','富饒城邦'],
  ['paladin','聖騎士','榮光聖殿'],['prophet','先知','榮光聖殿'],['priest','祭司','榮光聖殿'],['alchemist','煉金術師','榮光聖殿'],
  ['moon','朔月使','彼岸之使'],['dreamer','織夢者','彼岸之使'],['faerie','花妖精','彼岸之使'],['wordeater','噬言靈','彼岸之使'],
  ['lostsheep','迷失之羊','流火之民'],['claw','不屈之爪','流火之民'],['spear','追獵之矛','流火之民'],['ashflower','灰燼之花','流火之民'],
].map(([id,name,faction])=>({id,name,faction}));

const BLESSINGS=[
 ['newbirth','破土與新生','每輪第一次成功打出物資後抽1張。'],
 ['branch','新生理想的枝椏','每輪1次，可棄至多2張再抽等量。'],
 ['money+','安眠於不朽處','金錢合法性數字+1。'],['food+','豐盛的應許','糧食合法性數字+1。'],['sheep+','迷途亦有歸途','群羊合法性數字+1。'],
 ['echo','群星的迴響','每輪1次，額外疊1張物資作合法性判定。'],['firemagic','燈與火的魔法','每輪第一次獲得火種時額外+2。'],
 ['embers','重燃舊日的餘燼','每輪開始獲得2低溫火種。'],['trick','焰火戲法','每輪1次支付火種時可棄1張，使支付-4。'],
 ['calamity','荒唐白日夢','每輪1次，因災難少失3火或少棄1張。'],['gate','越過災厄的門扉','一次性：本輪不能成為單體災難目標。'],
 ['sail','駛向新生命','每輪1次支付3火，從棄牌區隨機3選1，再棄1。'],['silence','偶爾需要沉默','一次性：無效一張非應對神蹟或災難。'],
 ['peace','願紛爭止息','每輪1次支付2火，清除1個增益或負面。'],['memory','遺忘記憶的顏色','每輪1次清除自己的1個增益或負面，成功則抽1。'],
 ['wind','讓風替你承受','每輪1次支付3火，轉移1個可轉移狀態。'],['headwind','逆風揚帆啟航','每輪第一次手牌降至2以下，抽1。'],
 ['crossroad','抉擇十字路口','每輪1次，查看另一玩家隨機2張手牌。'],['ripple','往昔的漣漪','每輪1次，下一次效果抽牌的第1張改從棄牌區隨機取得。'],
 ['fate','命運從未公平','每輪1次，查看牌庫頂3張並可將1張正常牌置底。'],['butterfly','蝴蝶效應','每輪1次，查看另一玩家手牌；若至少2種牌型，+2火。'],
 ['oldfire','舊火與柴薪','每輪1次，1張手牌與棄牌區隨機正常牌交換。'],['offering','生命理應有所奉獻','負面：每輪第一次正常行動，若有群羊則棄1張。'],
 ['burial','埋葬於無風之地','負面：失去火種時額外失去2，每輪最多6。']
].map(([id,name,text])=>({id,name,text}));

const JUDGMENTS={
 'disorder-1':{id:'disorder-1',name:'失序 I',domain:'秩序',tier:1,upgrade:'disorder-2',text:'物資克制關係顛倒。'},
 'disorder-2':{id:'disorder-2',name:'失序 II',domain:'秩序',tier:2,text:'克制關係與數字大小關係皆顛倒。'},
 'blindness-1':{id:'blindness-1',name:'蒙蔽 I',domain:'資訊',tier:1,upgrade:'blindness-2',text:'神蹟與災難保持覆蓋；先選牌型，再隨機打出。'},
 'blindness-2':{id:'blindness-2',name:'蒙蔽 II',domain:'資訊',tier:2,text:'神蹟與災難全部混為未知功能牌，隨機打出。'},
 'sinking-1':{id:'sinking-1',name:'沉淪 I',domain:'災變',tier:1,text:'單體災難對原目標生效時，左右玩家也受到相同效果。'},
 'war-1':{id:'war-1',name:'爭戰 I',domain:'戰局',tier:1,upgrade:'war-2',text:'每輪1次，正常行動開始時可棄1抽1。'},
 'war-2':{id:'war-2',name:'爭戰 II',domain:'戰局',tier:2,text:'每輪1次，正常行動開始時可棄至多2張，再抽等量。'},
 'overturn-1':{id:'overturn-1',name:'傾覆 I',domain:'異變',tier:1,upgrade:'overturn-2',text:'效果區造成的單次手牌/火種變動絕對值最多3。'},
 'overturn-2':{id:'overturn-2',name:'傾覆 II',domain:'異變',tier:2,text:'效果區造成的單次手牌/火種變動固定為1。'},
 'division-1':{id:'division-1',name:'分裂 I',domain:'資源',tier:1,upgrade:'division-2',text:'一次獲得5火以上時，火種最少者+2。'},
 'division-2':{id:'division-2',name:'分裂 II',domain:'資源',tier:2,text:'一次獲得5火以上時，火種最少者+3。'},
 'revelation-1':{id:'revelation-1',name:'揭露 I',domain:'資訊',tier:1,upgrade:'revelation-2',text:'正常牌加入手牌前先公開。'},
 'revelation-2':{id:'revelation-2',name:'揭露 II',domain:'資訊',tier:2,text:'所有玩家手牌保持公開。'},
};
const BASE_JUDGMENTS=['disorder-1','blindness-1','sinking-1','war-1','overturn-1','division-1','revelation-1'];

function makePlayer(id,name,human=false,character=null){return{id,name,human,character,fire:8,lowFire:0,hand:[],mission:null,missionProgress:{sheep:0,food:0,money:0},completedMissions:0,carry:0,effects:[],blessings:[],round:{},qualified:false};}
function randomMission(rng){const patterns=[[7,7,7],[11,5,5],[4,5,12]];const p=[...pick(rng,patterns)];const keys=shuffle(rng,RESOURCE_TYPES);return Object.fromEntries(keys.map((k,i)=>[k,p[i]]));}

export function createPlayableGame({seed='v2-test',threshold=30,humanCharacter='jeweler'}={}){
 const rng=mulberry32(hashSeed(seed));
 const chars=shuffle(rng,CHARACTER_POOL.filter(c=>c.id!==humanCharacter));
 const humanChar=CHARACTER_POOL.find(c=>c.id===humanCharacter)||CHARACTER_POOL[0];
 const players=[makePlayer('p1','你',true,humanChar),makePlayer('p2','旅人・赫',false,chars[0]),makePlayer('p3','旅人・霧',false,chars[1]),makePlayer('p4','旅人・燼',false,chars[2])];
 const state={version:'2-playable',seed,threshold,corruption:0,totalJudgments:0,round:0,phase:'setup',players,seats:players.map(p=>p.id),direction:1,currentPlayer:null,currentResource:null,currentApostle:null,freeResource:false,deck:[],discard:[],played:[],removed:[],death:{triggered:false,finalOrder:[],remaining:[]},judgmentDeck:shuffle(rng,BASE_JUDGMENTS),activeJudgments:{},judgmentHistory:[],rngSeed:hashSeed(seed),rngCursor:0,passStreak:0,log:[],auction:null,gameOver:false,final:null};
 startRound(state); return state;
}
function rngFor(state){const r=mulberry32((state.rngSeed+state.rngCursor*2654435761)>>>0);state.rngCursor++;return r;}
function rand(state){return rngFor(state)();}
function shuffleState(state,a){return shuffle(rngFor(state),a);}
function player(state,id){const p=state.players.find(x=>x.id===id);if(!p)throw new Error(`Unknown player ${id}`);return p;}
function log(state,msg,type='info'){state.log.unshift({round:state.round,msg,type,at:Date.now()});state.log=state.log.slice(0,80);}

export function startRound(state){
 state.round++; state.phase='actions'; state.currentResource=null; state.currentApostle=null; state.played=[]; state.discard=[]; state.removed=[]; state.passStreak=0; state.freeResource=true;
 state.death={triggered:false,finalOrder:[],remaining:[]};
 const all=createFullDeck(); state.deck=shuffleState(state,all);
 for(const p of state.players){p.effects=[];p.round={firstFire:true,firstResource:true,warUsed:false,blessingUsed:{}};p.missionProgress={sheep:0,food:0,money:0};p.mission=state.round<=6?randomMission(rngFor(state)):null;p.qualified=p.completedMissions>=3; const base=7+(p.carry||0);p.hand=[];drawCards(state,p,base,{initial:true});p.carry=0;if(p.blessings.some(b=>b.id==='embers'))p.lowFire+=2;}
 const depthMin=10,depthMax=18;const depth=Math.min(state.deck.length-1,depthMin+Math.floor(rand(state)*(depthMax-depthMin+1)));state.deck.splice(depth,0,{id:`death-r${state.round}`,defId:'death',type:'death',name:'死亡',corruption:0});
 state.currentPlayer=state.seats[Math.floor(rand(state)*state.seats.length)];
 log(state,`第 ${state.round} 輪開始。${state.round<=6?'公開使命已生成。':'最終使徒爭奪開始。'}`,'round');
 return state;
}

export function currentPlayer(state){return player(state,state.currentPlayer);}
export function activeJudgments(state){return Object.values(state.activeJudgments).map(id=>JUDGMENTS[id]).filter(Boolean);}
function hasJudgment(state,id){return Object.values(state.activeJudgments).includes(id);}
function judgmentDomain(state,domain){const id=state.activeJudgments[domain];return id?JUDGMENTS[id]:null;}

function gainCorruption(state,n,source){if(!n)return;state.corruption+=n;log(state,`${source}使崩壞 +${n}（${state.corruption}/${state.threshold}）`,'corruption');while(state.corruption>=state.threshold && state.judgmentDeck.length){state.corruption-=state.threshold;triggerJudgment(state);} }
function triggerJudgment(state){const idx=Math.floor(rand(state)*state.judgmentDeck.length);const id=state.judgmentDeck.splice(idx,1)[0];const j=JUDGMENTS[id];const replaced=state.activeJudgments[j.domain];state.activeJudgments[j.domain]=id;state.judgmentHistory.push(id);state.totalJudgments++;if(j.upgrade&&!state.judgmentDeck.includes(j.upgrade))state.judgmentDeck.push(j.upgrade);log(state,`審判降臨：${j.name}${replaced?`（覆蓋 ${JUDGMENTS[replaced]?.name||replaced}）`:''}`,'judgment');}

function revealAdded(state,p,c){if(hasJudgment(state,'revelation-1')||hasJudgment(state,'revelation-2'))log(state,`${p.name} 將 ${c.name} 公開後加入手牌。`,'reveal');}
function drawOne(state,p,{initial=false}={}){
 if(!state.deck.length)return null;const c=state.deck.shift();if(c.type==='death'&&!initial){triggerDeath(state,p.id);return drawOne(state,p,{initial});}if(c.type==='death'&&initial){state.deck.push(c);return drawOne(state,p,{initial});}p.hand.push(c);revealAdded(state,p,c);return c;
}
export function drawCards(state,pOrId,count,opts={}){const p=typeof pOrId==='string'?player(state,pOrId):pOrId;const out=[];for(let i=0;i<count;i++){const c=drawOne(state,p,opts);if(c)out.push(c);}return out;}
function triggerDeath(state,revealerId){if(state.death.triggered)return;state.death.triggered=true;const i=state.seats.indexOf(revealerId),order=[];for(let k=1;k<=state.seats.length;k++)order.push(state.seats[(i+state.direction*k+state.seats.length*3)%state.seats.length]);state.death.finalOrder=order;state.death.remaining=[...order];state.phase='death-pending';log(state,`【死亡】被 ${player(state,revealerId).name} 揭示。效果結算後進入最後一圈。`,'death');}
function finishEffect(state){if(state.phase==='death-pending'){state.phase='death-round';state.currentPlayer=state.death.remaining[0];log(state,'死亡輪開始：每名玩家只剩最後 1 次正常行動。','death');}}

function effectiveNumber(state,p,c){let n=c.printedNumber;for(const b of p.blessings){if(b.id==='money+'&&c.resourceType==='money')n++;if(b.id==='food+'&&c.resourceType==='food')n++;if(b.id==='sheep+'&&c.resourceType==='sheep')n++;}if(p.round.road)n+=3;if(p.effects.some(e=>e.id==='locust'&&c.resourceType==='food'))n-=2;if(p.effects.some(e=>e.id==='plague'&&c.resourceType==='sheep'))n-=2;return Math.max(0,n);}
function declaredTypes(c){return c.resourceType==='grace'?RESOURCE_TYPES:[c.resourceType];}
export function isResourceLegal(state,p,c,declared){if(c.type!=='resource')return false;if(state.freeResource||p.round.crown)return true;if(!state.currentResource)return true;const disorder=judgmentDomain(state,'秩序');const rev=disorder?.id==='disorder-1'||disorder?.id==='disorder-2';const numRev=disorder?.id==='disorder-2';const n=effectiveNumber(state,p,c);if(declared===state.currentResource.type)return numRev?n<=state.currentResource.number:n>=state.currentResource.number;return beats(declared,state.currentResource.type,{reversed:rev});}
export function legalActions(state,pid=state.currentPlayer){const p=player(state,pid);if(state.gameOver||p.id!==state.currentPlayer)return[];const out=[];for(const c of p.hand){if(c.type==='resource'){for(const d of declaredTypes(c))if(isResourceLegal(state,p,c,d))out.push({type:'play',cardId:c.id,declared:d});}else out.push({type:'play',cardId:c.id});}if(!out.length&&state.phase==='actions')out.push({type:'prepare'});return out;}

function removeFromHand(p,id){const i=p.hand.findIndex(c=>c.id===id);if(i<0)return null;return p.hand.splice(i,1)[0];}
function randomDiscard(state,p,n=1,filter=()=>true){const eligible=p.hand.filter(filter);const chosen=shuffleState(state,eligible).slice(0,n);for(const c of chosen){removeFromHand(p,c.id);state.discard.push(c);}return chosen;}
function loseFire(state,p,n){let amount=Math.min(p.fire,n);if(p.blessings.some(b=>b.id==='burial')){const extra=Math.min(2,6-(p.round.burialExtra||0));amount=Math.min(p.fire,n+extra);p.round.burialExtra=(p.round.burialExtra||0)+extra;}p.fire-=amount;return amount;}
function gainFire(state,p,n,{jackpot=false,source=''}={}){if(!n)return 0;let amount=n;if(p.round.firstFire&&p.blessings.some(b=>b.id==='firemagic')){amount+=2;p.round.firstFire=false;}if(p.effects.some(e=>e.id==='coin')){const other=pick(rngFor(state),state.players.filter(x=>x.id!==p.id));if(other){other.fire+=1;amount=Math.max(0,amount-1);}}
p.fire+=amount;if(!jackpot&&(hasJudgment(state,'division-1')||hasJudgment(state,'division-2'))&&amount>=5){const min=Math.min(...state.players.map(x=>x.fire));const lows=state.players.filter(x=>x.fire===min&&x.id!==p.id);if(lows.length){const q=pick(rngFor(state),lows);q.fire+=hasJudgment(state,'division-2')?3:2;log(state,`【分裂】使 ${q.name} 獲得補償火種。`,'judgment');}}
return amount;}
function addMission(state,p,type,n){if(!p.mission||state.round>6)return;const cap=p.mission[type]??0;p.missionProgress[type]=Math.min(cap,p.missionProgress[type]+Math.max(0,n));}
function missionCompleted(p){return p.mission&&RESOURCE_TYPES.every(t=>(p.missionProgress[t]||0)>=(p.mission[t]||0));}

function normalizeTarget(state,sourceId,targetId){const pool=state.players.filter(x=>x.id!==sourceId);if(hasJudgment(state,'glass-sea'))return pick(rngFor(state),pool)?.id;return targetId||pick(rngFor(state),pool)?.id;}
function targetOf(state,sourceId,requested){return player(state,normalizeTarget(state,sourceId,requested));}

function resolveMiracle(state,p,c,choice={}){
 const other=()=>targetOf(state,p.id,choice.targetId); switch(c.effect){
 case'direction':state.direction*=-1;drawCards(state,p,1);break;
 case'road':p.round.road=true;break;case'crown':p.round.crown=true;break;
 case'rebirth':{const n=p.hand.length;state.discard.push(...p.hand);p.hand=[];drawCards(state,p,n);break;}
 case'wind':if(choice.mode==='cycle'&&p.hand.length){const n=Math.min(3,Math.max(1,choice.count||1));randomDiscard(state,p,n);drawCards(state,p,n);}else gainFire(state,p,4,{source:c.name});break;
 case'hope':{const seen=[];for(let i=0;i<3;i++){const x=state.deck.shift();if(!x)break;if(x.type==='death'){state.deck.unshift(x);break;}seen.push(x);}if(seen.length){const chosen=seen.sort((a,b)=>(b.corruption||0)-(a.corruption||0))[0];p.hand.push(chosen);for(const x of shuffleState(state,seen.filter(y=>y!==chosen)))state.deck.push(x);}break;}
 case'deep':{let normals=0;const seen=[],types=new Set();while(normals<5&&types.size<3&&state.deck.length){const x=state.deck.shift();if(x.type==='death'){triggerDeath(state,p.id);break;}seen.push(x);normals++;types.add(x.type);}for(const x of shuffleState(state,seen))state.deck.push(x);break;}
 case'rebuild':if(p.hand.length){randomDiscard(state,p,1);const seen=[];for(let i=0;i<5;i++){const x=state.deck.shift();if(!x)break;if(x.type==='death'){state.deck.unshift(x);break;}seen.push(x);}if(seen.length){const chosen=seen[0];p.hand.push(chosen);state.deck.push(...shuffleState(state,seen.slice(1)));if(chosen.type!=='resource'&&p.hand.length)randomDiscard(state,p,1);}}break;
 case'victory':p.effects.push({id:'victory',partner:other().id});break;
 case'bird':{const q=other();if(p.hand.length&&q.hand.length){const a=randomDiscard(state,p,1)[0],b=randomDiscard(state,q,1)[0];if(a&&b){p.hand.push(b);q.hand.push(a);if(a.type!==b.type){addMission(state,p,pick(rngFor(state),RESOURCE_TYPES),2);addMission(state,q,pick(rngFor(state),RESOURCE_TYPES),2);}}}break;}
 case'narrow':{const qs=state.players.filter(x=>x.id!==p.id);for(let i=0;i<3&&p.hand.length;i++){const x=removeFromHand(p,p.hand[0].id);qs[i%qs.length].hand.push(x);}drawCards(state,p,2);gainFire(state,p,5,{source:c.name});break;}
 case'cup':if(p.fire>=6){p.fire-=6;other().fire+=3;drawCards(state,p,1);}break;
 case'dawn':p.effects.push({id:'dawn'});break;
 case'overflow':{const q=other();if(choice.mode==='mission'){addMission(state,p,pick(rngFor(state),RESOURCE_TYPES),3);drawCards(state,q,1);}else{drawCards(state,p,1);addMission(state,q,pick(rngFor(state),RESOURCE_TYPES),3);}break;}
 case'cord':{const qs=shuffleState(state,state.players.filter(x=>x.id!==p.id)).slice(0,2);const cards=[p,...qs].map(x=>pick(rngFor(state),x.hand)).filter(Boolean);const kinds=new Set(cards.map(x=>x.type));if(kinds.size===1){for(const x of [p,...qs])addMission(state,x,pick(rngFor(state),RESOURCE_TYPES),3);}else if(kinds.size===cards.length){drawCards(state,p,1);for(const x of qs)addMission(state,x,pick(rngFor(state),RESOURCE_TYPES),2);}break;}
 case'night':p.effects.push({id:'night'});break;case'scapegoat':p.effects.push({id:'scapegoat'});break;case'tomb':p.effects.push({id:'tomb'});break;case'stronghold':p.effects.push({id:'stronghold'});break;case'overcome':p.effects.push({id:'overcome'});break;case'burning':p.effects.push({id:'burning'});break;
 case'rain':{const q=choice.targetId?player(state,choice.targetId):p;const i=q.effects.findIndex(e=>['locust','plague','coin','desire'].includes(e.id));if(i>=0){q.effects.splice(i,1);gainFire(state,q,2,{source:c.name});}}break;
 case'second':p.effects.push({id:'second'});break;
 case'aftermath':{let first=true;for(const q of seatOrderFrom(state,p.id)){if(q.fire>=4){q.fire-=4;drawCards(state,q,1);if(first){q.fire+=2;first=false;}}else if(q.hand.length){randomDiscard(state,q,1);q.fire+=3;if(first){q.fire+=2;first=false;}}}break;}
 }}

function applyDisasterTo(state,source,target,c){if(target.effects.some(e=>e.id==='night')){target.effects=target.effects.filter(e=>e.id!=='night');log(state,`${target.name} 以「越過長夜」免疫災難。`,'defense');return;}
 const mitigation=target.blessings.some(b=>b.id==='calamity')&&!target.round.calamityUsed;const drop=(n)=>{const actual=randomDiscard(state,target,Math.max(0,n-(mitigation?1:0)));if(mitigation)target.round.calamityUsed=true;return actual.length;};const fire=(n)=>{const actual=loseFire(state,target,Math.max(0,n-(mitigation?3:0)));if(mitigation)target.round.calamityUsed=true;if(target.effects.some(e=>e.id==='tomb')&&actual>=3){drawCards(state,target,Math.min(2,Math.floor(actual/3)));target.effects=target.effects.filter(e=>e.id!=='tomb');}if(target.effects.some(e=>e.id==='burning')){const refund=Math.min(4,Math.floor(actual/2));target.fire+=refund;target.effects=target.effects.filter(e=>e.id!=='burning');}return actual;};
 switch(c.effect){case'fruit':{const d=Math.min(2,Math.max(0,target.hand.length-1));const got=drop(d);fire((2-got)*3);break;}case'locust':target.effects.push({id:'locust'});break;case'treasure':fire(Math.min(12,Math.floor(target.fire/5)*2));break;case'plague':target.effects.push({id:'plague'});break;case'coin':target.effects.push({id:'coin'});break;case'steal':if(target.fire>=7){const a=fire(7);source.fire+=a;}else if(target.hand.length){const x=removeFromHand(target,pick(rngFor(state),target.hand).id);source.hand.push(x);}break;case'desire':target.effects.push({id:'desire'});break;case'cityfire':{const i=target.effects.findIndex(e=>!['locust','plague','coin','desire'].includes(e.id));if(i>=0)target.effects.splice(i,1);else fire(6);break;}case'tongue':fire(0);break;}}
function adjacentPlayers(state,targetId){const i=state.seats.indexOf(targetId),n=state.seats.length;return[player(state,state.seats[(i-1+n)%n]),player(state,state.seats[(i+1)%n])];}
function resolveDisaster(state,p,c,choice={}){
 const others=state.players.filter(x=>x.id!==p.id); if(c.target==='single'){
   const t=targetOf(state,p.id,choice.targetId);applyDisasterTo(state,p,t,c);if(hasJudgment(state,'sinking-1'))for(const q of adjacentPlayers(state,t.id))if(q.id!==p.id&&q.id!==t.id)applyDisasterTo(state,p,q,c);
 } else if(c.target==='multi'){
   const ts=choice.targetIds?.length?choice.targetIds.map(id=>player(state,id)):shuffleState(state,others).slice(0,2);for(const t of ts)applyDisasterTo(state,p,t,c);
 }
 switch(c.effect){
 case'ark':for(const q of state.players){const keep=pick(rngFor(state),RESOURCE_TYPES);randomDiscard(state,q,2,x=>x.type==='resource'&&x.resourceType!=='grace'&&x.resourceType!==keep);}break;
 case'lies':{const ts=shuffleState(state,others.filter(x=>x.hand.length)).slice(0,2);const shown=ts.map(x=>({p:x,c:pick(rngFor(state),x.hand)})).filter(x=>x.c);if(shown.length){const z=shown[0];removeFromHand(z.p,z.c.id);p.hand.push(z.c);}}break;
 case'old':{const eligible=state.players.filter(x=>x.hand.length>=2),moves=eligible.map(q=>({from:q,card:pick(rngFor(state),q.hand)}));for(const m of moves)removeFromHand(m.from,m.card.id);for(const m of moves){let i=state.seats.indexOf(m.from.id);for(let k=1;k<=state.seats.length;k++){const id=state.seats[(i+state.direction*k+state.seats.length*3)%state.seats.length];const q=eligible.find(x=>x.id===id);if(q){q.hand.push(m.card);break;}}}break;}
 case'mirror':{const counts=[...new Set(state.players.map(x=>x.hand.length))].sort((a,b)=>b-a).slice(0,2);for(const q of state.players.filter(x=>counts.includes(x.hand.length)&&x.hand.length))randomDiscard(state,q,1);break;}
 case'ashes':for(const q of state.players)loseFire(state,q,4);state.roundFlags={...(state.roundFlags||{}),miracleFirePenalty:2};break;
 case'armageddon':for(const q of state.players){const rs=q.hand.filter(x=>x.type==='resource'&&x.resourceType!=='grace').sort((a,b)=>b.printedNumber-a.printedNumber);if(rs[0]){removeFromHand(q,rs[0].id);state.discard.push(rs[0]);}}state.played.splice(0,Math.min(5,state.played.length));break;
 case'glass':state.activeJudgments['glass']='glass-sea';break;
 case'stars':for(let i=0;i<15&&state.deck.length;i++){if(state.deck[0].type==='death')break;state.removed.push(state.deck.shift());}break;
 case'tent':for(const q of state.players){const before=q.effects.length;q.effects=q.effects.filter(e=>!['victory','dawn','night','scapegoat','tomb','stronghold','overcome','burning','second'].includes(e.id));if(q.effects.length<before)q.fire+=5;}break;
 }}

function seatOrderFrom(state,startId){const i=state.seats.indexOf(startId),out=[];for(let k=0;k<state.seats.length;k++)out.push(player(state,state.seats[(i+state.direction*k+state.seats.length*3)%state.seats.length]));return out;}
function moveToNext(state){
 if(state.phase==='death-round'){
   state.death.remaining.shift();if(!state.death.remaining.length){settleRound(state);return;}state.currentPlayer=state.death.remaining[0];state.freeResource=false;return;
 }
 const i=state.seats.indexOf(state.currentPlayer);state.currentPlayer=state.seats[(i+state.direction+state.seats.length)%state.seats.length];state.freeResource=false;
}

export function playCard(state,pid,cardId,choice={}){
 if(state.gameOver||state.currentPlayer!==pid)throw new Error('Not this player turn');const p=player(state,pid),c=removeFromHand(p,cardId);if(!c)throw new Error('Card not in hand');
 if(c.type==='resource'){
  const declared=choice.declared||c.resourceType;if(!isResourceLegal(state,p,c,declared)){p.hand.push(c);throw new Error('Illegal resource play');}
  state.played.push(c);state.currentResource={type:declared,number:effectiveNumber(state,p,c),printedNumber:c.printedNumber};state.currentApostle=p.id;addMission(state,p,declared,c.printedNumber);gainCorruption(state,c.corruption,c.name);p.round.road=false;p.round.crown=false;state.passStreak=0;
  if(p.round.firstResource&&p.blessings.some(b=>b.id==='newbirth')){drawCards(state,p,1);p.round.firstResource=false;}
  if(p.effects.some(e=>e.id==='plague')&&declared==='sheep'){p.effects=p.effects.filter(e=>e.id!=='plague');const i=state.seats.indexOf(p.id),next=player(state,state.seats[(i+state.direction+state.seats.length)%state.seats.length]);next.effects.push({id:'plague'});}
 } else {
  state.played.push(c);gainCorruption(state,c.corruption,c.name);state.passStreak=0;if(c.type==='miracle')resolveMiracle(state,p,c,choice);else resolveDisaster(state,p,c,choice);state.freeResource=true;
 }
 log(state,`${p.name} 打出「${c.name}」${c.corruption?`（崩壞 +${c.corruption}）`:''}`,'play');finishEffect(state);if(!state.gameOver&&state.phase!=='settlement')moveToNext(state);return state;
}

export function prepare(state,pid){if(state.currentPlayer!==pid||state.phase!=='actions')throw new Error('Cannot prepare now');const p=player(state,pid);state.discard.push(...p.hand);p.hand=[];drawCards(state,p,2);state.passStreak++;log(state,`${p.name} 進行整備：棄置全手牌並抽2張。`,'prepare');finishEffect(state);if(state.phase==='actions')moveToNext(state);return state;}
export function skipNoLegalAction(state,pid){if(state.currentPlayer!==pid)throw new Error('Not this player turn');if(state.phase!=='death-round')throw new Error('Normal actions cannot be voluntarily skipped');if(legalActions(state,pid).some(a=>a.type==='play'))throw new Error('A legal card can still be played');log(state,`${player(state,pid).name} 在死亡輪沒有合法牌，最後行動自動略過。`,'forced-skip');moveToNext(state);return state;}

function settleRound(state){state.phase='settlement';for(const p of state.players){if(state.round<=6&&missionCompleted(p)){p.completedMissions++;log(state,`${p.name} 完成本輪使命（累計 ${p.completedMissions}）。`,'mission');}p.carry=p.hand.length;p.qualified=p.completedMissions>=3;}
 if(state.currentApostle){const a=player(state,state.currentApostle),jackpot=state.played.length;gainFire(state,a,jackpot,{jackpot:true});log(state,`${a.name} 成為本輪使徒，收取 ${jackpot} 點使徒火種。`,'apostle');for(const p of state.players){for(const e of p.effects.filter(e=>e.id==='victory'))if(p.id===a.id||e.partner===a.id){addMission(state,p,pick(rngFor(state),RESOURCE_TYPES),2);addMission(state,player(state,e.partner),pick(rngFor(state),RESOURCE_TYPES),2);}}}
 for(const p of state.players)p.effects=[];state.deck.push(...state.removed);state.removed=[];
 if([2,5].includes(state.round)){startAuction(state);return;}
 if(state.round>=7){finishGame(state);return;}startRound(state);
}
function startAuction(state){state.phase='auction';const pool=shuffleState(state,BLESSINGS.filter(b=>!b.id.startsWith('offering')&&!b.id.startsWith('burial'))).slice(0,state.players.length);state.auction={options:pool,humanBid:null};log(state,'火種精靈出現：祝福競拍開始。','auction');}
export function resolveAuction(state,{blessingId,bid=0}={}){if(state.phase!=='auction')throw new Error('No auction');const opts=state.auction.options;const bids=state.players.map((p,i)=>{if(i===0)return{p,bid:Math.min(p.fire,Math.max(0,bid)),target:blessingId||opts[0].id};return{p,bid:Math.min(p.fire,1+Math.floor(rand(state)*Math.max(1,Math.min(8,p.fire)))),target:pick(rngFor(state),opts).id};});const won=new Set();for(const o of opts){const contenders=bids.filter(x=>x.target===o.id&&!won.has(x.p.id)).sort((a,b)=>b.bid-a.bid||a.p.fire-b.p.fire);if(contenders.length){const w=contenders[0];w.p.fire-=w.bid;w.p.blessings.push(o);if(w.p.blessings.length>2)w.p.blessings.shift();won.add(w.p.id);log(state,`${w.p.name} 以 ${w.bid} 火種取得祝福「${o.name}」。`,'auction');}}
 state.auction=null;if(state.round>=7)finishGame(state);else startRound(state);}
function finishGame(state){state.gameOver=true;state.phase='complete';const finalA=state.currentApostle?player(state,state.currentApostle):null;let winner=null,reason='';if(finalA?.qualified){winner=finalA;reason='第7輪最終使徒且已完成資格';}else{const qualified=state.players.filter(p=>p.completedMissions>=3).sort((a,b)=>b.completedMissions-a.completedMissions||b.fire-a.fire);winner=qualified[0]||[...state.players].sort((a,b)=>b.completedMissions-a.completedMissions||b.fire-a.fire)[0];reason=finalA?'最終使徒未取得資格；測試版以使命完成數、火種作次順位':'本輪無使徒；測試版以使命完成數、火種作次順位';}state.final={winnerId:winner?.id||null,reason,finalApostleId:finalA?.id||null};log(state,`對局結束：${winner?.name||'無'}。${reason}`,'final');}

export function chooseDefaultForCard(state,p,c){const others=state.players.filter(x=>x.id!==p.id);const choice={};if(c.type==='resource'&&c.resourceType==='grace')choice.declared=pick(rngFor(state),RESOURCE_TYPES);if(c.target==='single'||['victory','bird','cup','overflow'].includes(c.effect))choice.targetId=pick(rngFor(state),others)?.id;if(c.target==='multi')choice.targetIds=shuffleState(state,others).slice(0,2).map(x=>x.id);if(c.effect==='wind')choice.mode=p.fire<6?'fire':'cycle';if(c.effect==='overflow')choice.mode='mission';return choice;}
function scoreCard(state,p,c){if(c.type==='resource'){let best=-99;for(const d of declaredTypes(c))if(isResourceLegal(state,p,c,d)){const need=p.mission?Math.max(0,(p.mission[d]||0)-(p.missionProgress[d]||0)):0;best=Math.max(best,20+Math.min(need,c.printedNumber)*2-c.corruption);}return best;}if(c.type==='miracle')return 11-c.corruption+(c.effect==='crown'?4:0)+(c.effect==='road'?2:0);return 10+c.corruption;}
export function runAiTurn(state){if(state.gameOver||state.phase==='auction')return state;const p=currentPlayer(state);if(p.human)return state;const actions=legalActions(state,p.id);const plays=actions.filter(a=>a.type==='play').map(a=>({...a,card:p.hand.find(c=>c.id===a.cardId)})).sort((a,b)=>scoreCard(state,p,b.card)-scoreCard(state,p,a.card));if(plays.length){const a=plays[0],choice=chooseDefaultForCard(state,p,a.card);if(a.declared)choice.declared=a.declared;playCard(state,p.id,a.cardId,choice);}else if(actions.some(a=>a.type==='prepare'))prepare(state,p.id);else skipNoLegalAction(state,p.id);return state;}

export function snapshot(state){return clone(state);}
export { CHARACTER_POOL, BLESSINGS, JUDGMENTS, RESOURCE_TYPES, LABEL, SPECIAL_BY_ID };

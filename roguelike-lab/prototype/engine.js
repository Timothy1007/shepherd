// 牧羊人 Roguelike 戰鬥核心。不依賴 DOM，可在 Node 或瀏覽器使用。
// 規則依據：docs/CONFIRMED_RULES_2026-11-03.md。一次行動 = 一個回合，預設 AP 3（maxAp，日後可由天賦增加）。
(function (root) {
"use strict";
const DEF = {
  sheep: { n: "群羊", t: "r", c: 1, i: "♧" }, food: { n: "糧食", t: "r", c: 1, i: "♧" },
  money: { n: "金錢", t: "r", c: 1, i: "◎" }, grace: { n: "恩典", t: "r", c: 1, i: "✧" },
  rescue: { n: "即刻救援", t: "m", c: 2, i: "⊞" }, flourish: { n: "生生不息", t: "m", c: 2, i: "❧" },
  repeat: { n: "重現", t: "m", c: 2, i: "▣" }, laststand: { n: "絕地反擊", t: "m", c: 2, i: "⚔" },
  insight: { n: "啟示", t: "m", c: 1, i: "◉" }
};
const DECK = { sheep: 9, food: 9, money: 9, grace: 3, rescue: 1, flourish: 1, repeat: 1, laststand: 1, insight: 2 };
const ENEMIES = { shadow: { n: "恐懼之影", hp: 65 }, locust: { n: "蝗災群", hp: 95 }, bull: { n: "金牛之像", hp: 160 } };
const BLESSINGS = { sanity: "理智上限 +8", seed: "起始播種 +2", shield: "起始護盾 +5" };
const MANUALS = { farm: "耕耘", guard: "守望", reap: "收割" };

function rng(s) { let a = s >>> 0; return () => { a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }
const shuffle = (S, a) => { for (let i = a.length - 1; i > 0; i--) { const j = Math.floor(S.rnd() * (i + 1)); [a[i], a[j]] = [a[j], a[i]]; } return a; };
const note = (S, m) => { S.logs.unshift("行動 " + S.round + "｜" + m); S.logs.length = Math.min(S.logs.length, 100); };
const isRes = c => DEF[c.key].t === "r";
const emptyCount = () => ({ sheep: 0, food: 0, money: 0, grace: 0 });

function newGame(o = {}) {
  const e = o.enemy || "shadow", b = o.blessing || "sanity";
  const S = { rnd: rng(o.seed == null ? Date.now() : o.seed), uid: 0, round: 1, maxAp: o.maxAp || 3, manual: "farm", locked: false,
    maxSanity: b === "sanity" ? 40 : 32, sanity: 0, shield: b === "shield" ? 5 : 0, seed: b === "seed" ? 2 : 0,
    herd: 0, investment: 0, fort: 0, reflect: 0, crit: 0, bleed: 0, halve: false, double: false,
    enemy: e, enemyHp: ENEMIES[e].hp, enemyMax: ENEMIES[e].hp, enemyPower: 0, count: emptyCount(),
    deck: [], discard: [], hand: [], staged: [], reveals: [], reshuffles: 0, redraw: 1, shieldUsed: false, pendingSeed: 0, done: false, logs: [] };
  S.sanity = S.maxSanity;
  for (const k in DECK) for (let i = 0; i < DECK[k]; i++) S.deck.push({ key: k, id: ++S.uid });
  shuffle(S, S.deck); refill(S); note(S, "戰鬥開始，起始手牌 " + S.hand.length + " 張");
  return S;
}

// ---- 抽牌：單一牌庫＋類別篩選，等同「物資保底＋混合池」----
function recycle(S) { if (!S.deck.length && S.discard.length) { S.deck = shuffle(S, S.discard.splice(0)); S.reshuffles++; note(S, "牌庫抽完，棄牌堆重新洗入牌庫"); } }
function draw(S, f) {
  recycle(S);
  let idx = []; const scan = () => { idx = []; S.deck.forEach((c, i) => { if (!f || f(c)) idx.push(i); }); };
  scan();
  if (!idx.length && S.discard.length) { S.deck.push(...shuffle(S, S.discard.splice(0))); S.reshuffles++; note(S, "牌庫內已無可抽的物資，棄牌堆提前洗回牌庫"); scan(); } // 物資保底優先
  if (!idx.length) return null;
  return S.deck.splice(idx[Math.floor(S.rnd() * idx.length)], 1)[0];
}
function refill(S) {
  let r = S.hand.filter(isRes).length;
  while (r < 5 && S.hand.length < 7) { const c = draw(S, isRes); if (!c) break; S.hand.push(c); r++; }
  while (S.hand.length < 7) { const c = draw(S); if (!c) break; S.hand.push(c); }
}

// ---- 暫存區：點牌只預留 AP，按「打出」才結算 ----
const apUsed = S => S.staged.reduce((a, c) => a + DEF[c.key].c, 0);
function stage(S, id) {
  if (S.done || S.reveals.length) return false;
  const i = S.hand.findIndex(c => c.id === id); if (i < 0) return false;
  if (apUsed(S) + DEF[S.hand[i].key].c > S.maxAp) return false;
  S.staged.push(S.hand.splice(i, 1)[0]); return true;
}
function unstage(S, id) { const i = S.staged.findIndex(c => c.id === id); if (i < 0) return false; S.hand.push(S.staged.splice(i, 1)[0]); return true; }
function clearStage(S) { while (S.staged.length) S.hand.push(S.staged.pop()); }
// 預覽用：含「重現」將複製的物資，三連以最終張數判定
function tally(S) {
  const t = emptyCount(), f = S.staged[0] && S.staged[0].key;
  for (const c of S.staged) { if (c.key in t) t[c.key]++; else if (c.key === "repeat" && f && f in t) t[f]++; }
  return t;
}
function selectManual(S, k) { if (!S.locked && !S.done && !S.reveals.length && MANUALS[k]) { S.manual = k; return true; } return false; }

// ---- 傷害與狀態 ----
function damage(S, v, o = {}) {
  let n = Math.max(0, v);
  if (o.direct) {
    if (S.manual === "reap" && S.sanity <= S.maxSanity * 0.3) n = Math.ceil(n * 1.25);
    if (S.double) { n *= 2; S.double = false; }
    if (o.guarantee || S.rnd() < S.crit) n = Math.ceil(n * (o.guarantee ? 2 : 1.5));
  }
  n = Math.ceil(n); S.enemyHp = Math.max(0, S.enemyHp - n);
  if (o.direct && S.manual === "reap") S.sanity = Math.min(S.maxSanity, S.sanity + Math.floor(n * 0.3));
  note(S, "對敵人造成 " + n + " 傷害" + (o.guarantee ? "（必爆）" : ""));
  if (!S.enemyHp && !S.done) { S.done = true; note(S, "勝利！"); }
  return n;
}
function seedTick(S) { // 只有播種實際觸發（層數>0）才回理智，與是否造成傷害無關
  if (S.seed <= 0) return;
  const n = S.seed * (2 + S.herd) + S.investment; damage(S, n); note(S, "播種觸發 " + n);
  if (S.manual === "farm") { const h = S.seed >= 10 ? 3 : S.seed >= 5 ? 2 : 1; S.sanity = Math.min(S.maxSanity, S.sanity + h); note(S, "收成回饋恢復理智 " + h); }
}
function enemyAttack(S, v) {
  let n = v; if (S.halve) { n = Math.ceil(n / 2); S.halve = false; }
  const blocked = Math.min(S.shield, n); S.shield -= blocked; const hurt = n - blocked; S.sanity = Math.max(0, S.sanity - hurt);
  note(S, "敵方攻擊 " + n + "，護盾抵擋 " + blocked + "，理智損失 " + hurt);
  if (blocked > 0 && S.fort > 0) { S.bleed += S.fort; note(S, "堅守使敵人流血 +" + S.fort); }            // 堅守：跨秘笈
  if (blocked > 0 && S.manual === "guard" && S.reflect > 0) { damage(S, Math.ceil(blocked * S.reflect)); note(S, "守望盾反觸發"); } // 盾反：僅守望
  if (!S.sanity) { S.done = true; note(S, "戰敗：理智耗盡"); }
}
function plan(S) {
  const r = S.round;
  if (S.enemy === "shadow") return [[10, 7, 18][(r - 1) % 3]];
  if (S.enemy === "locust") return Array(r % 3 === 0 ? 5 : 4).fill(r % 3 === 0 ? 5 : 3);
  const q = S.enemyHp / S.enemyMax, p = S.enemyPower;
  return q > 0.7 ? [10 + p] : q > 0.35 ? [13 + p] : [13 + p, 8 + p];
}

// ---- 結算 ----
function resolveRes(S, key, fin) {
  const n = ++S.count[key], m = S.manual;
  if (m === "farm") {
    if (key === "sheep") { S.herd++; note(S, "牧養 +1"); }
    if (key === "food") { S.seed++; note(S, "播種 +1"); }
    if (key === "money") { S.investment++; note(S, "投資 +1"); }
    if (key === "grace") { S.seed *= 2; note(S, "播種層數加倍"); }
  } else if (m === "guard") {
    if (key === "sheep") { S.shield += 5; note(S, "護盾 +5"); }
    if (key === "food") { S.sanity = Math.min(S.maxSanity, S.sanity + 2); S.fort++; note(S, "理智 +2，堅守 +1"); }
    if (key === "money") { S.shield += 4; S.reflect += 0.25; note(S, "護盾 +4，守望盾反 +25%"); }
    if (key === "grace") { S.halve = true; note(S, "下一次敵方攻擊減半"); }
  } else {
    if (key === "sheep") damage(S, 6 + (n >= 2 ? 4 : 0), { direct: true }); // 第二張起每張羊 +4
    if (key === "food") { S.sanity = Math.min(S.maxSanity, S.sanity + 2); const u = Math.min(2, S.seed); S.seed -= u; damage(S, 5 + u * 4, { direct: true }); }
    if (key === "money") { S.crit = Math.min(1, S.crit + 0.1); damage(S, 7, { direct: true, guarantee: fin.money === 3 && n === 3 }); } // 三金依「最終張數」判定
    if (key === "grace") { S.double = true; note(S, "下一次直接傷害加倍"); }
  }
}
function play(S, key, first, fin, copy) {
  if (copy) note(S, "重現複製 " + DEF[key].n);
  if (key in S.count) return resolveRes(S, key, fin);
  if (key === "rescue") { const n = 15 + (S.sanity < 5 ? 5 : 0); S.shield += n; note(S, "即刻救援：護盾 +" + n); }
  if (key === "flourish") { seedTick(S); S.pendingSeed += 3; note(S, "行動結束時播種 +3"); }
  if (key === "laststand") damage(S, S.maxSanity - S.sanity, { direct: true });
  if (key === "insight") { const ch = []; for (let i = 0; i < 3; i++) { const c = draw(S); if (c) ch.push(c); } if (ch.length) { S.reveals.push(ch); note(S, "啟示：結算後選一張入手牌"); } }
  if (key === "repeat" && !copy) { if (first && first !== "repeat") play(S, first, first, fin, true); else note(S, "沒有可複製的第一張牌"); }
}
function commit(S) { // 打出：依暫存順序一次結算
  if (S.done || S.reveals.length) return false;
  S.locked = true;
  const first = S.staged[0] && S.staged[0].key, fin = tally(S);
  const played = S.staged; S.staged = [];
  for (const c of played) { note(S, "打出 " + DEF[c.key].n); play(S, c.key, first, fin, false); if (S.done) break; }
  S.discard.push(...played); // 本次行動打出的牌，結算完才進棄牌區，避免被同一次行動的抽牌洗回
  if (!S.done && !S.reveals.length) endAction(S);
  return true;
}
function pick(S, id) { // 啟示：選到的牌進手牌，下一次行動才能用
  const set = S.reveals[0]; if (!set) return false;
  const c = set.find(x => x.id === id); if (!c) return false;
  S.hand.push(c); S.deck.push(...set.filter(x => x !== c)); shuffle(S, S.deck); S.reveals.shift();
  note(S, "啟示選擇 " + DEF[c.key].n);
  if (!S.reveals.length && !S.done) endAction(S);
  return true;
}
function endAction(S) {
  const c = S.count;
  if (c.food === 3 && S.manual === "farm") { S.seed++; note(S, "三糧：播種 +1"); }      // 恰好三張，以最終張數判定
  if (c.sheep === 3 && S.manual === "guard") { S.shield += 6; note(S, "三羊：護盾 +6"); }
  if (S.pendingSeed) { S.seed += S.pendingSeed; note(S, "生生不息：播種 +" + S.pendingSeed); S.pendingSeed = 0; }
  seedTick(S); if (S.done) return;
  if (S.bleed) { damage(S, S.bleed); note(S, "敵方流血 " + S.bleed); if (S.done) return; }
  for (const h of plan(S)) { if (S.done) break; enemyAttack(S, h); }
  if (S.enemy === "bull" && !S.done) {
    if (S.round % 4 === 0) { S.enemyHp = Math.min(S.enemyMax, S.enemyHp + 8); note(S, "金牛恢復 8 HP"); }
    if (S.round % 3 === 0) S.enemyPower = Math.min(8, S.enemyPower + 2);
  }
  if (S.done) return;
  S.round++; S.count = emptyCount(); S.locked = false; S.redraw = 1; S.shieldUsed = false; refill(S);
  note(S, "新行動開始，補牌至 " + S.hand.length + " 張");
}

// ---- 其他動作 ----
function redraw(S, id) { // 調度：洗入混合牌堆後隨機抽一張，可能抽回原牌，不套用物資保底
  if (S.done || !S.redraw || S.reveals.length) return false;
  const i = S.hand.findIndex(c => c.id === id); if (i < 0) return false;
  const c = S.hand.splice(i, 1)[0]; S.deck.push(c); shuffle(S, S.deck);
  const f = draw(S); if (f) S.hand.push(f); S.redraw = 0;
  note(S, "調度 " + DEF[c.key].n + "，抽入 " + (f ? DEF[f.key].n : "無牌")); return true;
}
function shieldStrike(S) {
  if (S.done || S.manual !== "guard" || S.shieldUsed || !S.shield || S.reveals.length) return false;
  const n = Math.min(8, S.shield); S.shield -= n; S.shieldUsed = true; S.locked = true;
  damage(S, n, { direct: true }); note(S, "盾轉攻：消耗 " + n + " 護盾"); return true;
}

const API = { DEF, DECK, ENEMIES, BLESSINGS, MANUALS, newGame, stage, unstage, clearStage, commit, pick, redraw, shieldStrike, selectManual, tally, apUsed, plan };
if (typeof module !== "undefined" && module.exports) module.exports = API; else root.Engine = API;
})(typeof window !== "undefined" ? window : globalThis);

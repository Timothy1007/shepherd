// 執行：node roguelike-lab/tests/engine.test.js
const E = require("../prototype/engine.js"), assert = require("assert");
let n = 0; const t = (name, f) => { f(); n++; console.log("ok -", name); };
const G = (o, hand) => { const S = E.newGame(Object.assign({ seed: 1 }, o)); S.hand = hand.map(k => ({ key: k, id: ++S.uid })); return S; };
const all = S => [...S.hand].forEach(c => E.stage(S, c.id));
const run = (S, m) => { if (m) E.selectManual(S, m); all(S); E.commit(S); return S; };
const log = S => S.logs.join("\n");

t("耕耘：播種 0 層不回理智", () => { const S = G({}, []); S.shield = 100; S.sanity = 20; E.commit(S); assert.equal(S.sanity, 20); });
t("耕耘：播種觸發才回理智", () => { const S = G({}, []); S.shield = 100; S.sanity = 20; S.seed = 1; E.commit(S); assert.equal(S.sanity, 21); });
t("盾反只在守望；堅守跨秘笈仍可流血", () => {
  const a = G({}, []); a.shield = 20; a.reflect = 0.5; a.fort = 1; E.commit(a);
  assert.equal(a.enemyHp, 65); assert.equal(a.bleed, 1);
  const b = G({}, []); b.shield = 20; b.reflect = 0.5; b.fort = 1; E.selectManual(b, "guard"); E.commit(b);
  assert.equal(b.enemyHp, 60);
});
t("無護盾時堅守不產生流血", () => { const S = G({}, []); S.fort = 2; E.commit(S); assert.equal(S.bleed, 0); });
t("守望三羊（恰好三張）：護盾 15+6，扣敵攻 10 → 11", () => assert.equal(run(G({}, ["sheep", "sheep", "sheep"]), "guard").shield, 11));
t("守望兩羊：沒有三連 → 10-10=0", () => assert.equal(run(G({}, ["sheep", "sheep"]), "guard").shield, 0));
t("四羊（AP4）不觸發三連 → 20-10=10", () => assert.equal(run(G({ maxAp: 4 }, ["sheep", "sheep", "sheep", "sheep"]), "guard").shield, 10));
t("重現複製計入張數：羊羊重現 → 三羊", () => assert.equal(run(G({ maxAp: 4 }, ["sheep", "sheep", "repeat"]), "guard").shield, 11));
t("羊羊重現羊 → 四羊，不觸發", () => assert.equal(run(G({ maxAp: 5 }, ["sheep", "sheep", "repeat", "sheep"]), "guard").shield, 10));
t("收割：兩羊 6+10，三羊 6+10+10（第二張起每張 +4）", () => {
  assert.equal(run(G({}, ["sheep", "sheep"]), "reap").enemyHp, 49);
  assert.equal(run(G({}, ["sheep", "sheep", "sheep"]), "reap").enemyHp, 39);
});
t("三金必爆依最終張數：三張有、四張沒有", () => {
  assert.ok(log(run(G({ enemy: "bull" }, ["money", "money", "money"]), "reap")).includes("必爆"));
  assert.ok(!log(run(G({ enemy: "bull", maxAp: 4 }, ["money", "money", "money", "money"]), "reap")).includes("必爆"));
});
t("暫存區：超過 AP 不能再放；取消不產生任何效果", () => {
  const S = G({}, ["sheep", "sheep", "sheep", "sheep"]); all(S); assert.equal(S.staged.length, 3);
  E.clearStage(S); assert.equal(S.staged.length, 0); assert.equal(S.hand.length, 4); assert.equal(S.shield, 0);
});
t("起始發牌：七張、至少五張物資", () => {
  for (let s = 1; s <= 50; s++) { const S = E.newGame({ seed: s }); assert.equal(S.hand.length, 7); assert.ok(S.hand.filter(c => E.DEF[c.key].t === "r").length >= 5); }
});
t("調度：牌池總數不變", () => {
  const S = E.newGame({ seed: 3 }), total = S.deck.length + S.hand.length; E.redraw(S, S.hand[0].id);
  assert.equal(S.deck.length + S.hand.length, total); assert.equal(S.hand.length, 7);
});
console.log("\n" + n + " 項測試通過");

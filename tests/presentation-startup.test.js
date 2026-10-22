import test from 'node:test';
import assert from 'node:assert/strict';

function makeElement() {
  return {
    textContent: '', innerHTML: '', hidden: false, className: '', dataset: {}, style: {},
    classList: { add() {}, remove() {}, toggle() {}, contains() { return false; } },
    addEventListener() {}, append() {}, appendChild() {}, replaceChildren() {}, remove() {},
    querySelector() { return makeElement(); }, querySelectorAll() { return []; },
    setAttribute() {}, getBoundingClientRect() { return { left: 0, top: 0, width: 100, height: 100 }; },
    animate() { return { finished: Promise.resolve() }; },
  };
}

test('V1 presentation module starts and renders an initial dealt table', async () => {
  const elements = new Map();
  const elementFor = (selector) => {
    if (!elements.has(selector)) elements.set(selector, makeElement());
    return elements.get(selector);
  };
  const previous = { document: globalThis.document, window: globalThis.window, CSS: globalThis.CSS, setTimeout: globalThis.setTimeout, clearTimeout: globalThis.clearTimeout };
  globalThis.document = { querySelector: elementFor, querySelectorAll: () => [], createElement: () => makeElement(), addEventListener() {} };
  globalThis.window = { addEventListener() {} };
  globalThis.CSS = { escape: (value) => String(value) };
  globalThis.setTimeout = () => 0;
  globalThis.clearTimeout = () => {};
  try {
    await import(`../src/presentation/app.js?startup-smoke=${Date.now()}`);
    assert.equal(elements.get('#resource-deck-count').textContent, '88');
    assert.match(elements.get('#seat-human').innerHTML, /牧羊人/);
  } finally {
    Object.assign(globalThis, previous);
  }
});

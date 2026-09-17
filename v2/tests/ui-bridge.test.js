import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const root = new URL('../', import.meta.url);
const read = (path) => readFile(new URL(path, root), 'utf8');

test('V2 shell loads optimized presentation bridge after base UI', async () => {
  const html = await read('index.html');
  const base = html.indexOf('./ui.css');
  const bridge = html.indexOf('./legacy-ui-bridge.css');
  assert.ok(base >= 0, 'base V2 UI stylesheet is missing');
  assert.ok(bridge > base, 'legacy UI bridge must load after base UI');
});

test('V2 shell has no manual skip button and restores the central played pile', async () => {
  const html = await read('index.html');
  assert.doesNotMatch(html, /id="pass"/);
  assert.doesNotMatch(html, />跳過</);
  assert.match(html, /id="played-pile"/);
  assert.match(html, /短按放大/);
  assert.match(html, /長按看詳細資料/);
});

test('V2 shell wires stable hover and drag-to-play helpers', async () => {
  const html = await read('index.html');
  assert.match(html, /hand-stability-v2\.js/);
  assert.match(html, /drag-play-v2\.js/);
  assert.match(html, /拖、拉或甩牌到中央出牌/);
});

test('optimized UI preserves readable illegal cards, short preview and three-card pile', async () => {
  const css = await read('legacy-ui-bridge.css');
  assert.match(css, /\.game-card\.illegal/);
  assert.match(css, /filter:none!important/);
  assert.match(css, /\.game-card\.click-preview/);
  assert.match(css, /\.played-pile/);
  assert.match(css, /\.pile-card-v2/);
});

test('gesture helper emits short preview, long detail, and direct play events', async () => {
  const js = await read('drag-play-v2.js');
  assert.match(js, /shepherd:v2-card-preview/);
  assert.match(js, /shepherd:v2-card-detail/);
  assert.match(js, /shepherd:v2-play-card/);
  assert.match(js, /LONG_PRESS_MS/);
  assert.match(js, /THROW_DISTANCE/);
  assert.doesNotMatch(js, /#play-selected/);
});

test('app renders only the latest three played cards', async () => {
  const js = await read('app.js');
  assert.match(js, /state\.played\.slice\(-3\)/);
  assert.match(js, /renderPlayedPile/);
  assert.match(js, /shepherd:v2-card-detail/);
});

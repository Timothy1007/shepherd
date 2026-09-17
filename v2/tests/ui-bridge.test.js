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

test('V2 shell wires stable hover and drag-to-play helpers', async () => {
  const html = await read('index.html');
  assert.match(html, /hand-stability-v2\.js/);
  assert.match(html, /drag-play-v2\.js/);
  assert.match(html, /拖曳卡牌至中央打出/);
});

test('optimized UI preserves readable illegal cards and stable hand classes', async () => {
  const css = await read('legacy-ui-bridge.css');
  assert.match(css, /\.game-card\.illegal/);
  assert.match(css, /filter:none!important/);
  assert.match(css, /\.game-card\.hand-hover/);
  assert.match(css, /\.hand\[data-count="10"\]/);
});

test('drag helper delegates play through the existing selected-card action', async () => {
  const js = await read('drag-play-v2.js');
  assert.match(js, /#play-selected:not\(:disabled\)/);
  assert.match(js, /center-stage/);
});

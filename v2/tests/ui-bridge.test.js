import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const root = new URL('../', import.meta.url);
const read = (path) => readFile(new URL(path, root), 'utf8');

test('V2 shell is rebased on the final V1 presentation stylesheet stack', async () => {
  const html = await read('index.html');
  for (const file of [
    'style.css','interaction-fix.css','hierarchy-polish.css','tabletop-history.css',
    'camera-perspective.css','round-resolution.css','effect-zone.css','final-results.css',
    'ui-layout-tuning.css','card-legality-visual.css','overlay-stack.css','hand-stability.css'
  ]) assert.ok(html.includes(`../src/presentation/${file}`), `${file} missing`);
  assert.ok(html.includes('./v1-ui-adapter.css'));
  assert.ok(!html.includes('./ui.css'));
});

test('V2 shell keeps V1 table/hand DOM and removes floating info panels', async () => {
  const html = await read('index.html');
  assert.match(html, /id="drop-zone" class="center-play"/);
  assert.match(html, /id="seat-human" class="human-meta"/);
  assert.match(html, /id="hand" class="hand"/);
  assert.match(html, /id="played-pile" class="tabletop-pile"/);
  assert.doesNotMatch(html, /class="left-panel/);
  assert.doesNotMatch(html, />跳過</);
});

test('adapter puts the center pile on the lower battlefield center and preserves V1 hand sizing', async () => {
  const css = await read('v1-ui-adapter.css');
  assert.match(css, /translate\(-50%,-50%\) translate\(35px,52px\)/);
  assert.match(css, /\.tabletop-pile/);
  assert.match(css, /width:152px!important/);
  assert.match(css, /\.hand \.card/);
  assert.match(css, /106px!important/);
});

test('V1 interaction port supports short press, long press, drag and throw', async () => {
  const js = await read('v1-interactions.js');
  assert.match(js, /shepherd:v2-card-preview/);
  assert.match(js, /shepherd:v2-card-detail/);
  assert.match(js, /shepherd:v2-play-card/);
  assert.match(js, /LONG_PRESS_MS/);
  assert.match(js, /THROW_DISTANCE/);
});

test('V2 app renders latest three played cards and makes pile history reachable', async () => {
  const js = await read('app.js');
  assert.match(js, /state\.played\.slice\(-3\)/);
  assert.match(js, /el\.pile\.addEventListener\('click',renderHistory\)/);
  assert.match(js, /played-history-card/);
  assert.match(js, /shepherd:v2-card-detail/);
});

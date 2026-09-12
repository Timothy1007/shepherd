const tableCard = document.querySelector('#table-card');
const centerPlay = document.querySelector('#drop-zone');
const status = document.querySelector('#status');
const roundDisplay = document.querySelector('#round-display');
const instruction = document.querySelector('#instruction');

if (tableCard && centerPlay && status && roundDisplay) {
  const pile = document.createElement('div');
  pile.className = 'tabletop-pile';
  pile.setAttribute('aria-hidden', 'true');
  centerPlay.insertBefore(pile, tableCard.nextSibling);

  const snapshots = [];
  let playSequence = 0;
  let lastPlayedCount = 0;
  let lastRound = null;
  let disposed = false;
  let finalSnapshotEmitted = false;

  const rotations = [-8, 5, -3, 7, -5, 4];
  const offsets = [[-26, 12], [18, 7], [-6, -4], [24, 10], [-20, 5], [8, -6]];

  function renderPile() {
    pile.replaceChildren(...snapshots.map((item, index) => {
      const node = item.cloneNode(true);
      node.style.setProperty('--pile-depth', String(index));
      node.classList.toggle('pile-latest', index === snapshots.length - 1);
      return node;
    }));
    pile.classList.toggle('has-cards', snapshots.length > 0);
  }

  function clearPile() {
    snapshots.length = 0;
    renderPile();
    tableCard.classList.remove('pile-source-hidden');
  }

  function captureCurrentCard() {
    const visual = tableCard.querySelector('.table-card-visual');
    const image = visual?.querySelector('img');
    if (!visual || !image) return;

    const clone = visual.cloneNode(true);
    clone.classList.add('pile-card');

    const slot = playSequence % offsets.length;
    const [x, y] = offsets[slot];
    clone.style.setProperty('--pile-x', `${x}px`);
    clone.style.setProperty('--pile-y', `${y}px`);
    clone.style.setProperty('--pile-r', `${rotations[slot]}deg`);
    playSequence += 1;

    snapshots.push(clone);
    while (snapshots.length > 3) snapshots.shift();
    renderPile();
    tableCard.classList.add('pile-source-hidden');
  }

  function readRound() {
    const match = roundDisplay.textContent?.match(/第\s*(\d+)\s*\/\s*7\s*輪/);
    return match ? Number(match[1]) : null;
  }

  function readPlayedCount() {
    const match = status.textContent?.match(/場內\s*(\d+)/);
    return match ? Number(match[1]) : null;
  }

  function isGameOver() {
    return instruction?.textContent?.startsWith('爭局結束') ?? false;
  }

  function emitRoundPileSnapshot(round) {
    if (!snapshots.length) return;
    const rect = pile.getBoundingClientRect();
    const pileClone = pile.cloneNode(true);
    window.dispatchEvent(new CustomEvent('shepherd:round-pile-snapshot', {
      detail: {
        round,
        rect: { left: rect.left, top: rect.top, width: rect.width, height: rect.height },
        pile: pileClone,
      },
    }));
  }

  function tick() {
    if (disposed) return;

    const round = readRound();
    const playedCount = readPlayedCount();

    if (round !== null && lastRound !== null && round !== lastRound) {
      emitRoundPileSnapshot(lastRound);
      clearPile();
      lastPlayedCount = 0;
      finalSnapshotEmitted = false;
    }
    if (round !== null) lastRound = round;

    if (playedCount !== null) {
      if (playedCount > lastPlayedCount) captureCurrentCard();
      if (playedCount < lastPlayedCount && playedCount === 0) clearPile();
      lastPlayedCount = playedCount;
    }

    // 第七輪不會再切到第八輪，因此另外在 gameOver 時觸發最後一次使徒結算動畫。
    if (isGameOver() && !finalSnapshotEmitted && lastRound !== null && snapshots.length) {
      finalSnapshotEmitted = true;
      emitRoundPileSnapshot(lastRound);
      clearPile();
    }

    window.setTimeout(tick, 120);
  }

  lastRound = readRound();
  lastPlayedCount = readPlayedCount() ?? 0;
  window.setTimeout(tick, 120);
  window.addEventListener('beforeunload', () => { disposed = true; }, { once: true });
}

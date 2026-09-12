const tableCard = document.querySelector('#table-card');
const centerPlay = document.querySelector('#drop-zone');
const status = document.querySelector('#status');
const roundDisplay = document.querySelector('#round-display');

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

  function tick() {
    if (disposed) return;

    const round = readRound();
    const playedCount = readPlayedCount();

    if (round !== null && lastRound !== null && round !== lastRound) {
      clearPile();
      lastPlayedCount = 0;
    }
    if (round !== null) lastRound = round;

    if (playedCount !== null) {
      // A count increase means app.js has already rendered the newly played card.
      // Capture exactly once per actual play. No MutationObserver is used here,
      // so changing classes/children can never feed back into this detector.
      if (playedCount > lastPlayedCount) captureCurrentCard();

      // Restart/new round can reset the count before the round label updates.
      if (playedCount < lastPlayedCount && playedCount === 0) clearPile();
      lastPlayedCount = playedCount;
    }

    window.setTimeout(tick, 120);
  }

  lastRound = readRound();
  lastPlayedCount = readPlayedCount() ?? 0;
  window.setTimeout(tick, 120);
  window.addEventListener('beforeunload', () => { disposed = true; }, { once: true });
}

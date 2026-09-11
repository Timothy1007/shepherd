const tableCard = document.querySelector('#table-card');
const centerPlay = document.querySelector('#drop-zone');

if (tableCard && centerPlay) {
  const pile = document.createElement('div');
  pile.className = 'tabletop-pile';
  pile.setAttribute('aria-hidden', 'true');
  centerPlay.insertBefore(pile, tableCard.nextSibling);

  const snapshots = [];
  let playSequence = 0;
  const rotations = [-8, 5, -3, 7, -5, 4];
  const offsets = [
    [-26, 12],
    [18, 7],
    [-6, -4],
    [24, 10],
    [-20, 5],
    [8, -6],
  ];

  function renderPile() {
    pile.replaceChildren(...snapshots.map((item, i) => {
      const node = item.cloneNode(true);
      node.style.setProperty('--pile-depth', String(i));
      node.classList.toggle('pile-latest', i === snapshots.length - 1);
      return node;
    }));
    pile.classList.toggle('has-cards', snapshots.length > 0);
  }

  function captureCurrentCard() {
    const visual = tableCard.querySelector('.table-card-visual');
    const image = visual?.querySelector('img');
    if (!visual || !image) return;

    const clone = visual.cloneNode(true);
    clone.classList.add('pile-card');

    // Use a lifetime play sequence instead of snapshots.length. Once the visible
    // pile reached three cards, snapshots.length stayed at 3 forever, which made
    // every later play reuse the same offset/rotation and visually collapse into
    // one stack. Cycling by sequence keeps the latest three visibly scattered.
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

  const observer = new MutationObserver((records) => {
    for (const record of records) {
      if (record.type !== 'attributes' || record.attributeName !== 'class') continue;
      const oldClass = record.oldValue || '';
      if (!oldClass.includes('impact') && tableCard.classList.contains('impact')) {
        requestAnimationFrame(captureCurrentCard);
        break;
      }
    }

    // Round changes clear the table source, so clear old-round history too.
    if (!tableCard.querySelector('.table-card-visual')) {
      snapshots.length = 0;
      renderPile();
      tableCard.classList.remove('pile-source-hidden');
    }
  });

  observer.observe(tableCard, {
    attributes: true,
    attributeOldValue: true,
    attributeFilter: ['class'],
    childList: true,
    subtree: true,
  });
}

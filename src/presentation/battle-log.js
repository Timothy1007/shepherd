const compactLog = document.querySelector('#ai-debug-log');

if (compactLog) {
  const overlay = document.createElement('div');
  overlay.id = 'battle-log-overlay';
  overlay.hidden = true;
  overlay.innerHTML = `
    <div class="battle-log-backdrop" data-close-battle-log></div>
    <section class="battle-log-panel" role="dialog" aria-modal="true" aria-labelledby="battle-log-title">
      <button class="battle-log-close" type="button" aria-label="關閉" data-close-battle-log>×</button>
      <h2 id="battle-log-title">戰況紀錄</h2>
      <div class="battle-log-lines"></div>
    </section>`;
  document.body.append(overlay);

  const close = () => { overlay.hidden = true; };
  const open = () => {
    const lines = overlay.querySelector('.battle-log-lines');
    const entries = [...compactLog.querySelectorAll(':scope > div')];
    lines.replaceChildren(...entries.map((entry) => {
      const line = document.createElement('div');
      line.className = 'battle-log-line';
      line.textContent = entry.textContent;
      return line;
    }));
    if (!lines.children.length) lines.textContent = '目前尚無戰況紀錄。';
    overlay.hidden = false;
  };

  compactLog.addEventListener('click', open);
  compactLog.addEventListener('keydown', (event) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      open();
    }
  });
  overlay.querySelectorAll('[data-close-battle-log]').forEach((node) => node.addEventListener('click', close));
}

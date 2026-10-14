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
  const collapseToggle = document.createElement('button');
  collapseToggle.type = 'button';
  collapseToggle.className = 'battle-log-collapse-toggle';
  collapseToggle.setAttribute('aria-label', '收合戰況紀錄');
  collapseToggle.setAttribute('title', '收合戰況紀錄');
  collapseToggle.textContent = '−';
  compactLog.prepend(collapseToggle);

  const setCollapsed = (collapsed) => {
    compactLog.classList.toggle('is-collapsed', collapsed);
    compactLog.setAttribute('aria-label', collapsed ? '開啟戰況紀錄' : '戰況紀錄，短按查看完整內容');
    collapseToggle.setAttribute('aria-label', collapsed ? '展開戰況紀錄' : '收合戰況紀錄');
    collapseToggle.setAttribute('title', collapsed ? '展開戰況紀錄' : '收合戰況紀錄');
    collapseToggle.textContent = collapsed ? '☷' : '−';
  };
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

  collapseToggle.addEventListener('click', (event) => {
    event.stopPropagation();
    setCollapsed(!compactLog.classList.contains('is-collapsed'));
  });
  compactLog.addEventListener('click', () => {
    if (compactLog.classList.contains('is-collapsed')) setCollapsed(false);
    else open();
  });
  compactLog.addEventListener('keydown', (event) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      if (compactLog.classList.contains('is-collapsed')) setCollapsed(false);
      else open();
    }
  });
  overlay.querySelectorAll('[data-close-battle-log]').forEach((node) => node.addEventListener('click', close));
}

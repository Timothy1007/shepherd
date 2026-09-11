function decorateStats(root = document) {
  root.querySelectorAll('.stats').forEach((el) => {
    // app.js rewrites .stats on each render. Only decorate plain-text stats once;
    // do not rewrite our own decorated markup, otherwise MutationObserver loops forever.
    if (el.querySelector('.stat-item')) return;

    const text = el.textContent || '';
    const fire = text.match(/🔥\s*(\d+)/)?.[1] ?? '0';
    const hand = text.match(/手牌\s*(\d+)/)?.[1] ?? '0';
    const low = text.match(/低溫火種\s*(\d+)/)?.[1] ?? '0';
    el.innerHTML = `<span class="stat-item stat-fire">🔥 <b>${fire}</b></span><span class="stat-item stat-hand">🂠 <b>${hand}</b></span><span class="stat-item stat-low"><i class="blue-flame" aria-hidden="true"></i><b>${low}</b></span>`;
  });
}

let scheduled = false;
const observer = new MutationObserver(() => {
  if (scheduled) return;
  scheduled = true;
  queueMicrotask(() => {
    scheduled = false;
    decorateStats();
  });
});

observer.observe(document.documentElement, { subtree: true, childList: true });
window.addEventListener('DOMContentLoaded', () => decorateStats());
queueMicrotask(() => decorateStats());

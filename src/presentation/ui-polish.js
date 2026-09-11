function decorateStats(root = document) {
  root.querySelectorAll('.stats').forEach((el) => {
    const text = el.textContent || '';
    const fire = text.match(/🔥\s*(\d+)/)?.[1] ?? '0';
    const hand = text.match(/手牌\s*(\d+)/)?.[1] ?? '0';
    const low = text.match(/低溫火種\s*(\d+)/)?.[1] ?? '0';
    el.innerHTML = `<span class="stat-item stat-fire">🔥 <b>${fire}</b></span><span class="stat-item stat-hand">🂠 <b>${hand}</b></span><span class="stat-item stat-low"><i class="blue-flame" aria-hidden="true"></i><b>${low}</b></span>`;
  });
}

const observer = new MutationObserver(() => decorateStats());
observer.observe(document.documentElement, { subtree: true, childList: true, characterData: false });
window.addEventListener('DOMContentLoaded', () => decorateStats());
setTimeout(() => decorateStats(), 0);

const overlay = document.querySelector('#detail-overlay');

if (overlay) {
  overlay.addEventListener('click', (event) => {
    const closeTarget = event.target.closest?.('[data-close="detail"]');
    if (!closeTarget) return;
    event.preventDefault();
    event.stopPropagation();
    overlay.hidden = true;
  }, true);
}

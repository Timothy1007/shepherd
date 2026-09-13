const overlay = document.querySelector('#detail-overlay');

function closeDetail(event) {
  if (!overlay || overlay.hidden) return;
  event?.preventDefault?.();
  event?.stopPropagation?.();
  event?.stopImmediatePropagation?.();
  overlay.hidden = true;
}

if (overlay) {
  // Use delegated capture listeners so the close control still works even when
  // the detail card adds interactive tilt/foil layers above the dialog content.
  const handlePointerDown = (event) => {
    const target = event.target.closest?.('[data-close="detail"]');
    if (!target) return;
    closeDetail(event);
  };

  overlay.addEventListener('pointerdown', handlePointerDown, true);
  overlay.addEventListener('click', (event) => {
    const target = event.target.closest?.('[data-close="detail"]');
    if (!target) return;
    closeDetail(event);
  }, true);

  // Keep outside-click behavior: only the backdrop closes, not clicks inside the panel.
  overlay.addEventListener('pointerdown', (event) => {
    if (event.target.classList?.contains('overlay-backdrop')) closeDetail(event);
  }, true);
}

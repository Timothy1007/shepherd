document.addEventListener('dragstart', (event) => {
  if (event.target.closest?.('.card, .drag-ghost, .card-art')) event.preventDefault();
});

document.addEventListener('selectstart', (event) => {
  if (event.target.closest?.('.card, .drag-ghost')) event.preventDefault();
});

// Keep restart independent from controller/presentation state. If any transient UI
// state or animation gets wedged, this button must still provide a clean recovery.
document.addEventListener('click', (event) => {
  if (!event.target.closest?.('#restart')) return;
  event.preventDefault();
  event.stopImmediatePropagation();
  window.location.reload();
}, true);

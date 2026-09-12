document.addEventListener('dragstart', (event) => {
  if (event.target.closest?.('.card, .drag-ghost, .card-art')) event.preventDefault();
});

document.addEventListener('selectstart', (event) => {
  if (event.target.closest?.('.card, .drag-ghost')) event.preventDefault();
});

// Use the same card artwork and runtime glyph as the player's hand.
export function choiceFaceMarkup(definition, alt = '', imageStyle = '') {
  const src = globalThis.__shepherdCardFaceAsset?.(definition) ?? definition.image;
  const glyph = globalThis.__shepherdCardFaceGlyph?.(definition) ?? '';
  return `<span class="choice-face" style="position:relative;display:block;width:fit-content;max-width:100%;margin:0 auto"><img src="${src}" alt="${alt}"${imageStyle ? ` style="${imageStyle}"` : ''}>${glyph}</span>`;
}

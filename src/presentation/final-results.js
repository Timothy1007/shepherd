import { GameController } from '../controller/game-controller.js';

const originalSafeRender = GameController.prototype.safeRender;
let revealTimer = null;
let shownForRound = null;

function ensurePanel() {
  let overlay = document.querySelector('#final-results-overlay');
  if (overlay) return overlay;

  overlay = document.createElement('div');
  overlay.id = 'final-results-overlay';
  overlay.className = 'final-results-overlay';
  overlay.hidden = true;
  overlay.innerHTML = `
    <div class="final-results-backdrop"></div>
    <section class="final-results-panel" role="dialog" aria-modal="true" aria-labelledby="final-results-title">
      <span class="final-results-eyebrow">七輪爭局結束</span>
      <h2 id="final-results-title">荒野終局</h2>
      <p class="final-apostle"></p>
      <div class="final-ranking" aria-label="火種最終排名"></div>
      <button class="final-restart" type="button">再開一局</button>
    </section>`;
  document.body.append(overlay);

  overlay.querySelector('.final-restart').addEventListener('click', () => {
    document.querySelector('#restart')?.click();
  });
  return overlay;
}

function rankedPlayers(state) {
  const sorted = [...state.players].sort((a, b) => b.fire - a.fire || a.seat - b.seat);
  let previousFire = null;
  let previousRank = 0;
  return sorted.map((player, index) => {
    const rank = player.fire === previousFire ? previousRank : index + 1;
    previousFire = player.fire;
    previousRank = rank;
    return { player, rank };
  });
}

function renderFinalResults(state) {
  const overlay = ensurePanel();
  const finalApostle = state.lastRoundResult?.apostle ?? null;
  const ranking = rankedPlayers(state);
  const winners = new Set(state.winner ?? []);

  overlay.querySelector('.final-apostle').innerHTML = finalApostle
    ? `終局使徒 <strong>${finalApostle}</strong> · 第七輪獲得 <strong>+${state.lastRoundResult?.reward ?? 0}</strong> 火種`
    : '第七輪沒有產生使徒';

  const host = overlay.querySelector('.final-ranking');
  host.replaceChildren(...ranking.map(({ player, rank }) => {
    const row = document.createElement('div');
    const isWinner = winners.has(player.playerId);
    const isFinalApostle = player.playerId === finalApostle;
    row.className = `final-rank-row ${isWinner ? 'winner' : ''} ${isFinalApostle ? 'final-apostle-row' : ''}`;
    row.innerHTML = `
      <span class="rank-number">${rank}</span>
      <span class="rank-player">Player ${player.seat}${player.type === 'human' ? ' · 你' : ''}</span>
      <span class="rank-badges">${isFinalApostle ? '<em>終局使徒</em>' : ''}${isWinner ? '<strong>勝者</strong>' : ''}</span>
      <span class="rank-fire">🔥 ${player.fire}</span>`;
    return row;
  }));

  overlay.hidden = false;
  requestAnimationFrame(() => overlay.classList.add('show'));
}

function hideFinalResults() {
  clearTimeout(revealTimer);
  revealTimer = null;
  shownForRound = null;
  const overlay = document.querySelector('#final-results-overlay');
  if (!overlay) return;
  overlay.classList.remove('show');
  overlay.hidden = true;
}

GameController.prototype.safeRender = function finalResultsSafeRender(available) {
  const rendered = originalSafeRender.call(this, available);
  try {
    if (!this.state?.gameOver) {
      hideFinalResults();
      return rendered;
    }
    if (shownForRound === this.state.round) return rendered;
    shownForRound = this.state.round;
    clearTimeout(revealTimer);
    // Let the final apostle's fire animation resolve before the ranking takes over.
    revealTimer = setTimeout(() => renderFinalResults(this.state), 1450);
  } catch (error) {
    console.error('[Shepherd] final results presentation failed; final state is preserved.', error);
  }
  return rendered;
};

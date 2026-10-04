(() => {
  const workspace = document.querySelector('.workspace');
  const panel = workspace.querySelector('.right');
  const storageKey = 'vbp.videoSetupWidth';
  const divider = document.createElement('div');
  panel.id = 'video-setup-panel';
  divider.id = 'video-setup-divider';
  divider.className = 'setup-divider';
  divider.tabIndex = 0;
  divider.setAttribute('role', 'separator');
  divider.setAttribute('aria-orientation', 'vertical');
  divider.setAttribute('aria-label', 'Resize Video setup');
  divider.setAttribute('aria-controls', panel.id);
  divider.title = 'Resize Video setup. Double-click to reset.';
  panel.before(divider);

  let preferredWidth = null;
  let drag = null;
  try {
    const stored = Number(localStorage.getItem(storageKey));
    if (Number.isFinite(stored) && stored >= 320 && stored <= 640) preferredWidth = stored;
  } catch { /* Resizing also works when WebView storage is unavailable. */ }

  function limits() {
    return { min: 320, max: Math.max(320, Math.min(640, workspace.clientWidth - 408)) };
  }

  function applyWidth(width = preferredWidth) {
    const { min, max } = limits();
    const fallback = window.innerWidth <= 1100 ? 340 : 400;
    const value = Math.round(Math.max(min, Math.min(max, width ?? fallback)));
    workspace.style.setProperty('--setup-width', `${value}px`);
    divider.setAttribute('aria-valuemin', min);
    divider.setAttribute('aria-valuemax', max);
    divider.setAttribute('aria-valuenow', value);
    divider.setAttribute('aria-valuetext', `${value} pixels`);
    return value;
  }

  function save() {
    try {
      if (preferredWidth === null) localStorage.removeItem(storageKey);
      else localStorage.setItem(storageKey, String(preferredWidth));
    } catch { /* The current window still keeps its chosen width. */ }
  }

  function finishDrag(commit) {
    if (!drag) return;
    const previous = drag;
    drag = null;
    if (!commit) preferredWidth = previous.preference;
    applyWidth();
    document.body.classList.remove('resizing-setup');
    if (divider.hasPointerCapture(previous.id)) divider.releasePointerCapture(previous.id);
    if (commit) save();
  }

  divider.addEventListener('pointerdown', event => {
    if (event.button !== 0 || !event.isPrimary || drag) return;
    drag = { id: event.pointerId, x: event.clientX, width: panel.getBoundingClientRect().width, preference: preferredWidth };
    divider.setPointerCapture(event.pointerId);
    divider.focus();
    document.body.classList.add('resizing-setup');
    event.preventDefault();
  });
  divider.addEventListener('pointermove', event => {
    if (!drag || event.pointerId !== drag.id) return;
    preferredWidth = applyWidth(drag.width + drag.x - event.clientX);
  });
  divider.addEventListener('pointerup', event => { if (drag?.id === event.pointerId) finishDrag(true); });
  divider.addEventListener('pointercancel', () => finishDrag(false));
  divider.addEventListener('lostpointercapture', () => finishDrag(false));
  window.addEventListener('blur', () => finishDrag(false));
  divider.addEventListener('dblclick', () => { preferredWidth = null; applyWidth(); save(); });
  divider.addEventListener('keydown', event => {
    if (event.key === 'Escape') { finishDrag(false); return; }
    if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
    event.preventDefault();
    const { min, max } = limits();
    const current = panel.getBoundingClientRect().width;
    preferredWidth = applyWidth(event.key === 'Home' ? min : event.key === 'End' ? max : current + (event.key === 'ArrowLeft' ? 1 : -1) * (event.shiftKey ? 40 : 10));
    save();
  });
  new ResizeObserver(() => {
    if (workspace.clientWidth <= 760) finishDrag(false);
    applyWidth();
  }).observe(workspace);
  applyWidth();
})();

window.portStatus = { phase: 'loading', error: null };
let frame;
window.portHost = {
  resize() {
    portLifecycle.prepare();
    document.getElementById('theCanvas').addEventListener('contextmenu', event => event.preventDefault());
  },
  size() {
    const canvas = document.getElementById('theCanvas');
    return { width: canvas.width, height: canvas.height };
  },
  status(value) {
    window.portStorage.updateStatus(value.storage);
    window.portStatus = { ...value, ...window.portAudio.status(), lifecycle: portLifecycle.status() };
    document.getElementById('status').textContent = value.phase === 'failed'
      ? value.error
      : value.phase === 'loading' ? value.message ?? 'Loading original game…'
      : portServices.statusMessage() ?? (value.game ? 'Browser port in development: original game running; single-player parity checks are pending.'
      : 'Original assets: graphics verification. Use arrow keys and click the canvas. Gameplay integration is pending.');
  },
  start(instance) {
    portServices.start(instance);
    portLifecycle.start(instance);
    const tick = () => {
      instance.invokeMethod('Tick');
      if (window.portStatus.phase !== 'failed') frame = requestAnimationFrame(tick);
    };
    frame = requestAnimationFrame(tick);
  },
  stop() { cancelAnimationFrame(frame); portLifecycle.stop(); portServices.stop(); }
};
window.addEventListener('keydown', event => {
  if (['ArrowLeft','ArrowRight','ArrowUp','ArrowDown',' '].includes(event.key)) event.preventDefault();
});

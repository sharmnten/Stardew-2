window.portStatus = { phase: 'waiting', error: null };
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
    const audioFailure = document.getElementById('audioFailure');
    if (audioFailure) {
      audioFailure.hidden = !window.portStatus.audioError;
      document.getElementById('audioError').textContent = window.portStatus.audioError ?? '';
    }
    document.getElementById('status').textContent = value.phase === 'failed'
      ? value.error
      : value.phase === 'loading' ? value.message ?? 'Loading original game…'
      : portServices.statusMessage() ?? (value.game ? 'Stardew Valley • Saves are stored in this browser.'
      : 'Original graphics and audio compatibility probe. Use arrow keys and click the canvas.');
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

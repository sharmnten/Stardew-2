window.portStatus = { phase: 'loading', error: null };
let frame;
window.portHost = {
  resize() {
    const canvas = document.getElementById('theCanvas');
    const holder = document.getElementById('canvasHolder');
    canvas.width = holder.clientWidth;
    canvas.height = holder.clientHeight;
    canvas.addEventListener('contextmenu', event => event.preventDefault());
  },
  status(value) {
    window.portStatus = { ...value, ...window.portAudio.status() };
    document.getElementById('status').textContent = value.phase === 'failed'
      ? value.error
      : 'Original assets: graphics verification. Use arrow keys and click the canvas. Gameplay integration is pending.';
  },
  start(instance) {
    const tick = () => {
      instance.invokeMethod('Tick');
      if (window.portStatus.phase !== 'failed') frame = requestAnimationFrame(tick);
    };
    frame = requestAnimationFrame(tick);
  },
  stop() { cancelAnimationFrame(frame); }
};
window.addEventListener('keydown', event => {
  if (['ArrowLeft','ArrowRight','ArrowUp','ArrowDown',' '].includes(event.key)) event.preventDefault();
});

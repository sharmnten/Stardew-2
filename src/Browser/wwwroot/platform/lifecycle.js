(() => {
  let instance;
  let focused = true;
  let browserFocused = true, events, observer;
  const diagnostic = new URLSearchParams(location.search).get('diagnostic') === '1';
  function prepare() {
    const canvas = document.getElementById('theCanvas');
    const holder = document.getElementById('canvasHolder');
    const width = Math.max(diagnostic ? 1 : 1280, holder.clientWidth);
    const height = Math.max(diagnostic ? 1 : 720, holder.clientHeight);
    const scale = Math.min(holder.clientWidth / width, holder.clientHeight / height);
    canvas.width = width; canvas.height = height;
    canvas.style.width = `${width * scale}px`; canvas.style.height = `${height * scale}px`;
    const rect = canvas.getBoundingClientRect();
    return { width, height, left: rect.left, top: rect.top, displayWidth: rect.width, displayHeight: rect.height };
  }
  function resize() { if (instance) instance.invokeMethod('Resize', prepare()); }
  function focus(active) {
    browserFocused = active;
    updateFocus();
  }
  function updateFocus() {
    const blocked = document.querySelector('#savePanel,#screenshotPanel') || document.querySelector('#saveFailure:not([hidden])');
    const next = browserFocused && !document.hidden && !blocked;
    if (focused === next) return;
    focused = next;
    if (instance) instance.invokeMethod('FocusChanged', focused);
  }
  window.portLifecycle = {
    prepare,
    isFullScreen() { return !!document.fullscreenElement; },
    async setFullscreen(enabled) {
      try {
        if (!!document.fullscreenElement === enabled) return;
        if (enabled) {
          if (!navigator.userActivation.isActive) return;
          await document.getElementById('canvasHolder').requestFullscreen();
        } else await document.exitFullscreen();
      } catch (error) { document.getElementById('status').textContent = error.message; }
    },
    start(reference) {
      instance = reference;
      events = new AbortController();
      const options = { signal: events.signal };
      instance.invokeMethod('InitializeGamepads');
      // KNI 4.3's constructor skips devices already present; feed its normal connection handler.
      for (const gamepad of navigator.getGamepads?.() ?? []) {
        if (!gamepad?.connected) continue;
        const event = new Event('gamepadconnected');
        Object.defineProperty(event, 'gamepad', { value: gamepad });
        window.dispatchEvent(event);
      }
      resize();
      window.addEventListener('resize', resize, options);
      document.addEventListener('fullscreenchange', resize, options);
      window.addEventListener('blur', () => focus(false), options);
      window.addEventListener('focus', () => focus(true), options);
      document.addEventListener('visibilitychange', () => focus(!document.hidden && document.hasFocus()), options);
      observer = new MutationObserver(updateFocus);
      observer.observe(document.getElementById('app'), { childList: true, subtree: true, attributes: true, attributeFilter: ['hidden'] });
      for (const type of ['keydown','keyup','keypress','mousedown','mouseup','mousemove','wheel']) {
        document.addEventListener(type, event => {
          if (event.target.closest?.('#savePanel,#screenshotPanel,#saveFailure,button,input,select,textarea')) event.stopPropagation();
        }, options);
      }
      document.getElementById('fullscreen')?.addEventListener('click', () => window.portLifecycle.setFullscreen(!document.fullscreenElement), options);
      updateFocus();
    },
    status() {
      const canvas = document.getElementById('theCanvas');
      return { focused, visible: !document.hidden, fullscreen: !!document.fullscreenElement, width: canvas.width, height: canvas.height };
    },
    stop() { events?.abort(); observer?.disconnect(); instance = null; }
  };
})();

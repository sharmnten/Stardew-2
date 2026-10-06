(() => {
  let instance, events, database;
  let pasteGesture = 0, keyboardPaste;
  const deliveredGestures = new Set();
  const screenshots = new Map();
  let nextScreenshot = 1;
  let notice;
  function message(text) { notice = { text, until: performance.now() + 10000 }; document.getElementById('status').textContent = text; }
  function deliverPaste(text, gesture, replacement = false) {
    if (deliveredGestures.has(gesture) || gesture < pasteGesture - 64) return;
    deliveredGestures.add(gesture);
    if (deliveredGestures.size > 64) deliveredGestures.delete(deliveredGestures.values().next().value);
    instance?.invokeMethod('Paste', text, replacement);
  }
  function openScreenshots() {
    return database ??= new Promise((resolve, reject) => {
      const request = indexedDB.open('stardew-browser-screenshots', 1);
      request.onupgradeneeded = () => request.result.createObjectStore('files', { keyPath: 'name' });
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error);
      request.onblocked = () => reject(new Error('Close another game tab to open screenshot storage.'));
    });
  }
  async function screenshotFiles() {
    const db = await openScreenshots();
    return new Promise((resolve, reject) => {
      const transaction = db.transaction('files', 'readonly');
      const request = transaction.objectStore('files').getAll();
      transaction.oncomplete = () => resolve(request.result);
      transaction.onabort = () => reject(transaction.error);
    });
  }
  function base64(bytes) {
    let binary = '';
    for (let start = 0; start < bytes.length; start += 32768)
      binary += String.fromCharCode(...bytes.subarray(start, start + 32768));
    return btoa(binary);
  }
  window.portServices = {
    statusMessage() { return notice?.until > performance.now() ? notice.text : null; },
    openLink(url) {
      const link = new URL(url);
      if (!['https:', 'http:'].includes(link.protocol)) throw new Error('Unsupported credit link.');
      window.open(link.href, '_blank', 'noopener,noreferrer');
    },
    async initialize() { return (await screenshotFiles()).map(file => ({ name: file.name, bytes: base64(file.bytes) })); },
    async storeScreenshot(name, bytes) {
      if (!name.endsWith('.png') || /[\\/]/.test(name)) throw new Error('Invalid screenshot filename.');
      const snapshot = new Uint8Array(bytes);
      try {
        const db = await openScreenshots();
        await new Promise((resolve, reject) => {
          const transaction = db.transaction('files', 'readwrite', { durability: 'strict' });
          transaction.objectStore('files').put({ name, bytes: snapshot });
          transaction.oncomplete = resolve;
          transaction.onabort = () => reject(transaction.error);
        });
        return true;
      } catch (error) { message(`Screenshot downloaded, but browser storage failed: ${error.message}`); return false; }
    },
    async browseScreenshots() {
      document.getElementById('screenshotPanel')?.remove();
      const panel = document.createElement('div'); panel.id = 'screenshotPanel';
      panel.setAttribute('role', 'dialog'); panel.setAttribute('aria-label', 'Screenshots');
      const urls = [];
      const close = document.createElement('button'); close.textContent = 'Close';
      close.onclick = () => { urls.forEach(URL.revokeObjectURL); panel.remove(); document.getElementById('theCanvas').focus(); };
      panel.append(close); document.getElementById('app').append(panel);
      try {
        const files = await screenshotFiles();
        if (!files.length) { const empty = document.createElement('p'); empty.textContent = 'No map screenshots saved yet.'; panel.append(empty); }
        for (const file of files) {
          const url = URL.createObjectURL(new Blob([file.bytes], { type: 'image/png' })); urls.push(url);
          const image = document.createElement('img'); image.src = url; image.alt = file.name;
          const button = document.createElement('button'); button.textContent = `Download ${file.name}`; button.dataset.file = file.name;
          button.onclick = () => portStorage.download(file.name, file.bytes);
          panel.append(image, button);
        }
      } catch (error) { const text = document.createElement('p'); text.textContent = error.message; panel.append(text); }
      close.focus();
    },
    start(reference) {
      instance = reference;
      events?.abort();
      events = new AbortController();
      document.addEventListener('keydown', event => {
        if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'v' && !event.repeat
            && !event.target.closest?.('input,textarea,select'))
          keyboardPaste = { gesture: ++pasteGesture, time: performance.now() };
      }, { signal: events.signal });
      document.addEventListener('paste', event => {
        if (event.target.closest?.('input,textarea,select')) return;
        if (event.clipboardData) {
          event.preventDefault();
          const gesture = keyboardPaste && performance.now() - keyboardPaste.time < 500 ? keyboardPaste.gesture : ++pasteGesture;
          keyboardPaste = null;
          deliverPaste(event.clipboardData.getData('text/plain'), gesture);
        }
      }, { signal: events.signal });
    },
    async readClipboard(replacement = false) {
      const gesture = replacement ? ++pasteGesture : pasteGesture;
      if (deliveredGestures.has(gesture)) return;
      try { deliverPaste(await navigator.clipboard.readText(), gesture, replacement); }
      catch {
        if (replacement) instance?.invokeMethod('CancelPaste');
        message('Clipboard access was blocked. Use the browser’s Paste command.');
      }
    },
    async writeClipboard(text) {
      try { await navigator.clipboard.writeText(text); }
      catch { message('The browser blocked copying to the clipboard.'); }
    },
    createScreenshot(width, height) {
      if (width <= 0 || height <= 0) throw new Error('Invalid screenshot size.');
      const canvas = document.createElement('canvas');
      canvas.width = width; canvas.height = height;
      const context = canvas.getContext('2d', { alpha: false });
      if (!context) throw new Error('The browser could not allocate this screenshot.');
      context.fillStyle = '#000'; context.fillRect(0, 0, width, height);
      const handle = nextScreenshot++;
      screenshots.set(handle, { canvas, context });
      return handle;
    },
    blitScreenshot(handle, x, y, width, height, pixels) {
      if (pixels.length !== width * height * 4) throw new Error('Invalid screenshot tile size.');
      const image = new ImageData(new Uint8ClampedArray(pixels), width, height);
      screenshots.get(handle).context.putImageData(image, x, y);
    },
    encodeScreenshot(handle) { return screenshots.get(handle).canvas.toDataURL('image/png').split(',')[1]; },
    disposeScreenshot(handle) { screenshots.delete(handle); },
    stop() { events?.abort(); instance = null; screenshots.clear(); document.getElementById('screenshotPanel')?.remove(); }
  };
})();

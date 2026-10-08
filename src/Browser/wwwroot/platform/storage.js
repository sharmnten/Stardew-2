(() => {
  'use strict';
  let connection;
  function open() {
    if (!connection) connection = new Promise((resolve, reject) => {
      const request = indexedDB.open('stardew-browser-saves', 1);
      request.onupgradeneeded = () => {
        request.result.createObjectStore('current', { keyPath: 'slot' });
        request.result.createObjectStore('previous', { keyPath: 'slot' });
      };
      request.onsuccess = () => {
        const db = request.result;
        db.onversionchange = () => { db.close(); connection = null; };
        resolve(db);
      };
      request.onerror = () => { connection = null; reject(request.error); };
      request.onblocked = () => { connection = null; reject(new Error('Close other game tabs, then retry opening saves.')); };
    });
    return connection;
  }
  function validate(snapshot) {
    const { slot, files } = snapshot;
    if (typeof slot !== 'string' || !slot || /[/\\\x00-\x1f]/.test(slot) || slot === '.' || slot === '..'
      || (slot.startsWith('@') && slot !== '@settings')) throw new Error('Invalid save folder.');
    const allowed = slot === '@settings' ? ['startup_preferences'] : [slot, 'SaveGameInfo', `${slot}_old`, 'SaveGameInfo_old'];
    if (!files || !Object.hasOwn(files, allowed[0]) || (slot !== '@settings' && !Object.hasOwn(files, 'SaveGameInfo')))
      throw new Error('Select the farm save and SaveGameInfo together.');
    for (const [name, bytes] of Object.entries(files))
      if (!allowed.includes(name) || !(bytes instanceof Uint8Array) || !bytes.length) throw new Error('Invalid save file: ' + name);
  }
  async function read(slot, store = 'current') {
    const db = await open();
    return new Promise((resolve, reject) => {
      const transaction = db.transaction(store, 'readonly');
      const request = transaction.objectStore(store).get(slot);
      transaction.oncomplete = () => resolve(request.result ?? null);
      transaction.onabort = () => reject(transaction.error ?? new DOMException('Save read aborted.', 'AbortError'));
    });
  }
  async function readAll() {
    const db = await open();
    return new Promise((resolve, reject) => {
      const transaction = db.transaction('current', 'readonly');
      const request = transaction.objectStore('current').getAll();
      transaction.oncomplete = () => resolve(request.result);
      transaction.onabort = () => reject(transaction.error ?? new DOMException('Save read aborted.', 'AbortError'));
    });
  }
  async function commit(snapshot) {
    validate(snapshot);
    // Clone before awaiting: the caller cannot change bytes while the transaction is queued.
    snapshot = structuredClone(snapshot);
    const db = await open();
    return new Promise((resolve, reject) => {
      const transaction = db.transaction(['current', 'previous'], 'readwrite', { durability: 'strict' });
      const current = transaction.objectStore('current');
      const previous = transaction.objectStore('previous');
      transaction.oncomplete = () => resolve();
      transaction.onabort = () => reject(transaction.error ?? new DOMException('The save transaction was interrupted.', 'AbortError'));
      const request = current.get(snapshot.slot);
      request.onsuccess = () => {
        if (request.result) previous.put(request.result);
        current.put(snapshot);
      };
    });
  }
  async function deleteSlot(slot) {
    const db = await open();
    return new Promise((resolve, reject) => {
      const transaction = db.transaction(['current', 'previous'], 'readwrite', { durability: 'strict' });
      transaction.objectStore('current').delete(slot);
      transaction.objectStore('previous').delete(slot);
      transaction.oncomplete = () => resolve();
      transaction.onabort = () => reject(transaction.error ?? new DOMException('Save deletion was interrupted.', 'AbortError'));
    });
  }
  function base64(bytes) {
    let binary = '';
    for (let start = 0; start < bytes.length; start += 32768)
      binary += String.fromCharCode(...bytes.subarray(start, start + 32768));
    return btoa(binary);
  }
  function forDotNet(snapshot) {
    return snapshot && { slot: snapshot.slot, files: Object.fromEntries(Object.entries(snapshot.files).map(([name, bytes]) => [name, base64(bytes)])) };
  }
  window.portStorage = { commit, read, readAll, deleteSlot, readPrevious: slot => read(slot, 'previous'),
    download(name, bytes) {
      const url = URL.createObjectURL(new Blob([bytes], { type: 'application/octet-stream' }));
      const anchor = document.createElement('a');
      anchor.href = url; anchor.download = name; document.body.append(anchor); anchor.click(); anchor.remove();
      setTimeout(() => URL.revokeObjectURL(url), 30000);
    },
    updateStatus(status) {
      const panel = document.getElementById('saveFailure');
      if (!panel) return;
      panel.hidden = status?.phase !== 'failed';
      document.getElementById('saveError').textContent = status?.error ?? '';
    },
    readForDotNet: async slot => forDotNet(await read(slot)), readAllForDotNet: async () => (await readAll()).map(forDotNet)
  };
})();

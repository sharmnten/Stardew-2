import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { readFile, stat, mkdir, writeFile } from 'node:fs/promises';
import { resolve, sep, extname } from 'node:path';
import { chromium } from 'playwright';

const types = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm', '.css': 'text/css' };

export async function withGame(testBody, setupPage = async () => {}, route = '/?diagnostic=1', { autoStart = true } = {}) {
  const root = resolve(process.env.PORT_STATIC_ROOT ?? 'src/Browser/bin/Release/net10.0/publish/wwwroot');
  assert.ok(await stat(resolve(root, 'index.html')).catch(() => null), 'Publish the browser host before running browser tests; index.html is absent');
  const server = createServer(async (request, response) => {
    try {
      const pathname = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
      const path = resolve(root, `.${pathname === '/' ? '/index.html' : pathname}`);
      if (!path.startsWith(root + sep)) { response.writeHead(403).end(); return; }
      const body = await readFile(path);
      response.writeHead(200, { 'Content-Type': types[extname(path)] ?? 'application/octet-stream' }).end(body);
    } catch { response.writeHead(404).end(); }
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  let browser;
  try {
    browser = await chromium.launch({ args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--autoplay-policy=user-gesture-required'] });
    const page = await browser.newPage({ viewport: { width: 960, height: 640 } });
    // Failure injection belongs to the harness, never the shipped storage API.
    await page.addInitScript(() => {
      let nextFailure;
      const failures = new WeakMap();
      const transaction = IDBDatabase.prototype.transaction;
      const put = IDBObjectStore.prototype.put;
      window.testSaveFailure = name => {
        if (!['AbortError', 'QuotaExceededError'].includes(name)) throw new Error('Unknown failure');
        nextFailure = name;
      };
      IDBDatabase.prototype.transaction = function(stores, mode, ...rest) {
        const result = transaction.call(this, stores, mode, ...rest);
        if (nextFailure && mode === 'readwrite' && Array.isArray(stores) && stores.includes('previous')) {
          failures.set(result, nextFailure);
          nextFailure = null;
        }
        return result;
      };
      IDBObjectStore.prototype.put = function(...args) {
        const request = put.apply(this, args), failure = failures.get(this.transaction);
        if (failure && this.name === 'current') {
          const tx = this.transaction;
          request.addEventListener('success', () => {
            Object.defineProperty(tx, 'error', { get: () => new DOMException('Injected save transaction failure.', failure) });
            tx.abort();
          });
        }
        return request;
      };
    });
    async function startIfRequired() {
      if (autoStart && !route.includes('diagnostic=1'))
        await page.locator('#startGame').click({ timeout: 30000 });
    }
    const reload = page.reload.bind(page);
    page.reload = async options => { const result = await reload(options); await startIfRequired(); return result; };
    const exceptions = [], resourceErrors = [], resources = new Map();
    page.on('request', request => resources.set(request.url(), { request, status: 0, finished: false }));
    page.on('response', response => {
      const resource = resources.get(response.url());
      if (resource?.request === response.request()) resource.status = response.status();
    });
    page.on('requestfinished', request => {
      const resource = resources.get(request.url());
      if (resource?.request === request) resource.finished = true;
    });
    page.on('pageerror', error => exceptions.push(error.message));
    page.on('console', message => {
      if (message.type() !== 'error') return;
      const url = message.location().url;
      if (url && message.text().startsWith('Failed to load resource:')) resourceErrors.push({ url, text: message.text() });
      else exceptions.push(message.text());
    });
    await setupPage(page);
    // Project sites mount below /repository/. Keep every runtime/content URL
    // under that base when checking the actual published release.
    const gameUrl = process.env.PORT_GAME_URL
      ? new URL(route.replace(/^\//, ''), process.env.PORT_GAME_URL).href
      : `http://127.0.0.1:${server.address().port}${route}`;
    await page.goto(gameUrl, { waitUntil: 'load' });
    try { await startIfRequired(); await testBody(page); }
    catch (error) {
      const output = resolve('.port-cache/browser-failures');
      await mkdir(output, { recursive: true });
      console.info(`Browser flow failed: ${error.stack ?? error}`);
      const mode = route.includes('diagnostic=1') ? 'diagnostic' : 'game';
      await writeFile(resolve(output, `${mode}.json`), JSON.stringify(await page.evaluate(async () => {
        let scenario = null;
        try { if (window.portScenarios) scenario = await window.portScenarios.snapshot(); }
        catch (error) { scenario = { error: String(error) }; }
        return { ...window.portStatus, scenario };
      }), null, 2));
      await page.screenshot({ path: resolve(output, `${mode}.png`) });
      throw error;
    }
    for (const error of resourceErrors) {
      const resource = resources.get(error.url);
      if (!resource?.finished || resource.status < 200 || resource.status >= 400)
        exceptions.push(`${error.text} (${error.url})`);
    }
    assert.deepEqual(exceptions, [], 'Browser raised an exception, logged an error, or left an asset download unrecovered');
  } finally {
    await browser?.close();
    await new Promise(resolve => server.close(resolve));
  }
}

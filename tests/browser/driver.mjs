import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { readFile, stat, mkdir, writeFile } from 'node:fs/promises';
import { resolve, sep, extname } from 'node:path';
import { chromium } from 'playwright';

const types = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm', '.css': 'text/css' };

export async function withGame(testBody, setupPage = async () => {}, route = '/?diagnostic=1') {
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
    const exceptions = [];
    page.on('pageerror', error => exceptions.push(error.message));
    page.on('console', message => { if (message.type() === 'error') exceptions.push(message.text()); });
    await setupPage(page);
    await page.goto(`http://127.0.0.1:${server.address().port}${route}`, { waitUntil: 'load' });
    try { await testBody(page); }
    catch (error) {
      const output = resolve('.port-cache/browser-failures');
      await mkdir(output, { recursive: true });
      const mode = route.includes('diagnostic=1') ? 'diagnostic' : 'game';
      await writeFile(resolve(output, `${mode}.json`), JSON.stringify(await page.evaluate(() => window.portStatus), null, 2));
      await page.screenshot({ path: resolve(output, `${mode}.png`) });
      throw error;
    }
    assert.deepEqual(exceptions, [], 'Browser raised an exception or logged an error');
  } finally {
    await browser?.close();
    await new Promise(resolve => server.close(resolve));
  }
}

import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';

test('original credit URLs open through the browser without a desktop process', { timeout: 60000 }, async () => {
  await withGame(async page => {
    const opened = await page.evaluate(() => {
      const open = window.open; let request;
      window.open = (...args) => { request = args; return null; };
      try { portServices.openLink('https://www.stardewvalley.net/'); return request; }
      finally { window.open = open; }
    });
    assert.deepEqual(opened, ['https://www.stardewvalley.net/', '_blank', 'noopener,noreferrer']);
  });
});

test('browser screenshot tiles preserve opaque original RGB pixels in a downloaded PNG', { timeout: 60000 }, async () => {
  await withGame(async page => {
    assert.equal(await page.evaluate(() => typeof window.portServices?.createScreenshot), 'function');
    const result = await page.evaluate(async () => {
      const handle = portServices.createScreenshot(3, 2);
      portServices.blitScreenshot(handle, 1, 0, 2, 1, new Uint8Array([255,0,0,255, 0,255,0,255]));
      const encoded = portServices.encodeScreenshot(handle);
      portServices.disposeScreenshot(handle);
      const blob = await (await fetch(`data:image/png;base64,${encoded}`)).blob();
      const bitmap = await createImageBitmap(blob);
      const canvas = document.createElement('canvas'); canvas.width = 3; canvas.height = 2;
      const context = canvas.getContext('2d'); context.drawImage(bitmap, 0, 0);
      return { signature: Array.from(new Uint8Array(await blob.arrayBuffer()).slice(0,8)),
        pixels: Array.from(context.getImageData(0,0,3,2).data) };
    });
    assert.deepEqual(result.signature, [137,80,78,71,13,10,26,10]);
    assert.deepEqual(result.pixels, [0,0,0,255,255,0,0,255,0,255,0,255,0,0,0,255,0,0,0,255,0,0,0,255]);
  });
});

test('downloaded screenshots persist byte-for-byte and remain browsable after reload', { timeout: 90000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 60000 });
    const png = await page.evaluate(() => {
      const handle = portServices.createScreenshot(2, 1);
      portServices.blitScreenshot(handle, 0, 0, 2, 1, new Uint8Array([70,80,90,255, 10,20,30,255]));
      const png = portServices.encodeScreenshot(handle); portServices.disposeScreenshot(handle); return png;
    });
    await page.evaluate(async png => portServices.storeScreenshot('Farm.png', Uint8Array.from(atob(png), c => c.charCodeAt(0))), png);
    await page.reload({ waitUntil: 'load' });
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 60000 });
    assert.deepEqual(await page.evaluate(() => portServices.initialize()), [{ name: 'Farm.png', bytes: png }]);
    await page.evaluate(() => portServices.browseScreenshots());
    assert.equal(await page.locator('#screenshotPanel').isVisible(), true);
    const download = page.waitForEvent('download');
    await page.locator('#screenshotPanel button[data-file]').click();
    const file = await download;
    assert.equal(file.suggestedFilename(), 'Farm.png');
    await page.locator('#screenshotPanel button').first().click();
    assert.equal(await page.locator('#screenshotPanel').count(), 0);
  });
});

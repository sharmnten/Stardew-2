import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';

test('two distinct paste gestures deliver identical text twice', { timeout: 60000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 45000 });
    const received = await page.evaluate(() => {
      const texts = [];
      portServices.start({ invokeMethod(name, text) { if (name === 'Paste') texts.push(text); } });
      for (let i = 0; i < 2; i++) {
        document.getElementById('theCanvas').dispatchEvent(new KeyboardEvent('keydown', { key: 'v', ctrlKey: true, bubbles: true }));
        const data = new DataTransfer(); data.setData('text/plain', 'Repeated');
        document.dispatchEvent(new ClipboardEvent('paste', { clipboardData: data, bubbles: true }));
      }
      return texts;
    });
    assert.deepEqual(received, ['Repeated', 'Repeated']);
  });
});

import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from '../browser/driver.mjs';

test('production recovers from a temporary asset-host 503 and starts with verified original content', { timeout: 180000 }, async () => {
  let requests = 0;
  await withGame(async page => {
    await page.waitForFunction(() => ['ready', 'failed'].includes(portStatus.phase), null, { timeout: 120000 });
    assert.equal(await page.evaluate(() => portStatus.phase), 'ready');
    assert.equal(requests, 2);
    assert.equal(await page.evaluate(() => typeof window.portScenarios), 'undefined');
  }, async page => {
    await page.route('**/Content/Characters/Dialogue/Emily.ru-RU.xnb', async route => {
      if (++requests === 1) await route.fulfill({ status: 503, body: 'Temporary asset-host failure' });
      else await route.continue();
    });
  }, '/');
});

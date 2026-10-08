import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';

test('startup waits for Start and reports verified download progress', { timeout: 180000 }, async () => {
  let contentRequests = 0;
  await withGame(async page => {
    await page.locator('#startGame').waitFor({ state: 'visible', timeout: 15000 });
    assert.equal(contentRequests, 0, 'Original content must wait for the player gesture');
    assert.equal(await page.evaluate(() => window.portStatus.phase), 'waiting');
    await page.locator('#startGame').click();
    await page.waitForFunction(() => window.portStatus.download?.total > 0, null, { timeout: 120000 });
    assert.match(await page.locator('#status').textContent(), /\d+.*\d+.*MB/);
    await page.waitForFunction(() => ['ready', 'failed'].includes(window.portStatus.phase), null, { timeout: 120000 });
    assert.equal(await page.evaluate(() => window.portStatus.phase), 'ready');
    assert.equal(await page.locator('#startGame').count(), 0);
    assert.doesNotMatch(await page.locator('#status').textContent(), /pending|in development/i);
  }, page => page.on('request', request => { if (request.url().includes('/Content/')) contentRequests++; }), '/', { autoStart: false });
});

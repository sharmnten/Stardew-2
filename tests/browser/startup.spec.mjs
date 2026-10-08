import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';

const deployed = process.env.PORT_GAME_URL?.startsWith('https:');
const readyTimeout = deployed ? 300000 : 120000;

test('startup waits for Start and reports verified download progress', { timeout: deployed ? 480000 : 180000 }, async () => {
  let contentRequests = 0;
  await withGame(async page => {
    await page.locator('#startGame').waitFor({ state: 'visible', timeout: deployed ? 120000 : 15000 });
    assert.equal(contentRequests, 0, 'Original content must wait for the player gesture');
    assert.equal(await page.evaluate(() => window.portStatus.phase), 'waiting');
    await page.locator('#startGame').click();
    await page.waitForFunction(() => window.portStatus.download?.total > 0, null, { timeout: readyTimeout });
    assert.match(await page.locator('#status').textContent(), /\d+.*\d+.*MB/);
    await page.waitForFunction(() => ['ready', 'failed'].includes(window.portStatus.phase), null, { timeout: readyTimeout });
    assert.equal(await page.evaluate(() => window.portStatus.phase), 'ready');
    assert.equal(await page.locator('#startGame').count(), 0);
    assert.doesNotMatch(await page.locator('#status').textContent(), /pending|in development/i);
  }, page => page.on('request', request => { if (request.url().includes('/Content/')) contentRequests++; }), '/', { autoStart: false });
});

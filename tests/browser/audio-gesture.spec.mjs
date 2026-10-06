import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';

test('keyboard input on the game canvas unlocks original audio', { timeout: 120000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => ['ready', 'failed'].includes(window.portStatus?.phase), null, { timeout: 90000 });
    const initial = await page.evaluate(() => window.portStatus);
    assert.equal(initial.phase, 'ready', initial.error);
    assert.equal(initial.audioState, 'suspended');
    await page.locator('#theCanvas').focus();
    await page.keyboard.press('ArrowRight');
    await page.waitForFunction(() => window.portAudio.status().audioState === 'running', null, { timeout: 5000 });
    assert.equal((await page.evaluate(() => window.portAudio.status())).audioError, null);
  });
});

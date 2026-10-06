import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';

test('the original custom font reader parses localized game content', { timeout: 120000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => ['ready','failed'].includes(window.portStatus?.phase), null, { timeout: 90000 });
    const status = await page.evaluate(() => window.portStatus);
    assert.equal(status.phase, 'ready', status.error);
    assert.equal(status.customFontCharacters, 2514, 'The original Japanese font must retain all glyphs (count from Japanese.fnt)');
    assert.ok(status.verifiedContentBytes > 0, 'Game content must pass the manifest checksum before reading');
  });
});

test('a corrupted original asset stops loading with its identity', { timeout: 120000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => ['ready','failed'].includes(window.portStatus?.phase), null, { timeout: 90000 });
    const status = await page.evaluate(() => window.portStatus);
    assert.equal(status.phase, 'failed');
    assert.match(status.error, /checksum mismatch.*Maps\/Farm/);
  }, page => page.route('**/Content/Maps/Farm.xnb', route => route.fulfill({ body: Buffer.from('altered original content') })));
});

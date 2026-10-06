import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';

// Catches missing original readers, failed draw/readback, and disconnected input.
test('original texture, font and map render through the browser framework', { timeout: 120000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => ['ready', 'failed'].includes(window.portStatus?.phase), null, { timeout: 90000 });
    const status = await page.evaluate(() => window.portStatus);
    assert.equal(status.phase, 'ready', status.error);
    assert.equal(status.renderer, 'StardewBrowser.Framework.Graphics.SpriteBatch');
    assert.equal(status.desktopFrameworkLoaded, false, 'Desktop MonoGame must not participate in browser content loading');
    assert.ok(status.originalTextureReads > 0, 'Original packed-size texture reader must load the probe assets');
    assert.ok(status.textureWidth > 0 && status.fontGlyphs > 0 && status.mapLayers > 0);
    assert.ok(status.renderedTiles > 0, 'Original map tiles must actually be drawn');
    assert.ok(status.targetDistinctColors > 20, 'Render target must contain original artwork');
    assert.ok(status.fontPixels > 0, 'Original font must draw visible glyphs');
    assert.equal(status.effectVerified, true, status.effectError);

    const before = status.cursorX;
    await page.keyboard.down('ArrowRight');
    await page.waitForFunction(x => window.portStatus.cursorX > x, before);
    await page.keyboard.up('ArrowRight');
    await page.locator('#theCanvas').click({ position: { x: 420, y: 300 }, delay: 80 });
    await page.waitForFunction(() => window.portStatus.pointerClicks > 0);
    const image = await page.locator('#theCanvas').screenshot();
    assert.ok(image.length > 10000, 'Visible canvas must contain the rendered assets');
  });
});

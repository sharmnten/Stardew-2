import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';
import { clickControl, pointAtWorld, walkToBed, waitForAsync } from './game-controls.mjs';

test('original museum donation input, quest and saved display match desktop', { timeout: 300000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => portStatus?.phase === 'ready', null, { timeout: 120000 });
    const id = 'museum-quests';
    await page.evaluate(id => portScenarios.load(id), id);
    const expected = await page.evaluate(async id => {
      const response = await fetch(`Fixtures/reference/${id}.json`);
      if (!response.ok) throw new Error('Generate the original museum fixture before publishing');
      return (await response.json()).scenario;
    }, id);
    const spot = await page.evaluate(() => portScenarios.run('museum-visit'));
    await page.evaluate(() => portScenarios.run('museum-donate-menu'));
    await clickControl(page, '0');
    await page.waitForFunction(() => portStatus.game.museum?.heldItem === '(O)86');
    await pointAtWorld(page, spot.x * 64 + 32, spot.y * 64 + 32);
    await page.mouse.down();
    await waitForAsync(page, async () => (await portScenarios.snapshot()).museum.pieces.length === 1);
    await page.mouse.up();
    assert.deepEqual(await museum(page), expected.afterDonation);
    await clickControl(page, 'okButton');
    await page.waitForFunction(() => !portStatus.game.menu.type && portStatus.game.player.canMove);
    await page.evaluate(() => portScenarios.run('museum-home'));
    await walkToBed(page);
    await clickControl(page, 'Yes');
    await page.waitForFunction(() => portStatus.game.menu.type === 'SaveGameMenu', null, { timeout: 90000 });
    await page.waitForFunction(() => portStatus.game.day === 6 && !portStatus.game.overnight
      && portStatus.game.mode === 3 && !portStatus.game.loading && portStatus.game.player.canMove
      && !portStatus.game.menu.type && portStatus.game.save?.mainBytes > 0, null, { timeout: 90000 });
    await waitForAsync(page, async () => {
      const slot = portStatus.game.save.slot;
      const stored = await portStorage.read(slot);
      if (!stored) return false;
      const xml = new DOMParser().parseFromString(new TextDecoder().decode(stored.files[slot]), 'application/xml');
      return xml.querySelector('SaveGame > dayOfMonth')?.textContent === '6';
    });
    await page.reload();
    await page.waitForFunction(() => portStatus?.phase === 'ready', null, { timeout: 120000 });
    await clickControl(page, 'Load');
    await page.waitForFunction(() => portStatus.game.menu.type === 'LoadGameMenu' && portStatus.game.menu.saves?.length === 1);
    await clickControl(page, '0');
    await page.waitForFunction(() => portStatus.game.mode === 3 && !portStatus.game.loading
      && portStatus.game.day === 6 && portStatus.game.player.canMove && !portStatus.game.menu.type
      && !portStatus.game.warping, null, { timeout: 90000 });
    assert.deepEqual(await museum(page), expected.afterReload);
  }, undefined, '/');
});

const museum = page => page.evaluate(async () => (await portScenarios.snapshot()).museum);

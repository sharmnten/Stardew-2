import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from '../browser/driver.mjs';
import { snapshot, clickControl, hold, walkToBed, waitForAsync } from '../browser/game-controls.mjs';

test('production creates an original farm, saves overnight, exports and cold-loads without test drivers', { timeout: 360000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => ['ready', 'failed'].includes(portStatus.phase), null, { timeout: 120000 });
    assert.equal(await page.evaluate(() => portStatus.phase), 'ready');
    assert.equal(await page.evaluate(() => typeof window.portScenarios), 'undefined');
    assert.equal(await page.evaluate(() => typeof portStorage.testing), 'undefined');
    await hold(page, 'Escape', 100);
    await page.waitForFunction(() => portStatus.game.menu.allowsInteraction);
    await clickControl(page, 'New');
    await page.waitForFunction(() => portStatus.game.menu.type === 'CharacterCustomization');
    for (const [name, value] of [['nameBoxCC', 'Release'], ['farmnameBoxCC', 'Browser'], ['favThingBoxCC', 'Farming']]) {
      await clickControl(page, name);
      await page.keyboard.type(value, { delay: 40 });
    }
    await clickControl(page, 'skipIntroButton');
    await clickControl(page, 'okButton');
    await page.waitForFunction(() => portStatus.game.player.customized && portStatus.game.location?.name === 'FarmHouse'
      && portStatus.game.day === 1 && !portStatus.game.overnight && !portStatus.game.menu.type, null, { timeout: 90000 });
    await walkToBed(page);
    await clickControl(page, 'Yes');
    await page.waitForFunction(() => portStatus.game.day === 2 && !portStatus.game.overnight && !portStatus.game.menu.type
      && portStatus.game.player.canMove && portStatus.storage.phase === 'saved', null, { timeout: 90000 });
    const morning = await snapshot(page);
    assert.equal(morning.player.name, 'Release');
    await waitForAsync(page, async () => {
      const slot = portStatus.game.save.slot, record = await portStorage.read(slot);
      if (!record) return false;
      const xml = new DOMParser().parseFromString(new TextDecoder().decode(record.files[slot]), 'application/xml');
      return xml.querySelector('SaveGame > dayOfMonth')?.textContent === '2';
    });
    await page.reload();
    await page.waitForFunction(() => portStatus.phase === 'ready', null, { timeout: 120000 });
    await page.locator('#saveTools').click();
    await page.waitForFunction(() => document.querySelector('#saveSlot')?.options.length === 1);
    const download = page.waitForEvent('download');
    await page.locator('#exportSave').click();
    await (await download).saveAs('.port-cache/release-save-export.zip');
    await page.locator('#savePanel button').first().click();
    await hold(page, 'Escape', 100);
    await clickControl(page, 'Load');
    await page.waitForFunction(() => portStatus.game.menu.type === 'LoadGameMenu' && portStatus.game.menu.saves?.length === 1);
    await clickControl(page, '0');
    await page.waitForFunction(() => portStatus.game.mode === 3 && !portStatus.game.loading && portStatus.game.day === 2
      && portStatus.game.player.canMove && !portStatus.game.menu.type, null, { timeout: 90000 });
    const loaded = await snapshot(page);
    assert.equal(loaded.player.name, morning.player.name);
    assert.equal(loaded.player.farmName, morning.player.farmName);
    assert.equal(loaded.player.money, morning.player.money);
    assert.equal(loaded.location.name, 'FarmHouse');
    assert.equal(await page.evaluate(() => typeof window.portScenarios), 'undefined');
  }, undefined, '/');
});

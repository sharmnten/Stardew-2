import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { withGame } from './driver.mjs';
import { snapshot, clickControl, hold, walkTo } from './game-controls.mjs';

test('advanced original desktop save sleeps across seasons, exports and cold reloads in browser', { timeout: 360000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
    await page.evaluate(() => window.portScenarios.load('advanced-desktop-roundtrip'));
    const reference = await page.evaluate(async () => {
      const response = await fetch('Fixtures/reference/advanced-desktop-roundtrip.json');
      if (!response.ok) throw new Error('Generate the advanced original desktop save before publishing');
      return (await response.json()).scenario;
    });
    assert.deepEqual((await page.evaluate(() => portScenarios.snapshot())).advanced, reference.advancedBefore);
    console.info('Advanced original desktop fixture loaded and populated fields matched.');
    const house = (await snapshot(page)).location;
    // A loaded save starts beside the bed; the doorway route is for entry from outside.
    const sleeping = state => state.menu.type === 'DialogueBox';
    await walkTo(page, house.bed.x - 128, (await snapshot(page)).player.positionY, sleeping);
    if ((await snapshot(page)).menu.type !== 'DialogueBox') await walkTo(page, house.bed.x - 128, house.bed.y, sleeping);
    if ((await snapshot(page)).menu.type !== 'DialogueBox') await walkTo(page, house.bed.x, house.bed.y, sleeping);
    await clickControl(page, 'Yes');
    console.info('Original bed sleep accepted.');
    await page.waitForFunction(() => window.portStatus.game.menu.type === 'ShippingMenu', null, { timeout: 90000 });
    // The original shipping menu runs its own intro before accepting OK.
    await page.waitForTimeout(4000);
    await clickControl(page, 'okButton');
    await page.waitForFunction(() => window.portStatus.game.day === 1 && !window.portStatus.game.overnight
      && window.portStatus.game.menu.type === null && window.portStatus.game.player.canMove, null, { timeout: 90000 });
    assert.deepEqual((await page.evaluate(() => portScenarios.snapshot())).advanced, reference.afterSleep);
    console.info('Original season rollover, shipping payment and machine completion matched desktop.');
    const stored = await page.evaluate(async () => (await portStorage.readAllForDotNet()).find(record => record.slot !== '@settings'));
    assert.ok(stored?.files?.SaveGameInfo);
    await page.reload({ waitUntil: 'load' });
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
    await page.locator('#saveTools').click();
    await page.waitForFunction(() => document.querySelector('#saveSlot')?.options.length === 1);
    const download = page.waitForEvent('download');
    await page.locator('#exportSave').click();
    await (await download).saveAs('.port-cache/task-7-advanced-export.zip');
    assert.ok((await readFile('.port-cache/task-7-advanced-export.zip')).length > 100000);
    await page.locator('#savePanel button').first().click();
    await hold(page, 'Escape', 100);
    await clickControl(page, 'Load');
    await page.waitForFunction(() => window.portStatus.game.menu.type === 'LoadGameMenu'
      && window.portStatus.game.menu.saves?.length === 1);
    await clickControl(page, '0');
    await page.waitForFunction(() => window.portStatus.game.mode === 3 && !window.portStatus.game.loading
      && window.portStatus.game.location?.name === 'FarmHouse'
      && window.portStatus.game.menu.type === null && window.portStatus.game.player.canMove
      && !window.portStatus.game.warping, null, { timeout: 90000 });
    assert.deepEqual((await page.evaluate(() => portScenarios.snapshot())).advanced, reference.afterSleepReload);
    console.info('Advanced original fields survived export and cold normal Load.');
    const x = (await snapshot(page)).player.positionX;
    await hold(page, 'a', 100);
    assert.ok((await snapshot(page)).player.positionX < x);
  }, undefined, '/');
});

import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';
import { clickControl, hold, pointAtWorld, walkToBed, waitForAsync } from './game-controls.mjs';

test('original generated dungeons, weapon encounter and saved kills match desktop in browser', { timeout: 360000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
    const id = 'combat-dungeons';
    await page.evaluate(id => portScenarios.load(id), id);
    const expected = await page.evaluate(async id => {
      const response = await fetch(`Fixtures/reference/${id}.json`);
      if (!response.ok) throw new Error('Generate the original desktop dungeon fixture before publishing');
      return (await response.json()).scenario;
    }, id);
    assert.deepEqual(await page.evaluate(id => portScenarios.run(id), id), expected.observations);
    const target = await page.evaluate(() => portScenarios.run('combat-enter'));
    await hold(page, '1', 0);
    await pointAtWorld(page, target.x, target.y);
    await page.mouse.down();
    await page.waitForFunction(() => portStatus.game.input.leftPressed);
    await page.mouse.up();
    await page.waitForFunction(() => !portStatus.game.input.leftPressed);
    await waitForAsync(page, async () => (await portScenarios.snapshot()).combat.killed);
    await page.waitForFunction(() => portStatus.game.player.canMove && !portStatus.game.player.usingTool);
    assert.deepEqual(await page.evaluate(async () => (await portScenarios.snapshot()).combat), expected.liveCombat);
    await page.evaluate(() => portScenarios.run('combat-home'));
    await walkToBed(page);
    await clickControl(page, 'Yes');
    await page.waitForFunction(() => portStatus.game.menu.type === 'SaveGameMenu', null, { timeout: 90000 });
    await page.waitForFunction(() => portStatus.game.day === 6 && !portStatus.game.overnight
      && portStatus.game.player.canMove && !portStatus.game.menu.type, null, { timeout: 90000 });
    await waitForAsync(page, async () => {
      const slot = portStatus.game.save.slot, stored = await portStorage.read(slot);
      if (!stored) return false;
      const xml = new DOMParser().parseFromString(new TextDecoder().decode(stored.files[slot]), 'application/xml');
      return xml.querySelector('SaveGame > dayOfMonth')?.textContent === '6';
    });
    await page.reload();
    await page.waitForFunction(() => portStatus?.phase === 'ready', null, { timeout: 120000 });
    await clickControl(page, 'Load');
    await page.waitForFunction(() => portStatus.game.menu.type === 'LoadGameMenu' && portStatus.game.menu.saves?.length === 1);
    await clickControl(page, '0');
    await page.waitForFunction(() => portStatus.game.mode === 3 && !portStatus.game.loading && portStatus.game.day === 6
      && portStatus.game.player.canMove && !portStatus.game.menu.type && !portStatus.game.warping,
      null, { timeout: 90000 });
    assert.deepEqual(await page.evaluate(async () => (await portScenarios.snapshot()).savedCombat), expected.afterCombatReload);
  }, undefined, '/');
});

import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';
import { clickControl, hold, pointAtWorld, walkToBed, waitForAsync } from './game-controls.mjs';

test('original fishing methods and shore trap harvest, bait and saved catch match desktop', { timeout: 300000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
    const id = 'fishing-gathering';
    await page.evaluate(id => portScenarios.load(id), id);
    const expected = await page.evaluate(async id => {
      const response = await fetch(`Fixtures/reference/${id}.json`);
      if (!response.ok) throw new Error('Generate the original desktop fishing fixture before publishing');
      return (await response.json()).scenario;
    }, id);
    assert.deepEqual(await page.evaluate(id => portScenarios.run(id), id), expected.observations);
    const pot = await page.evaluate(() => portScenarios.run('fishing-shore'));
    await hold(page, '1', 0);
    await pointAtWorld(page, pot.x * 64 + 32, pot.y * 64 + 32);
    await page.mouse.down({ button: 'right' });
    await waitForAsync(page, async () => !(await portScenarios.snapshot()).fishing.pot.ready);
    await page.mouse.up({ button: 'right' });
    await finishInteraction(page);
    assert.deepEqual(await fishing(page), expected.afterHarvest);
    await hold(page, '2', 0);
    await pointAtWorld(page, pot.x * 64 + 32, pot.y * 64 + 32);
    await page.mouse.down({ button: 'right' });
    await waitForAsync(page, async () => (await portScenarios.snapshot()).fishing.pot.bait === '(O)685');
    await page.mouse.up({ button: 'right' });
    assert.deepEqual(await fishing(page), expected.afterRebait);
    await finishInteraction(page);
    await page.evaluate(() => portScenarios.run('fishing-home'));
    await walkToBed(page);
    await clickControl(page, 'Yes');
    await page.waitForFunction(() => portStatus.game.menu.type === 'SaveGameMenu', null, { timeout: 90000 });
    await page.waitForFunction(() => portStatus.game.day === 2 && !portStatus.game.overnight
      && !portStatus.game.loading && portStatus.game.player.canMove && !portStatus.game.menu.type,
    null, { timeout: 90000 });
    await waitForAsync(page, async () => {
      const slot = portStatus.game.save.slot;
      const stored = await portStorage.read(slot);
      if (!stored) return false;
      const xml = new DOMParser().parseFromString(new TextDecoder().decode(stored.files[slot]), 'application/xml');
      return xml.querySelector('SaveGame > dayOfMonth')?.textContent === '2';
    });
    await page.reload();
    await page.waitForFunction(() => portStatus?.phase === 'ready', null, { timeout: 120000 });
    await clickControl(page, 'Load');
    await page.waitForFunction(() => portStatus.game.menu.type === 'LoadGameMenu' && portStatus.game.menu.saves?.length === 1);
    await clickControl(page, '0');
    await page.waitForFunction(() => portStatus.game.mode === 3 && !portStatus.game.loading
      && portStatus.game.day === 2 && portStatus.game.player.canMove && !portStatus.game.menu.type
      && !portStatus.game.warping, null, { timeout: 90000 });
    assert.deepEqual(await fishing(page), expected.afterReload);
  }, undefined, '/');
});

const fishing = page => page.evaluate(async () => (await portScenarios.snapshot()).fishing);

async function finishInteraction(page) {
  await page.waitForFunction(() => portStatus.game.player.canMove
    || (portStatus.game.menu.type === 'DialogueBox' && portStatus.game.menu.allowsInteraction));
  if (await page.evaluate(() => portStatus.game.menu.type === 'DialogueBox')) {
    const player = await page.evaluate(() => portStatus.game.player);
    await pointAtWorld(page, player.positionX, player.positionY + 96);
    await hold(page, 'x', 0);
    await page.waitForFunction(() => !portStatus.game.menu.type && portStatus.game.player.canMove);
  }
}

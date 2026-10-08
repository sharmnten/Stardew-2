import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';
import { snapshot, clickControl, hold, walkTo, waitForAsync } from './game-controls.mjs';

test('browser original overnight birth accepts a name and persists the child', { timeout: 360000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => portStatus?.phase === 'ready', null, { timeout: 120000 });
    const id = 'characters-family-birth';
    await page.evaluate(id => portScenarios.load(id), id);
    const expected = await page.evaluate(async id => (await (await fetch(`Fixtures/reference/${id}.json`)).json()).scenario, id);
    assert.deepEqual(await family(page), expected.beforeBirth);
    const route = await page.evaluate(() => portScenarios.run('family-bed-route'));
    console.info('Loaded birth prerequisites; original bed route', JSON.stringify({ route, player: (await snapshot(page)).player }));
    for (const point of route) {
      await walkTo(page, point.x * 64, point.y * 64, state => state.menu.type === 'DialogueBox', 8);
      if ((await snapshot(page)).menu.type === 'DialogueBox') break;
    }
    await clickControl(page, 'Yes');
    await page.waitForFunction(() => portStatus.game.menu.type === 'DialogueBox' && portStatus.game.menu.allowsInteraction,
      null, { timeout: 90000 });
    assert.equal((await page.evaluate(() => portScenarios.snapshot())).farmEvent, 'BirthingEvent');
    await hold(page, 'x', 0);
    await page.waitForFunction(() => portStatus.game.menu.type === 'NamingMenu');
    assert.equal((await snapshot(page)).menu.text.textBox, '');
    await page.keyboard.type('PortBaby');
    await clickControl(page, 'doneNamingButton');
    await page.waitForFunction(() => portStatus.game.menu.type === 'SaveGameMenu', null, { timeout: 90000 });
    await page.waitForFunction(() => portStatus.game.day === 6 && !portStatus.game.overnight
      && portStatus.game.player.canMove && !portStatus.game.menu.type, null, { timeout: 90000 });
    await waitForAsync(page, async () => (await portScenarios.snapshot()).morningQueueCount === 0);
    assert.deepEqual(await family(page), expected.afterBirth);
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
    assert.deepEqual(await family(page), expected.afterBirthReload);
  }, undefined, '/');
});

const family = page => page.evaluate(async () => (await portScenarios.snapshot()).family);

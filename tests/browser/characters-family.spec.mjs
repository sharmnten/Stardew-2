import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';
import { snapshot, clickControl, hold, pointAtWorld, walkTo, waitForAsync } from './game-controls.mjs';

test('original NPC gift input and overnight family persistence match desktop', { timeout: 300000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
    const id = 'characters-family';
    await page.evaluate(id => portScenarios.load(id), id);
    const expected = await page.evaluate(async id => {
      const response = await fetch(`Fixtures/reference/${id}.json`);
      if (!response.ok) throw new Error('Generate the original desktop family fixture before publishing');
      return (await response.json()).scenario;
    }, id);
    assert.deepEqual(await page.evaluate(id => portScenarios.run(id), id), expected.observations);
    await page.evaluate(id => portScenarios.load(id), id);
    await page.evaluate(() => portScenarios.run('family-visit-linus'));
    await hold(page, '1', 0);
    const target = await page.evaluate(async () => (await portScenarios.snapshot()).linusPosition);
    await pointAtWorld(page, target.x + 32, target.y + 32);
    await page.mouse.down({ button: 'right' });
    await waitForAsync(page, async () => (await portScenarios.snapshot()).family.coconuts === 0);
    await page.mouse.up({ button: 'right' });
    await page.waitForFunction(() => portStatus.game.menu.type === 'DialogueBox' && portStatus.game.menu.allowsInteraction);
    await pointAtWorld(page, (await snapshot(page)).player.positionX, (await snapshot(page)).player.positionY + 96);
    await hold(page, 'x', 0);
    await page.waitForFunction(() => !portStatus.game.menu.type && portStatus.game.player.canMove);
    assert.deepEqual(await family(page), expected.afterNormalGift);
    console.info('Original Linus gift consumed through browser input.');
    const route = await page.evaluate(() => portScenarios.run('family-home'));
    assert.ok(route.length > 0, 'Original upgraded-home pathfinder must find the bed');
    for (const point of route) {
      await walkTo(page, point.x * 64, point.y * 64, state => state.menu.type === 'DialogueBox', 8);
      if ((await snapshot(page)).menu.type === 'DialogueBox') break;
    }
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
    console.info('Original married household completed its overnight save.');
    await page.reload();
    await page.waitForFunction(() => portStatus?.phase === 'ready', null, { timeout: 120000 });
    await clickControl(page, 'Load');
    await page.waitForFunction(() => portStatus.game.menu.type === 'LoadGameMenu' && portStatus.game.menu.saves?.length === 1);
    await clickControl(page, '0');
    await page.waitForFunction(() => portStatus.game.mode === 3 && !portStatus.game.loading
      && portStatus.game.day === 6 && portStatus.game.player.canMove && !portStatus.game.menu.type
      && !portStatus.game.warping, null, { timeout: 90000 });
    assert.deepEqual(await family(page), expected.afterFamilyReload);
  }, undefined, '/');
});

const family = page => page.evaluate(async () => (await portScenarios.snapshot()).family);

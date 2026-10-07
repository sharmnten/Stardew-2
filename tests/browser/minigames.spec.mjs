import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';
import { snapshot, hold, clickControl, pointAtWorld, waitForAsync } from './game-controls.mjs';

test('original arcade input, firing and generated kart physics match desktop', { timeout: 240000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
    const id = 'original-minigames';
    await page.evaluate(id => portScenarios.load(id), id);
    const expected = await page.evaluate(async id => {
      const response = await fetch(`Fixtures/reference/${id}.json`);
      if (!response.ok) throw new Error('Generate the original desktop arcade fixture before publishing');
      return (await response.json()).scenario.observations;
    }, id);
    assert.deepEqual(await page.evaluate(id => portScenarios.run(id), id), expected);
    await clickControl(page, 'Continue');
    await page.waitForFunction(() => portStatus.game.minigame?.type === 'AbigailGame');
    const before = (await snapshot(page)).minigame;
    await hold(page, 'd', 100);
    assert.ok((await snapshot(page)).minigame.x > before.x, 'Normal browser input must move the resumed arcade player');
    await page.keyboard.down('ArrowUp');
    await page.waitForFunction(() => portStatus.game.minigame.bullets > 0);
    await page.keyboard.up('ArrowUp');
    await hold(page, 'Escape', 100);
    await page.waitForFunction(() => !portStatus.game.minigame);
    await page.waitForFunction(() => !portStatus.game.menu.type && portStatus.game.player.canMove);
    for (const [choice, mode] of [['Endless', 2], ['Progress', 3]]) {
      const cabinet = await page.evaluate(() => portScenarios.run('minigames-kart'));
      assert.equal((await snapshot(page)).location.name, 'Saloon', 'Visit the actual original arcade cabinet');
      assert.equal((await snapshot(page)).menu.type, null, 'Cabinet setup must leave opening the menu to ordinary world input');
      await pointAtWorld(page, cabinet.x * 64 + 32, cabinet.y * 64 + 32);
      await page.mouse.down({ button: 'right' });
      await page.waitForFunction(() => portStatus.game.menu.type === 'DialogueBox');
      await page.mouse.up({ button: 'right' });
      await clickControl(page, choice);
      await waitForAsync(page, async () => (await portScenarios.snapshot()).kart?.canStart);
      assert.equal((await kart(page)).mode, mode);
      for (let tap = 0; tap < 20 && (await kart(page)).state !== 'Ingame'; tap++) {
        await hold(page, 'Space', 100);
        await page.waitForTimeout(500);
      }
      assert.equal((await kart(page)).state, 'Ingame', 'Normal Space input must start the selected kart mode');
      await waitForAsync(page, async () => (await portScenarios.snapshot()).kart.grounded);
      await hold(page, 'p', 100);
      assert.equal((await kart(page)).paused, true);
      const paused = await kart(page);
      await page.waitForTimeout(150);
      assert.equal((await kart(page)).x, paused.x, 'Paused kart physics must stop advancing');
      await hold(page, 'p', 100);
      assert.equal((await kart(page)).paused, false);
      await page.keyboard.down('Space');
      await waitForAsync(page, async () => {
        const cart = (await portScenarios.snapshot()).kart;
        return cart.jumpPressed && cart.velocityY < 0;
      });
      assert.ok((await kart(page)).x > paused.x, 'The unpaused original kart must move forward');
      await page.keyboard.up('Space');
      await waitForAsync(page, async () => !(await portScenarios.snapshot()).kart.jumpPressed);
      await hold(page, 'Escape', 100);
      await waitForAsync(page, async () => !(await portScenarios.snapshot()).kart);
      await page.waitForFunction(() => !portStatus.game.menu.type && portStatus.game.player.canMove);
      assert.equal((await snapshot(page)).menu.type, null);
    }
  }, undefined, '/');
});

const kart = page => page.evaluate(async () => (await portScenarios.snapshot()).kart);

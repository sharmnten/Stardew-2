import test from 'node:test';
import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { withGame } from './driver.mjs';
import { clickControl, hold, pointAtWorld, walkToBed, waitForAsync } from './game-controls.mjs';

test('normal rod cast, hook, fishing minigame and saved catch work in browser', { timeout: 360000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => portStatus?.phase === 'ready', null, { timeout: 120000 });
    await page.evaluate(() => portScenarios.load('fishing-cast'));
    const aim = await page.evaluate(() => portScenarios.run('fishing-cast-shore'));
    await hold(page, '1', 0);
    await pointAtWorld(page, aim.x, aim.y);
    await page.keyboard.down('c');
    await waitForAsync(page, async () => (await portScenarios.snapshot()).fishingCast.rod.power >= 0.9);
    await page.keyboard.up('c');
    await waitForAsync(page, async () => (await portScenarios.snapshot()).fishingCast.rod.fishing);
    const cast = (await fishing(page)).cast;
    assert.equal(cast.timesFished, 1);
    assert.equal(cast.water, true);
    assert.equal(cast.stamina, 262.5);
    await page.evaluate(() => { window.castInputTrace = []; });
    await waitForAsync(page, async () => {
      const rod = (await portScenarios.snapshot()).fishingCast.rod;
      if (!rod.nibbling) return false;
      castInputTrace.push({ stage: 'bite', time: performance.now(), ...rod });
      return true;
    }, undefined, 45000);
    await page.keyboard.down('c');
    await waitForAsync(page, async () => {
      const rod = (await portScenarios.snapshot()).fishingCast.rod;
      if (!rod.keyDownSeen) return false;
      castInputTrace.push({ stage: 'processedDown', time: performance.now(), ...rod });
      return true;
    });
    await page.keyboard.up('c');
    try {
      await waitForAsync(page, async () => {
        const state = (await portScenarios.snapshot()).fishingCast;
        return state.rod.hit || !!state.bar;
      });
    } catch (error) {
      console.error(JSON.stringify(await page.evaluate(() => castInputTrace)));
      throw error;
    }
    await waitForAsync(page, async () => !!(await portScenarios.snapshot()).fishingCast.bar);
    const deadline = Date.now() + 60000;
    let pressed = false;
    while (true) {
      const { bar } = await fishing(page);
      if (!bar) break;
      assert.ok(Date.now() < deadline, 'Finish the original fishing bar within one minute');
      const target = bar.fish + 32 - bar.height / 2;
      const desiredSpeed = Math.max(-3, Math.min(3, (target - bar.position) * 0.12));
      const next = !bar.fadeOut && bar.speed > desiredSpeed;
      if (next !== pressed) {
        await page.keyboard[next ? 'down' : 'up']('c');
        pressed = next;
      }
      await page.waitForTimeout(16);
    }
    await page.keyboard.up('c');
    await waitForAsync(page, async () => (await portScenarios.snapshot()).fishingCast.rod.caught);
    await hold(page, 'c', 0);
    await waitForAsync(page, async () => (await portScenarios.snapshot()).fishingCast.state.inventory.length === 1);
    await page.waitForFunction(() => portStatus.game.player.canMove && !portStatus.game.player.usingTool);
    const caught = (await fishing(page)).state;
    assert.equal(caught.bait, 1);
    assert.ok(['(O)137', '(O)145'].includes(caught.inventory[0].id), 'Morning Spring Town tutorial catch is Smallmouth Bass or Sunfish');
    assert.equal(caught.collection[0].id, caught.inventory[0].id);
    assert.equal(caught.inventory[0].stack, 1);
    assert.equal(caught.collection[0].count, 1);
    assert.ok(caught.collection[0].size > 0);
    assert.ok(caught.experience > 2150);
    const hookInput = await page.evaluate(() => window.castInputTrace);
    await page.evaluate(() => portScenarios.run('fishing-cast-home'));
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
    const reloaded = (await fishing(page)).state;
    assert.deepEqual({ ...reloaded, day: 1 }, caught);
    await writeFile(resolve('.port-cache/task-7-fishing-cast-browser-observations.json'),
      JSON.stringify({ afterCast: cast, afterCatch: caught, afterReload: reloaded,
        hookInput }, null, 2) + '\n');
  }, undefined, '/');
});

const fishing = page => page.evaluate(async () => (await portScenarios.snapshot()).fishingCast);

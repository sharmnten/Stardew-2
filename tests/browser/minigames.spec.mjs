import test from 'node:test';
import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { withGame } from './driver.mjs';
import { snapshot, hold, clickControl, pointAtWorld, walkToBed, waitForAsync } from './game-controls.mjs';

test('original arcade cabinets, both Kart modes and saved Prairie King progress work in browser', { timeout: 420000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
    const id = 'original-minigames';
    await page.evaluate(id => portScenarios.load(id), id);
    const expected = await page.evaluate(async id => {
      const response = await fetch(`Fixtures/reference/${id}.json`);
      if (!response.ok) throw new Error('Generate the original desktop arcade fixture before publishing');
      return (await response.json()).scenario;
    }, id);
    assert.deepEqual(await page.evaluate(id => portScenarios.run(id), id), expected.observations);
    await clickControl(page, 'Exit');
    await page.waitForFunction(() => !portStatus.game.menu.type && portStatus.game.player.canMove);
    await openCabinet(page, 'minigames-king');
    await clickControl(page, 'Continue');
    await page.waitForFunction(() => portStatus.game.minigame?.type === 'AbigailGame');
    const before = (await snapshot(page)).minigame;
    await hold(page, 'd', 100);
    assert.ok((await snapshot(page)).minigame.x > before.x, 'Normal browser input must move the resumed arcade player');
    await page.keyboard.down('ArrowUp');
    await page.waitForFunction(() => portStatus.game.minigame.bullets > 0);
    await page.keyboard.up('ArrowUp');
    await waitForAsync(page, async () => {
      const saved = (await portScenarios.snapshot()).savedKing.progress;
      return saved.lives === 2 && saved.died;
    }, undefined, 90000);
    const earned = await savedKing(page);
    assert.equal(earned.progress.wave, 0);
    assert.equal(earned.progress.lives, 2);
    assert.equal(earned.progress.died, true);
    assert.ok(earned.progress.waveTimer > 0 && earned.progress.waveTimer <= 80000);
    await hold(page, 'Escape', 100);
    await page.waitForFunction(() => !portStatus.game.minigame);
    await page.waitForFunction(() => !portStatus.game.menu.type && portStatus.game.player.canMove);
    for (const [choice, mode] of [['Endless', 2], ['Progress', 3]]) {
      await openCabinet(page, 'minigames-kart');
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
    const saved = await savedKing(page);
    assert.deepEqual(saved, earned);
    await page.evaluate(() => portScenarios.run('minigames-home'));
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
    const reloaded = await savedKing(page);
    assert.deepEqual(reloaded.progress, earned.progress);
    assert.equal(reloaded.day, 2);
    await openCabinet(page, 'minigames-king');
    await clickControl(page, 'Continue');
    await page.waitForFunction(() => portStatus.game.minigame?.type === 'AbigailGame');
    assert.equal((await snapshot(page)).minigame.lives, 2);
    const resumed = (await snapshot(page)).minigame;
    await hold(page, 'd', 100);
    assert.ok((await snapshot(page)).minigame.x > resumed.x, 'The cold-loaded original progress must resume a playable arcade game');
    await hold(page, 'Escape', 100);
    await page.waitForFunction(() => !portStatus.game.minigame && !portStatus.game.menu.type && portStatus.game.player.canMove);
    await writeFile(resolve('.port-cache/task-7-arcade-earned-browser-observations.json'),
      JSON.stringify({ earnedCheckpoint: earned, beforeNight: saved, afterReload: reloaded }, null, 2) + '\n');
  }, undefined, '/');
});

const kart = page => page.evaluate(async () => (await portScenarios.snapshot()).kart);
const savedKing = page => page.evaluate(async () => (await portScenarios.snapshot()).savedKing);

async function openCabinet(page, action) {
  const cabinet = await page.evaluate(action => portScenarios.run(action), action);
  assert.equal((await snapshot(page)).location.name, 'Saloon', 'Visit the actual original arcade cabinet');
  assert.equal((await snapshot(page)).menu.type, null, 'Cabinet setup must leave opening the menu to ordinary world input');
  await pointAtWorld(page, cabinet.x * 64 + 32, cabinet.y * 64 + 32);
  await page.mouse.down({ button: 'right' });
  await page.waitForFunction(() => portStatus.game.menu.type === 'DialogueBox');
  await page.mouse.up({ button: 'right' });
}

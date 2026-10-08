import test from 'node:test';
import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { snapshot, pointAtWorld, hold, clickControl, walkToBed, waitForAsync } from './game-controls.mjs';
import { withGame } from './driver.mjs';

test('original construction, livestock, pet and stable methods match desktop in browser', { timeout: 300000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
    const id = 'animals-buildings';
    await page.evaluate(id => portScenarios.load(id), id);
    const expected = await page.evaluate(async id => {
      const response = await fetch(`Fixtures/reference/${id}.json`);
      if (!response.ok) throw new Error('Generate the original desktop livestock fixture before publishing');
      return (await response.json()).scenario;
    }, id);
    assert.deepEqual(await page.evaluate(id => portScenarios.run(id), id), expected.observations);
    await page.evaluate(() => portScenarios.run('animals-horse'));
    assert.equal((await snapshot(page)).location.name, 'Farm');
    assert.equal((await snapshot(page)).menu.type, null);
    await interactHorse(page);
    await page.waitForFunction(() => portStatus.game.menu.type === 'NamingMenu');
    const initial = (await snapshot(page)).menu.text.textBox;
    for (let i = 0; i < initial.length; i++) await hold(page, 'Backspace', 0);
    await page.keyboard.type('PortHorse', { delay: 30 });
    await page.waitForFunction(() => portStatus.game.menu.text.textBox === 'PortHorse');
    await clickControl(page, 'doneNamingButton');
    await page.waitForFunction(() => !portStatus.game.menu.type && portStatus.game.player.canMove);
    await interactHorse(page);
    await waitForAsync(page, async () => {
      const h = (await portScenarios.snapshot()).livestock.horse;
      return h.mounted && !h.mounting && portStatus.game.player.canMove;
    });
    const mounted = (await snapshot(page)).player;
    await hold(page, 's', 250);
    assert.ok((await snapshot(page)).player.positionY > mounted.positionY, 'Ordinary browser input must move the original mounted farmer');
    await hold(page, 'x', 0);
    await waitForAsync(page, async () => {
      const h = (await portScenarios.snapshot()).livestock.horse;
      return !h.mounted && !h.dismounting && portStatus.game.player.canMove;
    });
    const afterRiding = await livestock(page);
    assert.equal(afterRiding.horse.name, 'PortHorse');
    assert.equal(afterRiding.horse.farmerName, 'PortHorse');
    assert.equal(afterRiding.horse.ownerMatches, true);
    assert.equal(afterRiding.horse.stableMatches, true);
    await page.evaluate(() => portScenarios.run('animals-home'));
    await walkToBed(page);
    await clickControl(page, 'Yes');
    await page.waitForFunction(() => portStatus.game.menu.type === 'SaveGameMenu', null, { timeout: 90000 });
    await page.waitForFunction(() => portStatus.game.day === 2 && !portStatus.game.overnight
      && !portStatus.game.loading && portStatus.game.player.canMove && !portStatus.game.menu.type,
    null, { timeout: 90000 });
    const afterNight = await livestock(page);
    assert.deepEqual(afterNight, expected.afterNight);
    await waitForAsync(page, async () => {
      const slot = portStatus.game.save.slot, stored = await portStorage.read(slot);
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
    const afterReload = await livestock(page);
    assert.deepEqual(afterReload, afterNight);
    await page.evaluate(() => portScenarios.run('animals-horse'));
    await interactHorse(page);
    await waitForAsync(page, async () => (await portScenarios.snapshot()).livestock.horse.mounted);
    assert.equal((await snapshot(page)).menu.type, null, 'Cold-loaded named horse must mount without asking for a new name');
    await hold(page, 'x', 0);
    await waitForAsync(page, async () => !(await portScenarios.snapshot()).livestock.horse.mounted);
    await writeFile(resolve('.port-cache/task-7-horse-browser-observations.json'),
      JSON.stringify({ afterRiding, afterNight, afterReload }, null, 2) + '\n');
  }, undefined, '/');
});

const livestock = page => page.evaluate(async () => (await portScenarios.snapshot()).livestock);
async function interactHorse(page) {
  const h = await page.evaluate(async () => (await portScenarios.snapshot()).horseInteraction);
  await pointAtWorld(page, h.x, h.y);
  await page.mouse.down({ button: 'right' });
  await waitForAsync(page, async () => portStatus.game.menu.type === 'NamingMenu'
    || (await portScenarios.snapshot()).livestock.horse.mounting || (await portScenarios.snapshot()).livestock.horse.mounted);
  await page.mouse.up({ button: 'right' });
}

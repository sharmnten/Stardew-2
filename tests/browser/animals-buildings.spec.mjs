import test from 'node:test';
import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { snapshot, pointAtWorld, hold, clickControl, walkToBed, waitForAsync } from './game-controls.mjs';
import { withGame } from './driver.mjs';

test('original livestock and named horse equipment, feeding and riding survive browser sleep and cold load', { timeout: 300000 }, async () => {
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
    console.info("Original livestock/building methods match desktop.");
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
    await giveHorseItem(page, '3', 'hat', '(H)0');
    assert.equal((await snapshot(page)).menu.type, null, 'Equipping a hat must not open a menu or mount the horse');
    await giveHorseItem(page, '4', 'ateCarrotToday', true);
    assert.equal((await livestock(page)).carrots, 1, 'Original feeding must consume exactly one carrot');
    await waitForAsync(page, async () => !(await portScenarios.snapshot()).horseMunching);
    console.info("Original horse named, equipped and fed through browser controls.");
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
    assert.equal(afterRiding.horse.hat, '(H)0');
    assert.equal(afterRiding.horse.ateCarrotToday, true);
    assert.equal(afterRiding.carrots, 1);
    assert.equal(afterRiding.horse.name, 'PortHorse');
    assert.equal(afterRiding.horse.farmerName, 'PortHorse');
    assert.equal(afterRiding.horse.ownerMatches, true);
    assert.equal(afterRiding.horse.stableMatches, true);
    console.info("Original mounted movement and dismount completed.");
    await page.evaluate(() => portScenarios.run('animals-home'));
    await walkToBed(page);
    await clickControl(page, 'Yes');
    await page.waitForFunction(() => portStatus.game.menu.type === 'SaveGameMenu', null, { timeout: 90000 });
    await page.waitForFunction(() => portStatus.game.day === 2 && !portStatus.game.overnight
      && !portStatus.game.loading && portStatus.game.player.canMove && !portStatus.game.menu.type,
    null, { timeout: 90000 });
    const afterNight = await livestock(page);
    assert.deepEqual(afterNight, expected.afterNight);
    assert.equal(afterNight.horse.hat, '(H)0');
    assert.equal(afterNight.horse.ateCarrotToday, false);
    assert.equal(afterNight.carrots, 1);
    await waitForAsync(page, async () => {
      const slot = portStatus.game.save.slot, stored = await portStorage.read(slot);
      if (!stored) return false;
      const xml = new DOMParser().parseFromString(new TextDecoder().decode(stored.files[slot]), 'application/xml');
      return xml.querySelector('SaveGame > dayOfMonth')?.textContent === '2';
    });
    console.info('Original horse, livestock and buildings saved after a full night.');
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
    console.info("Cold normal Load matches the original post-night state.");
    await page.evaluate(() => portScenarios.run('animals-horse'));
    await hold(page, '1', 0);
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
async function clickHorse(page) {
  const target = await page.evaluate(async () => (await portScenarios.snapshot()).horseInteraction);
  await pointAtWorld(page, target.x, target.y);
  // Finish one processed click before observing its result; held right clicks repeat in the original game.
  await page.mouse.down({ button: 'right' });
  await page.waitForFunction(() => portStatus.game.input.rightPressed);
  await page.mouse.up({ button: 'right' });
  await page.waitForFunction(() => !portStatus.game.input.rightPressed);
}

async function interactHorse(page) {
  await clickHorse(page);
  await waitForAsync(page, async () => portStatus.game.menu.type === 'NamingMenu'
    || (await portScenarios.snapshot()).livestock.horse.mounting || (await portScenarios.snapshot()).livestock.horse.mounted);
}

async function giveHorseItem(page, slot, field, value) {
  await hold(page, slot, 0);
  const selected = await page.evaluate(async () => (await portScenarios.snapshot()).horseInput);
  assert.equal(selected.slot, Number(slot) - 1, 'The original game must select the inventory slot before interacting');
  assert.equal(selected.item, field === 'hat' ? '(H)0' : '(O)Carrot');
  await clickHorse(page);
  await waitForAsync(page, async ({ field, value }) => (await portScenarios.snapshot()).livestock.horse[field] === value,
    { field, value });
}

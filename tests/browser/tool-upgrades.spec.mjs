import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';
import { snapshot, clickControl, hold, walkToBed, pointAtWorld, waitForAsync } from './game-controls.mjs';

test('original tool upgrade purchase, countdown and collection match desktop', { timeout: 420000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
    const id = 'tool-upgrades';
    await page.evaluate(id => portScenarios.load(id), id);
    const expected = await page.evaluate(async id => {
      const response = await fetch(`Fixtures/reference/${id}.json`);
      if (!response.ok) throw new Error('Generate the original desktop tool upgrade fixture before publishing');
      return (await response.json()).scenario.observations;
    }, id);
    assert.deepEqual(await page.evaluate(id => portScenarios.run(id), id), expected);
    const reference = await page.evaluate(async id => (await (await fetch(`Fixtures/reference/${id}.json`)).json()).scenario, id);
    await page.evaluate(id => portScenarios.load(id), id);
    await page.evaluate(() => portScenarios.run('tool-upgrades-shop'));
    console.info('Original tool shop opened for normal browser purchase.');
    await clickControl(page, '0');
    await page.waitForFunction(() => portStatus.game.menu.type === 'DialogueBox');
    await page.waitForFunction(() => portStatus.game.menu.allowsInteraction);
    await hold(page, 'x', 100);
    await page.waitForFunction(() => !portStatus.game.menu.type && portStatus.game.player.canMove);
    console.info('Original tool purchase dialogue closed.');
    await sleepTo(page, 6);
    console.info('First tool upgrade night saved.');
    assert.deepEqual(await upgradeState(page), reference.afterFirstNight);
    await coldLoad(page, 6);
    console.info('Pending upgrade retained after cold Load.');
    assert.deepEqual(await upgradeState(page), reference.afterFirstReload);
    await sleepTo(page, 7);
    console.info('Second tool upgrade night completed.');
    assert.deepEqual(await upgradeState(page), reference.afterSecondNight);
    const counter = await page.evaluate(() => portScenarios.run('tool-upgrades-visit'));
    await pointAtWorld(page, counter.x * 64 + 32, counter.y * 64 + 32);
    await page.mouse.down({ button: 'right' });
    await waitForAsync(page, async () => (await portScenarios.snapshot()).toolUpgrade.pendingId === null);
    await page.mouse.up({ button: 'right' });
    await page.waitForFunction(() => portStatus.game.menu.type === 'DialogueBox' && portStatus.game.menu.allowsInteraction);
    await hold(page, 'x', 0);
    await page.waitForFunction(() => !portStatus.game.menu.type
      || portStatus.game.menu.controls.some(control => control.name === 'Leave'));
    if ((await snapshot(page)).menu.controls.some(control => control.name === 'Leave')) await clickControl(page, 'Leave');
    await page.waitForFunction(() => !portStatus.game.menu.type && portStatus.game.player.canMove);
    console.info('Original counter returned the axe through browser interaction.');
    assert.deepEqual((await upgradeState(page)).axes, [{ id: '(T)CopperAxe', level: 1 }]);
    await page.evaluate(() => portScenarios.run('tool-upgrades-home'));
    await sleepTo(page, 8);
    await coldLoad(page, 8);
    console.info('Collected axe retained after third night and cold Load.');
    assert.deepEqual(await upgradeState(page), reference.afterCollectedReload);
  }, undefined, '/');
});

const upgradeState = page => page.evaluate(async () => (await portScenarios.snapshot()).toolUpgrade);

async function sleepTo(page, day) {
  await walkToBed(page);
  await clickControl(page, 'Yes');
  await page.waitForFunction(() => portStatus.game.menu.type === 'SaveGameMenu', null, { timeout: 90000 });
  await page.waitForFunction(day => portStatus.game.day === day && !portStatus.game.overnight
    && portStatus.game.mode === 3 && !portStatus.game.loading && portStatus.game.player.canMove
    && !portStatus.game.menu.type && portStatus.game.save?.mainBytes > 0,
    day, { timeout: 90000 });
  await waitForAsync(page, async () => (await portScenarios.snapshot()).morningQueueCount === 0);
  await waitForAsync(page, async day => {
    const slot = portStatus.game.save.slot;
    const stored = await portStorage.read(slot);
    if (!stored) return false;
    const xml = new DOMParser().parseFromString(new TextDecoder().decode(stored.files[slot]), 'application/xml');
    return xml.querySelector('SaveGame > dayOfMonth')?.textContent === String(day);
  }, day, 30000);
}

async function coldLoad(page, day) {
  await page.reload();
  await page.waitForFunction(() => portStatus?.phase === 'ready', null, { timeout: 120000 });
  await clickControl(page, 'Load');
  await page.waitForFunction(() => portStatus.game.menu.type === 'LoadGameMenu' && portStatus.game.menu.saves?.length === 1);
  await clickControl(page, '0');
  await page.waitForFunction(day => portStatus.game.mode === 3 && !portStatus.game.loading
    && portStatus.game.day === day && portStatus.game.player.canMove && !portStatus.game.menu.type
    && !portStatus.game.warping, day, { timeout: 90000 });
}

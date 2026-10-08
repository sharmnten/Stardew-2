import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';
import { pointAtWorld, snapshot, clickControl, hold, walkToBed, waitForAsync } from './game-controls.mjs';

test('original desktop ready upgrade is collected through the browser counter', { timeout: 180000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => portStatus?.phase === 'ready', null, { timeout: 120000 });
    await page.evaluate(() => portScenarios.load('tool-upgrades-ready'));
    const read = () => page.evaluate(() => portScenarios.snapshot());
    const initial = await read();
    const expected = await page.evaluate(async () => (await (await fetch('Fixtures/reference/tool-upgrades-ready.json')).json()).scenario.observations);
    assert.deepEqual(initial.toolUpgrade, expected);
    assert.equal(initial.toolDiagnostics.pendingType, 'StardewValley.Tools.Axe');
    const counter = await page.evaluate(() => portScenarios.run('tool-upgrades-visit'));
    await pointAtWorld(page, counter.x * 64 + 32, counter.y * 64 + 32);
    await page.mouse.down({ button: 'right' });
    await waitForAsync(page, async () => (await portScenarios.snapshot()).toolUpgrade.pendingId === null);
    await page.mouse.up({ button: 'right' });
    await page.waitForFunction(() => portStatus.game.menu.type === 'DialogueBox' && portStatus.game.menu.allowsInteraction);
    assert.deepEqual((await read()).toolUpgrade.axes, [{ id: '(T)CopperAxe', level: 1 }]);
    await hold(page, 'x', 0);
    await page.waitForFunction(() => !portStatus.game.menu.type
      || portStatus.game.menu.controls.some(control => control.name === 'Leave'));
    if ((await snapshot(page)).menu.controls.some(control => control.name === 'Leave')) await clickControl(page, 'Leave');
    await page.waitForFunction(() => !portStatus.game.menu.type && portStatus.game.player.canMove);
    await page.evaluate(() => portScenarios.run('tool-upgrades-home'));
    await walkToBed(page);
    // Original DialogueBox builds response controls only after its opening
    // transition and question text finish. Reaching the bed precedes that.
    await page.waitForFunction(() => portStatus.game.menu.type === 'DialogueBox'
      && portStatus.game.menu.allowsInteraction
      && portStatus.game.menu.controls.some(control => control.name === 'Yes'));
    assert.ok((await snapshot(page)).menu.controls.some(control => control.name === 'Yes'), 'Returned player must reach the original bed sleep prompt');
  }, undefined, '/');
});

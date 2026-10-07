import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';
import { snapshot, walkTo, pointAtWorld, hold, clickControl } from './game-controls.mjs';

test('original sign accepts fresh clipboard text and keeps it after sleep and cold load', { timeout: 240000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
    await page.evaluate(() => portScenarios.load('text-sign-clipboard'));
    await page.context().grantPermissions(['clipboard-read', 'clipboard-write']);
    await page.evaluate(() => navigator.clipboard.writeText('Fresh farm sign'));
    console.info('Original text-sign fixture loaded.');
    const startY = (await snapshot(page)).player.positionY;
    for (let attempt = 0; attempt < 4; attempt++) {
      await walkTo(page, 7 * 64, startY, state => state.menu.type === 'DialogueBox');
      if ((await snapshot(page)).menu.type !== 'DialogueBox') break;
      await clickControl(page, 'No');
      await page.waitForFunction(() => !portStatus.game.menu.type && portStatus.game.player.canMove);
    }
    await walkTo(page, 7 * 64, 8 * 64);
    await walkTo(page, 4 * 64, 8 * 64);
    await pointAtWorld(page, 4 * 64 + 32, 7 * 64 + 32);
    await page.mouse.down({ button: 'right' });
    await page.waitForFunction(() => portStatus.game.menu.type === 'TitleTextInputMenu');
    await page.mouse.up({ button: 'right' });
    assert.equal((await snapshot(page)).menu.text.textBox, 'Old');
    for (let i = 0; i < 3; i++) await hold(page, 'Backspace', 80);
    await page.keyboard.down('Control');
    await hold(page, 'v', 80);
    await page.keyboard.up('Control');
    await page.waitForFunction(() => portStatus.game.menu.text.textBox === 'Fresh farm sign');
    await clickControl(page, 'doneNamingButton');
    await page.waitForFunction(() => !portStatus.game.menu.type);
    const expected = await page.evaluate(async () => (await (await fetch('Fixtures/reference/text-sign-clipboard.json')).json()).scenario.afterActionReload);
    assert.deepEqual(expected, { x: 4, y: 7, text: 'Fresh farm sign' });
    assert.deepEqual((await snapshot(page)).location.signs, [expected]);
    console.info('Fresh clipboard text committed through the original sign editor.');
    const bed = (await snapshot(page)).location.bed;
    await walkTo(page, bed.x - 128, 8 * 64);
    await walkTo(page, bed.x - 128, bed.y);
    await walkTo(page, bed.x, bed.y, state => state.menu.type === 'DialogueBox');
    await clickControl(page, 'Yes');
    await page.waitForFunction(() => portStatus.game.day === 2 && !portStatus.game.overnight
      && portStatus.game.player.canMove && !portStatus.game.menu.type && portStatus.game.save?.mainBytes > 0,
      null, { timeout: 90000 });
    assert.deepEqual((await snapshot(page)).location.signs, [expected]);
    console.info('Original sign text retained after normal sleep and save.');
    await page.reload();
    await page.waitForFunction(() => portStatus?.phase === 'ready', null, { timeout: 120000 });
    await clickControl(page, 'Load');
    await page.waitForFunction(() => portStatus.game.menu.type === 'LoadGameMenu' && portStatus.game.menu.saves?.length === 1);
    await clickControl(page, '0');
    await page.waitForFunction(() => portStatus.game.mode === 3 && !portStatus.game.loading
      && portStatus.game.day === 2 && portStatus.game.player.canMove && !portStatus.game.menu.type,
      null, { timeout: 90000 });
    assert.deepEqual((await snapshot(page)).location.signs, [expected]);
    console.info('Original sign text retained after cold normal Load.');
  }, undefined, '/');
});

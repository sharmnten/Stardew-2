import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';
import { snapshot, clickControl, hold, pointAtWorld, walkTo, walkToBed, waitForAsync } from './game-controls.mjs';

test('browser movie invitation, complete screening and saved viewing match desktop', { timeout: 600000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => portStatus?.phase === 'ready', null, { timeout: 120000 });
    const id = 'movie-screening';
    await page.evaluate(id => portScenarios.load(id), id);
    const expected = await page.evaluate(async id => (await (await fetch(`Fixtures/reference/${id}.json`)).json()).scenario, id);
    assert.deepEqual(await movie(page), expected.beforeMovie);
    const linus = await page.evaluate(() => portScenarios.run('movie-linus'));
    await hold(page, '1', 0);
    await rightClick(page, linus);
    await waitForAsync(page, async () => (await portScenarios.snapshot()).movie.invited.includes('Linus'));
    await page.waitForFunction(() => portStatus.game.menu.type === 'DialogueBox' && portStatus.game.menu.allowsInteraction);
    // Linus can have more than one original dialogue page queued.
    const invitationDeadline = Date.now() + 60000;
    while ((await snapshot(page)).menu.type === 'DialogueBox') {
      assert.ok(Date.now() < invitationDeadline, 'Finish the original invitation dialogue');
      await page.waitForFunction(() => portStatus.game.menu.allowsInteraction);
      await hold(page, 'x', 0);
    }
    await page.waitForFunction(() => !portStatus.game.menu.type && portStatus.game.player.canMove);
    assert.deepEqual(await movie(page), expected.afterInvitation);
    const entrance = await page.evaluate(() => portScenarios.run('movie-entrance'));
    await rightClick(page, entrance);
    await clickControl(page, 'Yes');
    await page.waitForFunction(() => portStatus.game.location.name === 'MovieTheater' && !portStatus.game.warping
      && portStatus.game.player.canMove && !portStatus.game.menu.type, null, { timeout: 60000 });
    const doors = await page.evaluate(() => portScenarios.run('movie-doors'));
    for (const point of doors.route) await walkTo(page, point.pixelX, point.pixelY, () => false, 8);
    await rightClick(page, doors);
    await page.waitForFunction(() => portStatus.game.activeEvent?.id === 'MovieTheaterScreening', null, { timeout: 60000 });
    const deadline = Date.now() + 240000;
    let dialogues = 0;
    while (true) {
      const state = await snapshot(page);
      if (!state.activeEvent && state.location.name === 'MovieTheater' && !state.warping
        && state.player.canMove && !state.menu.type) break;
      assert.ok(Date.now() < deadline, 'Finish the original movie scenes within four minutes');
      if (state.menu.type === 'DialogueBox' && state.menu.allowsInteraction) {
        await hold(page, 'x', 0);
        dialogues++;
      } else await page.waitForTimeout(100);
    }
    assert.ok(dialogues > 0, 'A complete original screening must include its scene and reaction text');
    assert.deepEqual(await movie(page), expected.afterMovie);
    console.info('Original screening finished, returned to lobby and applied the guest reaction.');
    await page.evaluate(() => portScenarios.run('movie-home'));
    await walkToBed(page);
    await clickControl(page, 'Yes');
    await page.waitForFunction(() => portStatus.game.menu.type === 'SaveGameMenu', null, { timeout: 90000 });
    await page.waitForFunction(() => portStatus.game.day === 6 && !portStatus.game.overnight
      && portStatus.game.player.canMove && !portStatus.game.menu.type, null, { timeout: 90000 });
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
    assert.deepEqual(await movie(page), expected.afterMovieReload);
  }, undefined, '/');
});

const movie = page => page.evaluate(async () => (await portScenarios.snapshot()).movie);

async function rightClick(page, target) {
  await pointAtWorld(page, target.x, target.y);
  await page.mouse.down({ button: 'right' });
  await page.waitForFunction(() => portStatus.game.input.rightPressed);
  await page.mouse.up({ button: 'right' });
  await page.waitForFunction(() => !portStatus.game.input.rightPressed);
}

import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';
import { snapshot, clickControl, hold, pointAtWorld, walkTo, waitForAsync } from './game-controls.mjs';

// Lost original menu actions, order callbacks, perfection calculation, or
// serialized fields must fail this comparison with the unchanged desktop game.
test('browser renovation, waiver purchase and overnight Qi/perfection match desktop', { timeout: 900000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => portStatus?.phase === 'ready', null, { timeout: 120000 });
    const id = 'late-game-completion';
    await page.evaluate(id => portScenarios.load(id), id);
    const expected = await page.evaluate(async id => {
      const response = await fetch(`Fixtures/reference/${id}.json`);
      if (!response.ok) throw new Error('Generate the original late-game reference first');
      return (await response.json()).scenario;
    }, id);
    assert.deepEqual(await completion(page), expected.before);
    await page.locator('#theCanvas').hover();
    await page.waitForFunction(() => portStatus.game.input.mouseX > 500 && portStatus.game.input.mouseX < 800
      && portStatus.game.input.mouseY > 250 && portStatus.game.input.mouseY < 450);
    const room = await page.evaluate(() => portScenarios.run('completion-renovation'));
    await page.waitForFunction(() => portStatus.game.menu.type === 'RenovateMenu' && portStatus.game.menu.allowsInteraction);
    await pointAtWorld(page, room.x, room.y);
    // Let the original hover select the room before its placement click.
    await page.waitForFunction(room => {
      const game = portStatus.game;
      return Math.abs(game.input.mouseX - (room.x - game.viewport.x) * game.viewport.zoom) <= 2
        && Math.abs(game.input.mouseY - (room.y - game.viewport.y) * game.viewport.zoom) <= 2;
    }, room);
    await page.mouse.down();
    await waitForAsync(page, async () => (await portScenarios.snapshot()).completion.bedroomOpen);
    await page.mouse.up();
    await page.waitForFunction(() => !portStatus.game.menu.type && !portStatus.game.warping && portStatus.game.player.canMove,
      null, { timeout: 60000 });
    console.info('Original renovation placement and animated return completed.');
    const fizz = await page.evaluate(() => portScenarios.run('completion-fizz'));
    await pointAtWorld(page, fizz.x, fizz.y);
    await page.mouse.down({ button: 'right' });
    await page.waitForFunction(() => portStatus.game.menu.type === 'DialogueBox');
    await page.mouse.up({ button: 'right' });
    await page.waitForFunction(() => portStatus.game.menu.allowsInteraction);
    await hold(page, 'x', 0);
    await clickControl(page, 'Yes');
    await waitForAsync(page, async () => (await portScenarios.snapshot()).completion.waivers === 100);
    await page.waitForFunction(() => !portStatus.game.menu.type && portStatus.game.player.canMove);
    assert.deepEqual(await completion(page), expected.afterActions);
    console.info('Original Fizz dialogue purchased the final waiver.');
    const route = await page.evaluate(() => portScenarios.run('completion-home'));
    assert.ok(route.length > 0);
    assert.ok(route.every(point => Number.isFinite(point.pixelX) && Number.isFinite(point.pixelY)),
      "Walking targets must account for the original farmer standing-box offset");
    // The original path controller advances past a satisfied starting tile.
    for (const point of (route.length > 1 ? route.slice(1) : route)) {
      await walkTo(page, point.pixelX, point.pixelY, state => state.menu.type === 'DialogueBox', 8);
      if ((await snapshot(page)).menu.type === 'DialogueBox') break;
    }
    await clickControl(page, 'Yes');
    await page.waitForFunction(() => portStatus.game.menu.type === 'ShippingMenu', null, { timeout: 90000 });
    await page.waitForTimeout(4000);
    await clickControl(page, 'okButton');
    await page.waitForFunction(() => portStatus.game.day === 2 && !portStatus.game.overnight
      && portStatus.game.mode === 3 && !portStatus.game.loading && portStatus.game.player.canMove
      && !portStatus.game.menu.type && portStatus.game.save?.mainBytes > 0, null, { timeout: 90000 });
    await waitForAsync(page, async () => {
      const current = (await portScenarios.snapshot()).completion;
      return current.perfect && current.eternal && current.qiGems === 100;
    });
    assert.deepEqual(await completion(page), expected.afterFirstNight);
    const nextRoute = await page.evaluate(() => portScenarios.run('completion-home'));
    for (const point of (nextRoute.length > 1 ? nextRoute.slice(1) : nextRoute)) {
      await walkTo(page, point.pixelX, point.pixelY, state => state.menu.type === 'DialogueBox', 8);
      if ((await snapshot(page)).menu.type === 'DialogueBox') break;
    }
    await clickControl(page, 'Yes');
    await page.waitForFunction(() => portStatus.game.menu.type === 'SaveGameMenu', null, { timeout: 90000 });
    await page.waitForFunction(() => portStatus.game.day === 3 && !portStatus.game.overnight
      && !portStatus.game.menu.type && portStatus.game.player.canMove, null, { timeout: 90000 });
    assert.deepEqual(await completion(page), expected.afterNight);
    await waitForAsync(page, async () => {
      const slot = portStatus.game.save.slot, stored = await portStorage.read(slot);
      if (!stored) return false;
      const xml = new DOMParser().parseFromString(new TextDecoder().decode(stored.files[slot]), 'application/xml');
      return xml.querySelector('SaveGame > dayOfMonth')?.textContent === '3';
    });
    await page.reload();
    await page.waitForFunction(() => portStatus?.phase === 'ready', null, { timeout: 120000 });
    await clickControl(page, 'Load');
    await page.waitForFunction(() => portStatus.game.menu.type === 'LoadGameMenu' && portStatus.game.menu.saves?.length === 1);
    await clickControl(page, '0');
    await page.waitForFunction(() => portStatus.game.mode === 3 && !portStatus.game.loading && portStatus.game.day === 3
      && portStatus.game.player.canMove && !portStatus.game.menu.type && !portStatus.game.warping,
    null, { timeout: 90000 });
    assert.deepEqual(await completion(page), expected.afterReload);
    console.info('Original Qi reward, perfection and two actual nights match after cold Load.');
    await page.evaluate(() => portScenarios.run('completion-summit'));
    await page.waitForFunction(() => !!portStatus.game.activeEvent, null, { timeout: 60000 });
    const deadline = Date.now() + 540000;
    let slideshowSprites = 0, nextReport = Date.now() + 60000;
    while (true) {
      const state = await snapshot(page);
      const ending = await page.evaluate(async () => (await portScenarios.snapshot()).ending);
      if (ending.slideshow) slideshowSprites = Math.max(slideshowSprites, ending.sprites);
      if (Date.now() >= nextReport) {
        console.info(`Original ending command ${state.activeEvent?.command ?? "finished"}; slideshow sprites ${slideshowSprites}.`);
        nextReport = Date.now() + 60000;
      }
      if (!state.activeEvent && !state.warping && state.player.canMove && !state.menu.type) break;
      assert.ok(Date.now() < deadline, 'Finish the original ending and slideshow within nine minutes');
      if (state.menu.type === 'DialogueBox' && state.menu.allowsInteraction) await hold(page, 'x', 0);
      else await page.waitForTimeout(100);
    }
    assert.ok(slideshowSprites > 100);
    assert.equal(slideshowSprites, expected.endingSlideshowSprites);
    assert.deepEqual(await completion(page), expected.afterEnding);
    console.info(`Original ending finished with ${slideshowSprites} sprites and returned to play.`);
    const finalRoute = await page.evaluate(() => portScenarios.run('completion-home'));
    for (const point of (finalRoute.length > 1 ? finalRoute.slice(1) : finalRoute)) {
      await walkTo(page, point.pixelX, point.pixelY, state => state.menu.type === 'DialogueBox', 8);
      if ((await snapshot(page)).menu.type === 'DialogueBox') break;
    }
    await clickControl(page, 'Yes');
    await page.waitForFunction(() => portStatus.game.menu.type === 'SaveGameMenu', null, { timeout: 90000 });
    await page.waitForFunction(() => portStatus.game.day === 4 && !portStatus.game.overnight
      && !portStatus.game.menu.type && portStatus.game.player.canMove, null, { timeout: 90000 });
    await waitForAsync(page, async () => {
      const slot = portStatus.game.save.slot, stored = await portStorage.read(slot);
      if (!stored) return false;
      const xml = new DOMParser().parseFromString(new TextDecoder().decode(stored.files[slot]), 'application/xml');
      return xml.querySelector('SaveGame > dayOfMonth')?.textContent === '4';
    });
    await page.reload();
    await page.waitForFunction(() => portStatus?.phase === 'ready', null, { timeout: 120000 });
    await clickControl(page, 'Load');
    await page.waitForFunction(() => portStatus.game.menu.type === 'LoadGameMenu' && portStatus.game.menu.saves?.length === 1);
    await clickControl(page, '0');
    await page.waitForFunction(() => portStatus.game.mode === 3 && !portStatus.game.loading && portStatus.game.day === 4
      && portStatus.game.player.canMove && !portStatus.game.menu.type && !portStatus.game.warping,
      null, { timeout: 90000 });
    assert.deepEqual(await completion(page), expected.afterEndingReload);
  }, undefined, '/');
});

const completion = page => page.evaluate(async () => (await portScenarios.snapshot()).completion);

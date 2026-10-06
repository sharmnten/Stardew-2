import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';
import { hold } from './game-controls.mjs';

test('all eight original desktop farms load with native structures and normal controls', { timeout: 600000 }, async t => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
    assert.equal(await page.evaluate(() => typeof window.portScenarios?.load), 'function', 'Development ScenarioBridge must load original reference saves');
    for (const layout of ['standard','riverland','forest','hilltop','wilderness','four-corners','beach','meadowlands']) {
      await t.test(layout, async () => {
        const id = `new-game-${layout}`;
        const expected = await page.evaluate(async id => {
          const response = await fetch(`Fixtures/reference/${id}.json`);
          if (!response.ok) throw new Error('Generate the supplied-desktop fixture before publishing the test host');
          return (await response.json()).scenario.afterLoad;
        }, id);
        await page.evaluate(id => window.portScenarios.load(id), id);
        const actual = await page.evaluate(() => window.portScenarios.snapshot());
        assert.equal(actual.farmId, expected.farmId);
        assert.equal(actual.map, expected.map);
        assert.equal(actual.day, expected.day);
        assert.deepEqual(actual.buildings, expected.buildings);
        assert.deepEqual(actual.animals, expected.animals);
        assert.equal(actual.locationCount, expected.locationCount);
        assert.deepEqual(actual.locations, expected.locations);
        assert.deepEqual(actual.farmer, expected.farmer);
        await page.waitForFunction(() => window.portStatus.game.player.canMove && !window.portStatus.game.warping);
        const x = await page.evaluate(() => window.portStatus.game.player.positionX);
        await hold(page, 'a', 100);
        assert.ok(await page.evaluate(() => window.portStatus.game.player.positionX) < x, 'Native input must work after the desktop save loads');
      });
    }
  }, undefined, '/');
});

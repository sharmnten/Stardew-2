import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';
import { snapshot, hold } from './game-controls.mjs';

test('original festival setup, birthday gift and movie calendar match desktop', { timeout: 240000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
    const id = 'festivals-events-movies';
    await page.evaluate(id => portScenarios.load(id), id);
    const expected = await page.evaluate(async id => {
      const response = await fetch(`Fixtures/reference/${id}.json`);
      if (!response.ok) throw new Error('Generate the original desktop calendar fixture before publishing');
      return (await response.json()).scenario.observations;
    }, id);
    assert.deepEqual(await page.evaluate(id => portScenarios.run(id), id), expected);
    const before = (await snapshot(page)).player;
    let moved = false;
    for (const direction of ['a', 'd', 'w', 's']) {
      await hold(page, direction, 100);
      const after = (await snapshot(page)).player;
      moved = after.positionX !== before.positionX || after.positionY !== before.positionY;
      if (moved) break;
    }
    assert.equal(moved, true, 'Original festival setup must return control to the player');
  }, undefined, '/');
});

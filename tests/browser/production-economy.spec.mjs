import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';

test('original browser inventory, shop, recipes and machine methods match desktop', { timeout: 240000 }, async t => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
    for (const id of ['inventory-economy', 'recipes-machines']) {
      await t.test(id, async () => {
        await page.evaluate(id => window.portScenarios.load(id), id);
        const expected = await page.evaluate(async id => {
          const response = await fetch(`Fixtures/reference/${id}.json`);
          if (!response.ok) throw new Error('Generate original desktop economy/production fixtures before publishing');
          return (await response.json()).scenario.observations;
        }, id);
        const actual = await page.evaluate(id => window.portScenarios.run(id), id);
        assert.deepEqual(actual, expected);
      });
    }
  }, undefined, '/');
});

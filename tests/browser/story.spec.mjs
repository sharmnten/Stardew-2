import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';

test('original bundle contribution and Joja purchase match desktop', { timeout: 240000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
    for (const id of ['community-center', 'joja-orders-museum']) {
      await page.evaluate(id => portScenarios.load(id), id);
      const expected = await page.evaluate(async id => {
        const response = await fetch(`Fixtures/reference/${id}.json`);
        if (!response.ok) throw new Error('Generate the original desktop story fixture before publishing');
        return (await response.json()).scenario.observations;
      }, id);
      assert.deepEqual(await page.evaluate(id => portScenarios.run(id), id), expected);
    }
  }, undefined, '/');
});

import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';

test('original browser crop methods match desktop growth, harvest and seasonal behavior', { timeout: 180000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
    assert.equal(await page.evaluate(() => typeof window.portScenarios?.run), 'function', 'Development scenario actions are required');
    const expected = await page.evaluate(async () => {
      const response = await fetch('Fixtures/reference/farming-season.json');
      if (!response.ok) throw new Error('Generate the original desktop farming fixture before publishing');
      return (await response.json()).scenario.observations;
    });
    await page.evaluate(() => window.portScenarios.load('farming-season'));
    const actual = await page.evaluate(() => window.portScenarios.run('farming-season'));
    assert.deepEqual(actual, expected);
    assert.equal(actual.harvestedParsnips, 1);
    assert.equal(actual.summer.outdoorDead, true);
    assert.equal(actual.summer.greenhouseDead, false);
  }, undefined, '/');
});

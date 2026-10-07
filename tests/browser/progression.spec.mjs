import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';
import { clickControl, waitForAsync } from './game-controls.mjs';

test('original skill gains, mastery reward and local achievement match desktop', { timeout: 180000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
    const id = 'skills-mastery-achievements';
    await page.evaluate(id => portScenarios.load(id), id);
    const expected = await page.evaluate(async id => {
      const response = await fetch(`Fixtures/reference/${id}.json`);
      if (!response.ok) throw new Error('Generate the original desktop progression fixture before publishing');
      return (await response.json()).scenario.observations;
    }, id);
    assert.deepEqual(await page.evaluate(id => portScenarios.run(id), id), expected);
    await page.evaluate(() => portScenarios.run('profession-farming-5'));
    await clickControl(page, 'leftProfession');
    await waitForAsync(page, async () => (await portScenarios.snapshot()).professions.includes(0));
    await page.evaluate(() => portScenarios.run('profession-farming-10'));
    await clickControl(page, 'leftProfession');
    await waitForAsync(page, async () => (await portScenarios.snapshot()).professions.includes(2));
    assert.deepEqual(await page.evaluate(async () => (await portScenarios.snapshot()).professions), [0, 2]);
  }, undefined, '/');
});

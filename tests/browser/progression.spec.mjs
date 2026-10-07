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
    for (const skill of [0, 1, 2, 3, 4]) {
      for (const level of [5, 10]) {
        await page.evaluate(({ skill, level }) => portScenarios.run(`profession-${skill}-${level}`), { skill, level });
        await clickControl(page, 'leftProfession');
        const profession = skill * 6 + (level === 5 ? 0 : 2);
        await waitForAsync(page, async profession => (await portScenarios.snapshot()).professions.includes(profession), profession);
      }
    }
    assert.deepEqual(await page.evaluate(async () => (await portScenarios.snapshot()).professions), [0, 2, 6, 8, 12, 14, 18, 20, 24, 26]);
  }, undefined, '/');
});

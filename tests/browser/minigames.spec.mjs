import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';
import { snapshot, hold, clickControl } from './game-controls.mjs';

test('original arcade input, firing and generated kart physics match desktop', { timeout: 240000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
    const id = 'original-minigames';
    await page.evaluate(id => portScenarios.load(id), id);
    const expected = await page.evaluate(async id => {
      const response = await fetch(`Fixtures/reference/${id}.json`);
      if (!response.ok) throw new Error('Generate the original desktop arcade fixture before publishing');
      return (await response.json()).scenario.observations;
    }, id);
    assert.deepEqual(await page.evaluate(id => portScenarios.run(id), id), expected);
    await clickControl(page, 'Continue');
    await page.waitForFunction(() => portStatus.game.minigame?.type === 'AbigailGame');
    const before = (await snapshot(page)).minigame;
    await hold(page, 'd', 100);
    assert.ok((await snapshot(page)).minigame.x > before.x, 'Normal browser input must move the resumed arcade player');
    await page.keyboard.down('ArrowUp');
    await page.waitForFunction(() => portStatus.game.minigame.bullets > 0);
    await page.keyboard.up('ArrowUp');
    await hold(page, 'Escape', 100);
    await page.waitForFunction(() => !portStatus.game.minigame);
  }, undefined, '/');
});

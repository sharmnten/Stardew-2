import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';
import { clickControl, walkToBed, waitForAsync } from './game-controls.mjs';

test('original skills, professions, mastery rewards and saved progression match desktop', { timeout: 360000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
    const id = 'skills-mastery-achievements';
    await page.evaluate(id => portScenarios.load(id), id);
    const expected = await page.evaluate(async id => {
      const response = await fetch(`Fixtures/reference/${id}.json`);
      if (!response.ok) throw new Error('Generate the original desktop progression fixture before publishing');
      return (await response.json()).scenario;
    }, id);
    assert.deepEqual(await page.evaluate(id => portScenarios.run(id), id), expected.observations);
    for (const skill of [0, 1, 2, 3, 4]) {
      for (const level of [5, 10]) {
        await page.evaluate(({ skill, level }) => portScenarios.run(`profession-${skill}-${level}`), { skill, level });
        await clickControl(page, 'leftProfession');
        const profession = skill * 6 + (level === 5 ? 0 : 2);
        await waitForAsync(page, async profession => (await portScenarios.snapshot()).professions.includes(profession), profession);
      }
    }
    assert.deepEqual(await page.evaluate(async () => (await portScenarios.snapshot()).professions), [0, 2, 6, 8, 12, 14, 18, 20, 24, 26]);
    await page.evaluate(() => portScenarios.run('mastery-prepare'));
    for (const skill of [0, 1, 2, 4]) {
      await page.evaluate(skill => portScenarios.run(`mastery-${skill}`), skill);
      await clickControl(page, 'mainButton');
      await waitForAsync(page, async skill => (await portScenarios.snapshot()).progression.mastery.claimed[skill] === 1, skill);
      await page.waitForFunction(() => !portStatus.game.menu.type);
    }
    assert.deepEqual(await progression(page), expected.afterAllMastery);
    await page.evaluate(() => portScenarios.run('progression-home'));
    await walkToBed(page);
    await clickControl(page, 'Yes');
    for (let notice = 0; notice < 40; notice++) {
      await page.waitForFunction(() => portStatus.game.menu.type === 'LevelUpMenu' && portStatus.game.menu.allowsInteraction,
        null, { timeout: 90000 });
      const pending = (await progression(page)).pendingLevels;
      await clickControl(page, 'okButton');
      await waitForAsync(page, async pending => (await portScenarios.snapshot()).progression.pendingLevels < pending, pending);
    }
    await page.waitForFunction(() => portStatus.game.menu.type === 'SaveGameMenu', null, { timeout: 90000 });
    await page.waitForFunction(() => portStatus.game.day === 2 && !portStatus.game.overnight
      && portStatus.game.mode === 3 && !portStatus.game.loading && portStatus.game.player.canMove
      && !portStatus.game.menu.type && portStatus.game.save?.mainBytes > 0, null, { timeout: 90000 });
    await waitForAsync(page, async () => {
      const slot = portStatus.game.save.slot;
      const stored = await portStorage.read(slot);
      if (!stored) return false;
      const xml = new DOMParser().parseFromString(new TextDecoder().decode(stored.files[slot]), 'application/xml');
      return xml.querySelector('SaveGame > dayOfMonth')?.textContent === '2';
    });
    await page.reload();
    await page.waitForFunction(() => portStatus?.phase === 'ready', null, { timeout: 120000 });
    await clickControl(page, 'Load');
    await page.waitForFunction(() => portStatus.game.menu.type === 'LoadGameMenu' && portStatus.game.menu.saves?.length === 1);
    await clickControl(page, '0');
    await page.waitForFunction(() => portStatus.game.mode === 3 && !portStatus.game.loading
      && portStatus.game.day === 2 && portStatus.game.player.canMove && !portStatus.game.menu.type
      && !portStatus.game.warping, null, { timeout: 90000 });
    assert.deepEqual(await progression(page), expected.afterProgressReload);
  }, undefined, '/');
});

const progression = page => page.evaluate(async () => (await portScenarios.snapshot()).progression);

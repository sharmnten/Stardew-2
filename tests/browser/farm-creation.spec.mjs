import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';
import { snapshot, clickControl, hold } from './game-controls.mjs';

test('all eight farms can be created with normal original browser controls', { timeout: 900000 }, async t => {
  await withGame(async page => {
    let first = true;
    for (const [layout, selector] of [
      ['standard','Standard'], ['riverland','Riverland'], ['forest','Forest'], ['hilltop','Hills'],
      ['wilderness','Wilderness'], ['four-corners','Four Corners'], ['beach','Beach'], ['meadowlands','ModFarm_MeadowlandsFarm']
    ]) {
      await t.test(layout, async () => {
        if (!first) await page.reload({ waitUntil: 'load' });
        first = false;
        await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
        const expected = await page.evaluate(async layout => {
          const response = await fetch(`Fixtures/reference/new-game-${layout}.json`);
          if (!response.ok) throw new Error('Generate original desktop farm fixtures before publishing');
          return (await response.json()).scenario;
        }, layout);
        await hold(page, 'Escape', 100);
        await clickControl(page, 'New');
        await page.waitForFunction(() => window.portStatus.game.menu.type === 'CharacterCustomization');
        for (const [name, value] of [['nameBoxCC','Reference'],['farmnameBoxCC','Parity'],['favThingBoxCC','Trees']]) {
          await clickControl(page, name);
          await page.keyboard.type(value, { delay: 30 });
        }
        for (let menuPage = 0; menuPage < 3; menuPage++) {
          if ((await snapshot(page)).menu.controls.some(control => control.name === selector)) break;
          await clickControl(page, 'farmTypeNextPageButton');
        }
        await clickControl(page, selector);
        await clickControl(page, 'skipIntroButton');
        await clickControl(page, 'okButton');
        await page.waitForFunction(() => window.portStatus.game.day === 1 && window.portStatus.game.player.customized
          && window.portStatus.game.location.name === 'FarmHouse' && !window.portStatus.game.overnight
          && window.portStatus.game.menu.type === null, null, { timeout: 90000 });
        const actual = await page.evaluate(() => window.portScenarios.snapshot());
        assert.equal(actual.farmId, expected.farmId);
        assert.equal(actual.map, expected.map);
        assert.deepEqual(actual.buildings, expected.buildings);
        assert.deepEqual(actual.animals.map(animal => animal.type), expected.animals.map(animal => animal.type));
        assert.equal(actual.farmer.name, 'Reference');
        assert.equal(actual.farmer.money, 500);
        const state = await snapshot(page);
        assert.equal(state.player.farmName, 'Parity');
        const saved = await page.evaluate(async () => (await portStorage.readAllForDotNet()).filter(record => record.slot !== '@settings'));
        assert.ok(saved.length >= 1, 'Original day-one save must commit before creation completes');
        const x = state.player.positionX;
        await hold(page, 'a', 100);
        assert.ok((await snapshot(page)).player.positionX < x, 'Ordinary input must work on every newly created layout');
      });
    }
  }, undefined, '/');
});

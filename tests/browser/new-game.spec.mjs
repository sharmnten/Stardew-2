import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { deflateSync } from 'node:zlib';
import { withGame } from './driver.mjs';

import { snapshot, clickControl, hold, walkTo } from './game-controls.mjs';

test('the original new-game journey persists, recovers a failed save, reloads and migrates imported saves', { timeout: 360000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => ['ready','failed'].includes(window.portStatus?.phase), null, { timeout: 120000 });
    const status = await page.evaluate(() => window.portStatus);
    assert.equal(status.phase, 'ready', status.error);
    assert.equal(status.game?.runtime, 'StardewValley.GameRunner', 'The recovered original lifecycle must run');
    assert.equal(status.game.audioEngine, 'AudioEngineWrapper', 'Original gameplay audio must initialize successfully');
    await hold(page, 'Escape', 100); // Skip the original logo animation through its normal input.
    await page.waitForFunction(() => window.portStatus.game.menu.allowsInteraction, null, { timeout: 30000 });
    await clickControl(page, 'New');
    console.info('Original title click:', (await snapshot(page)).lastTitleClick);
    await page.waitForFunction(() => window.portStatus.game.menu.type === 'CharacterCustomization', null, { timeout: 30000 });
    for (const [name, value] of [['nameBoxCC','Browser'],['farmnameBoxCC','Wasm'],['favThingBoxCC','Original']]) {
      await clickControl(page, name);
      await page.keyboard.type(value, { delay: 40 });
    }
    await clickControl(page, 'skipIntroButton');
    await clickControl(page, 'okButton');
    await page.waitForFunction(() => window.portStatus.game?.player?.customized && window.portStatus.game.location?.name === 'FarmHouse'
      && window.portStatus.game.day === 1 && !window.portStatus.game.overnight && window.portStatus.game.menu.type === null,
      null, { timeout: 60000 });
    const created = await snapshot(page);
    assert.equal(created.player.name, 'Browser');
    assert.equal(created.player.farmName, 'Wasm');
    assert.equal(created.day, 1);
    console.info('Original farmer created in FarmHouse.');
    const door = created.location.exit;
    await walkTo(page, door.x, door.y, state => state.location.name === 'Farm');
    if ((await snapshot(page)).location.name === 'FarmHouse') await hold(page, 's', 400);
    await page.waitForFunction(() => window.portStatus.game.location.name === 'Farm' && !window.portStatus.game.warping
      && window.portStatus.game.player.canMove, null, { timeout: 10000 });
    const before = await snapshot(page);
    await hold(page, 'd', 250);
    const moved = await snapshot(page);
    assert.notEqual(moved.player.positionX, before.player.positionX);
    await hold(page, 'c', 100);
    await page.waitForFunction(stamina => window.portStatus.game.player.stamina < stamina, moved.player.stamina, { timeout: 10000 });
    await page.waitForFunction(() => !window.portStatus.game.player.usingTool && window.portStatus.game.player.canMove, null, { timeout: 10000 });
    await hold(page, 'e', 100);
    await page.waitForFunction(() => window.portStatus.game.menu.type === 'GameMenu', null, { timeout: 10000 });
    console.info('Original movement, tool use and inventory verified.');
    await hold(page, 'Escape', 100);
    const entrance = (await snapshot(page)).location.houseEntrance;
    await walkTo(page, entrance.x, entrance.y);
    await hold(page, 'w', 400);
    await hold(page, 'x', 100); // The original building door uses the action button.
    await page.waitForFunction(() => window.portStatus.game.location.name === 'FarmHouse' && !window.portStatus.game.warping
      && window.portStatus.game.player.canMove, null, { timeout: 10000 });
    console.info('Original farmhouse door entered.');
    const house = (await snapshot(page)).location;
    await walkTo(page, house.exit.x, house.exit.y - 64); // Step out of the narrow doorway before crossing the room.
    const bed = house.bed;
    await walkTo(page, bed.x - 128, bed.y); // Enter the original bed from its open side.
    await walkTo(page, bed.x, bed.y, state => state.menu.type === 'DialogueBox');
    await page.waitForFunction(() => window.portStatus.game.menu.type === 'DialogueBox', null, { timeout: 10000 });
    const previousSave = await page.evaluate(async () => {
      const records = (await portStorage.readAllForDotNet()).filter(record => record.slot !== '@settings');
      return records[0];
    });
    assert.ok(previousSave, 'Original new-game setup saves the initial day before the first sleep');
    await page.evaluate(() => portStorage.testing.failNextCommit('QuotaExceededError'));
    await clickControl(page, 'Yes');
    await page.waitForFunction(() => window.portStatus.game.day === 2, null, { timeout: 60000 });
    await page.waitForFunction(() => window.portStatus.storage.phase === 'failed', null, { timeout: 60000 });
    assert.equal((await snapshot(page)).overnight, true, 'Original sleep must wait for the durable save');
    assert.deepEqual(await page.evaluate(slot => portStorage.readForDotNet(slot), previousSave.slot), previousSave,
      'A failed overnight transaction must retain the byte-identical original day-1 save');
    assert.equal(await page.locator('#saveFailure').isVisible(), true);
    const pendingDownload = page.waitForEvent('download');
    await page.locator('#exportPending').click();
    await (await pendingDownload).saveAs('.port-cache/task-5-pending.zip');
    assert.ok((await readFile('.port-cache/task-5-pending.zip')).length > 1000);
    await page.locator('#retrySave').click();
    await page.waitForFunction(() => !window.portStatus.game.overnight && window.portStatus.game.menu.type === null && window.portStatus.game.player.canMove
      && window.portStatus.game.location.name === 'FarmHouse', null, { timeout: 60000 });
    const morning = await snapshot(page);
    assert.equal(morning.player.name, 'Browser');
    assert.ok(morning.save.mainBytes > 1000, 'The original save serializer must write the full farm');
    assert.ok(morning.save.farmerBytes > 100, 'The original save serializer must write SaveGameInfo');
    console.info('Original day-2 save files:', morning.save);
    await page.screenshot({ path: '.port-cache/new-game-day-2.png' });
    const stored = await page.evaluate(async slot => {
      const record = await portStorage.read(slot);
      return record && { main: record.files[slot].length, info: record.files.SaveGameInfo.length };
    }, morning.save.slot);
    assert.deepEqual(stored, { main: morning.save.mainBytes, info: morning.save.farmerBytes }, 'Sleep completion must follow the original save pair committing to IndexedDB');
    await page.reload({ waitUntil: 'load' });
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
    await page.locator('#saveTools').click();
    await page.waitForFunction(() => document.querySelector('#saveSlot')?.options.length === 1);
    const exportDownload = page.waitForEvent('download');
    await page.locator('#exportSave').click();
    await (await exportDownload).saveAs('.port-cache/task-5-export.zip');
    const invalid = [
      { name: morning.save.slot, mimeType: 'application/xml', buffer: Buffer.from('<SaveGame>invalid</SaveGame>') },
      { name: 'SaveGameInfo', mimeType: 'application/xml', buffer: Buffer.from('<Farmer>invalid</Farmer>') }
    ];
    await page.locator('#importSave').setInputFiles(invalid);
    await page.waitForFunction(() => document.querySelector('#saveMessage')?.textContent.includes('do not describe the same farmer'), null, { timeout: 60000 });
    assert.ok(!await page.locator('#saveMessage').textContent().then(text => text.includes('Save imported')));
    assert.equal(await page.evaluate(async slot => (await portStorage.read(slot)).files[slot].length, morning.save.slot), morning.save.mainBytes);
    await page.locator('#importSave').setInputFiles('.port-cache/task-5-export.zip');
    await page.waitForFunction(() => document.querySelector('#saveMessage')?.textContent.includes('Save imported'), null, { timeout: 60000 });
    const legacy = await page.evaluate(async slot => {
      const original = await portStorage.readForDotNet(slot);
      const bytes = Uint8Array.from(atob(original.files[slot]), c => c.charCodeAt(0));
      const document = new DOMParser().parseFromString(new TextDecoder().decode(bytes), 'application/xml');
      const pair = document.querySelector('SaveGame > bundleData > item');
      if (!pair) throw new Error('Original save did not contain bundle data');
      const value = pair.querySelector('value > string');
      const fields = value.textContent.split('/').slice(0, 6);
      value.textContent = fields.join('/');
      document.querySelector('SaveGame > lastAppliedSaveFix').textContent = '48';
      return { main: new XMLSerializer().serializeToString(document), info: original.files.SaveGameInfo,
        key: pair.querySelector('key > string').textContent, expected: [...fields, fields[0]].join('/') };
    }, morning.save.slot);
    // Explicitly constructed legacy bundle fixture, not a claim of a genuine desktop 1.5 save.
    const compressedLegacy = deflateSync(Buffer.from(legacy.main));
    await page.locator('#importSave').setInputFiles([
      { name: morning.save.slot, mimeType: 'application/octet-stream', buffer: compressedLegacy },
      { name: 'SaveGameInfo', mimeType: 'application/xml', buffer: Buffer.from(legacy.info, 'base64') }
    ]);
    await page.waitForFunction(async ({ slot, bytes }) => (await portStorage.readForDotNet(slot)).files[slot] === bytes,
      { slot: morning.save.slot, bytes: compressedLegacy.toString('base64') }, { timeout: 60000 });
    await page.waitForFunction(() => document.querySelector('#saveMessage')?.textContent.includes('Save imported'), null, { timeout: 60000 });
    await page.locator('#savePanel button').first().click();
    await hold(page, 'Escape', 100);
    await clickControl(page, 'Load');
    await page.waitForFunction(() => window.portStatus.game.menu.type === 'LoadGameMenu'
      && window.portStatus.game.menu.saves?.some(save => save.farmer === 'Browser'), null, { timeout: 60000 });
    await clickControl(page, '0');
    await page.waitForFunction(() => window.portStatus.game.player?.customized && window.portStatus.game.day === 2
      && window.portStatus.game.location?.name === 'FarmHouse' && window.portStatus.game.menu.type === null
      && !window.portStatus.game.overnight && !window.portStatus.game.warping && window.portStatus.game.player.canMove,
      null, { timeout: 120000 });
    assert.equal((await snapshot(page)).player.farmName, 'Wasm');
    const migrated = await snapshot(page);
    assert.ok(migrated.migration.lastSaveFix >= 49);
    assert.equal(migrated.migration.bundles[legacy.key], legacy.expected, 'The original 1.6 migrator must restore the legacy seventh bundle field');
    await hold(page, 'a', 250);
    assert.notEqual((await snapshot(page)).player.positionX, migrated.player.positionX, 'Loaded gameplay must resume normal movement');
    console.info('Original day-2 save survived reload and loaded through the original Load menu.');
    await page.screenshot({ path: '.port-cache/task-5-loaded-save.png' });
  }, undefined, '/');
});

import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';
import { clickControl, hold } from './game-controls.mjs';

test('original title mute settings persist and apply on a fresh reload', { timeout: 180000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
    await hold(page, 'Escape', 100);
    await clickControl(page, 'muteMusicButton');
    await page.waitForFunction(() => window.portStatus.game.preferences?.startMuted === true && window.portStatus.storage.phase === 'saved');
    const before = await page.evaluate(() => window.portStatus.game.preferences.timesPlayed);
    const xml = await page.evaluate(async () => new TextDecoder().decode((await portStorage.read('@settings')).files.startup_preferences));
    assert.match(xml, /<startMuted>true<\/startMuted>/);
    await page.reload({ waitUntil: 'load' });
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
    const preferences = await page.evaluate(() => window.portStatus.game.preferences);
    assert.equal(preferences.startMuted, true);
    assert.ok(preferences.timesPlayed > before, 'Original StartupPreferences must read and apply the stored file');
  }, undefined, '/');
});

test('IndexedDB reload retains original bytes, settings and the previous snapshot', { timeout: 60000 }, async () => {
  await withGame(async page => {
    assert.equal(await page.evaluate(() => typeof window.portStorage?.commit), 'function');
    await page.evaluate(async () => {
      await portStorage.commit({ slot: 'Farm_123', files: { Farm_123: new Uint8Array([60, 1, 62]), SaveGameInfo: new Uint8Array([60, 70, 1, 62]) } });
      await portStorage.commit({ slot: 'Farm_123', files: { Farm_123: new Uint8Array([60, 2, 62]), SaveGameInfo: new Uint8Array([60, 70, 2, 62]) } });
      await portStorage.commit({ slot: '@settings', files: { startup_preferences: new Uint8Array([60, 83, 62]) } });
    });
    await page.reload({ waitUntil: 'load' });
    const result = await page.evaluate(async () => ({
      current: Array.from((await portStorage.read('Farm_123')).files.Farm_123),
      previous: Array.from((await portStorage.readPrevious('Farm_123')).files.Farm_123),
      settings: Array.from((await portStorage.read('@settings')).files.startup_preferences)
    }));
    assert.deepEqual(result, { current: [60, 2, 62], previous: [60, 1, 62], settings: [60, 83, 62] });
  });
});

for (const failure of ['AbortError', 'QuotaExceededError']) {
  test(`${failure} rolls back both save files and leaves the backup exportable`, { timeout: 60000 }, async () => {
    await withGame(async page => {
      assert.equal(await page.evaluate(() => typeof window.portStorage?.commit), 'function');
      const result = await page.evaluate(async failure => {
        const files = value => ({ Farm_123: new Uint8Array([60, value, 62]), SaveGameInfo: new Uint8Array([60, 70, value, 62]) });
        await portStorage.commit({ slot: 'Farm_123', files: files(1) });
        await portStorage.commit({ slot: 'Farm_123', files: files(2) });
        window.testSaveFailure(failure);
        let error;
        try { await portStorage.commit({ slot: 'Farm_123', files: files(9) }); } catch (e) { error = e.name; }
        return { error, current: Array.from((await portStorage.read('Farm_123')).files.Farm_123),
          previous: Array.from((await portStorage.readPrevious('Farm_123')).files.Farm_123),
          info: Array.from((await portStorage.read('Farm_123')).files.SaveGameInfo) };
      }, failure);
      assert.deepEqual(result, { error: failure, current: [60, 2, 62], previous: [60, 1, 62], info: [60, 70, 2, 62] });
    });
  });
}

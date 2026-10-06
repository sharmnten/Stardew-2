import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';

test('all 463 prepared original waves decode without exceeding the idle audio cache', { timeout: 900000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 90000 });
    await page.evaluate(() => window.portHost.stop());
    const manifest = await page.evaluate(async () => (await fetch('Audio/manifest.json')).json());
    assert.equal(manifest.waves.length, 463);
    let maximum = 0;
    for (let offset = 0; offset < manifest.waves.length; offset += 20) {
      const report = await page.evaluate(async waves => {
        let maximum = 0;
        for (const wave of waves) {
          const data = new Uint8Array(await (await fetch(wave.path)).arrayBuffer());
          const digest = [...new Uint8Array(await crypto.subtle.digest('SHA-256', data))].map(byte => byte.toString(16).padStart(2, '0')).join('');
          if (digest !== wave.sha256) throw new Error(`Prepared audio identity changed: ${wave.path}`);
          await window.portAudio.decode(`${wave.bank}/${wave.track}`, data, wave);
          maximum = Math.max(maximum, window.portAudio.status().audioDecodedBytes);
        }
        return maximum;
      }, manifest.waves.slice(offset, offset + 20));
      maximum = Math.max(maximum, report);
      assert.ok(report <= 128 * 1024 * 1024, `Inactive decoded audio exceeded cache limit: ${report}`);
      console.log(`Browser decoded ${Math.min(offset + 20, 463)}/463 original waves; largest idle resident cache ${maximum} bytes`);
    }
    const status = await page.evaluate(() => window.portAudio.status());
    assert.equal(status.audioDownloadedBytes, 888852358);
    assert.equal(status.audioError, null);
  });
});

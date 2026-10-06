import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';

test('the original bigSelect cue plays only after a player gesture', { timeout: 120000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => ['ready','failed'].includes(window.portStatus?.phase), null, { timeout: 90000 });
    const status = await page.evaluate(() => window.portStatus);
    assert.equal(status.phase, 'ready', status.error);
    assert.equal(status.audioState, 'suspended', 'Audio must wait for the player');
    assert.equal(status.audioDecodedBytes, 0, 'Wave banks must remain unloaded at startup');
    assert.equal(await page.getByRole('button', { name: 'Test original audio' }).count(), 1);
    await page.getByRole('button', { name: 'Test original audio' }).click();
    await page.waitForFunction(() => window.portStatus.audioPeak > 0.005 || window.portStatus.audioError, null, { timeout: 30000 });
    const playing = await page.evaluate(() => window.portStatus);
    assert.equal(playing.audioError, null);
    assert.equal(playing.audioState, 'running');
    assert.equal(playing.audioCue, 'bigSelect');
    assert.ok(playing.audioPeak > 0.005, 'Original sample must reach the audio graph');
    assert.equal(playing.audioDecodedBytes, 13824 * 2 * 4, 'Only the requested original wave is decoded');
    assert.equal(playing.audioDownloadedBytes, 16055);
    assert.equal(playing.audioScheduler, 'original-xact');
    await page.waitForFunction(() => window.portStatus.audioActiveVoices === 0, null, { timeout: 10000 });
    await page.getByRole('button', { name: 'Test original audio' }).click();
    await page.waitForFunction(() => window.portStatus.audioActiveVoices > 0, null, { timeout: 10000 });
    assert.equal((await page.evaluate(() => window.portStatus)).audioDownloadedBytes, 16055, 'Repeated cue reuses the decoded wave');
  });
});


test('corrupted original audio is rejected before decoding', { timeout: 120000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 90000 });
    await page.getByRole('button', { name: 'Test original audio' }).click();
    await page.waitForFunction(() => window.portStatus.audioError, null, { timeout: 30000 });
    const status = await page.evaluate(() => window.portStatus);
    assert.match(status.audioError, /checksum mismatch.*0\/3/i);
    assert.equal(status.audioDecodedBytes, 0);
    assert.equal(status.audioPeak, 0);
  }, async page => {
    await page.route('**/Audio/0/003.flac', async route => {
      const response = await route.fetch();
      const body = await response.body();
      body[body.length - 1] ^= 1;
      await route.fulfill({ response, body });
    });
  });
});

test('browser voices preserve repeats, pause, resume and release using an original sample', { timeout: 120000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 90000 });
    await page.getByRole('button', { name: 'Test original audio' }).click();
    await page.waitForFunction(() => window.portStatus.audioPeak > 0.005, null, { timeout: 30000 });
    const parameters = { volume: 1, pitch: 0, pan: 0, loopCount: 2, reverbMix: 0, filterEnabled: false };
    await page.evaluate(parameters => window.portAudio.createVoice(9001, '0/3', parameters), parameters);
    await page.waitForTimeout(450); // Original sample is about 313ms; finite repeats still run.
    assert.equal(await page.evaluate(() => window.portAudio.voiceStates()['9001']), 0);
    await page.waitForFunction(() => window.portAudio.voiceStates()['9001'] === 2, null, { timeout: 5000 });
    await page.evaluate(parameters => window.portAudio.createVoice(9002, '0/3', { ...parameters, loopCount: 255 }), parameters);
    await page.waitForTimeout(1100);
    assert.equal(await page.evaluate(() => window.portAudio.voiceStates()['9002']), 0);
    await page.evaluate(() => window.portAudio.pauseVoice(9002));
    assert.equal(await page.evaluate(() => window.portAudio.voiceStates()['9002']), 1);
    await page.evaluate(() => window.portAudio.resumeVoice(9002));
    assert.equal(await page.evaluate(() => window.portAudio.voiceStates()['9002']), 0);
    await page.evaluate(() => window.portAudio.stopVoice(9002, false));
    await page.waitForFunction(() => window.portAudio.voiceStates()['9002'] === 2, null, { timeout: 5000 });
  });
});

test('original audio output applies reverb sends and frequency filters', { timeout: 120000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 90000 });
    await page.getByRole('button', { name: 'Test original audio' }).click();
    await page.waitForFunction(() => window.portStatus.audioPeak > 0.005, null, { timeout: 30000 });
    await page.waitForFunction(() => window.portStatus.audioActiveVoices === 0, null, { timeout: 10000 });
    const parameters = { volume: 1, pitch: 0, pan: 0, loopCount: 0, reverbMix: 1, filterEnabled: false };
    await page.evaluate(parameters => window.portAudio.createVoice(9010, '0/3', parameters), parameters);
    await page.waitForTimeout(600);
    const tail = await page.evaluate(() => window.portAudio.status().audioCurrentLevel);
    assert.ok(tail > 0.00001, `Original reverb send must have a tail after the dry sample ends: ${tail}`);
    await page.evaluate(() => window.portAudio.destroyVoice(9010));
    await page.waitForTimeout(5000); // Let the previous reverb tail finish.
    await page.evaluate(parameters => window.portAudio.createVoice(9011, '0/3', { ...parameters, reverbMix: 0, loopCount: 255 }), parameters);
    await page.evaluate(() => window.portAudio.markCue('dry-filter-baseline'));
    await page.waitForTimeout(1400); // Measure across multiple complete sample periods.
    const dry = await page.evaluate(() => window.portAudio.status().audioPeak);
    await page.evaluate(parameters => window.portAudio.updateVoice(9011, { ...parameters, reverbMix: 0, loopCount: 255,
      filterEnabled: true, filterMode: 0, filterQ: 1, filterFrequency: 20 }), parameters);
    await page.waitForTimeout(200); // Discard the analyser's previous dry window and filter transient.
    await page.evaluate(() => window.portAudio.markCue('filtered-baseline'));
    await page.waitForTimeout(1400);
    const filtered = await page.evaluate(() => window.portAudio.status().audioPeak);
    assert.ok(filtered < dry / 4, `The original low-pass control must reduce high frequency output: dry=${dry}, filtered=${filtered}`);
    await page.evaluate(() => window.portAudio.destroyVoice(9011));
  });
});

test('active music stays available and released buffers return within the audio cache budget', { timeout: 180000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 90000 });
    await page.getByRole('button', { name: 'Test original audio' }).click();
    await page.waitForFunction(() => window.portStatus.audioPeak > 0.005, null, { timeout: 30000 });
    const held = await page.evaluate(async () => {
      const manifest = await (await fetch('Audio/manifest.json')).json();
      const parameters = { volume: 0.1, pitch: 0, pan: 0, loopCount: 255, reverbMix: 0, filterEnabled: false };
      for (const [track, id] of [[91,9020],[92,9021]]) {
        const wave = manifest.waves.find(wave => wave.bank === 0 && wave.track === track);
        const data = new Uint8Array(await (await fetch(wave.path)).arrayBuffer());
        await window.portAudio.decode(`0/${track}`, data, wave);
        window.portAudio.createVoice(id, `0/${track}`, parameters);
      }
      return { first: window.portAudio.hasWave('0/91'), second: window.portAudio.hasWave('0/92'), bytes: window.portAudio.status().audioDecodedBytes };
    });
    assert.ok(held.first && held.second, 'Active music must stay resident during a transition');
    assert.ok(held.bytes > 128 * 1024 * 1024, 'The original large active waves exercise the overflow condition');
    await page.evaluate(() => { window.portAudio.destroyVoice(9020); window.portAudio.destroyVoice(9021); });
    const released = await page.evaluate(() => window.portAudio.status().audioDecodedBytes);
    assert.ok(released <= 128 * 1024 * 1024, `Released audio must return to the idle cache limit: ${released}`);
  });
});

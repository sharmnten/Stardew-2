import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

test('original desktop arcade input, firing and generated kart physics', { timeout: 180000 }, () => {
  const id = 'original-minigames';
  const root = resolve(`.port-cache/reference/users/${id}`);
  mkdirSync(`${root}/data`, { recursive: true }); mkdirSync(`${root}/config`, { recursive: true });
  const report = resolve(`.port-cache/reference/${id}.json`);
  const result = spawnSync('timeout', ['-k','5s','160s','xvfb-run','-a','dotnet','run','--project','tools/DesktopReference','--',resolve('original'),report,id], {
    encoding: 'utf8', timeout: 170000,
    env: { ...process.env, ALSOFT_DRIVERS: 'null', LIBGL_ALWAYS_SOFTWARE: '1', XDG_DATA_HOME: `${root}/data`, XDG_CONFIG_HOME: `${root}/config` }
  });
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const scenario = JSON.parse(readFileSync(report)).scenario;
  const actual = scenario.observations;
  assert.equal(actual.king.started, true);
  assert.ok(actual.king.moved > 0);
  assert.ok(actual.king.bullets > 0);
  assert.equal(actual.king.lives, 3);
  assert.deepEqual(actual.kart.map(kart => kart.mode), [2, 3]);
  for (const kart of actual.kart) {
    assert.equal(kart.started, true);
    assert.ok(kart.furthestX > kart.startX);
    assert.ok(kart.tracks > 0);
  }
  assert.ok(scenario.beforeNight, 'Record original Prairie King saved progress before sleeping');
  assert.equal(scenario.beforeNight.day, 1);
  assert.equal(scenario.beforeNight.progress.wave, 0);
  assert.equal(scenario.beforeNight.progress.lives, 2, 'Original live monster collision must earn an automatic saved checkpoint');
  assert.equal(scenario.beforeNight.progress.died, true);
  assert.equal(scenario.baselineCheckpoint.progress.lives, 3);
  assert.equal(scenario.baselineCheckpoint.progress.died, false);
  assert.equal(scenario.beforeNight.progress.bulletDamage, 1);
  assert.equal(scenario.beforeNight.progress.heldItem, -100);
  assert.equal(scenario.afterReload.day, 2);
  assert.deepEqual(scenario.afterReload.progress, scenario.beforeNight.progress);
});

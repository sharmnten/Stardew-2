import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

test('populated original desktop save advances through season rollover, shipping and reload', { timeout: 180000 }, () => {
  const id = 'advanced-desktop-roundtrip';
  const root = resolve(`.port-cache/reference/users/${id}`);
  mkdirSync(`${root}/data`, { recursive: true }); mkdirSync(`${root}/config`, { recursive: true });
  const report = resolve(`.port-cache/reference/${id}.json`);
  const result = spawnSync('timeout', ['-k','5s','160s','xvfb-run','-a','dotnet','run','--project','tools/DesktopReference','--',resolve('original'),report,id], {
    encoding: 'utf8', timeout: 170000,
    env: { ...process.env, ALSOFT_DRIVERS: 'null', LIBGL_ALWAYS_SOFTWARE: '1', XDG_DATA_HOME: `${root}/data`, XDG_CONFIG_HOME: `${root}/config` }
  });
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const actual = JSON.parse(readFileSync(report)).scenario;
  assert.equal(actual.advancedBefore.day, 28);
  assert.equal(actual.advancedBefore.season, 'Spring');
  assert.equal(actual.afterSleep.day, 1);
  assert.equal(actual.afterSleep.season, 'Summer');
  assert.equal(actual.afterSleep.money, 657);
  assert.equal(actual.afterSleep.crops.outdoorDead, true);
  assert.equal(actual.afterSleep.crops.greenhouseDead, false);
  assert.equal(actual.afterSleep.machine.ready, true, result.stdout);
  assert.equal(actual.afterSleep.machine.output, '(O)306');
  assert.equal(typeof actual.afterSleep.weather.raining, 'boolean');
  assert.deepEqual(actual.afterSleepReload, actual.afterSleep);
});

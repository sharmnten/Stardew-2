import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

test('the supplied desktop game loads the browser-exported advanced save unchanged', { timeout: 120000 }, () => {
  const root = resolve('.port-cache/reference/users/browser-export');
  mkdirSync(`${root}/data`, { recursive: true }); mkdirSync(`${root}/config`, { recursive: true });
  const report = resolve('.port-cache/reference/browser-export.json');
  const result = spawnSync('timeout', ['-k','5s','100s','xvfb-run','-a','dotnet','run','--project','tools/DesktopReference','--',resolve('original'),report,'browser-export'], {
    encoding: 'utf8', timeout: 110000,
    env: { ...process.env, ALSOFT_DRIVERS: 'null', LIBGL_ALWAYS_SOFTWARE: '1', XDG_DATA_HOME: `${root}/data`, XDG_CONFIG_HOME: `${root}/config` }
  });
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const expected = JSON.parse(readFileSync('.port-cache/reference/advanced-desktop-roundtrip.json')).scenario.afterSleepReload;
  const actual = JSON.parse(readFileSync(report));
  assert.equal(actual.gameVersion, '1.6.15.24356');
  assert.deepEqual(actual.scenario.advanced, expected);
});

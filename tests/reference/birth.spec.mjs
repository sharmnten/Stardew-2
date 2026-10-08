import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

test('original overnight birth, naming and save reload retain the new child', { timeout: 240000 }, () => {
  const id = 'characters-family-birth';
  const root = resolve(`.port-cache/reference/users/${id}`);
  mkdirSync(`${root}/data`, { recursive: true }); mkdirSync(`${root}/config`, { recursive: true });
  const report = resolve(`.port-cache/reference/${id}.json`);
  const result = spawnSync('timeout', ['-k','5s','220s','xvfb-run','-a','dotnet','run','--project','tools/DesktopReference','--',resolve('original'),report,id], {
    encoding: 'utf8', timeout: 230000,
    env: { ...process.env, ALSOFT_DRIVERS: 'null', LIBGL_ALWAYS_SOFTWARE: '1', XDG_DATA_HOME: `${root}/data`, XDG_CONFIG_HOME: `${root}/config` }
  });
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const actual = JSON.parse(readFileSync(report)).scenario;
  assert.deepEqual(actual.beforeBirth.children, []);
  assert.equal(actual.birthEvent, 'BirthingEvent');
  assert.equal(actual.afterBirth.day, 6);
  assert.deepEqual(actual.afterBirth.children, [{ name: 'PortBaby', days: 0, age: 0 }]);
  assert.deepEqual(actual.afterBirthReload, actual.afterBirth);
});

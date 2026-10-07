import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

test('original desktop hopper, tailoring and furniture actions produce their native results', { timeout: 120000 }, () => {
  const id = 'tailoring-automation-decoration';
  const root = resolve(`.port-cache/reference/users/${id}`);
  mkdirSync(`${root}/data`, { recursive: true }); mkdirSync(`${root}/config`, { recursive: true });
  const report = resolve(`.port-cache/reference/${id}.json`);
  const result = spawnSync('timeout', ['-k','5s','100s','xvfb-run','-a','dotnet','run','--project','tools/DesktopReference','--',resolve('original'),report,id], {
    encoding: 'utf8', timeout: 110000,
    env: { ...process.env, ALSOFT_DRIVERS: 'null', LIBGL_ALWAYS_SOFTWARE: '1', XDG_DATA_HOME: `${root}/data`, XDG_CONFIG_HOME: `${root}/config` }
  });
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const actual = JSON.parse(readFileSync(report)).scenario.observations;
  assert.equal(actual.hopper.inputConsumed, 1);
  assert.equal(actual.hopper.output, '(O)306');
  assert.equal(actual.hopper.minutes, 180);
  assert.equal(actual.tailoring.clothing, true);
  assert.match(actual.tailoring.id, /^\(S\)/);
  assert.equal(actual.furniture.placed, true);
  assert.equal(actual.furniture.added, 1);
  assert.equal(actual.furniture.id, '(F)0');
});

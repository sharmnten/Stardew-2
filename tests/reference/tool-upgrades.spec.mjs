import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

test('original desktop charges, finishes and returns a copper tool upgrade', { timeout: 180000 }, () => {
  const id = 'tool-upgrades';
  const root = resolve(`.port-cache/reference/users/${id}`);
  mkdirSync(`${root}/data`, { recursive: true }); mkdirSync(`${root}/config`, { recursive: true });
  const report = resolve(`.port-cache/reference/${id}.json`);
  const result = spawnSync('timeout', ['-k','5s','160s','xvfb-run','-a','dotnet','run','--project','tools/DesktopReference','--',resolve('original'),report,id], {
    encoding: 'utf8', timeout: 170000,
    env: { ...process.env, ALSOFT_DRIVERS: 'null', LIBGL_ALWAYS_SOFTWARE: '1', XDG_DATA_HOME: `${root}/data`, XDG_CONFIG_HOME: `${root}/config` }
  });
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const actual = JSON.parse(readFileSync(report)).scenario.observations;
  assert.equal(actual.purchase.id, '(T)CopperAxe');
  assert.equal(actual.purchase.moneySpent, 2000);
  assert.equal(actual.purchase.barsSpent, 5);
  assert.equal(actual.purchase.originalRemoved, true);
  assert.equal(actual.purchase.level, 1);
  assert.deepEqual(actual.countdown, [2, 1, 0]);
  assert.equal(actual.collection.handled, true);
  assert.equal(actual.collection.pending, false);
  assert.equal(actual.collection.id, '(T)CopperAxe');
  assert.equal(actual.collection.level, 1);
});

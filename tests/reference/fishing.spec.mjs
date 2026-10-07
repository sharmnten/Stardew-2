import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

test('original desktop bait, tackle, crab pot, fish pond and fishing physics', { timeout: 150000 }, () => {
  const id = 'fishing-gathering';
  const root = resolve(`.port-cache/reference/users/${id}`);
  mkdirSync(`${root}/data`, { recursive: true }); mkdirSync(`${root}/config`, { recursive: true });
  const report = resolve(`.port-cache/reference/${id}.json`);
  const result = spawnSync('timeout', ['-k','5s','130s','xvfb-run','-a','dotnet','run','--project','tools/DesktopReference','--',resolve('original'),report,id], {
    encoding: 'utf8', timeout: 140000,
    env: { ...process.env, ALSOFT_DRIVERS: 'null', LIBGL_ALWAYS_SOFTWARE: '1', XDG_DATA_HOME: `${root}/data`, XDG_CONFIG_HOME: `${root}/config` }
  });
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const actual = JSON.parse(readFileSync(report)).scenario.observations;
  assert.equal(actual.rod.bait, '(O)685');
  assert.equal(actual.rod.baitStack, 20);
  assert.deepEqual(actual.rod.tackle, ['(O)686']);
  assert.equal(actual.pot.accepted, true);
  assert.equal(actual.pot.inputConsumed, 1);
  assert.equal(actual.pot.ready, true);
  assert.match(actual.pot.output, /^\(O\)/);
  assert.equal(actual.pond.before, 1);
  assert.equal(actual.pond.after, 2);
  assert.equal(actual.bar.initial.height, 136);
  // The original first-catch branch raises fish difficulty below50 to50.
  assert.equal(actual.bar.initial.difficulty, 50);
  assert.equal(actual.bar.after.scale, 1);
  assert.equal(Number.isFinite(actual.bar.after.position), true);
  assert.equal(Number.isFinite(actual.bar.after.distance), true);
});

import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

test('original desktop construction, livestock production, petting and stable ownership', { timeout: 150000 }, () => {
  const id = 'animals-buildings';
  const root = resolve(`.port-cache/reference/users/${id}`);
  mkdirSync(`${root}/data`, { recursive: true }); mkdirSync(`${root}/config`, { recursive: true });
  const report = resolve(`.port-cache/reference/${id}.json`);
  const result = spawnSync('timeout', ['-k','5s','130s','xvfb-run','-a','dotnet','run','--project','tools/DesktopReference','--',resolve('original'),report,id], {
    encoding: 'utf8', timeout: 140000,
    env: { ...process.env, ALSOFT_DRIVERS: 'null', LIBGL_ALWAYS_SOFTWARE: '1', XDG_DATA_HOME: `${root}/data`, XDG_CONFIG_HOME: `${root}/config` }
  });
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const actual = JSON.parse(readFileSync(report)).scenario.observations;
  assert.equal(actual.construction.built, true);
  assert.equal(actual.construction.couldAfford, true);
  assert.equal(actual.construction.cost, 6000);
  assert.equal(actual.construction.initialDays, 3);
  assert.equal(actual.construction.completedDays, 0);
  assert.equal(actual.upgrade.type, 'Big Barn');
  assert.equal(actual.upgrade.cost, 12000);
  assert.equal(actual.upgrade.days, 0);
  assert.equal(actual.upgrade.capacity, 8);
  assert.equal(actual.chicken.friendship, 15);
  assert.equal(actual.chicken.produce[0].id, '(O)176');
  assert.equal(actual.cow.friendship, 15);
  assert.equal(actual.cow.produce, '184');
  assert.equal(actual.pet.petted, true);
  assert.equal(actual.pet.friendship, 12);
  assert.equal(actual.pet.timesPet, 1);
  assert.equal(actual.stable.built, true);
  assert.equal(actual.stable.days, 0);
  assert.equal(actual.stable.horseOwned, true);
});

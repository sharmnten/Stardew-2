import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

test('original desktop construction, livestock production, petting and stable ownership', { timeout: 240000 }, () => {
  const id = 'animals-buildings';
  const root = resolve(`.port-cache/reference/users/${id}`);
  mkdirSync(`${root}/data`, { recursive: true }); mkdirSync(`${root}/config`, { recursive: true });
  const report = resolve(`.port-cache/reference/${id}.json`);
  const result = spawnSync('timeout', ['-k','5s','220s','xvfb-run','-a','dotnet','run','--project','tools/DesktopReference','--',resolve('original'),report,id], {
    encoding: 'utf8', timeout: 230000,
    env: { ...process.env, ALSOFT_DRIVERS: 'null', LIBGL_ALWAYS_SOFTWARE: '1', XDG_DATA_HOME: `${root}/data`, XDG_CONFIG_HOME: `${root}/config` }
  });
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const scenario = JSON.parse(readFileSync(report)).scenario;
  const actual = scenario.observations;
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
  assert.equal(scenario.afterRiding.horse.hat, '(H)0');
  assert.equal(scenario.afterRiding.horse.ateCarrotToday, true);
  assert.equal(scenario.afterRiding.carrots, 1);
  assert.equal(scenario.afterNight.horse.hat, '(H)0');
  assert.equal(scenario.afterNight.horse.ateCarrotToday, false);
  assert.equal(scenario.afterNight.carrots, 1);
  assert.equal(scenario.afterRiding.horse.name, 'PortHorse');
  assert.equal(scenario.afterRiding.horse.farmerName, 'PortHorse');
  assert.equal(scenario.afterRiding.horse.ownerMatches, true);
  assert.equal(scenario.afterRiding.horse.stableMatches, true);
  assert.equal(scenario.afterRiding.horse.mounted, false);
  assert.equal(scenario.riding.moved, true);
  assert.equal(scenario.riding.mounted, true);
  assert.equal(scenario.afterNight.day, 2);
  assert.deepEqual(scenario.afterReload, scenario.afterNight);
  assert.deepEqual(scenario.afterReload.buildings.map(b => b.type), ['Big Barn', 'Coop', 'Stable']);
  assert.deepEqual(scenario.afterReload.animals.map(a => a.type), ['White Chicken', 'White Cow']);
  assert.deepEqual(scenario.afterReload.animals.map(a => a.home), ['Coop', 'Big Barn']);
  assert.equal(scenario.afterReload.horse.name, 'PortHorse');
  assert.equal(scenario.afterReload.horse.stableMatches, true);
});

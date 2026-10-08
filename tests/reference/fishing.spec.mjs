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
  const scenario = JSON.parse(readFileSync(report)).scenario;
  const actual = scenario.observations;
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
  assert.ok(scenario.gathering.spawned > 0, 'Original daily spawning must create forage');
  assert.equal(scenario.gathering.harvested, true);
  assert.equal(scenario.gathering.removed, true);
  assert.equal(scenario.gathering.itemsForaged, 1);
  assert.equal(scenario.gathering.inventoryAdded, 1);
  assert.ok(scenario.gathering.regenerated > 0, 'A subsequent original spawn must replenish gathered resources');
  assert.ok(scenario.afterHarvest, 'Record original shore harvesting');
  assert.equal(scenario.afterHarvest.experience, 2155);
  assert.deepEqual(scenario.afterHarvest.professions, [6]);
  assert.equal(scenario.afterHarvest.baitCount, 1);
  assert.equal(scenario.afterHarvest.pot.ready, false);
  assert.equal(scenario.afterHarvest.pot.bait, null);
  assert.equal(scenario.afterHarvest.pot.output, null);
  assert.deepEqual(scenario.afterHarvest.catch, [{ id: actual.pot.output, stack: 1, quality: 0 }]);
  assert.equal(scenario.afterRebait.baitCount, 0);
  assert.equal(scenario.afterRebait.pot.bait, '(O)685');
  assert.equal(scenario.afterRebait.pot.ready, false);
  assert.equal(scenario.afterReload.day, 2);
  assert.equal(scenario.afterReload.pot.ready, true);
  assert.match(scenario.afterReload.pot.output, /^\(O\)/);
  assert.equal(scenario.afterReload.experience, 2155);
  assert.deepEqual(scenario.afterReload.professions, [6]);
  assert.deepEqual(scenario.afterReload.catch, scenario.afterHarvest.catch);
  assert.ok(scenario.afterPondHarvest, 'Collect the original pond produce after the first cold reload');
  assert.equal(scenario.afterPondHarvest.day, 2);
  assert.equal(scenario.afterPondHarvest.pond.population, 3);
  assert.equal(scenario.afterPondHarvest.pond.output, null);
  assert.equal(scenario.afterPondHarvest.experience, 2166);
  assert.deepEqual(scenario.afterPondHarvest.catch, [
    { id: '(O)812', stack: 2, quality: 0 },
    { id: '(O)717', stack: 1, quality: 0 }
  ]);
  assert.deepEqual(scenario.afterPondHarvest.roe, [{ parent: '145', preserve: 'Roe', price: 45 }]);
  assert.equal(scenario.afterPondReload.day, 3);
  assert.equal(scenario.afterPondReload.experience, 2166);
  assert.deepEqual(scenario.afterPondReload.catch, scenario.afterPondHarvest.catch);
  assert.deepEqual(scenario.afterPondReload.roe, scenario.afterPondHarvest.roe);
});

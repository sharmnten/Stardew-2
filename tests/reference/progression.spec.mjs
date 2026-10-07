import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

test('original desktop skill gains, mastery reward and local achievement', { timeout: 180000 }, () => {
  const id = 'skills-mastery-achievements';
  const root = resolve(`.port-cache/reference/users/${id}`);
  mkdirSync(`${root}/data`, { recursive: true }); mkdirSync(`${root}/config`, { recursive: true });
  const report = resolve(`.port-cache/reference/${id}.json`);
  const result = spawnSync('timeout', ['-k','5s','160s','xvfb-run','-a','dotnet','run','--project','tools/DesktopReference','--',resolve('original'),report,id], {
    encoding: 'utf8', timeout: 170000,
    env: { ...process.env, ALSOFT_DRIVERS: 'null', LIBGL_ALWAYS_SOFTWARE: '1', XDG_DATA_HOME: `${root}/data`, XDG_CONFIG_HOME: `${root}/config` }
  });
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const state = JSON.parse(readFileSync(report)).scenario;
  const actual = state.observations;
  assert.deepEqual(actual.skills.levels, [10, 10, 10, 10, 10]);
  assert.equal(actual.skills.pendingLevels, 50);
  assert.equal(actual.mastery.experience, 10000);
  assert.equal(actual.mastery.level, 1);
  assert.equal(actual.mastery.claimed, 1);
  assert.equal(actual.mastery.spent, 1);
  assert.equal(actual.mastery.heavyFurnaceRecipe, true);
  assert.equal(actual.mastery.dwarfStatueRecipe, true);
  assert.deepEqual(actual.achievements, [0]);
  assert.equal(actual.collection.fishRegistered, true);
  assert.deepEqual(actual.collection.fish, [1, 20]);
  assert.deepEqual(state.professionChoices, [0, 2, 6, 8, 12, 14, 18, 20, 24, 26],
    "Original level5/10 menus must select the left profession branch for every skill");
});

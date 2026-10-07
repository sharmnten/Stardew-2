import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

test('original desktop NPC gifts, schedules, dialogue and child growth', { timeout: 180000 }, () => {
  const id = 'characters-family';
  const root = resolve(`.port-cache/reference/users/${id}`);
  mkdirSync(`${root}/data`, { recursive: true }); mkdirSync(`${root}/config`, { recursive: true });
  const report = resolve(`.port-cache/reference/${id}.json`);
  const result = spawnSync('timeout', ['-k','5s','160s','xvfb-run','-a','dotnet','run','--project','tools/DesktopReference','--',resolve('original'),report,id], {
    encoding: 'utf8', timeout: 170000,
    env: { ...process.env, ALSOFT_DRIVERS: 'null', LIBGL_ALWAYS_SOFTWARE: '1', XDG_DATA_HOME: `${root}/data`, XDG_CONFIG_HOME: `${root}/config` }
  });
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const actual = JSON.parse(readFileSync(report)).scenario.observations;
  assert.equal(actual.gift.taste, 0);
  assert.equal(actual.gift.pointsGained, 100);
  assert.equal(actual.gift.giftsToday, 1);
  assert.equal(actual.gift.giftsThisWeek, 1);
  assert.equal(actual.gift.statsGiftsGiven, 1);
  assert.ok(actual.dialogue.length > 0);
  assert.equal(actual.schedule.loaded, true);
  assert.ok(actual.schedule.times.length > 0);
  assert.equal(actual.family.married, true);
  assert.equal(actual.family.spouse, 'Abigail');
  assert.equal(actual.family.children, 1);
  assert.deepEqual(actual.child.stages.map(stage => [stage.days, stage.age]), [[13, 1], [27, 2], [55, 3]]);
  assert.equal(actual.child.stages.at(-1).speed, 4);
});

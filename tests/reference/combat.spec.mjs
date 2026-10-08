import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

test('original desktop generates dungeons and applies combat damage and buffs', { timeout: 180000 }, () => {
  const id = 'combat-dungeons';
  const root = resolve(`.port-cache/reference/users/${id}`);
  mkdirSync(`${root}/data`, { recursive: true }); mkdirSync(`${root}/config`, { recursive: true });
  const report = resolve(`.port-cache/reference/${id}.json`);
  const result = spawnSync('timeout', ['-k','5s','160s','xvfb-run','-a','dotnet','run','--project','tools/DesktopReference','--',resolve('original'),report,id], {
    encoding: 'utf8', timeout: 170000,
    env: { ...process.env, ALSOFT_DRIVERS: 'null', LIBGL_ALWAYS_SOFTWARE: '1', XDG_DATA_HOME: `${root}/data`, XDG_CONFIG_HOME: `${root}/config` }
  });
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const scenario = JSON.parse(readFileSync(report)).scenario;
  const actual = scenario.observations;
  assert.deepEqual(actual.dungeons.map(dungeon => dungeon.name), ['UndergroundMine5', 'UndergroundMine121', 'VolcanoDungeon1']);
  for (const dungeon of actual.dungeons) {
    assert.ok(dungeon.width > 0 && dungeon.height > 0);
    assert.ok(dungeon.tileSheets.length > 0);
  }
  assert.ok(actual.weapon.minDamage > 0 && actual.weapon.maxDamage >= actual.weapon.minDamage);
  assert.ok(actual.monster.damage > 0);
  assert.equal(actual.monster.healthLost, actual.monster.damage);
  assert.equal(actual.monster.killed, true);
  assert.equal(actual.monster.slimesKilled, 1);
  assert.equal(actual.buff.applied, true);
  assert.equal(actual.buff.speed, 2);
  assert.equal(actual.buff.defense, 3);
  assert.equal(actual.buff.expired, true);
  assert.equal(scenario.liveCombat.location, 'UndergroundMine5');
  assert.equal(scenario.liveCombat.slimesKilled, 1);
  assert.equal(scenario.liveCombat.killed, true);
  assert.ok(scenario.liveCombat.experience > 0);
  assert.equal(scenario.liveCombat.loot, true);
  assert.equal(scenario.afterCombatReload.day, 6);
  assert.equal(scenario.afterCombatReload.deepest, 5);
  assert.equal(scenario.afterCombatReload.slimesKilled, 2);
  assert.equal(scenario.afterCombatReload.experience, scenario.liveCombat.experience);
  assert.equal(scenario.afterCombatReload.weapon, '(W)0');
});

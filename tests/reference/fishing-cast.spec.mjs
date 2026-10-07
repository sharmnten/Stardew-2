import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

test('original rod casts, hooks and catches a tutorial fish through hardware input', { timeout: 210000 }, () => {
  const id = 'fishing-cast';
  const root = resolve(`.port-cache/reference/users/${id}`);
  mkdirSync(`${root}/data`, { recursive: true }); mkdirSync(`${root}/config`, { recursive: true });
  const report = resolve(`.port-cache/reference/${id}.json`);
  const result = spawnSync('timeout', ['-k','5s','190s','xvfb-run','-a','dotnet','run','--project','tools/DesktopReference','--',resolve('original'),report,id], {
    encoding: 'utf8', timeout: 200000,
    env: { ...process.env, ALSOFT_DRIVERS: 'null', LIBGL_ALWAYS_SOFTWARE: '1', XDG_DATA_HOME: `${root}/data`, XDG_CONFIG_HOME: `${root}/config` }
  });
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const state = JSON.parse(readFileSync(report)).scenario;
  assert.equal(state.afterCast.timesFished, 1);
  assert.equal(state.afterCast.water, true);
  assert.equal(state.afterCast.stamina, 262.5);
  assert.equal(state.afterCatch.bait, 1);
  assert.equal(state.afterCatch.inventory.length, 1);
  assert.equal(state.afterCatch.inventory[0].id, '(O)145');
  assert.equal(state.afterCatch.inventory[0].stack, 1);
  assert.equal(state.afterCatch.collection[0].count, 1);
  assert.ok(state.afterCatch.collection[0].size > 0);
  assert.ok(state.afterCatch.experience > 2150);
  assert.equal(state.afterReload.day, 2);
  assert.deepEqual({ ...state.afterReload, day: 1 }, state.afterCatch);
});

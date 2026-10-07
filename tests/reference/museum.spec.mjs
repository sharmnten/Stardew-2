import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

test('original museum donation and journal reward survive a full night', { timeout: 180000 }, () => {
  const id = 'museum-quests';
  const root = resolve(`.port-cache/reference/users/${id}`);
  mkdirSync(`${root}/data`, { recursive: true }); mkdirSync(`${root}/config`, { recursive: true });
  const report = resolve(`.port-cache/reference/${id}.json`);
  const result = spawnSync('timeout', ['-k', '5s', '160s', 'xvfb-run', '-a', 'dotnet', 'run', '--project', 'tools/DesktopReference', '--', resolve('original'), report, id], {
    encoding: 'utf8', timeout: 170000,
    env: { ...process.env, ALSOFT_DRIVERS: 'null', LIBGL_ALWAYS_SOFTWARE: '1', XDG_DATA_HOME: `${root}/data`, XDG_CONFIG_HOME: `${root}/config` }
  });
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const state = JSON.parse(readFileSync(report)).scenario;
  assert.equal(state.afterDonation.day, 5);
  assert.equal(state.afterDonation.crystals, 0);
  assert.equal(state.afterDonation.pieces.length, 1);
  assert.equal(state.afterDonation.pieces[0].id, '86');
  assert.equal(state.afterDonation.quest.completed, true);
  assert.ok(state.afterDonation.quest.moneyReward > 0);
  assert.ok(state.afterReward, 'Record collection through the original quest journal');
  assert.equal(state.afterReward.money, state.afterDonation.money + 250);
  assert.equal(state.afterReward.quest, null);
  assert.deepEqual(state.afterReward.pieces, state.afterDonation.pieces);
  assert.equal(state.afterReload.day, 6);
  assert.deepEqual(state.afterReload.pieces, state.afterDonation.pieces);
  assert.equal(state.afterReload.quest, null);
  assert.equal(state.afterReload.money, state.afterReward.money);
  assert.equal(state.afterReload.crystals, 0);
});

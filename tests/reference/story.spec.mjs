import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

for (const id of ['community-center', 'joja-orders-museum']) {
  test(`original desktop story contribution: ${id}`, { timeout: 180000 }, () => {
    const root = resolve(`.port-cache/reference/users/${id}`);
    mkdirSync(`${root}/data`, { recursive: true }); mkdirSync(`${root}/config`, { recursive: true });
    const report = resolve(`.port-cache/reference/${id}.json`);
    const result = spawnSync('timeout', ['-k','5s','160s','xvfb-run','-a','dotnet','run','--project','tools/DesktopReference','--',resolve('original'),report,id], {
      encoding: 'utf8', timeout: 170000,
      env: { ...process.env, ALSOFT_DRIVERS: 'null', LIBGL_ALWAYS_SOFTWARE: '1', XDG_DATA_HOME: `${root}/data`, XDG_CONFIG_HOME: `${root}/config` }
    });
    assert.equal(result.status, 0, result.stdout + result.stderr);
    const actual = JSON.parse(readFileSync(report)).scenario.observations;
    if (id === 'community-center') {
      assert.equal(actual.bundle.completed, true);
      assert.equal(actual.bundle.consumed, 4);
      assert.equal(actual.bundle.rewardAvailable, true);
      assert.equal(actual.bundle.rewardId, '(O)465');
      assert.equal(actual.bundle.rewardStack, 20);
    } else {
      assert.equal(actual.project.cost, 40000);
      assert.equal(actual.project.completedCheckbox, true);
      assert.ok(actual.project.mail.some(mail => mail.startsWith('jojaVault')));
      assert.ok(actual.project.mail.some(mail => mail.startsWith('ccVault')));
    }
  });
}

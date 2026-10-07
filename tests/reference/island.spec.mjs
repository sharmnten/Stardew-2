import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

test('original desktop walnut limits, parrot upgrade, Qi orders and perfection', { timeout: 180000 }, () => {
  const id = 'island-qi-perfection';
  const root = resolve(`.port-cache/reference/users/${id}`);
  mkdirSync(`${root}/data`, { recursive: true }); mkdirSync(`${root}/config`, { recursive: true });
  const report = resolve(`.port-cache/reference/${id}.json`);
  const result = spawnSync('timeout', ['-k','5s','160s','xvfb-run','-a','dotnet','run','--project','tools/DesktopReference','--',resolve('original'),report,id], {
    encoding: 'utf8', timeout: 170000,
    env: { ...process.env, ALSOFT_DRIVERS: 'null', LIBGL_ALWAYS_SOFTWARE: '1', XDG_DATA_HOME: `${root}/data`, XDG_CONFIG_HOME: `${root}/config` }
  });
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const actual = JSON.parse(readFileSync(report)).scenario.observations;
  assert.equal(actual.walnuts.limitedDrops, 5);
  assert.equal(actual.walnuts.found, 5);
  assert.equal(actual.walnuts.balance, 4);
  assert.equal(actual.parrot.cost, 1);
  assert.equal(actual.parrot.complete, true);
  assert.ok(actual.parrot.mail.some(mail => mail.startsWith('Island_FirstParrot')));
  assert.equal(actual.qi.length, 12);
  assert.ok(actual.qi.every(order => order.type === 'Qi'));
  const unused = actual.qi.find(order => order.id === 'QiChallenge11');
  assert.equal(unused.tags, 'NOT_IMPLEMENTED');
  assert.equal(unused.tagsMatch, false);
  assert.deepEqual(unused.definedObjectives, ['Custom']);
  assert.deepEqual(unused.objectives, []);
  const implemented = actual.qi.filter(order => order.id !== 'QiChallenge11');
  assert.equal(implemented.length, 11);
  assert.ok(implemented.every(order => order.objectives.length > 0 && order.rewards.includes('GemsReward')));
  assert.ok(actual.perfection.after > actual.perfection.before);
  assert.ok(actual.perfection.after < 1);
});

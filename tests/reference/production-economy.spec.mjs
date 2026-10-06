import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

for (const id of ['inventory-economy', 'recipes-machines']) {
  test(`original desktop records ${id} behavior from serialized prerequisites`, { timeout: 120000 }, () => {
    const root = resolve(`.port-cache/reference/users/${id}`);
    mkdirSync(`${root}/data`, { recursive: true }); mkdirSync(`${root}/config`, { recursive: true });
    const report = resolve(`.port-cache/reference/${id}.json`);
    const result = spawnSync('timeout', ['-k', '5s', '100s', 'xvfb-run', '-a', 'dotnet', 'run', '--project', 'tools/DesktopReference', '--', resolve('original'), report, id], {
      encoding: 'utf8', timeout: 110000,
      env: { ...process.env, ALSOFT_DRIVERS: 'null', LIBGL_ALWAYS_SOFTWARE: '1', XDG_DATA_HOME: `${root}/data`, XDG_CONFIG_HOME: `${root}/config` }
    });
    assert.equal(result.status, 0, result.stdout + result.stderr);
    const actual = JSON.parse(readFileSync(report)).scenario.observations;
    if (id === 'inventory-economy') {
      assert.deepEqual(actual.stacks.map(item => [item.quality, item.stack]), [[0,999],[0,11],[2,2]]);
      assert.equal(actual.chestCapacity, 36);
      assert.deepEqual(actual.prices, [35,43,52,70]);
      assert.equal(actual.shop.seedPrice, 20);
      assert.equal(actual.shop.moneySpent, 20);
      assert.equal(actual.shop.purchasedId, '(O)472');
      assert.equal(actual.shippingTotal, 157);
    } else {
      assert.equal(actual.crafting.hadIngredients, true);
      assert.equal(actual.crafting.output, '(BC)130');
      assert.equal(actual.crafting.woodConsumed, 50);
      assert.equal(actual.cooking.hadIngredients, true);
      assert.equal(actual.cooking.output, '(O)194');
      assert.equal(actual.cooking.eggConsumed, 1);
      assert.equal(actual.machine.accepted, true);
      assert.equal(actual.machine.inputConsumed, 1);
      assert.equal(actual.machine.output, '(O)306');
      assert.equal(actual.machine.minutes, 180);
      assert.equal(actual.machine.ready, true);
    }
  });
}

import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { readFileSync, mkdirSync } from 'node:fs';
import { resolve } from 'node:path';

test('supplied desktop assembly initializes original title and farm configuration data', { timeout: 120000 }, () => {
  mkdirSync('.port-cache/reference', { recursive: true });
  mkdirSync('.port-cache/reference/user-data', { recursive: true });
  mkdirSync('.port-cache/reference/user-config', { recursive: true });
  const report = resolve('.port-cache/reference/desktop-startup.json');
  const result = spawnSync('timeout', ['-k', '5s', '100s', 'xvfb-run', '-a', 'dotnet', 'run', '--project', 'tools/DesktopReference', '--', resolve('original'), report], {
    encoding: 'utf8', timeout: 110000,
    env: { ...process.env, ALSOFT_DRIVERS: 'null', LIBGL_ALWAYS_SOFTWARE: '1', XDG_DATA_HOME: resolve('.port-cache/reference/user-data'), XDG_CONFIG_HOME: resolve('.port-cache/reference/user-config') }
  });
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const actual = JSON.parse(readFileSync(report));
  assert.equal(actual.gameVersion, '1.6.15.24356');
  assert.match(actual.gameAssembly, /^Stardew Valley, Version=1\.6\.15\.24356,/);
  assert.match(actual.frameworkAssembly, /^MonoGame\.Framework,/);
  assert.equal(actual.menu, 'TitleMenu');
  assert.equal(actual.farmLayouts.length, 8);
  assert.ok(actual.farmLayouts.some(farm => farm.id === 'MeadowlandsFarm'));
  assert.ok(actual.graphics.maxTextureSize >= 2048);
});

test('supplied desktop game creates a Standard farm and writes original day-one save', { timeout: 120000 }, () => {
  mkdirSync('.port-cache/reference/user-data', { recursive: true });
  mkdirSync('.port-cache/reference/user-config', { recursive: true });
  const report = resolve('.port-cache/reference/new-game-standard.json');
  const result = spawnSync('timeout', ['-k', '5s', '100s', 'xvfb-run', '-a', 'dotnet', 'run', '--project', 'tools/DesktopReference', '--', resolve('original'), report, 'new-game-standard'], {
    encoding: 'utf8', timeout: 110000,
    env: { ...process.env, ALSOFT_DRIVERS: 'null', LIBGL_ALWAYS_SOFTWARE: '1', XDG_DATA_HOME: resolve('.port-cache/reference/user-data'), XDG_CONFIG_HOME: resolve('.port-cache/reference/user-config') }
  });
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const actual = JSON.parse(readFileSync(report));
  assert.equal(actual.scenario.id, 'new-game-standard');
  assert.equal(actual.scenario.day, 1);
  assert.equal(actual.scenario.farmId, '0');
  assert.equal(actual.scenario.map, 'Maps\\Farm');
  assert.equal(actual.scenario.farmer.customized, true);
  assert.equal(actual.scenario.afterLoad.farmId, '0');
  assert.equal(actual.scenario.afterLoad.farmer.name, actual.scenario.farmer.name);
  assert.ok(actual.scenario.buildings.some(building => building.type === 'Farmhouse'));
  assert.ok(actual.scenario.saveFiles.some(file => file.name === 'SaveGameInfo' && file.bytes > 100));
  assert.ok(actual.scenario.saveFiles.some(file => file.name !== 'SaveGameInfo' && file.bytes > 1000));
});

for (const [layout, farmId, map] of [
  ['riverland','1','Farm_Fishing'], ['forest','2','Farm_Foraging'], ['hilltop','3','Farm_Mining'],
  ['wilderness','4','Farm_Combat'], ['four-corners','5','Farm_FourCorners'], ['beach','6','Farm_Island'],
  ['meadowlands','MeadowlandsFarm','Farm_Ranching']
]) {
  test(`supplied desktop creates original ${layout} farm and save`, { timeout: 120000 }, () => {
    const id = `new-game-${layout}`;
    const report = resolve(`.port-cache/reference/${id}.json`);
    const userRoot = resolve(`.port-cache/reference/users/${id}`);
    mkdirSync(`${userRoot}/data`, { recursive: true });
    mkdirSync(`${userRoot}/config`, { recursive: true });
    const result = spawnSync('timeout', ['-k', '5s', '100s', 'xvfb-run', '-a', 'dotnet', 'run', '--project', 'tools/DesktopReference', '--', resolve('original'), report, id], {
      encoding: 'utf8', timeout: 110000,
      env: { ...process.env, ALSOFT_DRIVERS: 'null', LIBGL_ALWAYS_SOFTWARE: '1', XDG_DATA_HOME: `${userRoot}/data`, XDG_CONFIG_HOME: `${userRoot}/config` }
    });
    assert.equal(result.status, 0, result.stdout + result.stderr);
    const actual = JSON.parse(readFileSync(report)).scenario;
    assert.equal(actual.day, 1);
    assert.equal(actual.farmId, farmId);
    assert.equal(actual.map, `Maps\\${map}`);
    assert.ok(actual.buildings.some(building => building.type === 'Farmhouse'));
    assert.ok(actual.saveFiles.every(file => file.bytes > 100));
    if (layout === 'meadowlands') {
      assert.ok(actual.buildings.some(building => building.type === 'Coop'));
      assert.equal(actual.animals.length, 2, 'Original Meadowlands starter chickens must be retained');
    }
  });
}

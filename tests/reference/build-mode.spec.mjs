import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { resolve } from 'node:path';

test('production browser assembly excludes the mutable ScenarioBridge and its JS module', { timeout: 60000 }, () => {
  const root = resolve(process.env.PORT_PRODUCTION_ROOT ?? '.port-cache/production-compile');
  const assembly = resolve(process.env.PORT_BROWSER_ASSEMBLY ?? `${root}/audit/Browser.dll`);
  const types = spawnSync('dotnet', ['tool', 'run', 'ilspycmd', '-l', 'c', assembly], { encoding: 'utf8', timeout: 45000 });
  assert.equal(types.status, 0, types.stderr);
  assert.ok(!types.stdout.includes('StardewBrowser.Platform.Testing.ScenarioBridge'), 'Production must exclude ScenarioBridge at compile time');
  for (const driver of ['FarmingActions', 'EconomyActions', 'ToolUpgradeActions', 'ProductionActions', 'AdvancedActions', 'DecorationActions', 'TextSignActions', 'AnimalActions', 'FishingActions', 'FishingCastActions', 'CombatActions', 'FamilyActions', 'ProgressionActions', 'StoryActions', 'MuseumActions', 'CalendarActions', 'MinigameActions', 'IslandActions'])
    assert.ok(!types.stdout.includes(`StardewBrowser.Testing.${driver}`), 'Production must exclude mutable gameplay test drivers');
  assert.ok(existsSync(resolve(root, 'wwwroot/index.html')), 'Publish the production-mode host before checking its assets');
  assert.ok(!existsSync(resolve(root, 'wwwroot/platform/scenarios.js')), 'Production must omit the scenario module');
  assert.ok(!existsSync(resolve(root, 'wwwroot/Fixtures')), 'Production must omit reference saves');
});

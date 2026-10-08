import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

// Catches lost original renovation, shipping-order reward, or overnight
// perfection side effects across serialization. No outcome is pre-populated.
test('original renovation, Qi shipment reward and perfection survive a real night and reload', { timeout: 720000 }, () => {
  const id = 'late-game-completion';
  const root = resolve(`.port-cache/reference/users/${id}`);
  mkdirSync(`${root}/data`, { recursive: true }); mkdirSync(`${root}/config`, { recursive: true });
  const report = resolve(`.port-cache/reference/${id}.json`);
  const result = spawnSync('timeout', ['-k','5s','700s','xvfb-run','-a','dotnet','run','--project','tools/DesktopReference','--',resolve('original'),report,id], {
    encoding: 'utf8', timeout: 710000,
    env: { ...process.env, ALSOFT_DRIVERS: 'null', LIBGL_ALWAYS_SOFTWARE: '1', XDG_DATA_HOME: `${root}/data`, XDG_CONFIG_HOME: `${root}/config` }
  });
  writeFileSync(report + ".log", result.stdout + result.stderr);
  assert.equal(result.status, 0, result.stdout + result.stderr);
  assert.ok(!result.stdout.includes('Error running event script'), result.stdout);
  const actual = JSON.parse(readFileSync(report)).scenario;
  assert.equal(actual.before.waivers, 99);
  assert.equal(actual.before.perfect, false);
  assert.equal(actual.before.eternal, false);
  assert.equal(actual.before.qiGems, 0);
  assert.equal(actual.before.order.count, 0);
  assert.equal(actual.afterActions.waivers, 100);
  assert.equal(actual.afterActions.perfect, false, 'Perfection must be earned by the original overnight calculation');
  assert.equal(actual.afterActions.bedroomOpen, true);
  assert.equal(actual.afterFirstNight.day, 2);
  assert.equal(actual.afterFirstNight.qiGems, 100);
  assert.equal(actual.afterNight.day, 3);
  assert.equal(actual.afterNight.perfect, true);
  assert.equal(actual.afterNight.eternal, true);
  assert.equal(actual.afterNight.qiGems, 100);
  assert.ok(actual.afterNight.completedOrders.includes('QiChallenge2'));
  assert.deepEqual(actual.afterReload, actual.afterNight);
  assert.equal(actual.endingStarted, true);
  assert.ok(actual.endingSlideshowSprites > 100, 'Render the original ending slideshow, not just its entry flags');
  assert.equal(actual.endingSlideshowSprites, 373, 'This original prerequisite save renders all credits groups');
  assert.equal(actual.afterEnding.summitSeen, true);
  assert.equal(actual.afterEnding.summitAchievement, true);
  assert.equal(actual.afterEnding.endingSong, true);
  assert.equal(actual.afterEndingReload.day, 4);
  assert.deepEqual({ ...actual.afterEndingReload, day: 3 }, actual.afterEnding);
});

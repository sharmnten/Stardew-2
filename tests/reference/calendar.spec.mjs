import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

test('original desktop festival setup, birthday gift and movie calendar', { timeout: 180000 }, () => {
  const id = 'festivals-events-movies';
  const root = resolve(`.port-cache/reference/users/${id}`);
  mkdirSync(`${root}/data`, { recursive: true }); mkdirSync(`${root}/config`, { recursive: true });
  const report = resolve(`.port-cache/reference/${id}.json`);
  const result = spawnSync('timeout', ['-k','5s','160s','xvfb-run','-a','dotnet','run','--project','tools/DesktopReference','--',resolve('original'),report,id], {
    encoding: 'utf8', timeout: 170000,
    env: { ...process.env, ALSOFT_DRIVERS: 'null', LIBGL_ALWAYS_SOFTWARE: '1', XDG_DATA_HOME: `${root}/data`, XDG_CONFIG_HOME: `${root}/config` }
  });
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const actual = JSON.parse(readFileSync(report)).scenario.observations;
  assert.equal(actual.festival.id, 'festival_spring13');
  assert.equal(actual.festival.freeMovement, true);
  assert.ok(actual.festival.actors.includes('Lewis'));
  assert.equal(actual.calendar.length, 8);
  assert.ok(actual.calendar.every(festival => festival.loaded && festival.scriptLength > 0));
  assert.equal(actual.birthday.isBirthday, true);
  assert.equal(actual.birthday.pointsGained, 640);
  assert.equal(actual.passive.available, true);
  assert.equal(actual.movies.length, 4);
  assert.ok(actual.movies.every(movie => movie.id && movie.title));
});

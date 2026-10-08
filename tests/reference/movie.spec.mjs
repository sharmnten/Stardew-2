import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

test('original ticket invitation and full movie event award friendship and persist viewing', { timeout: 360000 }, () => {
  const id = 'movie-screening';
  const root = resolve(`.port-cache/reference/users/${id}`);
  mkdirSync(`${root}/data`, { recursive: true }); mkdirSync(`${root}/config`, { recursive: true });
  const report = resolve(`.port-cache/reference/${id}.json`);
  const result = spawnSync('timeout', ['-k','5s','340s','xvfb-run','-a','dotnet','run','--project','tools/DesktopReference','--',resolve('original'),report,id], {
    encoding: 'utf8', timeout: 350000,
    env: { ...process.env, ALSOFT_DRIVERS: 'null', LIBGL_ALWAYS_SOFTWARE: '1', XDG_DATA_HOME: `${root}/data`, XDG_CONFIG_HOME: `${root}/config` }
  });
  writeFileSync(report + '.log', result.stdout + result.stderr);
  assert.equal(result.status, 0, result.stdout + result.stderr);
  assert.ok(!result.stdout.includes('Error running event script'), result.stdout);
  const actual = JSON.parse(readFileSync(report)).scenario;
  assert.equal(actual.beforeMovie.tickets, 2);
  assert.equal(actual.afterInvitation.tickets, 1);
  assert.deepEqual(actual.afterInvitation.invited, ['Linus']);
  assert.equal(actual.screeningEvent, 'MovieTheaterScreening');
  assert.equal(actual.afterMovie.tickets, 0);
  assert.equal(actual.afterMovie.viewedWeek, 16);
  assert.equal(actual.afterMovie.guestViewedWeek, 16);
  assert.equal(actual.afterMovie.response, 'dislike');
  assert.equal(actual.afterMovie.friendship, actual.beforeMovie.friendship,
    'The original disliked-movie reaction awards zero friendship');
  assert.equal(actual.afterMovie.theaterState, 2);
  assert.equal(actual.afterMovieReload.day, 6);
  assert.equal(actual.afterMovieReload.viewedWeek, 16);
  assert.equal(actual.afterMovieReload.guestViewedWeek, 16);
  assert.equal(actual.afterMovieReload.friendship, actual.afterMovie.friendship - 2);
  assert.equal(actual.afterMovieReload.tickets, 0);
});

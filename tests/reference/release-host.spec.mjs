import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';

test('shipping storage API has no mutable failure injector', () => {
  const window = {};
  runInNewContext(readFileSync('src/Browser/wwwroot/platform/storage.js', 'utf8'), { window });
  assert.equal(window.portStorage.testing, undefined);
  assert.equal(typeof window.portStorage.commit, 'function');
});

test('audio asset errors reach a visible actionable notice without hiding save access', () => {
  const nodes = { status: {}, audioFailure: { hidden: true }, audioError: {} };
  const window = { portStorage: { updateStatus() {} }, portAudio: { status: () => ({ audioError: 'Original audio checksum mismatch: 0/3' }) } };
  const lifecycle = { status: () => ({}) };
  runInNewContext(readFileSync('src/Browser/wwwroot/platform/host.js', 'utf8'), {
    window: Object.assign(window, { addEventListener() {} }), document: { getElementById: id => nodes[id] },
    portLifecycle: lifecycle, portServices: { statusMessage: () => null }
  });
  window.portHost.status({ phase: 'ready', game: {} });
  assert.equal(nodes.audioFailure.hidden, false);
  assert.match(nodes.audioError.textContent, /checksum mismatch.*0\/3/);
  assert.doesNotMatch(nodes.status.textContent, /pending|in development/i,
    'A running original game must show player status rather than obsolete integration claims');
});

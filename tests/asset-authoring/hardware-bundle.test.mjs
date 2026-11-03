import test from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync, mkdtempSync, mkdirSync, rmSync, cpSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { refreshHardwareBundle } from '../../scripts/asset-authoring/refresh-hardware-bundle.mjs';

const prefix = 'assets/shared/hardware/';
const blob = bytes => createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex');
function fixture(t) {
  const root = mkdtempSync(join(tmpdir(), 'heimdall-bundle-test-'));
  t.after(() => rmSync(root, { recursive: true, force: true }));
  const hardware = join(root, prefix); mkdirSync(hardware, { recursive: true });
  const source = resolve('data/shared-assets');
  const device = JSON.parse(readFileSync(join(source, prefix, 'manifest.json'))).devices.find(device => device.id === 'tm-sol-r-ava-l-buttons');
  assert.ok(device);
  for (const path of [device.drawio, device.svg]) {
    mkdirSync(join(hardware, path, '..'), { recursive: true });
    cpSync(join(source, prefix, path), join(hardware, path));
  }
  writeFileSync(join(hardware, 'manifest.json'), JSON.stringify({ devices: [device] }));
  writeFileSync(join(root, 'DCS-Common-LICENSE.txt'), 'test license');
  const paths = [prefix + device.drawio, prefix + device.svg, prefix + 'manifest.json', 'DCS-Common-LICENSE.txt'];
  const provenance = { schemaVersion: 1, sourceCommit: 'a'.repeat(40), sourceRepository: 'upstream', files: paths.map(path => {
    const bytes = readFileSync(join(root, path)); return { path, originalPath: path, size: bytes.length, gitBlob: blob(bytes) };
  }) };
  writeFileSync(join(root, 'provenance.json'), JSON.stringify(provenance));
  return { root, device, provenance };
}
test('edited drawing regenerates SVG and pins all bytes while retaining upstream attribution', t => {
  const { root, device, provenance } = fixture(t);
  const path = join(root, prefix, device.drawio);
  writeFileSync(path, readFileSync(path, 'utf8').replace('</mxfile>', '<!-- reviewed edit --></mxfile>'));
  writeFileSync(join(root, prefix, device.svg), 'stale');
  assert.equal(refreshHardwareBundle(root), 1); // Drawing hash changes; SVG is regenerated to its original bytes.
  const result = JSON.parse(readFileSync(join(root, 'provenance.json')));
  assert.equal(result.sourceCommit, provenance.sourceCommit);
  for (const item of result.files) {
    const bytes = readFileSync(join(root, item.path)); assert.equal(item.gitBlob, blob(bytes)); assert.equal(item.size, bytes.length);
  }
  assert.equal(result.files[0].upstreamGitBlob, provenance.files[0].gitBlob);
  const stable = readFileSync(join(root, 'provenance.json'), 'utf8');
  assert.equal(refreshHardwareBundle(root), 0); assert.equal(readFileSync(join(root, 'provenance.json'), 'utf8'), stable);
});
test('unrelated asset changes reject before SVG or provenance writes', t => {
  const { root, device } = fixture(t); const before = readFileSync(join(root, 'provenance.json'), 'utf8');
  writeFileSync(join(root, prefix, device.svg), 'stale'); writeFileSync(join(root, 'DCS-Common-LICENSE.txt'), 'changed');
  assert.throws(() => refreshHardwareBundle(root), /Unrelated bundled asset changed/);
  assert.equal(readFileSync(join(root, prefix, device.svg), 'utf8'), 'stale'); assert.equal(readFileSync(join(root, 'provenance.json'), 'utf8'), before);
});
test('malformed drawing cannot partially promote files or hashes', t => {
  const { root, device } = fixture(t); const before = readFileSync(join(root, 'provenance.json'), 'utf8');
  writeFileSync(join(root, prefix, device.drawio), '<broken>'); writeFileSync(join(root, prefix, device.svg), 'stale');
  assert.throws(() => refreshHardwareBundle(root));
  assert.equal(readFileSync(join(root, prefix, device.svg), 'utf8'), 'stale'); assert.equal(readFileSync(join(root, 'provenance.json'), 'utf8'), before);
});
test('CLI rejects unknown options', () => {
  const result = spawnSync(process.execPath, ['scripts/asset-authoring/refresh-hardware-bundle.mjs', '--unknown'], { encoding: 'utf8' });
  assert.notEqual(result.status, 0); assert.match(result.stderr, /Usage:/);
});

test('stale SVG alone is repaired even when regenerated hashes already match provenance', t => {
  const { root, device } = fixture(t);
  const path = join(root, prefix, device.svg); const expected = readFileSync(path, 'utf8');
  writeFileSync(path, 'stale'); assert.equal(refreshHardwareBundle(root), 0);
  assert.equal(readFileSync(path, 'utf8'), expected);
});
test('invalid and duplicate provenance paths fail before promotion', t => {
  const { root, provenance } = fixture(t);
  const file = join(root, 'provenance.json');
  for (const path of ['../outside', 'C:/outside', 'assets\\bad', 'assets//bad']) {
    writeFileSync(file, JSON.stringify({ ...provenance, files: [{ ...provenance.files[0], path }] }));
    assert.throws(() => refreshHardwareBundle(root), /Unsafe bundle path/);
  }
  writeFileSync(file, JSON.stringify({ ...provenance, files: [provenance.files[0], provenance.files[0]] }));
  assert.throws(() => refreshHardwareBundle(root), /Duplicate provenance path/);
  writeFileSync(file, JSON.stringify({ ...provenance, schemaVersion: 2 }));
  assert.throws(() => refreshHardwareBundle(root), /Invalid bundle provenance/);
  writeFileSync(file, JSON.stringify({ ...provenance, files: provenance.files.slice(1) }));
  assert.throws(() => refreshHardwareBundle(root), /lacks provenance/);
});
test('CLI accepts an explicit bundle and a second edit preserves the original upstream hash', t => {
  const { root, device, provenance } = fixture(t); const path = join(root, prefix, device.drawio);
  for (const comment of ['first', 'second']) {
    writeFileSync(path, readFileSync(path, 'utf8').replace('</mxfile>', `<!-- ${comment} --></mxfile>`));
    const result = spawnSync(process.execPath, ['scripts/asset-authoring/refresh-hardware-bundle.mjs', '--bundle-root', root], { encoding: 'utf8' });
    assert.equal(result.status, 0, result.stderr);
    assert.equal(JSON.parse(readFileSync(join(root, 'provenance.json'))).files[0].upstreamGitBlob, provenance.files[0].gitBlob);
  }
});

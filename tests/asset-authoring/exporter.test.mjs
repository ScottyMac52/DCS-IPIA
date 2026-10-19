import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync, mkdtempSync, mkdirSync, rmSync, existsSync } from 'node:fs';
import { resolve, join } from 'node:path';
import { tmpdir } from 'node:os';
import { deflateRawSync } from 'node:zlib';
import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { renderDrawioSvg } from '../../scripts/asset-authoring/drawio-svg.mjs';
import { decodeDrawioGraph } from '../../scripts/asset-authoring/drawio-source.mjs';
import { exportHardware } from '../../scripts/asset-authoring/hardware-export.mjs';

const hardwareRoot = resolve('data/shared-assets/assets/shared/hardware');
const manifest = JSON.parse(readFileSync(join(hardwareRoot, 'manifest.json'), 'utf8'));
const devices = manifest.devices.filter(x => x.drawio && x.deterministicExport !== false);
const device = devices.find(x => x.id === 'moza-ab9');
const xml = readFileSync(join(hardwareRoot, device.drawio), 'utf8');
const render = source => renderDrawioSvg(device, source);
const cli = (...args) => spawnSync(process.execPath, ['scripts/asset-authoring/export-hardware.mjs', ...args], { encoding: 'utf8' });

// Real pinned graphics prove all layout/image bytes, not just selected labels.
test('all bundled outputs are exact and repeatable through compressed and native inputs', () => {
  assert.equal(devices.length, 16);
  const report = readFileSync('docs/migration/drawio-export-parity.md', 'utf8');
  for (const d of devices) {
    const source = readFileSync(join(hardwareRoot, d.drawio), 'utf8');
    const graph = decodeDrawioGraph(source);
    const compressed = `<mxfile><diagram>${deflateRawSync(encodeURIComponent(graph)).toString('base64')}</diagram></mxfile>`;
    const expected = readFileSync(join(hardwareRoot, d.svg), 'utf8').replaceAll('\r\n', '\n');
    assert.equal(renderDrawioSvg(d, source), expected, d.id);
    assert.equal(renderDrawioSvg(d, compressed), expected, d.id);
    assert.equal(renderDrawioSvg(d, source), renderDrawioSvg(d, source));
    assert.ok(report.includes(createHash('sha256').update(readFileSync(join(hardwareRoot, d.drawio))).digest('hex')));
    assert.ok(report.includes(createHash('sha256').update(readFileSync(join(hardwareRoot, d.svg))).digest('hex')));
  }
});

test('UserObject labels retain identity and XML escapes; optional watermarks do not alter defaults', () => {
  const wrapped = xml.replace(/<mxCell id="(label-[^"]+)" value="([^"]*)"([^>]*)>([\s\S]*?)<\/mxCell>/,
    '<UserObject id="$1" label="$2"><mxCell$3>$4</mxCell></UserObject>');
  assert.equal(render(wrapped), render(xml));
  const wrappedEdge = wrapped.replace(/<mxCell id="(connector-[^"]+)"([^>]*)>([\s\S]*?)<\/mxCell>/,
    '<UserObject id="$1"><mxCell$2>$3</mxCell></UserObject>');
  assert.equal(render(wrappedEdge), render(xml));
  const numbered = wrapped.replace(/style="rounded=1;/, 'style="rounded=1;buttonNumber=12;');
  assert.doesNotMatch(render(numbered), /btn-watermark/);
  assert.match(renderDrawioSvg(device, numbered, { watermarks: true }), /data-button="12"/);
});

test('rejects invalid geometry, duplicate identities, incomplete images and multiple diagrams', () => {
  assert.throws(() => render(xml.replace(/pageWidth="[^"]+"/, 'pageWidth="NaN"')), /geometry|canvas/i);
  assert.throws(() => render(xml.replace('id="footer"', 'id="canvas"')), /duplicate/i);
  assert.throws(() => render(xml.replace(/image=data:image\/(?:png|jpeg),[^;]+;/, 'image=data:image/png,AAAA;')), /image/i);
  assert.throws(() => render(xml.replace('</mxfile>', '<diagram><mxGraphModel /></diagram></mxfile>')), /one diagram/i);
  assert.throws(() => render(xml.replace(/id="connector-/, 'id="bad-')), /stable connector/i);
  assert.throws(() => render(xml.replace(/source="label-/, 'source="bad-')), /must connect/i);
  assert.throws(() => render(xml.replace(/id="anchor-/, 'id="missing-')), /missing source or target/i);
  assert.throws(() => render(xml.replace(/id="hardware-image-/, 'id="missing-image-')), /incomplete/i);
  assert.throws(() => render(xml.replace(/(<mxGeometry[^>]*width=")[^"]+/, '$1-2')), /geometry/i);
});

test('staging is deterministic, check-only is read-only and existing output is protected', () => {
  const root = mkdtempSync(join(tmpdir(), 'ipia-svg-'));
  try {
    assert.equal(exportHardware({ hardwareRoot }).length, 16);
    for (const name of ['first', 'second']) assert.equal(exportHardware({ hardwareRoot, outputRoot: join(root, name) }).length, 16);
    for (const d of devices) assert.deepEqual(readFileSync(join(root, 'first', d.svg)), readFileSync(join(root, 'second', d.svg)));
    assert.throws(() => exportHardware({ hardwareRoot, outputRoot: join(root, 'first') }), /already exists/);
    assert.equal(cli('--check').status, 0);
    assert.equal(cli('--hardware-root', hardwareRoot, '--output', join(root, 'cli')).status, 0);
    assert.notEqual(cli('--output', join(root, 'cli')).status, 0);
    for (const args of [['--check', '--output', 'unused'], ['--bad'], ['--output']]) assert.notEqual(cli(...args).status, 0);
  } finally { rmSync(root, { recursive: true, force: true }); }
});

test('unsafe, duplicate and broken sources fail before any staging output is created', () => {
  const root = mkdtempSync(join(tmpdir(), 'ipia-svg-invalid-'));
  const source = join(root, 'hardware');
  const outputRoot = join(root, 'output');
  mkdirSync(join(source, 'drawio'), { recursive: true });
  const valid = { ...device, drawio: 'drawio/device.drawio', svg: 'svg/device.svg' };
  writeFileSync(join(source, valid.drawio), xml);
  const run = entries => { writeFileSync(join(source, 'manifest.json'), JSON.stringify({ devices: entries })); return exportHardware({ hardwareRoot: source, outputRoot }); };
  try {
    for (const path of ['../escape.svg', '/absolute.svg', 'C:bad.svg', 'svg\\bad.svg'])
      assert.throws(() => run([{ ...valid, svg: path }]), /Unsafe/);
    assert.throws(() => run([valid, { ...valid, svg: 'SVG/DEVICE.SVG' }]), /Duplicate/);
    assert.throws(() => run([]), /no exportable/);
    writeFileSync(join(source, 'manifest.json'), '{}');
    assert.throws(() => exportHardware({ hardwareRoot: source, outputRoot }), /requires devices/);
    writeFileSync(join(source, valid.drawio), '<mxfile />');
    assert.throws(() => run([valid]), /diagram/);
    assert.equal(existsSync(outputRoot), false);
    writeFileSync(join(source, valid.drawio), xml);
    writeFileSync(join(source, 'export-config.json'), '{"includeButtonNumberWatermarks":true}');
    assert.equal(run([valid]).length, 1);
    assert.throws(() => exportHardware({ hardwareRoot: source }), /ENOENT/);
    mkdirSync(join(source, 'svg'));
    writeFileSync(join(source, valid.svg), 'stale');
    assert.throws(() => exportHardware({ hardwareRoot: source }), /stale/);
  } finally { rmSync(root, { recursive: true, force: true }); }
});

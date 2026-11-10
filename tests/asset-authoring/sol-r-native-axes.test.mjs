import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve, dirname } from 'node:path';
import { renderDrawioSvg } from '../../scripts/asset-authoring/drawio-svg.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../../data/shared-assets/assets/shared/hardware');
const expected = ['JOY_X', 'JOY_Y', 'JOY_Z', 'JOY_RX', 'JOY_RY', 'JOY_RZ', 'JOY_SLIDER1', 'JOY_SLIDER2'].sort();

for (const mode of ['buttons', 'pov']) {
  test(`right SOL-R ${mode} layout retains all eight verified native axes without inventing axis 8`, () => {
    const id = `tm-sol-r-ava-r-${mode}`;
    const lua = readFileSync(resolve(root, 'lua', `${id}.lua`), 'utf8');
    const axes = [...lua.matchAll(/key = "([^"]+)", type = "axis"/g)].map(match => match[1]).sort();
    assert.deepEqual(axes, expected);
    const drawio = readFileSync(resolve(root, 'drawio', `${id}.drawio`), 'utf8');
    const labels = [...drawio.matchAll(/id="label-([^"]+)"/g)].map(match => match[1]);
    assert.equal(labels.length, 37);
    for (const key of expected) assert.ok(labels.includes(key), `${id}: missing ${key} label`);
    assert.match(drawio, /DRIVER-EXPOSED AXES/);
    assert.doesNotMatch(drawio, /id="label-JOY_(?:AXIS8|AXIS9|SLIDER3)"/);
    const svg = readFileSync(resolve(root, 'svg', `${id}.svg`), 'utf8');
    assert.equal(svg, renderDrawioSvg({ id }, drawio));
    const declared = [...lua.matchAll(/\{ id = "([^"]+)"/g)].map(match => match[1]).sort();
    assert.deepEqual(labels.sort(), declared);
    for (const key of expected) assert.ok(svg.includes(`id="lbl-${key}"`), `${id}: missing exported ${key}`);
  });
}

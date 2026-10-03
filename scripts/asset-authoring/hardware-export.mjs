import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { dirname, join, resolve, relative, isAbsolute } from 'node:path';
import { renderDrawioSvg } from './drawio-svg.mjs';

function assetPath(root, path) {
  if (typeof path !== 'string' || !path || path.includes('\\') || path.includes(':')
      || path.split('/').some(part => !part || part === '.' || part === '..')) {
    throw new Error(`Unsafe hardware asset path: ${path}`);
  }
  const target = resolve(root, path);
  const local = relative(root, target);
  if (isAbsolute(local) || local.startsWith('..')) throw new Error(`Unsafe hardware asset path: ${path}`);
  return target;
}

// Authoring tool only: validates and renders every source before creating output.
export function exportHardware({ hardwareRoot, outputRoot, watermarks = false }) {
  const manifest = JSON.parse(readFileSync(join(hardwareRoot, 'manifest.json'), 'utf8'));
  const configPath = join(hardwareRoot, 'export-config.json');
  const config = existsSync(configPath) ? JSON.parse(readFileSync(configPath, 'utf8')) : {};
  if (!Array.isArray(manifest.devices)) throw new Error('Hardware manifest requires devices.');
  const outputs = new Map();
  for (const device of manifest.devices.filter(entry => entry.drawio && entry.deterministicExport !== false)) {
    const source = assetPath(hardwareRoot, device.drawio);
    assetPath(hardwareRoot, device.svg);
    if (outputs.has(device.svg.toLowerCase())) throw new Error(`Duplicate hardware SVG path: ${device.svg}`);
    const svg = renderDrawioSvg(device, readFileSync(source, 'utf8'), {
      watermarks: watermarks || config.includeButtonNumberWatermarks === true,
    });
    outputs.set(device.svg.toLowerCase(), { path: device.svg, svg });
  }
  if (!outputs.size) throw new Error('Hardware manifest has no exportable draw.io devices.');
  if (outputRoot) {
    if (existsSync(outputRoot)) throw new Error('Output folder already exists; choose a new folder.');
    for (const { path, svg } of outputs.values()) {
      const target = assetPath(outputRoot, path);
      mkdirSync(dirname(target), { recursive: true });
      writeFileSync(target, svg, 'utf8');
    }
  } else {
    const stale = [...outputs.values()].filter(({ path, svg }) =>
      readFileSync(assetPath(hardwareRoot, path), 'utf8').replaceAll('\r\n', '\n') !== svg.replaceAll('\r\n', '\n'));
    if (stale.length) throw new Error(`Published draw.io SVG output is stale: ${stale.map(x => x.path).join(', ')}`);
  }
  return [...outputs.values()].map(({ path }) => path);
}

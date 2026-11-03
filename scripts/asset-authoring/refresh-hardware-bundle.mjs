import { createHash, randomUUID } from 'node:crypto';
import { readFileSync, writeFileSync, mkdtempSync, rmSync, renameSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve, relative, isAbsolute } from 'node:path';
import { fileURLToPath } from 'node:url';
import { exportHardware } from './hardware-export.mjs';

const prefix = 'assets/shared/hardware/';
const blob = bytes => createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex');
function asset(root, path) {
  if (typeof path !== 'string' || !path || path.includes('\\') || path.includes(':')
      || path.split('/').some(part => !part || part === '.' || part === '..')) throw new Error(`Unsafe bundle path: ${path}`);
  const target = resolve(root, path);
  if (isAbsolute(relative(root, target)) || relative(root, target).startsWith('..')) throw new Error(`Unsafe bundle path: ${path}`);
  return target;
}

// Explicit developer operation: render everything and verify unrelated assets before promoting any bytes.
export function refreshHardwareBundle(bundleRoot) {
  const root = resolve(bundleRoot);
  const provenancePath = join(root, 'provenance.json');
  const original = readFileSync(provenancePath);
  const provenance = JSON.parse(original);
  if (provenance.schemaVersion !== 1 || !/^[a-f0-9]{40}$/.test(provenance.sourceCommit)
      || !Array.isArray(provenance.files) || !provenance.files.length) throw new Error('Invalid bundle provenance.');
  const hardwareRoot = join(root, prefix);
  const manifest = JSON.parse(readFileSync(join(hardwareRoot, 'manifest.json'), 'utf8'));
  if (!Array.isArray(manifest.devices)) throw new Error('Hardware manifest requires devices.');
  const devices = manifest.devices.filter(device => device.drawio && device.deterministicExport !== false);
  const allowed = new Set(devices.flatMap(device => [prefix + device.drawio, prefix + device.svg]));
  const indexed = new Map();
  for (const item of provenance.files) {
    asset(root, item.path);
    if (indexed.has(item.path)) throw new Error(`Duplicate provenance path: ${item.path}`);
    indexed.set(item.path, item);
    const bytes = readFileSync(asset(root, item.path));
    if (bytes.length !== item.size || blob(bytes) !== item.gitBlob) {
      if (!allowed.has(item.path)) throw new Error(`Unrelated bundled asset changed: ${item.path}`);
    }
  }
  for (const path of allowed) if (!indexed.has(path)) throw new Error(`Hardware asset lacks provenance: ${path}`);
  const temporary = mkdtempSync(join(tmpdir(), 'heimdall-hardware-'));
  const replacements = new Map();
  const backups = new Map();
  try {
    const output = join(temporary, 'svg');
    for (const path of exportHardware({ hardwareRoot, outputRoot: output }))
      replacements.set(prefix + path, readFileSync(asset(output, path)));
    let changed = 0;
    for (const path of allowed) {
      const item = indexed.get(path);
      const bytes = replacements.get(path) ?? readFileSync(asset(root, path));
      const hash = blob(bytes);
      if (hash !== item.gitBlob || bytes.length !== item.size) {
        item.upstreamGitBlob ??= item.gitBlob;
        item.gitBlob = hash; item.size = bytes.length; changed++;
      }
    }
    if (!changed && [...replacements].every(([path, bytes]) => bytes.equals(readFileSync(asset(root, path))))) return 0;
    for (const [path, bytes] of replacements) {
      const target = asset(root, path); backups.set(target, readFileSync(target)); writeFileSync(target, bytes);
    }
    // Preserve the upstream commit and original blob IDs; only reviewed hardware entries are refreshed.
    const staged = join(root, `provenance.json.${randomUUID()}.tmp`);
    try {
      writeFileSync(staged, JSON.stringify(provenance, null, 2) + '\n', { flag: 'wx' });
      renameSync(staged, provenancePath);
    } finally { rmSync(staged, { force: true }); }
    return changed;
  } catch (error) {
    for (const [path, bytes] of backups) writeFileSync(path, bytes);
    throw error;
  } finally { rmSync(temporary, { recursive: true, force: true }); }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const args = process.argv.slice(2);
  if (args.length && (args.length !== 2 || args[0] !== '--bundle-root')) throw new Error('Usage: refresh-hardware-bundle.mjs [--bundle-root PATH]');
  const root = args[1] ?? resolve(dirname(fileURLToPath(import.meta.url)), '../../data/shared-assets');
  console.log(`Refreshed ${refreshHardwareBundle(root)} hardware provenance entries; upstream attribution retained. Rebuild HEIMDALL to copy the bundle.`);
}

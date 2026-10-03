import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { exportHardware } from './hardware-export.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const args = process.argv.slice(2);
let hardwareRoot = resolve(root, 'data/shared-assets/assets/shared/hardware');
let outputRoot;
let watermarks = false;
let check = false;
for (let i = 0; i < args.length; i++) {
  if (args[i] === '--check') check = true;
  else if (args[i] === '--watermarks') watermarks = true;
  else if (args[i] === '--hardware-root' || args[i] === '--output') {
    const flag = args[i];
    if (!args[i + 1] || args[i + 1].startsWith('--')) throw new Error(`Missing value for ${flag}`);
    const path = resolve(args[++i]);
    if (flag === '--hardware-root') hardwareRoot = path;
    else outputRoot = path;
  } else throw new Error(`Unknown argument: ${args[i]}`);
}
if (check && outputRoot) throw new Error('Use --check or --output, not both.');
const paths = exportHardware({ hardwareRoot, outputRoot, watermarks });
console.log(`${outputRoot ? 'Exported' : 'Verified'} ${paths.length} draw.io hardware SVGs${outputRoot ? ` to ${outputRoot}` : ''}.`);

"""Verify the committed Flight Patch resource/installer contract using only Python's standard library."""
import hashlib
import json
from pathlib import Path
import struct
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'src/Dcs.Ipia.App/Branding'
manifest = json.loads((ASSETS / 'manifest.json').read_text())
for name, expected in manifest['sha256'].items():
    assert hashlib.sha256((ROOT / name).read_bytes()).hexdigest() == expected, f'Stale Flight Patch asset/input: {name}; regenerate assets'

ico = (ASSETS / 'flight-patch.ico').read_bytes()
reserved, kind, count = struct.unpack_from('<HHH', ico)
assert (reserved, kind, count) == (0, 1, 9), 'Invalid multi-resolution ICO directory'
sizes = []
for index in range(count):
    width, height, _, _, _, _, length, offset = struct.unpack_from('<BBBBHHII', ico, 6 + index * 16)
    sizes.append(width or 256)
    assert width == height and offset >= 6 + count * 16 and offset + length <= len(ico), 'Invalid ICO frame'
assert sorted(sizes) == [16, 20, 24, 32, 40, 48, 64, 128, 256], 'Missing Windows icon sizes'
for filename, expected in [('flight-patch.png', (512, 512)), ('flight-patch-banner.png', (1200, 300))]:
    data = (ASSETS / filename).read_bytes()
    assert data[:8] == b'\x89PNG\r\n\x1a\n' and struct.unpack_from('>II', data, 16) == expected, f'Invalid {filename}'
for filename, expected in [('setup-sidebar.bmp', (164, 314)), ('setup-small.bmp', (55, 55))]:
    data = (ASSETS / filename).read_bytes()
    assert data[:2] == b'BM' and struct.unpack_from('<ii', data, 18) == expected and struct.unpack_from('<H', data, 28)[0] == 24, f'Invalid {filename}'
for path in ASSETS.glob('*.svg'):
    tree = ET.parse(path)
    assert not any(node.tag.rsplit('}', 1)[-1] in {'text', 'image', 'script'} for node in tree.iter()), f'Non-self-contained vector master: {path.name}'
project = ET.parse(ROOT / 'src/Dcs.Ipia.App/Dcs.Ipia.App.csproj')
assert project.findtext('.//ApplicationIcon') == 'Branding/flight-patch.ico', 'Application executable icon missing'
resources = {node.attrib['Include'] for node in project.findall('.//Resource')}
assert {'Branding/*.ico', 'Branding/*.png'} <= resources, 'WPF brand resources missing'
setup = (ROOT / 'installer/installer.iss').read_text()
for setting, filename in [('SetupIconFile', 'flight-patch.ico'), ('WizardImageFile', 'setup-sidebar.bmp'), ('WizardSmallImageFile', 'setup-small.bmp')]:
    assert f'{setting}=..\\src\\Dcs.Ipia.App\\Branding\\{filename}' in setup, f'Installer {setting} missing'
assert 'UninstallDisplayIcon={app}\\{#AppExeName}' in setup, 'Installed application icon missing'
assert 'flight-patch-PROVENANCE.md' in (ROOT / 'scripts/Write-DistributionNotices.ps1').read_text(), 'Brand provenance not shipped'
print('Flight Patch vector/raster resources, nine icon sizes, executable and installer wiring verified.')

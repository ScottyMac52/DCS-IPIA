"""Verify the committed HEIMDALL resource/installer contract using only Python's standard library."""
import hashlib
import json
import subprocess
import sys
from pathlib import Path
import struct
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'src/Heimdall.App/Branding'
manifest = json.loads((ASSETS / 'manifest.json').read_text())
for name, expected in manifest['sha256'].items():
    assert hashlib.sha256((ROOT / name).read_bytes()).hexdigest() == expected, f'Stale HEIMDALL asset/input: {name}; regenerate assets'

for filename in ['heimdall.ico', 'heimdall-monochrome.ico']:
    ico = (ASSETS / filename).read_bytes()
    reserved, kind, count = struct.unpack_from('<HHH', ico)
    assert (reserved, kind, count) == (0, 1, 9), 'Invalid multi-resolution ICO directory'
    sizes = []
    for index in range(count):
        width, height, _, _, _, _, length, offset = struct.unpack_from('<BBBBHHII', ico, 6 + index * 16)
        sizes.append(width or 256)
        assert width == height and offset >= 6 + count * 16 and offset + length <= len(ico), 'Invalid ICO frame'
    assert sorted(sizes) == [16, 20, 24, 32, 40, 48, 64, 128, 256], 'Missing Windows icon sizes'
for filename, expected in [('heimdall-monochrome.png', (512, 512)), ('heimdall.png', (512, 512)), ('heimdall-banner.png', (1200, 300))]:
    data = (ASSETS / filename).read_bytes()
    assert data[:8] == b'\x89PNG\r\n\x1a\n' and struct.unpack_from('>II', data, 16) == expected, f'Invalid {filename}'
for filename, expected in [('setup-sidebar.bmp', (164, 314)), ('setup-small.bmp', (55, 55))]:
    data = (ASSETS / filename).read_bytes()
    assert data[:2] == b'BM' and struct.unpack_from('<ii', data, 18) == expected and struct.unpack_from('<H', data, 28)[0] == 24, f'Invalid {filename}'
project = ET.parse(ROOT / 'src/Heimdall.App/Heimdall.App.csproj')
assert project.findtext('.//ApplicationIcon') == 'Branding/heimdall.ico', 'Application executable icon missing'
resources = {node.attrib['Include'] for node in project.findall('.//Resource')}
assert {'Branding/*.ico', 'Branding/*.png'} <= resources, 'WPF brand resources missing'
setup = (ROOT / 'installer/installer.iss').read_text()
for setting, filename in [('SetupIconFile', 'heimdall.ico'), ('WizardImageFile', 'setup-sidebar.bmp'), ('WizardSmallImageFile', 'setup-small.bmp')]:
    assert f'{setting}=..\\src\\Heimdall.App\\Branding\\{filename}' in setup, f'Installer {setting} missing'
assert 'UninstallDisplayIcon={app}\\{#AppExeName}' in setup, 'Installed application icon missing'
assert 'heimdall-PROVENANCE.md' in (ROOT / 'scripts/Write-DistributionNotices.ps1').read_text(), 'Brand provenance not shipped'
print('HEIMDALL raster resources, nine icon sizes, executable and installer wiring verified.')

assert project.findtext('.//AssemblyName') == 'Heimdall', 'Executable assembly must match resource URIs'
assert '#define AppExeName "Heimdall.exe"' in setup
assert 'UsePreviousAppDir=yes' in setup
for path in (ROOT / 'src').rglob('*'):
    if path.suffix in {'.cs', '.xaml'}:
        assert 'Heimdall.App;component/' not in path.read_text(), f'Stale pack URI: {path}'

subprocess.run([sys.executable, '-m', 'unittest', 'discover', '-s', str(ROOT / 'tests/ci'), '-p', 'test_heimdall_icons.py'], check=True)

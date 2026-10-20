"""Package approved raster masters. Requires Pillow; normal builds use committed assets."""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageOps

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'src/Dcs.Ipia.App/Branding'
SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]


def fitted(source, size):
    canvas = Image.new('RGB', size, '#071522')
    art = ImageOps.contain(source.convert('RGB'), size, Image.Resampling.LANCZOS)
    canvas.paste(art, ((size[0] - art.width) // 2, (size[1] - art.height) // 2))
    return canvas


def main():
    for stem, master in [('heimdall', 'heimdall-icon'), ('heimdall-monochrome', 'heimdall-monochrome')]:
        source = Image.open(ASSETS / 'Masters' / f'{master}.png')
        fitted(source, (512, 512)).save(ASSETS / f'{stem}.png')
        fitted(source, (256, 256)).save(ASSETS / f'{stem}.ico', sizes=[(n, n) for n in SIZES])
    logo = Image.open(ASSETS / 'Masters/heimdall-logo.png')
    logo.save(ASSETS / 'heimdall-logo.png')
    fitted(Image.open(ASSETS / 'Masters/heimdall-banner.png'), (1200, 300)).save(ASSETS / 'heimdall-banner.png')
    fitted(logo, (164, 314)).save(ASSETS / 'setup-sidebar.bmp')
    fitted(Image.open(ASSETS / 'heimdall.png'), (55, 55)).save(ASSETS / 'setup-small.bmp')
    paths = [Path(__file__)] + sorted((ASSETS / 'Masters').glob('*.png'))
    paths += sorted(ASSETS.glob('*.png')) + sorted(ASSETS.glob('*.ico')) + sorted(ASSETS.glob('*.bmp'))
    manifest = {'schemaVersion': 1, 'sha256': {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}}
    (ASSETS / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')


if __name__ == '__main__':
    main()

"""Package approved raster masters. Requires Pillow; normal builds use committed assets."""
import hashlib
import json
import struct
from pathlib import Path
from PIL import Image, ImageOps

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'src/Heimdall.App/Branding'
SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]


def fitted(source, size):
    canvas = Image.new('RGB', size, '#071522')
    art = ImageOps.contain(source.convert('RGB'), size, Image.Resampling.LANCZOS)
    canvas.paste(art, ((size[0] - art.width) // 2, (size[1] - art.height) // 2))
    return canvas


def write_windows_icon(source, destination):
    """Write explicit 32-bit BGRA DIBs and aligned AND masks for Windows/WPF."""
    frames = []
    for size in SIZES:
        art = fitted(source, (size, size)).convert('RGBA')
        pixels = art.tobytes('raw', 'BGRA', 0, -1)
        mask = bytes(((size + 31) // 32) * 4 * size)
        # BI_RGB permits a zero image-size field; ICO directory entries bound each payload.
        header = struct.pack('<IiiHHIIiiII', 40, size, size * 2, 1, 32, 0,
                             0, 0, 0, 0, 0)
        frames.append((size, header + pixels + mask))
    directory = bytearray(struct.pack('<HHH', 0, 1, len(frames)))
    offset = 6 + 16 * len(frames)
    for size, payload in frames:
        directory.extend(struct.pack('<BBBBHHII', size % 256, size % 256,
                                     0, 0, 1, 32, len(payload), offset))
        offset += len(payload)
    destination.write_bytes(directory + b''.join(payload for _, payload in frames))


def main():
    for stem, master in [('heimdall', 'heimdall-icon'), ('heimdall-monochrome', 'heimdall-monochrome')]:
        source = Image.open(ASSETS / 'Masters' / f'{master}.png')
        fitted(source, (512, 512)).save(ASSETS / f'{stem}.png')
        write_windows_icon(source, ASSETS / f'{stem}.ico')
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

#!/usr/bin/env python3
"""Regenerate Flight Patch vector masters and Windows assets (Inkscape, Pillow, fontTools)."""
from pathlib import Path
import hashlib
import json
import io
import subprocess
import tempfile
from fontTools.ttLib import TTFont
from fontTools.pens.svgPathPen import SVGPathPen
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'src/Dcs.Ipia.App/Branding'
FONT = TTFont(ROOT / 'src/Dcs.Ipia.Rendering/Fonts/DejaVuSans-IPIA.ttf')
GLYPHS = FONT.getGlyphSet()
CMAP = FONT.getBestCmap()
GOLD, CREAM, NAVY = '#F5B83D', '#F5E9CE', '#14283E'


def text(value, x, y, size, color=CREAM, center=False):
    """Outline bundled-font lettering so SVG consumers need no installed font."""
    scale = size / FONT['head'].unitsPerEm
    width = sum(GLYPHS[CMAP[ord(c)]].width for c in value) * scale
    if center:
        x -= width / 2
    result = []
    for char in value:
        glyph = GLYPHS[CMAP[ord(char)]]
        pen = SVGPathPen(GLYPHS)
        glyph.draw(pen)
        result.append(f'<path fill="{color}" transform="translate({x:g},{y:g}) scale({scale:g},-{scale:g})" d="{pen.getCommands()}"/>')
        x += glyph.width * scale
    return ''.join(result)


def svg(body, width, height, label):
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}" role="img" aria-label="{label}"><title>{label}</title>{body}</svg>\n'


def emblem(compact=False):
    wings = '''<path d="M224 239 L37 172 Q39 201 64 214 L186 258 L199 281 L77 239 Q82 261 104 270 L207 303 L220 318 L119 285 Q129 307 154 316 L228 338 Z"/>'''
    body = f'<circle cx="256" cy="256" r="242" fill="{NAVY}" stroke="#091827" stroke-width="9"/><circle cx="256" cy="256" r="228" fill="none" stroke="{GOLD}" stroke-width="9"/>'
    if not compact:
        body += f'<circle cx="256" cy="256" r="212" fill="none" stroke="{GOLD}" stroke-width="1.5" stroke-dasharray="3 8" opacity=".6"/>'
        body += f'<path d="M117 163 Q190 73 333 123" fill="none" stroke="{CREAM}" stroke-width="4" opacity=".65"/>'
        body += f'<path fill="{CREAM}" d="M267 104 L294 99 L318 72 L328 72 L316 99 L358 111 L359 118 L312 113 L306 132 L298 132 L297 112 L272 112 Z"/>'
    body += f'<g fill="{GOLD}" stroke="{NAVY}" stroke-width="5" stroke-linejoin="round">{wings}<g transform="translate(512 0) scale(-1 1)">{wings}</g></g>'
    joystick = f'''<path fill="{CREAM}" stroke="{NAVY}" stroke-width="6" d="M234 170 Q219 171 215 186 Q211 198 223 211 L235 222 L232 278 Q230 299 219 309 L219 319 Q256 330 293 319 L293 307 Q273 301 274 280 L270 219 Q288 208 287 193 Q286 172 267 170 Z"/>
      <ellipse cx="259" cy="192" rx="14" ry="11" fill="{NAVY}"/><ellipse cx="262" cy="190" rx="6" ry="4" fill="{GOLD}"/>
      <path fill="{CREAM}" stroke="{NAVY}" stroke-width="6" d="M213 315 Q256 331 300 315 L311 335 Q256 355 201 335 Z"/>'''
    body += joystick
    if compact:
        # Expand the essential controller/wing silhouette in the small icon; omit tiny lettering and aircraft.
        body = body.replace('r="228"', 'r="224"').replace('stroke-width="9"', 'stroke-width="14"')
    else:
        body += text('DCS-IPIA', 256, 390, 46, center=True)
        body += f'<path fill="{GOLD}" d="M256 410 L262 428 L281 428 L266 440 L272 458 L256 447 L240 458 L246 440 L231 428 L250 428 Z"/>'
    return body


def render(source, width, height):
    with tempfile.TemporaryDirectory() as folder:
        path = Path(folder) / 'source.svg'
        target = Path(folder) / 'render.png'
        path.write_text(source, encoding='utf-8')
        subprocess.run(['inkscape', str(path), '--export-type=png', f'--export-filename={target}',
                        f'--export-width={width}', f'--export-height={height}'], check=True, capture_output=True)
        return Image.open(io.BytesIO(target.read_bytes())).convert('RGBA')


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    patch = svg(emblem(), 512, 512, 'DCS-IPIA Flight Patch')
    compact = svg(emblem(True), 512, 512, 'DCS-IPIA compact Flight Patch')
    (OUT / 'flight-patch.svg').write_text(patch)
    (OUT / 'flight-patch-compact.svg').write_text(compact)
    render(patch, 512, 512).save(OUT / 'flight-patch.png')
    frames = [render(compact if size <= 48 else patch, size, size) for size in (16, 20, 24, 32, 40, 48, 64, 128, 256)]
    frames[-1].save(OUT / 'flight-patch.ico', format='ICO', sizes=[frame.size for frame in frames], append_images=frames[:-1])
    banner_body = f'''<defs><linearGradient id="sky" x2="1" y2="1"><stop stop-color="{NAVY}"/><stop offset="1" stop-color="#29445D"/></linearGradient></defs>
      <path fill="url(#sky)" d="M0 0H1200V300H0Z"/><circle cx="1030" cy="168" r="118" fill="{GOLD}" opacity=".85"/>
      <path fill="#30465B" d="M620 300 L806 143 L875 204 L929 161 L1064 238 L1130 178 L1200 230 V300Z"/>
      <path fill="#1B3249" d="M360 300 L680 218 L757 249 L875 203 L989 260 L1073 230 L1200 267 V300Z"/>
      <path fill="#0C1E30" d="M0 274 L220 254 L400 275 L620 260 L800 284 L1001 263 L1200 288 V300H0Z"/>
      <path d="M790 164 Q930 106 1088 87" fill="none" stroke="{CREAM}" stroke-width="2" opacity=".7"/>
      <path fill="{NAVY}" d="M1052 96 L1079 85 L1094 62 L1102 61 L1096 82 L1126 84 L1132 90 L1095 92 L1087 107 L1080 108 L1081 94 L1056 101Z"/>
      <g transform="translate(28 22) scale(.5)">{emblem()}</g>'''
    banner_body += text('DCS-IPIA', 322, 137, 66) + text('INPUTS  /  PROFILES  /  AIRCRAFT', 326, 180, 18, GOLD)
    banner = svg(banner_body, 1200, 300, 'DCS-IPIA Flight Patch — Inputs, Profiles, Aircraft')
    (OUT / 'flight-patch-banner.svg').write_text(banner)
    render(banner, 1200, 300).save(OUT / 'flight-patch-banner.png')
    wizard = svg(f'<path fill="{NAVY}" d="M0 0H164V314H0Z"/><g transform="translate(8 39) scale(.289)">{emblem()}</g>' +
                 text('DCS-IPIA', 82, 232, 22, center=True) + text('INPUTS / PROFILES', 82, 257, 9, GOLD, True) +
                 text('/ AIRCRAFT', 82, 273, 9, GOLD, True), 164, 314, 'DCS-IPIA setup artwork')
    (OUT / 'setup-sidebar.svg').write_text(wizard)
    for filename, source, size in [('setup-sidebar.bmp', wizard, (164, 314)), ('setup-small.bmp', compact, (55, 55))]:
        rgba = render(source, *size)
        backdrop = Image.new('RGB', size, NAVY)
        backdrop.paste(rgba, mask=rgba.getchannel('A'))
        backdrop.save(OUT / filename)
    generated = [path for path in OUT.iterdir() if path.suffix in {'.svg', '.png', '.ico', '.bmp'}]
    inputs = [Path(__file__), ROOT / 'src/Dcs.Ipia.Rendering/Fonts/DejaVuSans-IPIA.ttf']
    hashes = {path.relative_to(ROOT).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest() for path in sorted(generated + inputs)}
    (OUT / 'manifest.json').write_text(json.dumps({'schemaVersion': 1, 'sha256': hashes}, indent=2) + '\n')
    print(f'Flight Patch assets generated in {OUT.relative_to(ROOT)}')


if __name__ == '__main__':
    main()

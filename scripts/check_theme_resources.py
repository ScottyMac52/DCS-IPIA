"""Validate theme key parity and dynamic resource references for the WPF workspace."""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1] / "src" / "Dcs.Ipia.App"
KEY = re.compile(r'x:Key="([^"]+)"')
DYNAMIC = re.compile(r"\{DynamicResource ([^}]+)\}")


def keys(path):
    return set(KEY.findall(path.read_text(encoding="utf-8")))


light = keys(ROOT / "Themes" / "Light.xaml")
palettes = ("Light", "Dark", "NavyAmber", "CockpitGreen")
missing_palettes = [name for name in palettes if not (ROOT / "Themes" / f"{name}.xaml").exists()]
styles = keys(ROOT / "Themes" / "Styles.xaml")
used = set()
for path in ROOT.rglob("*.xaml"):
    if "obj" in path.parts or "bin" in path.parts:
        continue
    used.update(DYNAMIC.findall(path.read_text(encoding="utf-8")))

problems = []
for name in missing_palettes:
    problems.append(f"Missing palette: {name}")
for name in palettes:
    path = ROOT / "Themes" / f"{name}.xaml"
    if path.exists() and keys(path) != light:
        problems.append(f"Palette key mismatch: {name}")
for filename in ("MainWindow.xaml", "SettingsWindow.xaml"):
    text = (ROOT / filename).read_text(encoding="utf-8")
    tags = re.findall(r'<ComboBoxItem Content="[^"]+" Tag="([^"]+)"', text)
    if [tag for tag in tags if tag in palettes] != list(palettes):
        problems.append(f"Built-in skin choices missing or out of order in {filename}")
for path in (ROOT / "Themes").glob("*.xaml"):
    if re.search(r"\{StaticResource \w+Brush\}", path.read_text(encoding="utf-8-sig")):
        problems.append(f"Fixed palette reference in {path.name}; use DynamicResource")
styles.update(keys(ROOT / "Themes" / "Controls.xaml"))
if missing := used - light - styles:
    problems.append(f"Unresolved dynamic resources: {sorted(missing)}")
if problems:
    print("\n".join(problems), file=sys.stderr)
    sys.exit(1)
print(f"All {len(palettes)} palettes match ({len(light)} brushes); all {len(used)} dynamic references resolve.")

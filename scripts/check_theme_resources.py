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
dark = keys(ROOT / "Themes" / "Dark.xaml")
styles = keys(ROOT / "Themes" / "Styles.xaml")
used = set()
for path in (ROOT / "MainWindow.xaml", ROOT / "Themes" / "Styles.xaml"):
    used.update(DYNAMIC.findall(path.read_text(encoding="utf-8")))

problems = []
if light != dark:
    problems.append(f"Palette key mismatch: light-only={sorted(light - dark)}, dark-only={sorted(dark - light)}")
if missing := used - light - styles:
    problems.append(f"Unresolved dynamic resources: {sorted(missing)}")
if problems:
    print("\n".join(problems), file=sys.stderr)
    sys.exit(1)
print(f"Theme keys match ({len(light)} brushes); all {len(used)} dynamic references resolve.")

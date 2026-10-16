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
for dictionary in ("Primitives", "Collections"):
    path = ROOT / "Themes" / f"{dictionary}.xaml"
    if path.exists():
        styles.update(keys(path))
    else:
        problems.append(f"Missing shared control dictionary: {dictionary}")
menu_styles = (ROOT / "Themes" / "Menus.xaml")
if not menu_styles.exists():
    problems.append("Missing shared menu templates: Menus.xaml")
else:
    styles.update(keys(menu_styles))
styles.update(keys(ROOT / "Themes" / "Controls.xaml"))
if missing := used - light - styles:
    problems.append(f"Unresolved dynamic resources: {sorted(missing)}")
for path in ROOT.rglob("*.xaml"):
    if any(part in {"Themes", "obj", "bin"} for part in path.parts):
        continue
    if re.search(r'(?:Background|Foreground|BorderBrush)="(?:#[0-9a-fA-F]+|White|Black|DarkRed|Gray|Silver)"', path.read_text(encoding="utf-8-sig")):
        problems.append(f"Fixed UI color in {path.relative_to(ROOT)}")
for required in ("DataGrid", "DataGridCell", "DataGridColumnHeader", "ListBox", "ListBoxItem", "TreeView", "TreeViewItem", "CheckBox", "Slider", "ScrollBar", "TextBox", "ToolTip", "Label", "Expander", "Menu", "MenuItem", "ContextMenu"):
    source = "\n".join(path.read_text(encoding="utf-8-sig") for path in (ROOT / "Themes").glob("*.xaml"))
    if f'<Style TargetType="{required}">' not in source:
        problems.append(f"Missing shared appearance style: {required}")
for path in ROOT.rglob("*.cs"):
    if path.name != "ThemeMessageBox.cs" and not any(part in {"obj", "bin"} for part in path.parts):
        source = path.read_text(encoding="utf-8-sig")
        if re.search(r"Brushes\.(?:White|Black|Gray|DarkRed|Gold|Yellow|SlateGray|SteelBlue|LemonChiffon|DarkGoldenrod)\b", source):
            problems.append(f"Fixed interactive UI brush in {path.relative_to(ROOT)}")
        if re.search(r"(?<!Theme)MessageBox\.Show\(", source):
            problems.append(f"Unthemed message dialog in {path.relative_to(ROOT)}")
if problems:
    print("\n".join(problems), file=sys.stderr)
    sys.exit(1)
print(f"All {len(palettes)} palettes match ({len(light)} brushes); all {len(used)} dynamic references resolve.")

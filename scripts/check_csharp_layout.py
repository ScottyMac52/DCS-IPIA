"""Check the approved C# type, namespace, XML comment, and region conventions."""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
TYPE = re.compile(
    r"^\s*(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|file)\s+)*"
    r"(?:record(?:\s+(?:struct|class))?|class|struct|enum|interface)\s+(\w+)", re.M
)
errors = []
files = sorted((*ROOT.glob("src/**/*.cs"), *ROOT.glob("tests/**/*.cs")))
for path in files:
    text = path.read_text(encoding="utf-8")
    matches = list(TYPE.finditer(text))
    relative = path.relative_to(ROOT)
    if len(matches) != 1:
        errors.append(f"{relative}: expected one declared type, found {len(matches)}")
        continue
    name = matches[0].group(1)
    expected = path.name.removesuffix(".xaml.cs").removesuffix(".cs")
    if name != expected:
        errors.append(f"{relative}: type {name} does not match filename {expected}")
    if name.startswith("I") and "interface " in matches[0].group(0):
        if "/Interfaces/" not in f"/{relative.as_posix()}/" or not re.search(r"namespace\s+Dcs\.Ipia\.Application\.Interfaces\s*;", text):
            errors.append(f"{relative}: interface must be in Dcs.Ipia.Application.Interfaces")
    prefix = text[:matches[0].start()].rstrip().splitlines()
    if not prefix or not any(line.lstrip().startswith("/// <summary>") for line in prefix[-30:]):
        errors.append(f"{relative}: type needs an XML summary")
    for begin, end in (("#region BEGIN Public", "#endregion END Public"),
                       ("#region BEGIN private helpers", "#endregion END private helpers")):
        if text.count(begin) != text.count(end) or text.count(begin) > 1:
            errors.append(f"{relative}: unmatched or duplicate region {begin}")
    public = text.find("#region BEGIN Public")
    private = text.find("#region BEGIN private helpers")
    if private >= 0 and (public < 0 or private < text.find("#endregion END Public")):
        errors.append(f"{relative}: private helpers must follow the public region")
    if private >= 0:
        for member in re.finditer(r"^\s*private\s+(?:(?:static|async)\s+)*(?:[\w<>?\[\],]+\s+)+\w+\s*\(", text, re.M):
            if member.start() < private or member.start() > text.find("#endregion END private helpers"):
                errors.append(f"{relative}: private method outside private helpers region")
    if relative.parts[0] == "src":
        lines = text.splitlines()
        for i, line in enumerate(lines):
            if not re.match(r"\s*public\s+(?!.*\b(?:class|record|struct|enum|interface)\s+\w+)", line):
                continue
            if not any(item.lstrip().startswith("/// <summary>") for item in lines[max(0, i-35):i]):
                errors.append(f"{relative}:{i + 1}: public member needs an XML summary")

if errors:
    print("\n".join(errors), file=sys.stderr)
    sys.exit(1)
print(f"Checked {len(files)} C# files: one declared type per file, XML summaries, namespaces, and regions.")

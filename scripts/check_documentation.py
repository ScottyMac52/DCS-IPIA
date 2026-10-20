#!/usr/bin/env python3
"""Check current guide links and require release notes to match the requested version."""

import argparse
import json
from pathlib import Path
import re
import sys
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]


def check(root, requested_version=None):
    """Return actionable errors without rewriting source or historical fixtures."""
    errors = []
    version = json.loads((root / "release/version.json").read_text(encoding="utf-8"))["version"]
    if not isinstance(version, str) or not re.fullmatch(r"\d+\.\d+\.\d+\.\d+", version):
        errors.append("release/version.json: expected a four-component version")
    notes = (root / "release/RELEASE-NOTES.md").read_text(encoding="utf-8")
    heading = notes.splitlines()[0] if notes else ""
    product = "DCS-IPIA" if version == "1.1.1.0" else "HEIMDALL"
    if heading != f"# {product} v{version}":
        errors.append(f"release/RELEASE-NOTES.md: heading must be '# {product} v{version}'")
    if requested_version is not None and requested_version != version:
        errors.append(f"Requested release {requested_version} differs from documented version {version}; update version.json and notes together")
    files = [root / name for name in ("README.md", "CONTRIBUTING.md", "SECURITY.md", "tests/README.md", "release/RELEASE-NOTES.md")]
    files += sorted((root / "docs").rglob("*.md"))
    for directory in ("data", "snapshots", "src"):
        files += sorted((root / directory).rglob("*.md"))
    for path in files:
        if not path.exists():
            errors.append(f"Missing guide: {path.relative_to(root)}")
            continue
        # Examples inside fenced code blocks are not Markdown links.
        text = re.sub(r"(?ms)^\s*(`{3,}|~{3,})[^\n]*\n.*?^\s*\1\s*$", "", path.read_text(encoding="utf-8"))
        for target in re.findall(r"\]\(([^\s)]+)(?:\s+[^)]*)?\)", text):
            url = urlsplit(target.strip("<>"))
            if url.scheme or url.netloc or not url.path:
                continue
            destination = path.parent / unquote(url.path)
            if not destination.exists():
                errors.append(f"{path.relative_to(root)}: missing local link target {target}")
    return errors


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", help="Actual version requested by the publishing workflow")
    args = parser.parse_args()
    try:
        errors = check(ROOT, args.version)
    except (OSError, ValueError, KeyError, TypeError) as error:
        errors = [str(error)]
    if errors:
        print("Documentation check failed:\n" + "\n".join(errors), file=sys.stderr)
        return 1
    print("Documentation links and release version/notes agree.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

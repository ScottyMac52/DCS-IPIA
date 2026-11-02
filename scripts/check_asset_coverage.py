"""Enforce independent line and branch coverage for each asset exporter module."""
from pathlib import Path
import sys

EXPECTED = {"drawio-source.mjs", "drawio-svg.mjs", "hardware-export.mjs", "export-hardware.mjs", "refresh-hardware-bundle.mjs"}


def check(text):
    seen = set()
    for record in text.split("end_of_record"):
        fields = dict(line.split(":", 1) for line in record.strip().splitlines()
                      if line.startswith(("SF:", "LF:", "LH:", "BRF:", "BRH:")))
        if not fields:
            continue
        name = fields["SF"].replace("\\", "/").split("/")[-1]
        if name not in EXPECTED:
            raise ValueError(f"Unexpected coverage module: {name}")
        if name in seen:
            raise ValueError(f"Duplicate coverage module: {name}")
        seen.add(name)
        for total, hit, label in [("LF", "LH", "line"), ("BRF", "BRH", "branch")]:
            count, covered = int(fields[total]), int(fields[hit])
            if count <= 0 or covered < 0 or covered > count or covered * 100 < count * 80:
                raise ValueError(f"{name}: {label} coverage below 80% or invalid counts")
            print(f"{name}: {label} {covered * 100 / count:.2f}%")
    if seen != EXPECTED:
        raise ValueError(f"Missing coverage modules: {sorted(EXPECTED - seen)}")


if __name__ == "__main__":
    check(Path(sys.argv[1]).read_text(encoding="utf-8"))

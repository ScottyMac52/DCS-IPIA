"""Enforce ProjectHope-style independent coverage and method CRAP gates."""
import argparse
import math
from pathlib import Path
import xml.etree.ElementTree as ET

from publish_test_results import cell, read_metrics

ASSEMBLIES = tuple("Dcs.Ipia." + name for name in
                   ("Domain", "Application", "Infrastructure", "Dcs", "Rendering", "Packaging"))


def coverage_rows(report, assemblies=ASSEMBLIES, line_threshold=80, branch_threshold=80):
    """Require exactly one measured package per production assembly."""
    root = ET.parse(report).getroot()
    if root.tag != "coverage":
        raise ValueError("Expected a Cobertura coverage root.")
    rows, failures = [], []
    for assembly in assemblies:
        packages = [node for node in root.findall("./packages/package")
                    if node.get("name") == assembly]
        if len(packages) != 1:
            rows.append((assembly, "missing/ambiguous", "missing/ambiguous", "FAIL"))
            failures.append(f"{assembly}: expected exactly one coverage package; found {len(packages)}.")
            continue
        package = packages[0]
        lines = package.findall("./classes/class/lines/line")
        if not lines:
            rows.append((assembly, "missing", "missing", "FAIL"))
            failures.append(f"{assembly}: no instrumented lines.")
            continue
        rates = [float(package.attrib[key]) * 100 for key in ("line-rate", "branch-rate")]
        if any(not math.isfinite(rate) or not 0 <= rate <= 100 for rate in rates):
            raise ValueError(f"{assembly}: invalid coverage rates.")
        line, branch = rates
        has_branches = any(node.get("branch", "").lower() == "true" for node in lines)
        branch_label = f"{branch:.2f}%" if has_branches else "N/A (no branch points)"
        if not has_branches:
            branch = 100
        passed = line >= line_threshold and branch >= branch_threshold
        rows.append((assembly, f"{line:.2f}%", branch_label, "PASS" if passed else "FAIL"))
        if not passed:
            failures.append(f"{assembly}: requires {line_threshold}% line / {branch_threshold}% branch.")
    return rows, failures


def risk_rows(raw, assemblies=ASSEMBLIES, ceiling=50):
    """Use the existing OpenCover formula, without rounding at the gate boundary."""
    metrics = [row for row in read_metrics(raw) if row["assembly"] in assemblies]
    if not metrics:
        raise ValueError("No measured production methods; CRAP gate cannot pass.")
    missing = set(assemblies) - {row["assembly"] for row in metrics}
    failures = [f"{assembly}: missing OpenCover method metrics." for assembly in sorted(missing)]
    for row in metrics:
        score = row["complexity"] ** 2 * (1 - row["line_coverage_percent"] / 100) ** 3 + row["complexity"]
        if score > ceiling:
            failures.append(f'{row["assembly"]}: {row["method"]} CRAP {score:.4f} exceeds {ceiling}.')
    return metrics, failures


def publish_gate(report, raw, output, summary, ceiling=50):
    """Publish every available diagnostic before failing either gate."""
    failures, rows, metrics = [], [], []
    for label, action in (
            ("Coverage", lambda: coverage_rows(report)),
            ("CRAP", lambda: risk_rows(raw, ceiling=ceiling))):
        try:
            values, problems = action()
            if label == "Coverage":
                rows = values
            else:
                metrics = values
            failures.extend(problems)
        except (ET.ParseError, KeyError, ValueError, OSError) as error:
            failures.append(f"{label} gate unavailable: {error}")
    lines = ["## Independent production assembly coverage", "",
             "| Assembly | Line coverage | Branch coverage | Gate |",
             "|---|---:|---:|---|"]
    lines.extend("| " + " | ".join(cell(value) for value in row) + " |" for row in rows)
    lines.extend(["", f"## Method CRAP gate (maximum {ceiling:g})", "",
                  "CRAP = complexity² × (1 − method line coverage fraction)³ + complexity.",
                  "All measured methods in the six gated assemblies are included, including compiler-generated methods.",
                  f"Measured methods: {len(metrics)}. Gate: **{'FAIL' if failures else 'PASS'}**.", ""])
    if failures:
        lines.extend("- " + cell(failure) for failure in failures)
    markdown = "\n".join(lines) + "\n"
    print(markdown)
    output.mkdir(parents=True, exist_ok=True)
    (output / "QualityGateSummary.md").write_text(markdown, encoding="utf-8")
    with summary.open("a", encoding="utf-8") as stream:
        stream.write(markdown)
    return 1 if failures else 0


def main():
    """Accept report paths and the explicit CRAP ceiling."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--coverage", required=True, type=Path)
    parser.add_argument("--raw", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--summary", required=True, type=Path)
    parser.add_argument("--crap-ceiling", default=50.0, type=float)
    args = parser.parse_args()
    if not math.isfinite(args.crap_ceiling) or args.crap_ceiling <= 0:
        parser.error("--crap-ceiling must be a finite positive number")
    return publish_gate(args.coverage, args.raw, args.output, args.summary, args.crap_ceiling)


if __name__ == "__main__":
    raise SystemExit(main())

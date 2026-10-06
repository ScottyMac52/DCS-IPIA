"""Publish method risk metrics and TRX results to files and an Actions summary."""

import argparse
import csv
from datetime import datetime
import html
import json
import math
from pathlib import Path
import xml.etree.ElementTree as ET


def read_metrics(root):
    """Calculate CRAP from Coverlet's method complexity and line coverage."""
    rows = []
    for path in sorted(root.rglob("*.opencover.xml")):
        document = ET.parse(path).getroot()
        for module in document.findall("./Modules/Module"):
            files = {item.get("uid"): item.get("fullPath") for item in module.findall("./Files/File")}
            for cls in module.findall("./Classes/Class"):
                for method in cls.findall("./Methods/Method"):
                    complexity = int(method.attrib["cyclomaticComplexity"])
                    coverage = float(method.attrib["sequenceCoverage"])
                    if complexity < 1 or not math.isfinite(coverage) or not 0 <= coverage <= 100:
                        raise ValueError(f"Invalid method metrics in {path.name}")
                    point = method.find("./SequencePoints/SequencePoint")
                    file_ref = method.find("FileRef")
                    rows.append({
                        "report": path.relative_to(root).as_posix(),
                        "assembly": module.findtext("ModuleName", ""),
                        "class": cls.findtext("FullName", ""),
                        "method": method.findtext("Name", ""),
                        "file": files.get(file_ref.get("uid"), "") if file_ref is not None else "",
                        "line": int(point.get("sl", "0")) if point is not None else 0,
                        "complexity": complexity,
                        "line_coverage_percent": coverage,
                        "branch_coverage_percent": method.get("branchCoverage"),
                        "crap_score": round(complexity ** 2 * (1 - coverage / 100) ** 3 + complexity, 4),
                    })
    return sorted(rows, key=lambda row: (-row["crap_score"], row["assembly"], row["method"], row["report"]))


def read_tests(root):
    """Read each TRX run separately so Linux and Windows counts stay distinct."""
    rows = []
    for path in sorted(root.rglob("*.trx")):
        document = ET.parse(path).getroot()
        # TRX uses a default namespace; removing it simplifies all subsequent queries.
        for element in document.iter():
            element.tag = element.tag.rsplit("}", 1)[-1]
        counters = document.find("./ResultSummary/Counters")
        if counters is None:
            raise ValueError(f"Missing test counters in {path.name}")
        times = document.find("Times")
        duration = None
        if times is not None and times.get("start") and times.get("finish"):
            start = datetime.fromisoformat(times.get("start").replace("Z", "+00:00"))
            finish = datetime.fromisoformat(times.get("finish").replace("Z", "+00:00"))
            duration = round((finish - start).total_seconds(), 3)
        failures = [{
            "name": item.get("testName", ""),
            "duration": item.get("duration", ""),
            "message": item.findtext("./Output/ErrorInfo/Message", ""),
            "stack_trace": item.findtext("./Output/ErrorInfo/StackTrace", ""),
        } for item in document.findall("./Results/UnitTestResult") if item.get("outcome") == "Failed"]
        rows.append({
            "report": path.relative_to(root).as_posix(),
            "total": int(counters.get("total", "0")),
            "executed": int(counters.get("executed", "0")),
            "passed": int(counters.get("passed", "0")),
            "failed": int(counters.get("failed", "0")),
            "skipped": int(counters.get("notExecuted", "0")),
            "duration_seconds": duration,
            "counters": dict(counters.attrib),
            "failures": failures,
        })
    return rows


def cell(value):
    """Escape report content before placing it into a Markdown table."""
    return html.escape(str(value)).replace("|", "&#124;").replace("\r", " ").replace("\n", " ")


def publish(raw, output, summary):
    """Preserve available diagnostics; fail reporting when required data is missing."""
    output.mkdir(parents=True, exist_ok=True)
    errors = []
    datasets = {}
    for name, reader, label in [("metrics", read_metrics, "Coverage metrics"), ("tests", read_tests, "Test results")]:
        try:
            datasets[name] = reader(raw)
        except (ET.ParseError, KeyError, ValueError, OSError) as error:
            datasets[name] = []
            errors.append(f"{label} unavailable: {error}")
    metrics, tests = datasets["metrics"], datasets["tests"]
    if not metrics:
        errors.append("No OpenCover method metrics were available; CRAP scores are unavailable.")
    if not tests:
        errors.append("No TRX test results were available; test outcomes are unavailable.")
    for filename, rows in [("crap-scores.json", metrics), ("test-results.json", tests)]:
        (output / filename).write_text(json.dumps(rows, indent=2) + "\n", encoding="utf-8")
    if metrics:
        with (output / "crap-scores.csv").open("w", newline="", encoding="utf-8") as stream:
            writer = csv.DictWriter(stream, fieldnames=list(metrics[0]))
            writer.writeheader()
            writer.writerows(metrics)

    lines = ["## Unit test results", "", "Each row is a separate test run; platform counts are not combined.", "",
             "| Run | Total | Passed | Failed | Skipped | Seconds |", "|---|---:|---:|---:|---:|---:|"]
    for row in tests:
        lines.append("| " + " | ".join(cell(row[key] if row[key] is not None else "N/A") for key in
                     ("report", "total", "passed", "failed", "skipped", "duration_seconds")) + " |")
        for failure in row["failures"][:20]:
            lines.extend(["", f"Failed: **{cell(failure['name'])}** — {cell(failure['message'][:1000])}"])
    lines.extend(["", "## Method risk (CRAP)", "",
                  "CRAP = complexity² × (1 − line coverage fraction)³ + complexity. Lower is better.",
                  "Complexity comes from Coverlet's IL branch analysis, including compiler-generated methods.",
                  "Scores are diagnostic, not an additional pass/fail gate. Full metrics are in crap-scores.csv/json.", "",
                  f"Methods analyzed: {len(metrics)}; methods with CRAP ≥ 30: {sum(row['crap_score'] >= 30 for row in metrics)}.", "",
                  "### Highest 20 scores", "", "| Assembly | Method | Complexity | Line coverage % | CRAP |",
                  "|---|---|---:|---:|---:|"])
    for row in metrics[:20]:
        lines.append("| " + " | ".join(cell(row[key]) for key in
                     ("assembly", "method", "complexity", "line_coverage_percent", "crap_score")) + " |")
    if errors:
        lines.extend(["", "## Reporting incomplete", ""] + [f"- {cell(error)}" for error in errors])
    markdown = "\n".join(lines) + "\n"
    (output / "TestAndRiskSummary.md").write_text(markdown, encoding="utf-8")
    with summary.open("a", encoding="utf-8") as stream:
        stream.write(markdown)
    return 1 if errors else 0


def main():
    """Accept explicit paths for downloaded reports and the Actions summary."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--raw", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--summary", type=Path, required=True)
    args = parser.parse_args()
    return publish(args.raw, args.output, args.summary)


if __name__ == "__main__":
    raise SystemExit(main())

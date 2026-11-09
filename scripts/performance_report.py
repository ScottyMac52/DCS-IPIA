#!/usr/bin/env python3
"""Summarize bounded Profile-build captures without third-party dependencies."""
import argparse
import csv
import json
import math
from pathlib import Path
import re
import statistics


def load(path):
    """Reject incomplete or unsupported evidence rather than publishing invented results."""
    data = json.loads(path.read_text(encoding="utf-8"))
    if data.get("SchemaVersion") != 1 or data.get("Configuration") != "Profile":
        raise ValueError("Expected a schema-1 Profile capture")
    capacity, dropped, samples = data["Capacity"], data["Dropped"], data["Samples"]
    if type(capacity) is not int or not 1 <= capacity <= 100_000 or type(dropped) is not int or dropped < 0:
        raise ValueError("Invalid capture bounds")
    if not isinstance(samples, list) or len(samples) > capacity:
        raise ValueError("Capture exceeds its declared capacity")
    for sample in samples:
        duration = sample["DurationMs"]
        if type(duration) not in (int, float) or not math.isfinite(duration) or duration < 0:
            raise ValueError("Invalid duration")
        if not re.fullmatch(r"[a-z][a-z.-]*", sample["Operation"]) or sample["Component"] not in {"Shared", "DCS", "Elite", "Squadrons", "WarThunder"}:
            raise ValueError("Invalid operation/component tag")
    return data


def percentile(values, fraction):
    """Use the nearest-rank definition, including for single-sample groups."""
    return values[max(0, math.ceil(len(values) * fraction) - 1)]


def summarize(data):
    """Group inclusive timings; nested operations are deliberately not summed across groups."""
    groups = {}
    for sample in data["Samples"]:
        groups.setdefault((sample["Component"], sample["Operation"]), []).append(sample["DurationMs"])
    rows = []
    for (component, operation), values in sorted(groups.items()):
        values.sort()
        rows.append(dict(component=component, operation=operation, count=len(values),
                         mean_ms=statistics.fmean(values), p50_ms=percentile(values, .5),
                         p95_ms=percentile(values, .95), max_ms=values[-1],
                         over_16ms=sum(value > 16 for value in values), over_50ms=sum(value > 50 for value in values)))
    return rows


def cell(value):
    return str(value).replace("|", "\\|").replace("\n", " ").replace("\r", " ")


def publish(path, output):
    """Publish one report per capture so separate sessions never silently mix."""
    data = load(path)
    rows = summarize(data)
    output.mkdir(parents=True, exist_ok=True)
    fields = ["component", "operation", "count", "mean_ms", "p50_ms", "p95_ms", "max_ms", "over_16ms", "over_50ms"]
    with (output / (path.stem + ".csv")).open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        writer.writerows(rows)
    lines = ["# HEIMDALL performance capture", "",
             f"Build: `{cell(data['Build'])}`; configuration: Profile.",
             f"UTC interval: {cell(data['StartedUtc'])} to {cell(data['EndedUtc'])}.",
             f"Runtime: {cell(data['Runtime'])}; OS: {cell(data['OS'])}; architecture: {cell(data['Architecture'])}; logical processors: {data['LogicalProcessors']}.",
             f"Retained: {len(data['Samples'])}; capacity: {data['Capacity']}; dropped: {data['Dropped']}.",
             f"Process CPU at exit: {data['ProcessCpuMs']:.2f} ms; private bytes at exit: {data['PrivateBytesAtExit']}; managed bytes allocated since process start: {data['ManagedAllocatedBytes']}.",
             f"GC collections during capture (Gen 0/1/2): {cell(data['Collections'])}.", "",
             "Durations are inclusive wall time, including awaits and failed operations. Nested groups overlap; do not add their totals. CPU/allocation fields describe the process, not individual operations.",
             "Heartbeat delay measures excess over a 100 ms background-dispatcher interval; it is a responsiveness proxy, not frame rate. Background priority can delay it during other dispatcher work.", ""]
    if data["Dropped"]:
        lines += ["**TRUNCATED: first-N capture only. Later work was omitted; these distributions cannot describe the whole session. Repeat with a shorter scenario.**", ""]
    if not rows:
        lines += ["No operation measurements were retained; this is not performance acceptance evidence.", ""]
    lines += ["| Component | Operation | Count | Mean ms | P50 ms | P95 ms | Max ms | >16 ms | >50 ms |",
              "| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |"]
    for row in rows:
        lines.append(f"| {row['component']} | {row['operation']} | {row['count']} | {row['mean_ms']:.3f} | {row['p50_ms']:.3f} | {row['p95_ms']:.3f} | {row['max_ms']:.3f} | {row['over_16ms']} | {row['over_50ms']} |")
    lines += ["", "Percentiles use nearest rank. Counts and threshold columns describe retained samples, not success counts or performance gates. Compare identical scenarios on the same machine; Profile instrumentation itself adds overhead.", ""]
    (output / (path.stem + ".md")).write_text("\n".join(lines), encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    try:
        publish(args.capture, args.output)
    except (OSError, ValueError, KeyError, TypeError) as error:
        parser.exit(1, f"Performance report failed: {error}\n")


if __name__ == "__main__":
    main()

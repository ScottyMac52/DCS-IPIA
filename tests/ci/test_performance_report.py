"""Verify performance evidence and reports independently of the Windows profiler."""
import copy
import csv
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location("performance_report", Path(__file__).resolve().parents[2] / "scripts/performance_report.py")
report = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(report)


class PerformanceReportTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.path = self.root / "capture-test.json"
        self.data = dict(SchemaVersion=1, Configuration="Profile", Build="1.0+abc", StartedUtc="2026-01-01T00:00:00Z",
                         EndedUtc="2026-01-01T00:01:00Z", Runtime=".NET 10", OS="Windows", Architecture="X64",
                         LogicalProcessors=8, ProcessCpuMs=123.5, PrivateBytesAtExit=4096,
                         ManagedAllocatedBytes=8192, Collections=[1, 0, 0], Capacity=100, Dropped=0,
                         Samples=[dict(Operation="browser.filter", Component="DCS", DurationMs=value) for value in range(1, 21)])

    def save(self, data=None):
        self.path.write_text(json.dumps(self.data if data is None else data), encoding="utf-8")

    def test_nearest_rank_percentiles_and_threshold_counts(self):
        row = report.summarize(self.data)[0]
        self.assertEqual((20, 10.5, 10, 19, 20, 4, 0),
                         tuple(row[key] for key in ("count", "mean_ms", "p50_ms", "p95_ms", "max_ms", "over_16ms", "over_50ms")))

    def test_simulators_and_operations_are_not_silently_pooled(self):
        self.data["Samples"] += [dict(Operation="browser.filter", Component="Elite", DurationMs=70),
                                 dict(Operation="editor.render", Component="DCS", DurationMs=80)]
        rows = report.summarize(self.data)
        self.assertEqual(3, len(rows))
        self.assertEqual(22, sum(row["count"] for row in rows))

    def test_invalid_or_non_profile_evidence_is_rejected(self):
        mutations = [("Configuration", "Release"), ("SchemaVersion", 2), ("Capacity", 0),
                     ("Dropped", -1), ("Capacity", 1)]
        for key, value in mutations:
            data = copy.deepcopy(self.data)
            data[key] = value
            self.save(data)
            with self.subTest(key=key, value=value), self.assertRaises(ValueError):
                report.load(self.path)
        for duration in (-1, float("nan"), float("inf"), True):
            data = copy.deepcopy(self.data)
            data["Samples"][0]["DurationMs"] = duration
            self.save(data)
            with self.subTest(duration=duration), self.assertRaises(ValueError):
                report.load(self.path)

    def test_truncation_is_explicit_and_csv_matches_markdown_evidence(self):
        self.data["Dropped"] = 7
        self.save()
        report.publish(self.path, self.root)
        markdown = self.path.with_suffix(".md").read_text(encoding="utf-8")
        self.assertIn("TRUNCATED", markdown)
        self.assertIn("dropped: 7", markdown)
        self.assertIn("1.0+abc", markdown)
        with self.path.with_suffix(".csv").open(encoding="utf-8", newline="") as stream:
            row = list(csv.DictReader(stream))[0]
        self.assertEqual("19", row["p95_ms"])
        self.assertEqual("20", row["count"])

    def test_empty_capture_is_not_reported_as_performance_acceptance(self):
        self.data["Samples"] = []
        self.save()
        report.publish(self.path, self.root)
        self.assertIn("not performance acceptance evidence", self.path.with_suffix(".md").read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()

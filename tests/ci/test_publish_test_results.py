"""Regression tests for the CI reports, independent of the .NET test runner."""

from html.parser import HTMLParser
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


SCRIPT = Path(__file__).resolve().parents[2] / "scripts/publish_test_results.py"


class PublishTestResultsTests(unittest.TestCase):
    def setUp(self):
        spec = importlib.util.spec_from_file_location("reports", SCRIPT)
        self.reports = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.reports)
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.raw = self.root / "raw"
        self.raw.mkdir()
        self.output = self.root / "report"
        self.summary = self.root / "summary.md"

    def write_coverage(self, coverage="50", complexity="10"):
        path = self.raw / "coverage.opencover.xml"
        path.write_text(f'''<CoverageSession><Modules><Module>
          <ModuleName>Example.Domain</ModuleName><Files><File uid="1" fullPath="src/Example.cs"/></Files>
          <Classes><Class><FullName>Example.Class</FullName><Methods>
          <Method cyclomaticComplexity="{complexity}" sequenceCoverage="{coverage}" branchCoverage="25">
          <Name>System.Void Example.Class::Run()</Name><FileRef uid="1"/>
          <SequencePoints><SequencePoint sl="12" vc="1"/></SequencePoints>
          </Method></Methods></Class></Classes></Module></Modules></CoverageSession>''', encoding="utf-8")
        return path

    def write_trx(self):
        path = self.raw / "linux.trx"
        path.write_text('''<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <Times start="2026-10-06T12:00:00Z" finish="2026-10-06T12:00:05Z"/>
          <Results><UnitTestResult testName="A|B&lt;script&gt;" outcome="Failed" duration="00:00:01">
          <Output><ErrorInfo><Message>Expected | actual\nsecond line</Message></ErrorInfo></Output>
          </UnitTestResult></Results><ResultSummary><Counters total="4" executed="3" passed="2" failed="1" notExecuted="1"/></ResultSummary>
          </TestRun>''', encoding="utf-8")
        return path

    def test_crap_formula_uses_fraction_and_preserves_complexity_floor(self):
        for coverage, expected in [("0", 110), ("50", 22.5), ("100", 10)]:
            with self.subTest(coverage=coverage):
                self.write_coverage(coverage)
                rows = self.reports.read_metrics(self.raw)
                self.assertAlmostEqual(expected, rows[0]["crap_score"])
                self.assertEqual("src/Example.cs", rows[0]["file"])
                self.assertEqual(12, rows[0]["line"])

    def test_missing_or_invalid_metrics_are_not_reported_as_zero_risk(self):
        for coverage, complexity in [("NaN", "10"), ("101", "10"), ("50", "0"), ("", "10")]:
            with self.subTest(coverage=coverage, complexity=complexity):
                self.write_coverage(coverage, complexity)
                with self.assertRaises(ValueError):
                    self.reports.read_metrics(self.raw)

    def test_namespaced_trx_counts_duration_and_failure_details(self):
        self.write_trx()
        rows = self.reports.read_tests(self.raw)
        self.assertEqual((4, 2, 1, 1, 5.0), tuple(rows[0][key] for key in
                         ("total", "passed", "failed", "skipped", "duration_seconds")))
        self.assertEqual("A|B<script>", rows[0]["failures"][0]["name"])

    def test_publishes_test_failures_without_hiding_them(self):
        self.write_coverage()
        self.write_trx()
        self.assertEqual(0, self.reports.publish(self.raw, self.output, self.summary))
        data = json.loads((self.output / "test-results.json").read_text())
        self.assertEqual(1, data[0]["failed"])
        markdown = self.summary.read_text()
        self.assertIn("A&#124;B&lt;script&gt;", markdown)
        self.assertIn("CRAP", markdown)
        self.assertTrue((self.output / "crap-scores.csv").exists())

    def test_missing_inputs_publish_explicit_incomplete_summary_and_fail(self):
        self.assertEqual(1, self.reports.publish(self.raw, self.output, self.summary))
        self.assertIn("No OpenCover", self.summary.read_text())
        self.assertIn("No TRX", self.summary.read_text())

    def test_malformed_coverage_still_publishes_available_test_results(self):
        self.write_coverage().write_text("<broken>", encoding="utf-8")
        self.write_trx()
        self.assertEqual(1, self.reports.publish(self.raw, self.output, self.summary))
        self.assertIn("Coverage metrics unavailable", self.summary.read_text())
        self.assertIn("linux.trx", self.summary.read_text())

    def test_html_homepage_preserves_coverage_and_replaces_its_metrics_on_rerun(self):
        self.output.mkdir()
        (self.output / "index.html").write_text(
            '<html><body><h1>Coverage overview</h1><h1>Risk Hotspots</h1></body></html>')
        self.write_coverage()
        self.write_trx()
        for _ in range(2):
            self.assertEqual(0, self.reports.publish(self.raw, self.output, self.summary))
        document = (self.output / "index.html").read_text()
        self.assertIn('<h1>Coverage overview</h1>', document)
        self.assertIn('<h1>Risk Hotspots</h1>', document)
        self.assertEqual(1, document.count('id="ipia-method-table"'))
        self.assertIn('Line coverage %', document)
        self.assertIn('Branch coverage %', document)
        self.assertIn('<td>50.0</td><td>25</td><td>22.5</td>', document)

    def test_html_contains_every_method_and_escapes_untrusted_names(self):
        class Cells(HTMLParser):
            def __init__(self):
                super().__init__()
                self.cells = []
                self.current = None
            def handle_starttag(self, tag, attrs):
                if tag == "td":
                    self.current = ""
            def handle_data(self, data):
                if self.current is not None:
                    self.current += data
            def handle_endtag(self, tag):
                if tag == "td":
                    self.cells.append(self.current)
                    self.current = None
        self.write_coverage()
        metrics = self.reports.read_metrics(self.raw) * 25
        metrics[0]["method"] = '<script>alert("test")</script>'
        metrics[0]["branch_coverage_percent"] = None
        self.output.mkdir()
        self.reports.publish_html_metrics(self.output, metrics, [])
        document = (self.output / "index.html").read_text()
        parsed = Cells()
        parsed.feed(document)
        self.assertEqual(25 * 8, len(parsed.cells))
        self.assertIn('<script>alert("test")</script>', parsed.cells)
        self.assertIn('N/A', parsed.cells)
        self.assertNotIn('<script>alert("test")</script>', document)
        self.assertIn('id="ipia-method-filter"', document)
        self.assertIn("aria-sort", document)

    def test_html_missing_coverage_shows_incomplete_reporting(self):
        self.assertEqual(1, self.reports.publish(self.raw, self.output, self.summary))
        document = (self.output / "index.html").read_text()
        self.assertIn('No OpenCover method metrics', document)
        self.assertIn('No TRX test results', document)


    def test_metrics_are_sorted_by_descending_crap(self):
        self.write_coverage("100").rename(self.raw / "low.opencover.xml")
        self.write_coverage("0")
        self.assertEqual([110, 10], [row["crap_score"] for row in self.reports.read_metrics(self.raw)])


if __name__ == "__main__":
    unittest.main()

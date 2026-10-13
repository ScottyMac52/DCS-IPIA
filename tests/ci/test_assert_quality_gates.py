"""Regression tests for independent coverage and enforcing CRAP diagnostics."""
import contextlib
import importlib.util
import io
from pathlib import Path
import sys
import tempfile
import unittest

SCRIPTS = Path(__file__).resolve().parents[2] / "scripts"
sys.path.insert(0, str(SCRIPTS))
import assert_quality_gates as gate


class QualityGateTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.report = self.root / "Cobertura.xml"

    def coverage(self, line="0.8", branch="0.8", branch_points=True, measured=True):
        point = '<line number="1" hits="1" branch="' + str(branch_points).lower() + '"/>'
        package = f'<package name="Example" line-rate="{line}" branch-rate="{branch}"><classes><class><lines>{point if measured else ""}</lines></class></classes></package>'
        self.report.write_text(f"<coverage><packages>{package}</packages></coverage>")
        return package

    def metrics(self, coverage="100", complexity="50"):
        self.report.write_text(f"""<CoverageSession><Modules><Module><ModuleName>Example</ModuleName>
        <Classes><Class><Methods><Method cyclomaticComplexity="{complexity}" sequenceCoverage="{coverage}">
        <Name>Run()</Name></Method></Methods></Class></Classes></Module></Modules></CoverageSession>""")
        self.report.rename(self.root / "coverage.opencover.xml")

    def test_exact_80_80_passes(self):
        self.coverage()
        rows, failures = gate.coverage_rows(self.report, ("Example",))
        self.assertEqual("PASS", rows[0][-1])
        self.assertFalse(failures)

    def test_each_threshold_and_each_assembly_are_independent(self):
        for line, branch in (("0.79999", "1"), ("1", "0.79999")):
            with self.subTest(line=line):
                self.coverage(line, branch)
                rows, failures = gate.coverage_rows(self.report, ("Example", "Missing"))
                self.assertEqual(["FAIL", "FAIL"], [row[-1] for row in rows])
                self.assertEqual(2, len(failures))

    def test_no_branch_points_is_not_missing_coverage(self):
        self.coverage(branch="0", branch_points=False)
        rows, failures = gate.coverage_rows(self.report, ("Example",))
        self.assertFalse(failures)
        self.assertIn("N/A", rows[0][2])

    def test_coverlet_branch_flags_are_case_insensitive(self):
        self.coverage(branch="0.79999")
        self.report.write_text(self.report.read_text().replace('branch="true"', 'branch="True"'))
        rows, failures = gate.coverage_rows(self.report, ("Example",))
        self.assertTrue(failures)
        self.assertEqual("FAIL", rows[0][-1])
        self.assertNotIn("N/A", rows[0][2])

    def test_missing_duplicate_or_unmeasured_packages_fail(self):
        for mode in ("missing", "duplicate", "unmeasured"):
            with self.subTest(mode=mode):
                package = self.coverage(measured=mode != "unmeasured")
                if mode != "unmeasured":
                    self.report.write_text("<coverage><packages>" +
                                           (package * 2 if mode == "duplicate" else "") + "</packages></coverage>")
                self.assertTrue(gate.coverage_rows(self.report, ("Example",))[1])

    def test_invalid_or_missing_rates_cannot_pass(self):
        for value in ("NaN", "inf", "-0.01", "1.01", ""):
            self.coverage(line=value)
            with self.assertRaises(ValueError):
                gate.coverage_rows(self.report, ("Example",))
        self.report.write_text("<other/>")
        with self.assertRaises(ValueError):
            gate.coverage_rows(self.report, ("Example",))

    def test_crap_ceiling_and_uncovered_risk(self):
        for complexity, coverage, passes in (("50", "100", True), ("51", "100", False), ("10", "0", False)):
            with self.subTest(complexity=complexity):
                self.metrics(coverage, complexity)
                rows, failures = gate.risk_rows(self.root, ("Example",))
                self.assertEqual(1, len(rows))
                self.assertEqual(passes, not failures)
                (self.root / "coverage.opencover.xml").unlink()

    def test_missing_assembly_metrics_fail(self):
        self.metrics()
        self.assertTrue(gate.risk_rows(self.root, ("Example", "Missing"))[1])
        with self.assertRaises(ValueError):
            gate.risk_rows(self.root, ("Unrelated",))

    def test_invalid_method_metrics_fail(self):
        self.metrics("NaN")
        with self.assertRaises(ValueError):
            gate.risk_rows(self.root, ("Example",))

    def test_incomplete_inputs_publish_both_failures_and_return_nonzero(self):
        summary = self.root / "summary.md"
        with contextlib.redirect_stdout(io.StringIO()):
            status = gate.publish_gate(self.report, self.root, self.root / "output", summary)
        self.assertEqual(1, status)
        text = summary.read_text()
        self.assertIn("Coverage gate unavailable", text)
        self.assertIn("CRAP gate unavailable", text)
        self.assertEqual(text, (self.root / "output/QualityGateSummary.md").read_text())

    def test_complete_inputs_publish_pass(self):
        package = self.coverage()
        self.metrics()
        self.report.write_text("<coverage><packages>" +
                               "".join(package.replace('name="Example"', f'name="{name}"') for name in gate.ASSEMBLIES) +
                               "</packages></coverage>")
        raw = self.root / "coverage.opencover.xml"
        module = raw.read_text().split("<Modules>")[1].split("</Modules>")[0]
        raw.write_text("<CoverageSession><Modules>" +
                       "".join(module.replace("<ModuleName>Example</ModuleName>", f"<ModuleName>{name}</ModuleName>")
                               for name in gate.ASSEMBLIES) + "</Modules></CoverageSession>")
        with contextlib.redirect_stdout(io.StringIO()):
            status = gate.publish_gate(self.report, self.root, self.root / "output", self.root / "summary.md")
        self.assertEqual(0, status)


if __name__ == "__main__":
    unittest.main()

"""Coverage gate rejects missing files and weak modules independently."""
import importlib.util
from pathlib import Path
import unittest

path = Path(__file__).resolve().parents[2] / "scripts/check_asset_coverage.py"
spec = importlib.util.spec_from_file_location("asset_coverage", path)
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)


class AssetCoverageTests(unittest.TestCase):
    def report(self, weak=None):
        return "".join(f"SF:scripts/asset-authoring/{name}\nLF:100\nLH:100\nBRF:100\nBRH:{79 if name == weak else 100}\nend_of_record\n"
                       for name in sorted(gate.EXPECTED))

    def test_weak_module_cannot_hide_behind_total(self):
        with self.assertRaisesRegex(ValueError, "below 80"):
            gate.check(self.report("drawio-svg.mjs"))

    def test_missing_module_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "Missing"):
            gate.check("")

    def test_duplicate_and_unknown_modules_are_rejected(self):
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            gate.check(self.report() + self.report())
        with self.assertRaisesRegex(ValueError, "Unexpected"):
            gate.check("SF:other.mjs\nend_of_record")

    def test_complete_independent_coverage_passes(self):
        gate.check(self.report())


if __name__ == "__main__":
    unittest.main()

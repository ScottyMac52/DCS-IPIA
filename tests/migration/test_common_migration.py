"""Failure-path coverage for the offline migration ownership and evidence checks."""

import copy
from contextlib import redirect_stderr, redirect_stdout
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "scripts"))
import check_common_migration as audit


class MigrationInventoryTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.source = self.root / "common"
        self.source.mkdir()
        content = {"LICENSE": b"MIT license\n", "scripts/tool.mjs": b"export const version = 1;\n"}
        for path, value in content.items():
            target = self.source / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(value)
        self.data = {
            "schemaVersion": 1, "sourceRepository": "ScottyMac52/DCS-Common",
            "sourceCommit": "a" * 40, "ipiaBaseline": "b" * 40, "bundledSourceCommit": "c" * 40,
            "files": [{"path": p, "mode": "100644", "gitBlob": audit.git_blob(v), "size": len(v)} for p, v in sorted(content.items())],
            "capabilities": [{"id": "test-tools", "title": "Tools", "status": "migration-pending", "owner": "Dcs",
                              "sourcePaths": sorted(content), "targetEvidence": ["README.md"],
                              "current": "Characterization only.", "exitCriteria": "Prove byte and behavior parity."}],
            "knownGaps": []
        }
        (self.root / "README.md").write_text("Evidence\n", encoding="utf-8")
        bundle = self.root / "data/shared-assets"
        bundle.mkdir(parents=True)
        (bundle / "DCS-Common-LICENSE.txt").write_bytes(content["LICENSE"])
        self.provenance = {"sourceCommit": "c" * 40, "files": [{"path": "DCS-Common-LICENSE.txt", "size": len(content["LICENSE"]), "gitBlob": audit.git_blob(content["LICENSE"])}]}
        (bundle / "provenance.json").write_text(json.dumps(self.provenance), encoding="utf-8")

    def test_complete_inventory_and_renamed_license_report_are_deterministic(self):
        owners, drift = audit.validate(self.data, self.root, self.source)
        self.assertEqual({"LICENSE": "test-tools", "scripts/tool.mjs": "test-tools"}, owners)
        self.assertEqual([("LICENSE", "unchanged")], drift)
        self.assertEqual(audit.render(self.data, owners, drift), audit.render(copy.deepcopy(self.data), owners, drift))
        self.assertIn("No capability is marked parity-complete", audit.render(self.data, owners, drift))
        self.assertEqual(audit.digest(self.data), audit.digest(json.loads(json.dumps(self.data))))

    def test_unclassified_future_source_is_rejected(self):
        self.data["files"].append({"path": "unclassified.mjs", "mode": "100644", "gitBlob": "d" * 40, "size": 0})
        with self.assertRaisesRegex(ValueError, "Unclassified sources"):
            audit.validate(self.data, self.root)

    def test_duplicate_sources_and_owners_are_rejected(self):
        self.data["files"].append(copy.deepcopy(self.data["files"][0]))
        with self.assertRaisesRegex(ValueError, "Duplicate source"):
            audit.validate(self.data, self.root)
        self.data["files"].pop()
        second = copy.deepcopy(self.data["capabilities"][0])
        second["id"] = "other-owner"
        self.data["capabilities"].append(second)
        with self.assertRaisesRegex(ValueError, "multiple owners"):
            audit.validate(self.data, self.root)

    def test_unsafe_paths_and_invalid_git_metadata_are_rejected(self):
        for path in ("../file", "/absolute", "a\\b", "a//b", "C:/file", "a/./b", "a\n"):
            with self.subTest(path=path), self.assertRaises(ValueError):
                audit.safe_path(path)
        for field, value in (("gitBlob", "bad"), ("size", -1), ("size", True), ("mode", "120000")):
            changed = copy.deepcopy(self.data)
            changed["files"][0][field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                audit.validate(changed, self.root)

    def test_unknown_schema_fields_and_implicit_parity_promotion_are_rejected(self):
        changed = copy.deepcopy(self.data)
        changed["parityComplete"] = True
        with self.assertRaisesRegex(ValueError, "Unknown inventory fields"):
            audit.validate(changed, self.root)
        self.data["capabilities"][0]["status"] = "complete"
        with self.assertRaisesRegex(ValueError, "parity cannot be inferred"):
            audit.validate(self.data, self.root)

    def test_missing_target_evidence_and_unpinned_sources_are_rejected(self):
        (self.root / "README.md").unlink()
        with self.assertRaisesRegex(ValueError, "Target evidence is missing"):
            audit.validate(self.data, self.root)
        (self.root / "README.md").write_text("Evidence", encoding="utf-8")
        self.data["capabilities"][0]["sourcePaths"].append("z-unpinned.mjs")
        with self.assertRaisesRegex(ValueError, "Unpinned source"):
            audit.validate(self.data, self.root)

    def test_bundle_pin_change_and_modified_bytes_are_rejected(self):
        self.data["bundledSourceCommit"] = "d" * 40
        with self.assertRaisesRegex(ValueError, "Bundle source pin changed"):
            audit.validate(self.data, self.root)
        self.data["bundledSourceCommit"] = "c" * 40
        (self.root / "data/shared-assets/DCS-Common-LICENSE.txt").write_bytes(b"tampered")
        with self.assertRaisesRegex(ValueError, "Bundled file differs"):
            audit.validate(self.data, self.root)

    def test_newer_source_asset_drift_is_reported_without_silent_promotion(self):
        self.data["files"][0]["gitBlob"] = "d" * 40
        owners, drift = audit.validate(self.data, self.root)
        self.assertEqual([("LICENSE", "changed")], drift)
        report = audit.render(self.data, owners, drift)
        self.assertIn("0 of 1 bundled files", report)
        self.assertIn("migration-pending", report)

    def test_optional_source_checkout_is_checked_byte_for_byte(self):
        (self.source / "scripts/tool.mjs").write_bytes(b"local change")
        with self.assertRaisesRegex(ValueError, "Source checkout differs"):
            audit.validate(self.data, self.root, self.source)

    def test_known_missing_entrypoint_must_match_the_pinned_tree(self):
        self.data["knownGaps"] = [{"id": "missing", "evidence": "LICENSE", "missingPath": "scripts/missing.mjs", "description": "Not present."}]
        audit.validate(self.data, self.root)
        self.data["knownGaps"][0]["missingPath"] = "scripts/tool.mjs"
        with self.assertRaisesRegex(ValueError, "Gap differs"):
            audit.validate(self.data, self.root)

    def test_cli_detects_stale_report_and_regenerates_without_network(self):
        inventory = self.root / audit.INVENTORY
        inventory.parent.mkdir(parents=True)
        inventory.write_text(json.dumps(self.data), encoding="utf-8")
        (self.root / audit.REPORT).write_text("stale", encoding="utf-8")
        with patch.object(audit, "ROOT", self.root), redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
            self.assertEqual(1, audit.main(["--check"]))
            self.assertEqual(0, audit.main(["--write-report", "--source-root", str(self.source)]))
            self.assertEqual(0, audit.main(["--check"]))
            before = (self.root / audit.REPORT).read_bytes()
            self.assertEqual(0, audit.main(["--write-report"]))
            self.assertEqual(before, (self.root / audit.REPORT).read_bytes())


if __name__ == "__main__":
    unittest.main()

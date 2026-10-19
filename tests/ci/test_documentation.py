"""Exercise documentation failures that must prevent mismatched release publication."""

import importlib.util
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location("documentation", Path(__file__).resolve().parents[2] / "scripts/check_documentation.py")
documentation = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(documentation)


class DocumentationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        for name, value in {
            "release/version.json": '{"version":"1.1.1.0"}',
            "release/RELEASE-NOTES.md": "# DCS-IPIA v1.1.1.0\n\nCurrent behavior.\n",
            "README.md": "[Start](docs/start.md)\n",
            "CONTRIBUTING.md": "Contributing\n",
            "SECURITY.md": "Security\n",
            "tests/README.md": "Tests\n",
            "docs/start.md": "[Root](../README.md)\n",
        }.items():
            path = self.root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(value, encoding="utf-8")

    def test_matching_release_and_relative_links_pass(self):
        self.assertEqual([], documentation.check(self.root, "1.1.1.0"))

    def test_tag_cannot_reuse_notes_from_another_version(self):
        errors = documentation.check(self.root, "1.1.2.0")
        self.assertTrue(any("Requested release 1.1.2.0" in error for error in errors))

    def test_new_version_cannot_keep_old_release_heading(self):
        (self.root / "release/version.json").write_text('{"version":"1.1.2.0"}', encoding="utf-8")
        self.assertTrue(any("heading must be '# DCS-IPIA v1.1.2.0'" in error for error in documentation.check(self.root)))

    def test_invalid_version_fails(self):
        (self.root / "release/version.json").write_text('{"version":"1.1.1"}', encoding="utf-8")
        self.assertTrue(any("four-component" in error for error in documentation.check(self.root)))

    def test_missing_guide_link_reports_source_and_target(self):
        (self.root / "docs/start.md").write_text("[Recovery](missing.md#restore)", encoding="utf-8")
        self.assertIn("docs/start.md: missing local link target missing.md#restore", documentation.check(self.root))

    def test_examples_external_links_fragments_and_encoded_paths(self):
        (self.root / "docs/image name.png").write_bytes(b"example")
        (self.root / "docs/start.md").write_text(
            '[Image](image%20name.png)\n[Web](https://example.org/unavailable)\n[Topic](#local)\n'
            '```markdown\n[Example](not-a-real-file.md)\n```\n', encoding="utf-8")
        self.assertEqual([], documentation.check(self.root))


if __name__ == "__main__":
    unittest.main()

"""Regression checks for handwritten sources and generated build-output exclusions."""
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest


class CSharpLayoutTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        script = self.root / "scripts" / "check_csharp_layout.py"
        script.parent.mkdir()
        shutil.copyfile(Path(__file__).resolve().parents[2] / "scripts" / script.name, script)
        self.script = script

    def write(self, path, content):
        destination = self.root / path
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_text(content, encoding="utf-8")

    def check(self):
        return subprocess.run(
            [sys.executable, str(self.script)], capture_output=True, text=True, check=False
        )

    def test_generated_outputs_are_excluded_without_hiding_source_violations(self):
        self.write("src/App/Good.cs", """namespace Example
{
    /// <summary>Documented handwritten type.</summary>
    public class Good { }
}
""")
        for folder in ("src/App/bin", "src/App/obj", "tests/Tests/BIN", "tests/Tests/OBJ"):
            self.write(folder + "/Generated.g.cs", "namespace Generated { }")
        result = self.check()
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("Checked 1 C# files", result.stdout)
        self.write("tests/Tests/Broken.cs", "namespace Example { }")
        result = self.check()
        self.assertNotEqual(0, result.returncode)
        self.assertIn(str(Path("tests/Tests/Broken.cs")), result.stderr)
        self.assertNotIn("Generated.g.cs", result.stderr)

    def test_multiple_handwritten_types_remain_rejected(self):
        self.write("src/App/First.cs", """namespace Example
{
    public class First { }
    public class Second { }
}
""")
        result = self.check()
        self.assertNotEqual(0, result.returncode)
        self.assertIn("expected one declared type, found 2", result.stderr)

    def test_public_documentation_and_region_checks_remain_active(self):
        self.write("src/App/Missing.cs", """namespace Example
{
    /// <summary>Handwritten type with an undocumented member.</summary>
    public class Missing
    {
        public void Execute() { }
    }
}
""")
        result = self.check()
        self.assertNotEqual(0, result.returncode)
        self.assertIn("public member needs an adjacent XML comment", result.stderr)
        self.assertIn("public member outside the Public region", result.stderr)


if __name__ == "__main__":
    unittest.main()

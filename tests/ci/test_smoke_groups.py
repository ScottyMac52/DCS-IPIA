"""The isolated smoke groups keep every previous Program scenario, in the previous order, and the shared host setup."""
from pathlib import Path
import hashlib
import re
import unittest

PROGRAM = Path(__file__).resolve().parents[2] / "tests/Heimdall.App.Smoke/Program.cs"
# SHA-256 of the scenario statements of the single-process Main at 90deaf2 (setup and host creation excluded).
PREVIOUS_SCENARIOS = "7c86b3f965306a02abbc15e6fd51411a6f33ee45cb4d80c7a6bb513e47a94322"


def method(source, name):
    match = re.search(r"private static void " + name + r"\(\)\n        \{\n(.*?)\n        \}\n", source, re.S)
    assert match, name
    return match.group(1) + "\n"


class SmokeGroupTests(unittest.TestCase):
    def test_groups_preserve_the_previous_scenarios_in_order(self):
        source = PROGRAM.read_text(encoding="utf-8-sig")
        self.assertIn('Groups = ["startup", "games", "squadrons", "dcs-shared"]', source)
        startup = method(source, "StartupGroup").replace("            CreateHost();\n", "", 1)
        scenarios = startup + "".join(method(source, name) for name in ("GamesGroup", "SquadronsGroup", "DcsSharedGroup"))
        self.assertEqual(PREVIOUS_SCENARIOS, hashlib.sha256(scenarios.encode()).hexdigest())

    def test_every_group_creates_the_shared_host_and_reports_its_own_result(self):
        source = PROGRAM.read_text(encoding="utf-8-sig")
        self.assertEqual(1, method(source, "StartupGroup").count("CreateHost();"))
        self.assertIn('if (group != "startup") CreateHost();', source)
        for group, name in (("games", "GamesGroup"), ("squadrons", "SquadronsGroup"), ("dcs-shared", "DcsSharedGroup")):
            self.assertIn('"' + group + '" => ' + name + ",", source)
        self.assertIn("Console.WriteLine(SmokeGroupRunner.PassMarker + group);", source)
        self.assertIn("ApplicationTheme.Initialize();", method(source, "CreateHost"))

    def test_later_groups_start_from_the_state_edition_smoke_left_for_them(self):
        source = PROGRAM.read_text(encoding="utf-8")
        edition = (PROGRAM.parent / "EditionSmoke.cs").read_text(encoding="utf-8")
        left = ('EditionAccess.SetForTests(issuer.Policy); AppearanceManager.Configure(null); ThemeManager.Apply("Light");\n'
                "                AppearanceManager.ResetForTests();")
        self.assertIn(left, edition)  # the single-process Main ran every later scenario after this finally
        fixture = re.search(r"StartupInheritedState\(OwnerLicenseTestIssuer issuer\)\n        \{\n(.*?)\n        \}", source, re.S).group(1)
        self.assertEqual(" ".join(left.split()), " ".join(fixture.split()))
        self.assertIn('using var inherited = group == "startup" ? null : new OwnerLicenseTestIssuer();', source)
        self.assertNotIn("StartupInheritedState", method(source, "StartupGroup"))
        for free in ("EditionSmoke.cs", "FreeSnapshotImportSmoke.cs", "SquadronsWorkspaceSmoke.cs"):
            self.assertIn("EditionAccess.SetForTests(new EditionPolicy());", (PROGRAM.parent / free).read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()

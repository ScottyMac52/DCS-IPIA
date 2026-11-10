"""Guard the opt-in build boundary without pretending to compile the Windows application."""
from pathlib import Path
import re
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]


def release_source(source):
    """Select non-profiling branches of the simple profiling guards in the integration hooks."""
    result, include = [], True
    for line in source.splitlines():
        if line.strip() == "#if PERFORMANCE_TRACE":
            include = False
        elif line.strip() == "#else":
            include = not include
        elif line.strip() == "#endif":
            include = True
        elif include:
            result.append(line)
    return "\n".join(result)


class PerformanceBuildTests(unittest.TestCase):
    def test_profile_is_optimized_and_mapped_for_every_solution_project(self):
        props = ET.parse(ROOT / "Directory.Build.props").getroot()
        group = next(group for group in props.findall("PropertyGroup") if group.get("Condition") == "'$(Configuration)' == 'Profile'")
        self.assertEqual("true", group.findtext("Optimize"))
        self.assertIn("PERFORMANCE_TRACE", group.findtext("DefineConstants"))
        solution = (ROOT / "HEIMDALL.sln").read_text(encoding="utf-8")
        projects = re.findall(r'^Project\([^\n]+, "(\{[^}]+\})"$', solution, re.M)
        self.assertTrue(projects)
        for project in projects:
            for suffix in ("ActiveCfg", "Build.0"):
                self.assertIn(f"{project}.Profile|Any CPU.{suffix} = Profile|Any CPU", solution)

    def test_normal_builds_exclude_session_timer_and_all_instrumentation_hooks(self):
        for path in ("src/Heimdall.App/App.xaml.cs", "src/Heimdall.App/Games/GameAssignmentsView.cs",
                     "src/Heimdall.App/Games/DispatcherWorkspaceScheduler.cs",
                     "src/Heimdall.Application/Games/GameAssignmentBrowserBase.cs"):
            source = (ROOT / path).read_text(encoding="utf-8")
            with self.subTest(path=path):
                self.assertIn("#if PERFORMANCE_TRACE", source)
                self.assertNotIn("PerformanceTrace", release_source(source))
                self.assertNotIn("PerformanceSession", release_source(source))


if __name__ == "__main__":
    unittest.main()

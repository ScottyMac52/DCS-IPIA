"""Check literal DCS runner host-control names against the real window XAML."""
from pathlib import Path
import re
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]


class PerformanceSmokeControlTests(unittest.TestCase):
    def test_dcs_runner_host_controls_exist(self):
        source = (ROOT / 'tests/Heimdall.App.Smoke/DcsConfigurationPerformanceSmoke.cs').read_text(encoding='utf-8')
        window = ET.parse(ROOT / 'src/Heimdall.App/DcsModuleWorkspaceWindow.xaml')
        names = {node.attrib.get('{http://schemas.microsoft.com/winfx/2006/xaml}Name') for node in window.iter()}
        referenced = set(re.findall(r'WorkspaceUi.Find\(window,\s*"([^"]+)"', source))
        referenced.update(re.findall(r'window.FindName\("([^"]+)"', source))
        referenced.update(re.findall(r'Output\(window,\s*"([^"]+)"', source))
        self.assertTrue(referenced, 'No literal host controls were checked')
        self.assertFalse(referenced - names, f'Unknown DCS host controls: {sorted(referenced - names)}')


if __name__ == '__main__':
    unittest.main()

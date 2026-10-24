"""Catch broken project/XAML links and stale runtime identities after project moves."""
from pathlib import Path
import json
import re
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]


class ProjectIdentityTests(unittest.TestCase):
    def test_solution_and_all_project_references_resolve(self):
        solution = (ROOT / 'HEIMDALL.sln').read_text()
        projects = re.findall(r'"([^"\n]+\.csproj)"', solution)
        self.assertEqual(11, len(projects))
        self.assertNotIn('Dcs.Ipia', solution)
        for relative in projects:
            project = ROOT / relative.replace('\\', '/')
            self.assertTrue(project.is_file(), str(project))
            xml = ET.parse(project)
            for node in xml.findall('.//ProjectReference') + xml.findall('.//Compile'):
                path = node.get('Include', '')
                if path and '*' not in path and '$(' not in path:
                    self.assertTrue((project.parent / path.replace('\\', '/')).is_file(), f'{project}: {path}')
        self.assertFalse(list((ROOT / 'src').glob('Dcs.Ipia.*')))
        self.assertFalse(list((ROOT / 'tests').glob('Dcs.Ipia.*')))

    def test_xaml_classes_match_the_renamed_code_behind(self):
        for path in (ROOT / 'src/Heimdall.App').rglob('*.xaml'):
            cls = ET.parse(path).getroot().get('{http://schemas.microsoft.com/winfx/2006/xaml}Class')
            if not cls:
                continue
            source = Path(str(path) + '.cs').read_text()
            namespace, name = cls.rsplit('.', 1)
            self.assertRegex(source, r'namespace\s+' + re.escape(namespace) + r'\b')
            self.assertRegex(source, r'\bclass\s+' + re.escape(name) + r'\b')

    def test_runtime_namespace_logging_and_training_use_heimdall(self):
        for path in (ROOT / 'src').rglob('*.cs'):
            source = path.read_text()
            self.assertFalse(re.search(r'\b(?:namespace|using)\s+(?:Dcs\.Ipia|DcsIpia)\b', source), str(path))
        app = ROOT / 'src/Heimdall.App'
        settings = json.loads((app / 'appsettings.json').read_text())
        self.assertEqual('%LOCALAPPDATA%/HEIMDALL/Logs/heimdall-.log', settings['Serilog']['WriteTo'][0]['Args']['path'])
        self.assertTrue(all(key.startswith('Heimdall.') for key in settings['Serilog']['MinimumLevel']['Override'] if key not in {'Microsoft', 'System'}))
        for path in (app / 'Training/Assets').glob('*.json'):
            self.assertNotRegex(path.read_text(), r'\bIPIA\b|DCS-IPIA')


if __name__ == '__main__':
    unittest.main()

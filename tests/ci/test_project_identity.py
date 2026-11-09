"""Catch broken project/XAML links and stale runtime identities after project moves."""
from pathlib import Path
import json
import re
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]


def source_files(root, pattern):
    """Inspect authored files while ignoring local build output left by project moves."""
    return (path for path in root.rglob(pattern)
            if path.is_file() and not {'bin', 'obj'}.intersection(part.casefold() for part in path.relative_to(root).parts))


class ProjectIdentityTests(unittest.TestCase):
    def test_solution_and_all_project_references_resolve(self):
        solution = (ROOT / 'HEIMDALL.sln').read_text(encoding="utf-8")
        projects = re.findall(r'"([^"\n]+\.csproj)"', solution)
        expected = {
            str(path.relative_to(ROOT)).replace('\\', '/')
            for folder in ('src', 'tests')
            for path in source_files(ROOT / folder, '*.csproj')
        }
        self.assertEqual(expected, {path.replace('\\', '/') for path in projects})
        self.assertEqual(len(expected), len(projects), 'Duplicate solution project')
        self.assertNotIn('Dcs.Ipia', solution)
        for relative in projects:
            project = ROOT / relative.replace('\\', '/')
            self.assertTrue(project.is_file(), str(project))
            xml = ET.parse(project)
            for node in xml.findall('.//ProjectReference') + xml.findall('.//Compile'):
                path = node.get('Include', '')
                if path and '*' not in path and '$(' not in path:
                    self.assertTrue((project.parent / path.replace('\\', '/')).is_file(), f'{project}: {path}')
        for folder in ('src', 'tests'):
            for legacy in (ROOT / folder).glob('Dcs.Ipia.*'):
                self.assertFalse(list(source_files(legacy, '*')), str(legacy))

    def test_xaml_classes_match_the_renamed_code_behind(self):
        for path in source_files(ROOT / 'src/Heimdall.App', '*.xaml'):
            cls = ET.parse(path).getroot().get('{http://schemas.microsoft.com/winfx/2006/xaml}Class')
            if not cls:
                continue
            source = Path(str(path) + '.cs').read_text(encoding="utf-8")
            namespace, name = cls.rsplit('.', 1)
            self.assertRegex(source, r'namespace\s+' + re.escape(namespace) + r'\b')
            self.assertRegex(source, r'\bclass\s+' + re.escape(name) + r'\b')

    def test_runtime_namespace_logging_and_training_use_heimdall(self):
        for path in source_files(ROOT / 'src', '*.cs'):
            source = path.read_text(encoding="utf-8")
            self.assertFalse(re.search(r'\b(?:namespace|using)\s+(?:Dcs\.Ipia|DcsIpia)\b', source), str(path))
        app = ROOT / 'src/Heimdall.App'
        settings = json.loads((app / 'appsettings.json').read_text(encoding="utf-8"))
        self.assertEqual('%LOCALAPPDATA%/HEIMDALL/Logs/heimdall-.log', settings['Serilog']['WriteTo'][0]['Args']['path'])
        self.assertTrue(all(key.startswith('Heimdall.') for key in settings['Serilog']['MinimumLevel']['Override'] if key not in {'Microsoft', 'System'}))
        for path in (app / 'Training/Assets').glob('*.json'):
            self.assertNotRegex(path.read_text(encoding="utf-8"), r'\bIPIA\b|DCS-IPIA')


class ProjectIdentityPortabilityTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.app = self.root / 'src/Heimdall.App'
        self.app.mkdir(parents=True)
        (self.root / 'tests').mkdir()
        (self.root / 'HEIMDALL.sln').write_text('', encoding='utf-8')
        settings = {'Serilog': {'WriteTo': [{'Args': {'path': '%LOCALAPPDATA%/HEIMDALL/Logs/heimdall-.log'}}],
                                'MinimumLevel': {'Override': {'Heimdall.App': 'Information'}}}}
        (self.app / 'appsettings.json').write_text(json.dumps(settings), encoding='utf-8')

    def write(self, relative, text):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding='utf-8')

    def test_legacy_build_outputs_are_ignored_but_legacy_sources_are_rejected(self):
        for folder in ('src/Dcs.Ipia.App', 'tests/Dcs.Ipia.Tests'):
            self.write(folder + '/obj/Old.csproj', '<Project/>')
            self.write(folder + '/bin/Old.cs', 'namespace Dcs.Ipia;')
            self.write(folder + '/OBJ/Other.cs', 'namespace Dcs.Ipia;')
        with patch.dict(globals(), ROOT=self.root):
            checks = ProjectIdentityTests()
            checks.test_solution_and_all_project_references_resolve()
            checks.test_runtime_namespace_logging_and_training_use_heimdall()
            self.write('src/Dcs.Ipia.App/Old.cs', 'namespace Dcs.Ipia;')
            with self.assertRaises(AssertionError):
                checks.test_solution_and_all_project_references_resolve()
            with self.assertRaises(AssertionError):
                checks.test_runtime_namespace_logging_and_training_use_heimdall()

    def test_utf8_source_reads_are_explicit_and_generated_xaml_is_ignored(self):
        self.write('src/Heimdall.App/View.xaml',
                   '<Window xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" x:Class="Heimdall.App.View"/>')
        self.write('src/Heimdall.App/View.xaml.cs', 'namespace Heimdall.App { class View { } } // “UTF-8”')
        self.write('src/Heimdall.App/obj/Generated.xaml', 'Not authored XAML')
        self.write('src/Heimdall.App/obj/Generated.cs', 'namespace Dcs.Ipia;')
        original = Path.read_text

        def require_utf8(path, *args, **kwargs):
            self.assertEqual('utf-8', kwargs.get('encoding'))
            return original(path, *args, **kwargs)

        with patch.dict(globals(), ROOT=self.root), patch.object(Path, 'read_text', autospec=True, side_effect=require_utf8):
            checks = ProjectIdentityTests()
            checks.test_xaml_classes_match_the_renamed_code_behind()
            checks.test_runtime_namespace_logging_and_training_use_heimdall()


if __name__ == '__main__':
    unittest.main()

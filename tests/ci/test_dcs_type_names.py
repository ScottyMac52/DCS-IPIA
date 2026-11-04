"""Check DCS ownership names separately from persisted appearance identities."""
import json
from pathlib import Path
import re
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = json.loads((ROOT / 'docs/architecture/dcs-type-names.json').read_text())


class DcsTypeNameTests(unittest.TestCase):
    def test_dcs_types_and_filenames_use_the_explicit_prefix(self):
        for item in MANIFEST:
            with self.subTest(type=item['new']):
                self.assertTrue(item['new'].startswith(('Dcs', 'IDcs')))
                for filename in item['files']:
                    source = (ROOT / filename).read_text(encoding='utf-8-sig')
                    obsolete = Path(filename).with_name(Path(filename).name.replace(item['new'], item['old'], 1))
                    self.assertFalse((ROOT / obsolete).exists(), str(obsolete))
                    self.assertRegex(source, rf'\b(?:class|record|interface|enum)\s+{item["new"]}\b')
                    self.assertNotRegex(source, rf'\b(?:class|record|interface|enum)\s+{item["old"]}\b')

    def test_renamed_xaml_retains_existing_appearance_identities(self):
        for item in MANIFEST:
            if 'appearance_key' not in item:
                continue
            for filename in item['files']:
                if not filename.endswith('.xaml.cs'):
                    if not any(path.endswith('.xaml.cs') for path in item['files']):
                        source = (ROOT / filename).read_text()
                        self.assertIn(f'AppearanceManager.SetTarget(this, "{item["appearance_key"]}")', source)
                    continue
                with self.subTest(type=item['new']):
                    root = ET.parse(ROOT / filename.removesuffix('.cs')).getroot()
                    self.assertTrue(root.attrib['{http://schemas.microsoft.com/winfx/2006/xaml}Class'].endswith('.' + item['new']))
                    self.assertEqual(root.attrib['{clr-namespace:Heimdall.App}AppearanceManager.Target'], item['appearance_key'])

    def test_shared_components_keep_their_general_names(self):
        shared = {'GameWorkspaceWindow', 'GameWorkspaceShell', 'GameWorkspaceFeatureHost',
                  'HardwareDiagramPresenter', 'CompositeKneeboardRenderer', 'ICommandCatalog',
                  'UiLayerDiagram', 'IConnectedDeviceDiscovery', 'BundledSharedCatalog'}
        self.assertTrue(shared.isdisjoint(item['old'] for item in MANIFEST))


if __name__ == '__main__':
    unittest.main()

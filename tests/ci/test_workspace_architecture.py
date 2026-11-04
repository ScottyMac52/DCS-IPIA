"""Enforce the requested ownership boundaries, independently of behavior tests."""
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
APP = ROOT / 'src/Heimdall.App'


class WorkspaceArchitectureTests(unittest.TestCase):
    def test_dcs_inherits_the_shared_window_in_csharp_and_xaml(self):
        source = (APP / 'DcsModuleWorkspaceWindow.xaml.cs').read_text(encoding='utf-8-sig')
        self.assertRegex(source, r'class DcsModuleWorkspaceWindow : GameWorkspaceWindow')
        root = ET.parse(APP / 'DcsModuleWorkspaceWindow.xaml').getroot()
        self.assertEqual(root.attrib['{http://schemas.microsoft.com/winfx/2006/xaml}Class'],
                         'Heimdall.App.DcsModuleWorkspaceWindow')
        self.assertEqual(root.attrib['{clr-namespace:Heimdall.App}AppearanceManager.Target'],
                         'ModuleWorkspaceWindow')
        self.assertFalse((APP / 'ModuleWorkspaceWindow.xaml.cs').exists())
        self.assertEqual(root.tag,
                         '{clr-namespace:Heimdall.App.Games}GameWorkspaceWindow')

    def test_base_has_no_game_or_native_session_dependencies(self):
        source = (APP / 'Games/GameWorkspaceWindow.cs').read_text()
        for forbidden in ('Heimdall.Dcs', 'Heimdall.EliteDangerous', 'Heimdall.Squadrons',
                          'Heimdall.WarThunder', 'DcsEditorWorkspaceSnapshot', 'GameProfileSession'):
            self.assertNotIn(forbidden, source)
        for responsibility in ('NavigateToFeature', 'OnClosing', 'OnClosed', 'RegisterWorkspaceCleanup'):
            self.assertIn(responsibility, source)

    def test_dcs_window_delegates_editing_output_and_feature_construction(self):
        source = (APP / 'DcsModuleWorkspaceWindow.xaml.cs').read_text(encoding='utf-8-sig')
        for collaborator in ('DcsWorkspaceSession', 'DcsWorkspaceOutput', 'DcsWorkspaceFeatures'):
            self.assertIn(collaborator, source)
        for hook in ('SaveWorkspaceCoreAsync', 'DiscardWorkspaceCore', 'OnWorkspaceStateChanged'):
            self.assertRegex(source, rf'override \w+(?:<[^>]+>)? {hook}\(')
        for forbidden in ('new DcsDraftAssignmentService', 'DcsModuleSnapshotWorkflow.Freeze',
                          'DcsModuleCompleteReleaseWorkflow.Write', '_repository.SaveAsync',
                          'new DcsDevicesView', 'Dictionary<string, UserControl>'):
            self.assertNotIn(forbidden, source)

    def test_editing_and_output_do_not_depend_on_a_concrete_window(self):
        for name in ('DcsWorkspaceSession', 'DcsWorkspaceOutput'):
            source = (APP / f'Games/Dcs/{name}.cs').read_text()
            self.assertNotRegex(source, r'\b(?:DcsModuleWorkspaceWindow|System.Windows|FindName|TextBlock)\b')


if __name__ == '__main__':
    unittest.main()

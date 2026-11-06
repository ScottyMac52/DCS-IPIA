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

    def test_assignments_are_composed_by_a_game_neutral_base(self):
        base = (APP / 'Games/GameAssignmentsView.cs').read_text()
        for forbidden in ('Heimdall.Dcs', 'Dcs', 'Module', 'UiLayerDiagram'):
            self.assertNotIn(forbidden, base)
        for responsibility in ('CommandsPanel', 'ControllerPanel', 'EditorPanel',
                               'ExecuteAssignment', 'SaveAssignmentsAsync', 'DiscardAssignments',
                               'BindAssignmentChanges'):
            self.assertIn(responsibility, base)
        root = ET.parse(APP / 'Features/Assignments/DcsAssignmentsView.xaml').getroot()
        self.assertEqual(root.tag, '{clr-namespace:Heimdall.App.Games}GameAssignmentsView')
        source = (APP / 'Features/Assignments/DcsAssignmentsView.xaml.cs').read_text()
        self.assertIn('DcsAssignmentsView : GameAssignmentsView', source)
        for collaborator in ('DcsAssignmentDiagram', 'DcsAssignmentEditing',
                             'DcsAssignmentLabels', 'DcsAssignmentDragDrop', 'DcsAssignmentPreview'):
            self.assertIn(collaborator, source)
            implementation = (APP / f'Features/Assignments/{collaborator}.cs').read_text()
            self.assertNotIn('DcsAssignmentsView', implementation)
        self.assertLess(len(source.splitlines()), 350)
        self.assertFalse((APP / 'Features/Assignments/Selection/DcsAssignmentsView.cs').exists())
        self.assertFalse((APP / 'Features/Assignments/Workflow/DcsAssignmentsView.cs').exists())

    def test_elite_uses_shared_workspace_and_assignment_algorithms(self):
        window = (APP / 'Games/EliteWorkspaceWindow.xaml.cs').read_text()
        assignments = (APP / 'Games/EliteAssignmentsView.xaml.cs').read_text()
        self.assertIn('EliteWorkspaceWindow : GameWorkspaceWindow', window)
        self.assertIn('EliteAssignmentsView : GameAssignmentsView', assignments)
        for filename, base in [('EliteWorkspaceWindow.xaml', 'GameWorkspaceWindow'),
                               ('EliteAssignmentsView.xaml', 'GameAssignmentsView')]:
            root = ET.parse(APP / 'Games' / filename).getroot()
            self.assertEqual(root.tag, '{clr-namespace:Heimdall.App.Games}' + base)
        for hook in ('SaveWorkspaceCoreAsync', 'DiscardWorkspaceCore', 'OnWorkspaceStateChanged'):
            self.assertIn(hook, window)
        for collaborator in ('EliteControllerPresenter', 'EliteAssignmentEditing',
                             'EliteAssignmentBrowser', 'EliteAssignmentDrawing',
                             'EliteProfileOperations', 'EliteReferencePresenter'):
            self.assertIn(collaborator, window)
            implementation = (APP / f'Games/Elite/{collaborator}.cs').read_text()
            self.assertNotIn('EliteWorkspaceWindow', implementation)
        self.assertNotIn('new GameWorkspaceFeatureHost', window)
        self.assertNotIn('GameWorkspaceLeaveGuard.Confirm', window)
        self.assertNotIn('new WorkspaceAssignmentWorkflow', window)
        self.assertFalse((APP / 'Games/Elite/Workflow/EliteWorkspaceWindow.cs').exists())
        self.assertLess(len(window.splitlines()), 450)

    def test_squadrons_uses_shared_workspace_and_assignment_algorithms(self):
        window = (APP / 'Games/SquadronsWorkspaceWindow.xaml.cs').read_text()
        assignments = (APP / 'Games/SquadronsAssignmentsView.xaml.cs').read_text()
        self.assertIn('SquadronsWorkspaceWindow : GameWorkspaceWindow', window)
        self.assertIn('SquadronsAssignmentsView : GameAssignmentsView', assignments)
        for filename, base in [('SquadronsWorkspaceWindow.xaml', 'GameWorkspaceWindow'),
                               ('SquadronsAssignmentsView.xaml', 'GameAssignmentsView')]:
            root = ET.parse(APP / 'Games' / filename).getroot()
            self.assertEqual(root.tag, '{clr-namespace:Heimdall.App.Games}' + base)
        for hook in ('SaveWorkspaceCoreAsync', 'DiscardWorkspaceCore', 'OnWorkspaceStateChanged'):
            self.assertIn(hook, window)
        for collaborator in ('SquadronsControllerPresenter', 'SquadronsAssignmentEditing',
                             'SquadronsAssignmentBrowser', 'SquadronsAssignmentDrawing',
                             'SquadronsProfileOperations'):
            self.assertIn(collaborator, window)
            implementation = (APP / f'Games/Squadrons/{collaborator}.cs').read_text()
            self.assertNotIn('SquadronsWorkspaceWindow', implementation)
        self.assertNotIn('new GameWorkspaceFeatureHost', window)
        self.assertNotIn('GameWorkspaceLeaveGuard.Confirm', window)
        self.assertNotIn('new WorkspaceAssignmentWorkflow', window)
        self.assertFalse((APP / 'Games/Squadrons/Workflow/SquadronsWorkspaceWindow.cs').exists())
        self.assertLess(len(window.splitlines()), 450)

    def test_editing_and_output_do_not_depend_on_a_concrete_window(self):
        for name in ('DcsWorkspaceSession', 'DcsWorkspaceOutput'):
            source = (APP / f'Games/Dcs/{name}.cs').read_text()
            self.assertNotRegex(source, r'\b(?:DcsModuleWorkspaceWindow|System.Windows|FindName|TextBlock)\b')


if __name__ == '__main__':
    unittest.main()

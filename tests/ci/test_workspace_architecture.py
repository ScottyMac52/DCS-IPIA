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

    def test_profile_operations_are_owned_by_one_game_neutral_sequence(self):
        application = ROOT / 'src/Heimdall.Application'
        base = (application / 'Games/GameProfileOperationsBase.cs').read_text(encoding='utf-8-sig')
        store = (application / 'Games/GameStoreProfileOperationsBase.cs').read_text(encoding='utf-8-sig')
        command = (application / 'Games/GameProfileCommandBase.cs').read_text(encoding='utf-8-sig')
        for source in (base, store, command):
            for forbidden in ('Dcs', 'Elite', 'Squadrons', 'WarThunder', 'switch'):
                self.assertNotIn(forbidden, source)
        for sealed_entry in ('public async Task<WorkspaceOperationResult> SaveAsync(', 'public Task<WorkspaceOperationResult> DiscardAsync('):
            self.assertIn(sealed_entry, base)
        for hook in ('PrepareSave', 'PersistAsync', 'AcceptCommit', 'RestoreSaved', 'RefreshAfterPersistence'):
            self.assertRegex(base, rf'protected abstract [^;(]+ {hook}\(')
        window = (APP / 'Games/GameWorkspaceWindow.cs').read_text(encoding='utf-8-sig')
        for removed in ('SaveWorkspaceCoreAsync', 'DiscardWorkspaceCore'):
            self.assertNotIn(removed, window)
        self.assertIn('ProfileOperations.SaveAsync(', window)
        self.assertIn('ProfileOperations.DiscardAsync(', window)
        services = (APP / 'Games/GameWorkspaceServices.cs').read_text(encoding='utf-8-sig')
        for moved in ('EliteDangerousProfileBackend', 'EliteDeviceReferenceMatcher', 'Heimdall.EliteDangerous'):
            self.assertNotIn(moved, services)
        self.assertIn('interface IEliteWorkspaceServices', (APP / 'Games/Elite/IEliteWorkspaceServices.cs').read_text(encoding='utf-8-sig'))

    def test_dcs_window_delegates_editing_output_and_feature_construction(self):
        source = (APP / 'DcsModuleWorkspaceWindow.xaml.cs').read_text(encoding='utf-8-sig')
        for collaborator in ('DcsWorkspaceSession', 'DcsWorkspaceOutput', 'DcsWorkspaceFeatures'):
            self.assertIn(collaborator, source)
        for hook in ('OnWorkspaceStateChanged',):
            self.assertRegex(source, rf'override \w+(?:<[^>]+>)? {hook}\(')
        self.assertRegex(source, r'override IGameProfileOperations ProfileOperations\b')
        self.assertIn('new DcsProfileOperations(', source)
        operations = (APP / 'Games/Dcs/DcsProfileOperations.cs').read_text(encoding='utf-8-sig')
        self.assertIn(': GameProfileOperationsBase<DcsEditorWorkspaceSnapshot, DcsEditorWorkspaceSnapshot>', operations)
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
        window = (APP / 'Games/Elite/EliteWorkspaceWindow.xaml.cs').read_text()
        assignments = (APP / 'Games/Elite/EliteAssignmentsView.xaml.cs').read_text()
        self.assertIn('EliteWorkspaceWindow : GameWorkspaceWindow', window)
        self.assertIn('EliteAssignmentsView : GameAssignmentsView', assignments)
        for filename, base in [('EliteWorkspaceWindow.xaml', 'GameWorkspaceWindow'),
                               ('EliteAssignmentsView.xaml', 'GameAssignmentsView')]:
            root = ET.parse(APP / 'Games' / 'Elite' / filename).getroot()
            self.assertEqual(root.tag, '{clr-namespace:Heimdall.App.Games}' + base)
        for hook in ('override IGameProfileOperations ProfileOperations', 'OnWorkspaceStateChanged'):
            self.assertIn(hook, window)
        saving = (APP / 'Games/Elite/EliteProfileSaving.cs').read_text()
        self.assertIn(': GameStoreProfileOperationsBase(', saving)
        self.assertNotIn('Store.Save(', window)
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
        window = (APP / 'Games/Squadrons/SquadronsWorkspaceWindow.xaml.cs').read_text()
        assignments = (APP / 'Games/Squadrons/SquadronsAssignmentsView.xaml.cs').read_text()
        self.assertIn('SquadronsWorkspaceWindow : GameWorkspaceWindow', window)
        self.assertIn('SquadronsAssignmentsView : GameAssignmentsView', assignments)
        for filename, base in [('SquadronsWorkspaceWindow.xaml', 'GameWorkspaceWindow'),
                               ('SquadronsAssignmentsView.xaml', 'GameAssignmentsView')]:
            root = ET.parse(APP / 'Games' / 'Squadrons' / filename).getroot()
            self.assertEqual(root.tag, '{clr-namespace:Heimdall.App.Games}' + base)
        for hook in ('override IGameProfileOperations ProfileOperations', 'OnWorkspaceStateChanged'):
            self.assertIn(hook, window)
        saving = (APP / 'Games/Squadrons/SquadronsProfileSaving.cs').read_text()
        self.assertIn(': GameStoreProfileOperationsBase(', saving)
        self.assertNotIn('Store.Save(', window)
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

    def test_war_thunder_uses_shared_workspace_and_assignment_algorithms(self):
        window = (APP / 'Games/WarThunder/WarThunderWorkspaceWindow.xaml.cs').read_text()
        assignments = (APP / 'Games/WarThunder/WarThunderAssignmentsView.xaml.cs').read_text()
        self.assertIn('WarThunderWorkspaceWindow : GameWorkspaceWindow', window)
        self.assertIn('WarThunderAssignmentsView : GameAssignmentsView', assignments)
        for filename, base in [('WarThunderWorkspaceWindow.xaml', 'GameWorkspaceWindow'),
                               ('WarThunderAssignmentsView.xaml', 'GameAssignmentsView')]:
            root = ET.parse(APP / 'Games' / 'WarThunder' / filename).getroot()
            self.assertEqual(root.tag, '{clr-namespace:Heimdall.App.Games}' + base)
        for hook in ('override IGameProfileOperations ProfileOperations', 'OnWorkspaceStateChanged'):
            self.assertIn(hook, window)
        saving = (APP / 'Games/WarThunder/WarThunderProfileSaving.cs').read_text()
        self.assertIn(': GameStoreProfileOperationsBase(', saving)
        self.assertNotIn('Store.Save(', window)
        for collaborator in ('WarThunderControllerPresenter', 'WarThunderAssignmentEditing',
                             'WarThunderAssignmentBrowser', 'WarThunderAssignmentDrawing',
                             'WarThunderProfileOperations'):
            self.assertIn(collaborator, window)
            implementation = (APP / f'Games/WarThunder/{collaborator}.cs').read_text()
            self.assertNotIn('WarThunderWorkspaceWindow', implementation)
        self.assertNotIn('new GameWorkspaceFeatureHost', window)
        self.assertNotIn('GameWorkspaceLeaveGuard.Confirm', window)
        self.assertNotIn('new WorkspaceAssignmentWorkflow', window)
        self.assertFalse((APP / 'Games/WarThunder/Workflow/WarThunderWorkspaceWindow.cs').exists())
        self.assertLess(len(window.splitlines()), 450)

    def test_native_presentation_types_live_in_their_game_namespace(self):
        self.assertFalse((APP / 'Games/GameExportResolution.cs').exists())
        self.assertTrue((APP / 'Games/Elite/EliteExportResolution.cs').exists())
        for game in ('Elite', 'Squadrons', 'WarThunder'):
            self.assertEqual(list((APP / 'Games').glob(game + '*.cs')), [])
            self.assertEqual(list((APP / 'Games').glob(game + '*.xaml')), [])
            folder = APP / 'Games' / game
            for source in folder.glob('*.cs'):
                self.assertIn('namespace Heimdall.App.Games.' + game, source.read_text())
            for view in folder.glob('*.xaml'):
                root = ET.parse(view).getroot()
                self.assertEqual(root.attrib['{http://schemas.microsoft.com/winfx/2006/xaml}Class'],
                                 'Heimdall.App.Games.' + game + '.' + view.stem)

    def test_editing_and_output_do_not_depend_on_a_concrete_window(self):
        for name in ('DcsWorkspaceSession', 'DcsWorkspaceOutput'):
            source = (APP / f'Games/Dcs/{name}.cs').read_text()
            self.assertNotRegex(source, r'\b(?:DcsModuleWorkspaceWindow|System.Windows|FindName|TextBlock)\b')


if __name__ == '__main__':
    unittest.main()

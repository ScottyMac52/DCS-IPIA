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
        self.assertFalse((APP / 'Games/Dcs/DcsWorkspaceOutput.cs').exists())
        # The approved Elite edge interface keeps native types out of the shared layer: Application stays ED-free.
        application = APP.parent / 'Heimdall.Application'
        for source in [*application.rglob('*.cs'), *application.glob('*.csproj')]:
            if {'bin', 'obj'} & set(source.relative_to(application).parts):
                continue
            text = source.read_text(encoding='utf-8-sig')
            self.assertNotIn('Heimdall.EliteDangerous', text, str(source))
            self.assertNotIn('IEliteWorkspaceServices', text, str(source))
        # One command type per file (C# layout gate); each still derives from the shared command base.
        commands = {f'{game}/{name}.cs': (name,) for game, names in {
            'Dcs': ('DcsOutputCommand',),
            'Elite': ('EliteImportCommand', 'EliteCreateConfigurationCommand', 'EliteDeleteConfigurationCommand', 'EliteExportCommand'),
            'Squadrons': ('SquadronsImportCommand', 'SquadronsNewProfileCommand', 'SquadronsExportCommand'),
            'WarThunder': ('WarThunderImportCommand', 'WarThunderDuplicateCommand', 'WarThunderExportCommand'),
        }.items() for name in names}
        for path, names in commands.items():
            source = (APP / 'Games' / path).read_text(encoding='utf-8-sig')
            for name in names:
                self.assertRegex(source, rf'class {name}\b[^{{]*:\s*GameProfileCommandBase<')
            self.assertNotRegex(source, r'throw new NotImplementedException|throw new NotSupportedException')
        self.assertIn('interface IEliteWorkspaceServices', (APP / 'Games/Elite/IEliteWorkspaceServices.cs').read_text(encoding='utf-8-sig'))

    def test_selection_hot_paths_skip_native_reloads_and_serialization(self):
        for view in ('Pages/DcsPagesView', 'Kneeboards/DcsKneeboardsView', 'Documentation/DcsDocumentationView',
                     'Modifiers/DcsModifiersView', 'Devices/DcsDevicesView'):
            source = (APP / f'Features/{view}.xaml.cs').read_text(encoding='utf-8-sig')
            self.assertIn('WorkspaceChangeObserver.ReloadUnlessSelectionOnly(', source)
            self.assertNotRegex(source, r'StateChanged \+= \(_, _\) => Refresh\(\)')
        manager = (APP / 'Features/Commands/DcsCommandsManagementView.xaml.cs').read_text(encoding='utf-8-sig')
        self.assertIn('_browser = new(context,', manager)
        self.assertIn('_browser.ProjectionChanged +=', manager)
        self.assertNotIn('DcsCommandBrowser.Filter(', manager)
        self.assertNotIn('new GameAssignmentSelection', manager)
        browser = (ROOT / 'src/Heimdall.Application/Games/GameAssignmentBrowserBase.cs').read_text(encoding='utf-8-sig')
        self.assertIn('_context.ContentVersion', browser)
        self.assertIn('else _observed = selection;', browser)
        self.assertNotIn('CaptureSnapshot(', browser)
        assignments = (APP / 'Games/GameAssignmentsView.cs').read_text(encoding='utf-8-sig')
        changed = assignments[assignments.index('private void Changed('):assignments.index('private void RefreshAvailabilityOnly(')]
        self.assertNotIn('CaptureAssignmentSnapshot', changed)
        self.assertIn('WorkspaceChangeKind.Content', changed)
        profile = (ROOT / 'src/Heimdall.Application/Games/GameProfileWorkspaceContextBase.cs').read_text(encoding='utf-8-sig')
        self.assertNotIn('Snapshot.Id', profile)
        self.assertIn('ContentFingerprint', profile)
        self.assertNotRegex('\n'.join(p.read_text(encoding='utf-8-sig') for p in (APP / 'Games').rglob('*.cs')), r'Snapshot\.Hardware')
        games = '\n'.join(p.read_text(encoding='utf-8-sig') for p in (APP / 'Games').rglob('*.cs'))
        self.assertNotRegex(games, r'Snapshot\.(Id|Name)\b')
        thunder = (APP / 'Games/WarThunder/WarThunderControllerPresenter.cs').read_text(encoding='utf-8-sig')
        self.assertNotIn('Session?.Snapshot', thunder)
        self.assertNotIn('WarThunderPhysicalMap.Create(', thunder)
        for path in ('Elite/EliteWorkspaceContext.cs', 'Squadrons/SquadronsWorkspaceContext.cs', 'WarThunder/WarThunderWorkspaceContext.cs'):
            self.assertIn('ProfileContentCache<', (APP / 'Games' / path).read_text(encoding='utf-8-sig'))

    def test_dcs_window_delegates_editing_output_and_feature_construction(self):
        source = (APP / 'DcsModuleWorkspaceWindow.xaml.cs').read_text(encoding='utf-8-sig')
        for collaborator in ('DcsWorkspaceSession', 'DcsOutputCommand', 'DcsWorkspaceFeatures'):
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
        for name in ('DcsWorkspaceSession', 'DcsOutputCommand'):
            source = (APP / f'Games/Dcs/{name}.cs').read_text()
            self.assertNotRegex(source, r'\b(?:DcsModuleWorkspaceWindow|System.Windows|FindName|TextBlock)\b')


if __name__ == '__main__':
    unittest.main()


class BrowserEditorOwnershipTests(unittest.TestCase):
    def test_editor_renderers_do_not_write_navigation_selection_from_controls(self):
        thunder = (APP / 'Games/WarThunder/WarThunderAssignmentEditing.cs').read_text()
        render = thunder[thunder.index('internal void RefreshEditor()'):thunder.index('private WarThunderPhysicalInput[]')]
        self.assertNotIn('SelectCommand(', render)
        self.assertNotIn('SelectAlternative(', render)
        self.assertNotIn('Records.SelectedItem', render)
        self.assertIn('context.State.Selection', render)
        elite = (APP / 'Games/Elite/EliteAssignmentEditing.cs').read_text()
        render = elite[elite.index('internal void LoadAssignment()'):elite.index('internal void ApplyClicked')]
        self.assertNotIn('_selection.SelectSlot(', render)
        self.assertNotIn('SelectInput(', render)
        squadrons = (APP / 'Games/Squadrons/SquadronsAssignmentEditing.cs').read_text()
        render = squadrons[squadrons.index('internal void UpdateEditor()'):squadrons.index('private void UpdateInputChoices()')]
        self.assertNotIn('BindingsGrid.SelectedItem', render)
        self.assertIn('context.State.Selection', render)

    def test_shared_view_owns_completed_editor_revisions(self):
        view = (APP / 'Games/GameAssignmentsView.cs').read_text()
        self.assertIn('WorkspaceEditorObserver', view)
        self.assertLess(view.index('nativeRefresh(); NativeRefreshPasses++'), view.index('_editorObserver?.Accept('))
        for game in ('Elite', 'Squadrons', 'WarThunder'):
            browser = (APP / f'Games/{game}/{game}AssignmentBrowser.cs').read_text()
            self.assertNotIn('_editorKey', browser)
            window = (APP / f'Games/{game}/{game}WorkspaceWindow.xaml.cs').read_text()
            self.assertIn('BindBrowser(_browser, () => _context.Refreshing)', window)
            self.assertIn('SynchronizeNativeAssignments()', window)


class AssignmentBrowserRoleTests(unittest.TestCase):
    def test_all_native_roles_inherit_the_shared_algorithm_and_presentation_consumes_them(self):
        for assembly, game, native in (("Dcs", "Dcs", "DcsCommandPaletteEntry"), ("EliteDangerous", "Elite", "SimCommand"),
                                       ("Squadrons", "Squadrons", "SimCommand"), ("WarThunder", "WarThunder", "SimCommand")):
            source = (ROOT / f"src/Heimdall.{assembly}/{game}AssignmentBrowser.cs").read_text()
            self.assertIn(f"GameAssignmentBrowserBase<{native},", source)
            for hook in ("ReadCommands", "ReadRecords", "IncludeCommand", "IncludeRecord", "SelectionForRecord", "FilterForSelection"):
                self.assertIn("override", source); self.assertIn(hook, source)
            if game != "Dcs":
                edge = (APP / f"Games/{game}/{game}AssignmentBrowser.cs").read_text()
                self.assertIn(f": Heimdall.{assembly}.{game}AssignmentBrowser(context,", edge)
                self.assertNotIn("GameCommandBrowser.Filter(", edge)
                self.assertNotIn(".Snapshot", edge)
                self.assertNotRegex(edge, r"Func<(?:Elite|Squadrons|WarThunder)(?:AssignmentEditing|AssignmentDrawing|ControllerPresenter)>")
        editing = (APP / 'Games/Squadrons/SquadronsAssignmentEditing.cs').read_text()
        self.assertIn('Func<ISquadronsAssignmentBrowser<SquadronsWorkspaceRow>>', editing)
        self.assertNotIn('Func<SquadronsAssignmentBrowser>', editing)

if __name__ == "__main__":
    unittest.main()


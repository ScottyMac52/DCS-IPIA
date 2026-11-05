# DCS type naming

Audited DCS production classes and their supporting contracts use a `Dcs` prefix.
Their interfaces use `IDcs`.
The [rename inventory](dcs-type-names.json) records all 202 old/new names and the
new source paths, including partial classes and supporting data contracts.

| Responsibility | Examples |
| --- | --- |
| Workspace presentation | `DcsModuleWorkspaceViewModel`, `DcsWindowContext`, `DcsAssignmentsView`, `DcsDevicesView`, `DcsCommandsManagementView` |
| Authoring and import | `DcsNewModuleWindow`, `DcsSavedGamesImportWindow`, `DcsModuleWorkspaceLoader`, `DcsWorkspaceStructureEditor` |
| Drafts and native state | `DcsDraftAssignmentService`, `DcsEditorWorkspaceSnapshot`, `DcsModuleConfiguration`, `DcsDeviceProfile` |
| Feature context and persistence contracts | `IDcsModuleWorkspaceContext`, `IDcsEditorWorkspaceRepository`, `IDcsModuleRepository` |
| Storage implementations | `DcsSqliteEditorWorkspaceRepository`, `DcsSqliteModuleRepository`, `DcsSqliteSharedUiLayerWorkspaceRepository` |
| DCS output | `DcsModuleSnapshotWorkflow`, `DcsModuleLuaProfileRenderer`, `DcsSnapshotSummaryKneeboardRenderer`, `DcsOvGmePackageBuilder` |
| DCS training implementation | `DcsTrainingWindow`, `DcsTrainingEnvironment`, `DcsTrainingActions` |

Classification follows responsibility and native data dependencies. It does not
follow namespace location or the fact that DCS happens to be one caller.
`GameWorkspaceWindow`, `GameWorkspaceShell`, generic hardware discovery, command
catalog interfaces for multiple simulators, reusable diagrams/renderers, app
appearance, and the general training runner retain their shared identities.
For example, `CompositeKneeboardRenderer` serves the other games as well and is
not renamed despite using the original DCS-style artwork layout.

## Compatibility

This is a source type/file naming change. Namespaces, assemblies, schema versions,
JSON property names, database tables, native paths, embedded assets and authored
configuration files retain their existing identities. C# identifiers are rewritten
as syntax tokens; string literals are preserved. XAML class/type references,
source includes, callers, tests and documentation use the new names.

Existing appearance keys such as `AssignmentsView`, `DevicesView`, `TrainingWindow`
and `SharedUiLayerWorkspaceWindow` remain persisted keys. Each renamed WPF surface
sets an explicit appearance target to its previous identity. The shared appearance
loader checks that explicit target before falling back to the runtime type name.
Existing backgrounds, opacity choices and child-view inheritance therefore retain
their original lookup paths. The earlier renamed `DcsModuleWorkspaceWindow` keeps
its `ModuleWorkspaceWindow` key as well.

These are source-breaking type renames for code referencing the old CLR names;
no obsolete wrapper classes are retained. Saved user data does not depend on those
CLR names. The existing snapshot/import/export, database, appearance, training and
Windows smoke suites remain the behavioral acceptance checks.

The Python naming checks enforce the audited declarations and filenames, reject
obsolete duplicate source files, and check XAML class/appearance identities.
Windows smoke execution is still required to validate mounted WPF behavior.

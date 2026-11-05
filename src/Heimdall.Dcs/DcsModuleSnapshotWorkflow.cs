using System.Text.Json;
using System.Text.Json.Serialization;
using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Packaging;

namespace Heimdall.Dcs
{
    /// <summary>Validates, exports, restores and builds complete module package inputs without source checkouts.</summary>
    public static class DcsModuleSnapshotWorkflow
    {
        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        #region BEGIN Public
        /// <summary>Freezes and validates the exact saved editor state and requested package version.</summary>
        /// <param name="workspace">Saved module state with pinned assets.</param>
        /// <param name="version">Package version.</param>
        /// <returns>Portable validated package inputs.</returns>
        public static DcsModuleBuildSnapshot Freeze(DcsEditorWorkspaceSnapshot workspace, string version)
        {
            var snapshot = new DcsModuleBuildSnapshot(version, DcsEditorSnapshotSerializer.Serialize(workspace));
            Build(snapshot);
            return snapshot;
        }

        /// <summary>Generates and independently verifies an archive entirely from frozen inputs.</summary>
        /// <param name="snapshot">Verified editor state and package version.</param>
        /// <returns>Deterministic OvGME archive and checksum.</returns>
        public static DcsOvGmePackage Build(DcsModuleBuildSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var workspace = DcsEditorSnapshotSerializer.Deserialize(snapshot.Workspace);
            DcsNewModuleWorkspaceFactory.ValidateIdentity(workspace.Configuration.Module.DcsId, workspace.Configuration.Module.DisplayName);
            if (workspace.Sources?.ContainsKey("config/ipia-created-module.json") == true)
                _ = new DcsDraftAssignmentService(workspace.Configuration,
                    workspace.ModuleCommands.Select(x => new DcsDraftCommand(x.Id, x.Name, x.IsAxis, AssignmentLayer.Module))
                        .Concat(workspace.UiCommands.Select(x => new DcsDraftCommand(x.DcsCommandId, x.Name, x.IsAxis, AssignmentLayer.UiLayer))),
                    workspace.Assignments, DcsWorkspaceStructureEditor.UiModifierNames(workspace));
            DcsModuleIntegrationImport.Instructions(workspace);
            var package = DcsSnapshotOvGmeComposer.Build(workspace, snapshot.Version);
            DcsOvGmePackageVerifier.Verify(package);
            return package;
        }

        /// <summary>Encodes portable package inputs after verifying the complete generated output.</summary>
        /// <param name="snapshot">Frozen module inputs.</param>
        /// <returns>One UTF-8 JSON document with canonical workspace content and digest.</returns>
        public static string Export(DcsModuleBuildSnapshot snapshot)
        {
            Build(snapshot);
            return JsonSerializer.Serialize(snapshot, Options) + "\n";
        }

        /// <summary>Writes a complete snapshot to a new file without replacing an existing artifact.</summary>
        /// <param name="snapshot">Frozen module inputs.</param>
        /// <param name="path">New snapshot file path.</param>
        public static void Write(DcsModuleBuildSnapshot snapshot, string path)
        {
            var json = Export(snapshot);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, json, new System.Text.UTF8Encoding(false));
                File.Move(temporary, path, false);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        /// <summary>Reads portable package inputs and rejects corrupt, unsupported or unbuildable state.</summary>
        /// <param name="json">Previously exported JSON.</param>
        /// <returns>Validated package inputs without database writes.</returns>
        public static DcsModuleBuildSnapshot Read(string json)
        {
            var snapshot = JsonSerializer.Deserialize<DcsModuleBuildSnapshot>(json, Options)
                ?? throw new InvalidDataException("The module snapshot is empty.");
            Build(snapshot);
            return snapshot;
        }

        /// <summary>Restores verified package inputs as a new module without replacing existing saved identities.</summary>
        /// <param name="snapshot">Verified package inputs.</param>
        /// <param name="repository">Atomic insert-only local storage.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Frozen restored workspace; no shared default revision is modified.</returns>
        public static async Task<DcsEditorWorkspaceSnapshot> RestoreAsync(DcsModuleBuildSnapshot snapshot,
            IDcsEditorWorkspaceCreationRepository repository, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(repository);
            Build(snapshot);
            var workspace = DcsEditorSnapshotSerializer.Deserialize(snapshot.Workspace);
            await repository.CreateAsync(workspace, ct);
            return workspace;
        }
        #endregion END Public
    }
}

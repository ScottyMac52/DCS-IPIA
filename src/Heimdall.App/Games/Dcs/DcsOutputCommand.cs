using System.IO;
using Heimdall.App.Games.Profiles;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Dcs;

namespace Heimdall.App.Games.Dcs
{
    /// <summary>DCS validation, OvGME package, module snapshot, documentation and complete-release output through the shared command sequence.</summary>
    /// <remarks>Native boundaries are unchanged: outputs read only the saved snapshot, re-check edition access, freeze it, and the native writers own atomic/no-overwrite semantics.</remarks>
    internal sealed class DcsOutputCommand(DcsWorkspaceContext context, DcsWorkspaceSession session, Func<bool> viewModelErrors,
        Func<bool, string?> outputPath, Func<string?> documentationFolder, Func<string?> completeReleaseFolder,
        Action<string, DcsEditorWorkspaceSnapshot, string, Exception> logFailure)
        : GameProfileCommandBase<DcsOutputRequest, DcsOutputPlan, ProfileCommandReceipt>(context, new DelegateInputLifetime(context.StopActivity))
    {
        #region BEGIN Public
        internal string Status { get; private set; } = "Build uses saved database state. Save pending edits first.";
        internal string FailureDiagnostic { get; private set; } = "";
        internal event EventHandler? Changed;
        #endregion END Public
        /// <inheritdoc/>
        protected override WorkspaceOperationKind Kind => WorkspaceOperationKind.Export;
        /// <inheritdoc/>
        protected override WorkspaceAvailability ReadAvailability() =>
            session.Repository is null || session.Saved is null ? new(false, "Open a saved DCS workspace before building output.")
            : !EditionAccess.AllowsWorkspace(session.Saved, session.Training) ? new(false, "This edition does not allow output for this workspace.")
            : viewModelErrors() ? new(false, "Fix the highlighted module errors before building output.")
            : session.HasPendingChanges ? new(false, "Build uses saved database state. Save pending edits first.")
            : new(true, "");
        /// <inheritdoc/>
        protected override DcsOutputPlan Prepare(DcsOutputRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.Version)) throw new InvalidOperationException("Enter a package version before building output.");
            var saved = session.Saved ?? throw new InvalidOperationException("Open a saved DCS workspace before building output.");
            var destination = request.Kind switch
            {
                DcsOutputKind.Validate => "",
                DcsOutputKind.CompleteRelease => completeReleaseFolder(),
                DcsOutputKind.Documentation => documentationFolder(),
                DcsOutputKind.ModuleSnapshot => outputPath(true),
                _ => outputPath(false)
            };
            return new(saved, request.Version.Trim(), request.Kind, destination);
        }
        /// <inheritdoc/>
        protected override async Task<ProfileCommandReceipt> RunNativeAsync(DcsOutputPlan prepared, IWorkspaceOperationLease lease)
        {
            ArgumentNullException.ThrowIfNull(prepared);
            if (prepared.Destination is null) return ProfileCommandReceipt.Cancel("Output cancelled; no files were written.");
            SetStatus("Validating saved assignments, physical devices, pinned assets and generated output…");
            var saved = prepared.Saved; var version = prepared.Version;
            try
            {
                EditionAccess.RequireWorkspace(saved, session.DeviceDiscovery, session.Training);
                var snapshot = await Task.Run(() => DcsModuleSnapshotWorkflow.Freeze(saved, version));
                var path = prepared.Destination;
                switch (prepared.Kind)
                {
                    case DcsOutputKind.Validate:
                        return new(ProfileCommandOutcome.Succeeded, $"Validated {saved.Configuration.Module.DisplayName}; Lua, device/summary pages and OvGME layout are complete. Snapshot SHA-256 {snapshot.Workspace.Sha256}");
                    case DcsOutputKind.CompleteRelease:
                        var release = Path.Combine(path, $"DCS-{saved.Configuration.Module.DcsId}-Components-{version}-Complete");
                        await Task.Run(() => DcsModuleCompleteReleaseWorkflow.Write(snapshot, release));
                        return new(ProfileCommandOutcome.Succeeded, $"Exported Complete release: {release} · Verified frozen inputs SHA-256 {snapshot.Workspace.Sha256}", release);
                    case DcsOutputKind.Documentation:
                        var documentation = Path.Combine(path, $"DCS-{saved.Configuration.Module.DcsId}-Components-{version}-Documentation");
                        await Task.Run(() => DcsModuleDocumentationWorkflow.Write(snapshot, documentation));
                        return new(ProfileCommandOutcome.Succeeded, $"Exported module documentation: {documentation} · Snapshot SHA-256 {snapshot.Workspace.Sha256}", documentation);
                    case DcsOutputKind.ModuleSnapshot:
                        await Task.Run(() => DcsModuleSnapshotWorkflow.Write(snapshot, path));
                        return new(ProfileCommandOutcome.Succeeded, $"Exported module snapshot: {path} · SHA-256 {snapshot.Workspace.Sha256}. Restore preserves its module identity and package version.", path);
                    default:
                        var package = await Task.Run(() => DcsModuleSnapshotWorkflow.Build(snapshot));
                        var written = DcsOvGmeArtifactWriter.Write(package, path);
                        return new(ProfileCommandOutcome.Succeeded, $"Wrote {written.ArchivePath} and {written.ChecksumPath} · SHA-256 {written.Sha256}", written.ArchivePath);
                }
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                await lease.ResumeOnOwnerAsync();
                var operation = prepared.Kind switch
                {
                    DcsOutputKind.Validate => "Validate",
                    DcsOutputKind.CompleteRelease => "Export Complete release",
                    DcsOutputKind.Documentation => "Export documentation",
                    DcsOutputKind.ModuleSnapshot => "Export module snapshot",
                    _ => "Build OvGME package"
                };
                logFailure(operation, saved, version, error);
                FailureDiagnostic = operation + ": " + error.Message;
                return new(ProfileCommandOutcome.Failed, "Module output failed: " + error.Message);
            }
        }
        /// <inheritdoc/>
        protected override ProfileCommandEffects ReadEffects(ProfileCommandReceipt result) => result.Detail.Length > 0 ? ProfileCommandEffects.OutputWrite : ProfileCommandEffects.None;
        /// <inheritdoc/>
        protected override bool IsSuccessful(ProfileCommandReceipt result) => result?.Succeeded == true;
        /// <inheritdoc/>
        protected override void Accept(ProfileCommandReceipt result, IWorkspaceOperationLease lease) => SetStatus(result.Message);
        /// <inheritdoc/>
        protected override void ReportUnsuccessful(ProfileCommandReceipt result) => SetStatus(result.Message);
        private void SetStatus(string status) { Status = status; Changed?.Invoke(this, EventArgs.Empty); }
    }
}

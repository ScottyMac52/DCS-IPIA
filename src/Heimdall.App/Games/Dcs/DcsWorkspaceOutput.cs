using System.IO;
using Heimdall.Dcs;
using Heimdall.Application;

namespace Heimdall.App.Games.Dcs
{
    /// <summary>Runs DCS validation and exports against saved snapshots, with injected destinations and failure reporting.</summary>
    internal sealed class DcsWorkspaceOutput(DcsWorkspaceSession session,
        Func<bool, string?> outputPath, Func<string?> documentationFolder, Func<string?> completeReleaseFolder,
        Action<string, DcsEditorWorkspaceSnapshot, string, Exception> logFailure)
    {
        #region BEGIN Public
        internal string Status { get; private set; } = "Build uses saved database state. Save pending edits first.";
        internal string FailureDiagnostic { get; private set; } = "";
        internal event EventHandler? Changed;

        internal bool CanRun(bool viewModelErrors, string version) => session.Repository is not null && session.Saved is not null &&
            EditionAccess.AllowsWorkspace(session.Saved, session.Training) && !viewModelErrors && !session.HasPendingChanges &&
            !string.IsNullOrWhiteSpace(version);

        internal async Task RunAsync(string version, bool? snapshotExport, bool documentation = false, bool completeRelease = false)
        {
            if (session.Saved is not { } saved || session.HasPendingChanges) return;
            Status = "Validating saved assignments, physical devices, pinned assets and generated output…";
            Changed?.Invoke(this, EventArgs.Empty);
            try
            {
                EditionAccess.RequireWorkspace(saved, session.DeviceDiscovery, session.Training);
                var snapshot = await Task.Run(() => DcsModuleSnapshotWorkflow.Freeze(saved, version));
                if (snapshotExport is null)
                {
                    Status = $"Validated {saved.Configuration.Module.DisplayName}; Lua, device/summary pages and OvGME layout are complete. Snapshot SHA-256 {snapshot.Workspace.Sha256}";
                    return;
                }
                var export = snapshotExport.Value;
                var path = completeRelease ? completeReleaseFolder() : documentation ? documentationFolder() : outputPath(export);
                if (path is null)
                {
                    Status = "Output cancelled; no files were written.";
                    return;
                }
                if (completeRelease)
                {
                    var destination = Path.Combine(path, $"DCS-{saved.Configuration.Module.DcsId}-Components-{version}-Complete");
                    await Task.Run(() => DcsModuleCompleteReleaseWorkflow.Write(snapshot, destination));
                    Status = $"Exported Complete release: {destination} · Verified frozen inputs SHA-256 {snapshot.Workspace.Sha256}";
                }
                else if (documentation)
                {
                    var destination = Path.Combine(path, $"DCS-{saved.Configuration.Module.DcsId}-Components-{version}-Documentation");
                    await Task.Run(() => DcsModuleDocumentationWorkflow.Write(snapshot, destination));
                    Status = $"Exported module documentation: {destination} · Snapshot SHA-256 {snapshot.Workspace.Sha256}";
                }
                else if (export)
                {
                    await Task.Run(() => DcsModuleSnapshotWorkflow.Write(snapshot, path));
                    Status = $"Exported module snapshot: {path} · SHA-256 {snapshot.Workspace.Sha256}. Restore preserves its module identity and package version.";
                }
                else
                {
                    var package = await Task.Run(() => DcsModuleSnapshotWorkflow.Build(snapshot));
                    var written = DcsOvGmeArtifactWriter.Write(package, path);
                    Status = $"Wrote {written.ArchivePath} and {written.ChecksumPath} · SHA-256 {written.Sha256}";
                }
            }
            catch (Exception error)
            {
                var operation = snapshotExport is null ? "Validate" : completeRelease ? "Export Complete release" :
                    documentation ? "Export documentation" : snapshotExport.Value ? "Export module snapshot" : "Build OvGME package";
                logFailure(operation, saved, version, error);
                Status = "Module output failed: " + error.Message;
                FailureDiagnostic = operation + ": " + error.Message;
            }
            finally { Changed?.Invoke(this, EventArgs.Empty); }
        }
        #endregion END Public
    }
}

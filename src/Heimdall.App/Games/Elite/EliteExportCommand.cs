using System.IO;
using Heimdall.App.Games.Profiles;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Infrastructure;

namespace Heimdall.App.Games.Elite
{
    /// <summary>ED preset export through the shared command sequence; replacement requires review and writes an exact backup.</summary>
    internal sealed class EliteExportCommand(EliteWorkspaceContext context, IWorkspaceInputLifetime input, Func<string> bindingsDirectory,
        Func<string?> pickReviewParent, Func<string, bool> confirmReplacement, Action<string> exportState, Action<string> status)
        : GameProfileCommandBase<EliteExportRequest, EliteExportPlan, ProfileCommandReceipt>(context, input)
    {
        /// <inheritdoc/>
        protected override WorkspaceOperationKind Kind => WorkspaceOperationKind.Export;
        /// <inheritdoc/>
        protected override WorkspaceAvailability ReadAvailability() =>
            context.Session is null || context.Chrome.Pending ? new(false, "Save the configuration before exporting.") : new(true, "");
        /// <inheritdoc/>
        protected override EliteExportPlan Prepare(EliteExportRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var session = context.Session!;
            if (session.ReviewGameExport().Count > 0) throw new InvalidOperationException("Review unresolved controller references before exporting.");
            var plan = session.ExportForGame();
            if (request.ReviewCopy)
            {
                var parent = pickReviewParent();
                // The review dialog may pump messages; re-read the native plan after it returns.
                return parent is null ? new(plan, null, null, "") : new(session.ExportForGame(), parent, null, "");
            }
            var prepared = GameBindingsExport.Prepare(bindingsDirectory(), plan.Files);
            var target = Path.Combine(prepared.Directory, prepared.FileName);
            return prepared.ExpectedHash is not null && !confirmReplacement(target)
                ? new(plan, null, null, "Export cancelled; existing preset unchanged.") : new(plan, null, prepared, "");
        }
        /// <inheritdoc/>
        protected override Task<ProfileCommandReceipt> RunNativeAsync(EliteExportPlan prepared, IWorkspaceOperationLease lease)
        {
            ArgumentNullException.ThrowIfNull(prepared);
            var session = context.Session!; var name = session.Name;
            var complete = "Export complete. " + string.Join(" ", prepared.Plan.Diagnostics.Select(x => x.Message));
            if (prepared.ReviewParent is { } parent)
            {
                var active = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Frontier Developments", "Elite Dangerous", "Options", "Bindings");
                var output = GameReviewExport.Write(parent, prepared.Plan.Files, [active, bindingsDirectory()]);
                return Task.FromResult(new ProfileCommandReceipt(ProfileCommandOutcome.Succeeded, complete, "Exported separate review copy: " + output
                    + "\nInstallation into ED is manual. With ED closed, copy the reviewed preset into Bindings, restart ED and select " + name + "."));
            }
            if (prepared.Install is not { } install) return Task.FromResult(ProfileCommandReceipt.Cancel(prepared.CancelMessage));
            var fresh = session.ExportForGame();
            if (context.Chrome.Pending || !fresh.Files.TryGetValue(install.FileName, out var content) || content != install.Content)
                throw new InvalidOperationException("The profile changed during export. Save and export again.");
            var backup = GameBindingsExport.Write(install);
            return Task.FromResult(new ProfileCommandReceipt(ProfileCommandOutcome.Succeeded, complete, "Exported: " + Path.Combine(install.Directory, install.FileName)
                + (backup is null ? "" : "\nBackup: " + backup) + "\nRestart ED and select " + name + ". Existing preset selectors are unchanged."));
        }
        /// <inheritdoc/>
        protected override bool IsSuccessful(ProfileCommandReceipt result) => result?.Succeeded == true;
        /// <inheritdoc/>
        protected override void Accept(ProfileCommandReceipt result, IWorkspaceOperationLease lease) { exportState(result.Detail); status(result.Message); }
    }
}

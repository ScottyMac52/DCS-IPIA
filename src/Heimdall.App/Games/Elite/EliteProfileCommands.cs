using System.IO;
using Heimdall.App.Games.Profiles;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Infrastructure;

namespace Heimdall.App.Games.Elite
{
    /// <summary>Imports an ED .binds preset as a new unsaved configuration with a unique name.</summary>
    internal sealed class EliteImportCommand(EliteWorkspaceContext context, IWorkspaceInputLifetime input,
        Func<GameWorkspace, GameProfileSession> createSession, Action<GameSessionInstall> install, Action<string> status)
        : GameProfileCommandBase<string, GameWorkspace, GameSessionInstall>(context, input)
    {
        /// <inheritdoc/>
        protected override WorkspaceOperationKind Kind => WorkspaceOperationKind.Import;
        /// <inheritdoc/>
        protected override WorkspaceAvailability ReadAvailability() => new(true, "");
        /// <inheritdoc/>
        protected override GameWorkspace Prepare(string request)
        {
            var profile = context.Backend.Import("controls", new Dictionary<string, string> { [Path.GetFileName(request)] = File.ReadAllText(request) });
            var baseName = profile.Name.Length > 80 ? profile.Name[..80] + " Controls" : profile.Name + " Controls";
            var used = context.Services.Store.List().Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var name = baseName; var suffix = 2;
            while (used.Contains(name)) name = baseName + " " + suffix++;
            return new(1, Guid.NewGuid(), name, profile, new());
        }
        /// <inheritdoc/>
        protected override Task<GameSessionInstall> RunNativeAsync(GameWorkspace prepared, IWorkspaceOperationLease lease) => Task.FromResult(new GameSessionInstall(createSession(prepared), null));
        /// <inheritdoc/>
        protected override bool IsSuccessful(GameSessionInstall result) => result is not null;
        /// <inheritdoc/>
        protected override void Accept(GameSessionInstall result, IWorkspaceOperationLease lease)
        { install(result); status("Imported bindings. Associate your hardware drawing, then review and save this profile."); }
    }

    /// <summary>A new (assignments cleared) or duplicate configuration from an explicit source and validated unique name.</summary>
    internal sealed record EliteConfigurationRequest(GameWorkspace Source, string Name, bool Clear);

    /// <summary>A persisted new/duplicate configuration ready to install.</summary>
    internal sealed record EliteConfigurationResult(GameSessionInstall Installed, bool Clear);

    /// <summary>Persists a new/duplicate ED configuration once and installs it as the open session.</summary>
    internal sealed class EliteCreateConfigurationCommand(EliteWorkspaceContext context, IWorkspaceInputLifetime input,
        Func<GameWorkspace, GameProfileSession> createSession, Action<GameSessionInstall> install, Action<string> status)
        : GameProfileCommandBase<EliteConfigurationRequest, (GameWorkspace Workspace, bool Clear), EliteConfigurationResult>(context, input)
    {
        /// <inheritdoc/>
        protected override WorkspaceOperationKind Kind => WorkspaceOperationKind.Replace;
        /// <inheritdoc/>
        protected override WorkspaceAvailability ReadAvailability() => new(true, "");
        /// <inheritdoc/>
        protected override (GameWorkspace Workspace, bool Clear) Prepare(EliteConfigurationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return (GameConfigurations.Create(request.Source, request.Name, request.Clear), request.Clear);
        }
        /// <inheritdoc/>
        protected override Task<EliteConfigurationResult> RunNativeAsync((GameWorkspace Workspace, bool Clear) prepared, IWorkspaceOperationLease lease)
        {
            var session = createSession(prepared.Workspace); // Native edition/backend boundary before any write.
            var revision = context.Services.Store.Save(prepared.Workspace, null);
            return Task.FromResult(new EliteConfigurationResult(new(session, revision), prepared.Clear));
        }
        /// <inheritdoc/>
        protected override bool IsSuccessful(EliteConfigurationResult result) => result is not null;
        /// <inheritdoc/>
        protected override void Accept(EliteConfigurationResult result, IWorkspaceOperationLease lease)
        {
            install(result.Installed);
            status(result.Clear ? "New configuration saved with assignments cleared. Controller setup and game settings retained."
                : "Independent configuration copy saved, including your current assignment edits.");
        }
    }

    /// <summary>Deletes the saved revision of the open ED configuration after review; exported presets are never touched.</summary>
    internal sealed class EliteDeleteConfigurationCommand(EliteWorkspaceContext context, IWorkspaceInputLifetime input,
        Func<string, bool> confirm, Action deleted)
        : GameProfileCommandBase<Guid, (Guid Id, string Revision)?, ProfileCommandReceipt>(context, input)
    {
        /// <inheritdoc/>
        protected override WorkspaceOperationKind Kind => WorkspaceOperationKind.Replace;
        /// <inheritdoc/>
        protected override WorkspaceAvailability ReadAvailability() =>
            context.Session is null || context.Revision is null ? new(false, "Open a saved configuration before deleting it.") : new(true, "");
        /// <inheritdoc/>
        protected override (Guid Id, string Revision)? Prepare(Guid request)
        {
            var workspace = context.Session!.Snapshot;
            if (workspace.Id != request) throw new InvalidOperationException("The open configuration changed. Choose it again before deleting.");
            return confirm(workspace.Name) ? (workspace.Id, context.Revision!) : null;
        }
        /// <inheritdoc/>
        protected override Task<ProfileCommandReceipt> RunNativeAsync((Guid Id, string Revision)? prepared, IWorkspaceOperationLease lease)
        {
            if (prepared is not { } target) return Task.FromResult(ProfileCommandReceipt.Cancel(""));
            context.Services.Store.Delete(target.Id, target.Revision);
            return Task.FromResult(new ProfileCommandReceipt(ProfileCommandOutcome.Succeeded, "HEIMDALL configuration deleted. Exported ED presets remain in place."));
        }
        /// <inheritdoc/>
        protected override bool IsSuccessful(ProfileCommandReceipt result) => result?.Succeeded == true;
        /// <inheritdoc/>
        protected override void Accept(ProfileCommandReceipt result, IWorkspaceOperationLease lease) => deleted();
    }

    /// <summary>Install into the ED Bindings directory (reviewed replacement with exact backup) or a separate review copy.</summary>
    internal sealed record EliteExportRequest(bool ReviewCopy);

    /// <summary>The reviewed export: a review-copy parent, or a prepared preset replacement; null parent and plan mean cancelled.</summary>
    internal sealed record EliteExportPlan(SimProfileExportPlan Plan, string? ReviewParent, GameBindingsExportPlan? Install, string CancelMessage);

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

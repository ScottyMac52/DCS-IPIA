using System.IO;
using Heimdall.App.Games.Profiles;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;
using Heimdall.Infrastructure;
using Heimdall.WarThunder;

namespace Heimdall.App.Games.WarThunder
{
    /// <summary>A bounded, already-parsed War Thunder capture; parsing happens before the leave decision so an invalid file never discards edits.</summary>
    internal sealed record WarThunderImportRequest(string FullPath, ProfileDocument Profile)
    {
        /// <summary>Gets the native capture size limit.</summary>
        internal const long MaximumBytes = 8 * 1024 * 1024;
        /// <summary>Reads a capture through the native backend, enforcing the 8 MiB limit.</summary>
        internal static WarThunderImportRequest Read(WarThunderProfileBackend backend, string path)
        {
            var full = Path.GetFullPath(path);
            if (new FileInfo(full).Length > MaximumBytes) throw new InvalidDataException("Controls capture exceeds 8 MiB.");
            return new(full, backend.Import("controls", new Dictionary<string, string> { [Path.GetFileName(full)] = File.ReadAllText(full) }));
        }
    }

    /// <summary>Installs an imported capture as a new unsaved War Thunder profile that remembers its source directory.</summary>
    internal sealed class WarThunderImportCommand(IGameWorkspaceContext context, IWorkspaceInputLifetime input,
        Func<GameWorkspace, GameProfileSession> createSession, Action<GameSessionInstall> install, Action<string> status)
        : GameProfileCommandBase<WarThunderImportRequest, GameWorkspace, GameSessionInstall>(context, input)
    {
        /// <inheritdoc/>
        protected override WorkspaceOperationKind Kind => WorkspaceOperationKind.Import;
        /// <inheritdoc/>
        protected override WorkspaceAvailability ReadAvailability() => new(true, "");
        /// <inheritdoc/>
        protected override GameWorkspace Prepare(WarThunderImportRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return new(1, Guid.NewGuid(), Path.GetFileNameWithoutExtension(request.FullPath), request.Profile, new()) { SourceDirectory = Path.GetDirectoryName(request.FullPath) };
        }
        /// <inheritdoc/>
        protected override Task<GameSessionInstall> RunNativeAsync(GameWorkspace prepared, IWorkspaceOperationLease lease) => Task.FromResult(new GameSessionInstall(createSession(prepared), null));
        /// <inheritdoc/>
        protected override bool IsSuccessful(GameSessionInstall result) => result is not null;
        /// <inheritdoc/>
        protected override void Accept(GameSessionInstall result, IWorkspaceOperationLease lease)
        { install(result); status("Imported captured commands. Save this profile, then export a separate review copy."); }
    }

    /// <summary>Installs an unsaved independent copy of the current War Thunder profile.</summary>
    internal sealed class WarThunderDuplicateCommand(WarThunderWorkspaceContext context, IWorkspaceInputLifetime input,
        Func<GameWorkspace, GameProfileSession> createSession, Action<GameSessionInstall> install)
        : GameProfileCommandBase<string, GameWorkspace, GameSessionInstall>(context, input)
    {
        /// <inheritdoc/>
        protected override WorkspaceOperationKind Kind => WorkspaceOperationKind.Replace;
        /// <inheritdoc/>
        protected override WorkspaceAvailability ReadAvailability() => context.Session is null ? new(false, "Open a War Thunder profile before duplicating.") : new(true, "");
        /// <inheritdoc/>
        protected override GameWorkspace Prepare(string request) => GameConfigurations.Create(context.Session!.Snapshot, request, false);
        /// <inheritdoc/>
        protected override Task<GameSessionInstall> RunNativeAsync(GameWorkspace prepared, IWorkspaceOperationLease lease) => Task.FromResult(new GameSessionInstall(createSession(prepared), null));
        /// <inheritdoc/>
        protected override bool IsSuccessful(GameSessionInstall result) => result is not null;
        /// <inheritdoc/>
        protected override void Accept(GameSessionInstall result, IWorkspaceOperationLease lease) => install(result);
    }

    /// <summary>Writes a separate source-preserving review copy; the source capture directory is protected from overwrite.</summary>
    internal sealed class WarThunderExportCommand(WarThunderWorkspaceContext context, IWorkspaceInputLifetime input, Action<string> status)
        : GameProfileCommandBase<string, WarThunderExportPlan, ProfileCommandReceipt>(context, input)
    {
        /// <inheritdoc/>
        protected override WorkspaceOperationKind Kind => WorkspaceOperationKind.Export;
        /// <inheritdoc/>
        protected override WorkspaceAvailability ReadAvailability() =>
            context.Session is null || context.Chrome.Pending ? new(false, "Save the profile before exporting.") : new(true, "");
        /// <inheritdoc/>
        protected override WarThunderExportPlan Prepare(string request)
        {
            var session = context.Session ?? throw new InvalidOperationException("Save the profile before exporting.");
            return new(request, session.Export(), session.Snapshot.SourceDirectory);
        }
        /// <inheritdoc/>
        protected override Task<ProfileCommandReceipt> RunNativeAsync(WarThunderExportPlan prepared, IWorkspaceOperationLease lease)
        {
            ArgumentNullException.ThrowIfNull(prepared);
            var directory = GameReviewExport.Write(prepared.Parent, prepared.Plan.Files, prepared.SourceDirectory is { } source ? [source] : [], "war-thunder");
            return Task.FromResult(new ProfileCommandReceipt(ProfileCommandOutcome.Succeeded, "Review copy: " + directory, directory));
        }
        /// <inheritdoc/>
        protected override bool IsSuccessful(ProfileCommandReceipt result) => result?.Succeeded == true;
        /// <inheritdoc/>
        protected override void Accept(ProfileCommandReceipt result, IWorkspaceOperationLease lease) => status(result.Message);
    }

    /// <summary>The validated native export plan and its protected source directory.</summary>
    internal sealed record WarThunderExportPlan(string Parent, SimProfileExportPlan Plan, string? SourceDirectory);
}

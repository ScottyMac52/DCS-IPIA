using System.IO;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;

namespace Heimdall.App.Games.Squadrons
{
    /// <summary>A parsed Squadrons import ready to install, with its unique profile name and protected source directory.</summary>
    internal sealed record SquadronsImportPlan(GameWorkspace Workspace, string SourceDirectory);

    /// <summary>A validated imported session and the directory it came from.</summary>
    internal sealed record SquadronsImportResult(GameProfileSession Session, string SourceDirectory);

    /// <summary>Imports a native Squadrons controls file (4 MiB limit) as a new unsaved profile.</summary>
    internal sealed class SquadronsImportCommand(SquadronsWorkspaceContext context, IWorkspaceInputLifetime input,
        Func<GameWorkspace, GameProfileSession> createSession, Action<SquadronsImportResult> install)
        : GameProfileCommandBase<string, SquadronsImportPlan, SquadronsImportResult>(context, input)
    {
        /// <summary>Gets the native capture size limit.</summary>
        internal const long MaximumBytes = 4 * 1024 * 1024;
        /// <inheritdoc/>
        protected override WorkspaceOperationKind Kind => WorkspaceOperationKind.Import;
        /// <inheritdoc/>
        protected override WorkspaceAvailability ReadAvailability() => new(true, "");
        /// <inheritdoc/>
        protected override SquadronsImportPlan Prepare(string request)
        {
            if (new FileInfo(request).Length > MaximumBytes) throw new InvalidDataException("Squadrons controls exceed the 4 MiB import limit.");
            var profile = context.Backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = File.ReadAllText(request) });
            var name = "Squadrons Controls"; var number = 2;
            var existing = context.Services.Store.List().Where(x => x.BackendId == context.Backend.SimId).Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            while (existing.Contains(name)) name = "Squadrons Controls " + number++;
            return new(new(1, Guid.NewGuid(), name, profile, new()), Path.GetDirectoryName(Path.GetFullPath(request))!);
        }
        /// <inheritdoc/>
        protected override Task<SquadronsImportResult> RunNativeAsync(SquadronsImportPlan prepared, IWorkspaceOperationLease lease)
        {
            ArgumentNullException.ThrowIfNull(prepared);
            return Task.FromResult(new SquadronsImportResult(createSession(prepared.Workspace), prepared.SourceDirectory));
        }
        /// <inheritdoc/>
        protected override bool IsSuccessful(SquadronsImportResult result) => result is not null;
        /// <inheritdoc/>
        protected override void Accept(SquadronsImportResult result, IWorkspaceOperationLease lease) => install(result);
    }

    /// <summary>Saves a new profile with joystick assignments and templates cleared, then opens it; the source session's export boundary is enforced first.</summary>
    internal sealed class SquadronsNewProfileCommand(SquadronsWorkspaceContext context, IWorkspaceInputLifetime input, Action<Guid> open)
        : GameProfileCommandBase<string, GameWorkspace, ProfileCommandReceipt>(context, input)
    {
        /// <inheritdoc/>
        protected override WorkspaceOperationKind Kind => WorkspaceOperationKind.Replace;
        /// <inheritdoc/>
        protected override WorkspaceAvailability ReadAvailability() =>
            context.Session is null ? new(false, "Import native Squadrons settings or open a saved profile first.") : new(true, "");
        /// <inheritdoc/>
        protected override GameWorkspace Prepare(string request)
        {
            var sourceSession = context.Session ?? throw new InvalidOperationException("Import native Squadrons settings or open a saved profile first.");
            sourceSession.Export(); // Require the existing session's edition and native validation boundary.
            var source = sourceSession.Snapshot;
            return GameConfigurations.Create(source, request, false) with { Profile = context.Backend.CreateUnassignedProfile(source.Profile) };
        }
        /// <inheritdoc/>
        protected override Task<ProfileCommandReceipt> RunNativeAsync(GameWorkspace prepared, IWorkspaceOperationLease lease)
        {
            ArgumentNullException.ThrowIfNull(prepared);
            context.Services.Store.Save(prepared, null);
            return Task.FromResult(new ProfileCommandReceipt(ProfileCommandOutcome.Succeeded,
                "New profile saved with no joystick assignments or imported assignment templates. Controller setup and native game settings retained.", prepared.Id.ToString("D")));
        }
        /// <inheritdoc/>
        protected override bool IsSuccessful(ProfileCommandReceipt result) => result?.Succeeded == true;
        /// <inheritdoc/>
        protected override void Accept(ProfileCommandReceipt result, IWorkspaceOperationLease lease) => open(Guid.Parse(result.Detail));
    }

    /// <summary>Writes a separate Squadrons review copy; active game settings folders and the import source are protected.</summary>
    internal sealed class SquadronsExportCommand(SquadronsWorkspaceContext context, IWorkspaceInputLifetime input, Action<string> status)
        : GameProfileCommandBase<string, SquadronsExportPlan, ProfileCommandReceipt>(context, input)
    {
        /// <inheritdoc/>
        protected override WorkspaceOperationKind Kind => WorkspaceOperationKind.Export;
        /// <inheritdoc/>
        protected override WorkspaceAvailability ReadAvailability() =>
            context.Session is null || context.Chrome.Pending ? new(false, "Save the Squadrons profile before exporting.") : new(true, "");
        /// <inheritdoc/>
        protected override SquadronsExportPlan Prepare(string request)
        {
            var session = context.Session ?? throw new InvalidOperationException("Save the Squadrons profile before exporting.");
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var roots = new List<string> { Path.Combine(documents, "STAR WARS Squadrons", "settings"), Path.Combine(documents, "STAR WARS Squadrons Steam", "settings") };
            if (context.SourceDirectory is not null) roots.Add(context.SourceDirectory);
            session.Export(); // Retain the session's edition and native-document validation boundary.
            var snapshot = session.Snapshot;
            var plan = context.Backend.ExportReview(snapshot.Profile, snapshot.Hardware.ToDictionary(x => x.Key, x => x.Value.Device.Name));
            if (!plan.Succeeded) throw new InvalidOperationException(string.Join("; ", plan.Diagnostics.Select(x => x.Message)));
            return new(request, plan.Files, roots);
        }
        /// <inheritdoc/>
        protected override Task<ProfileCommandReceipt> RunNativeAsync(SquadronsExportPlan prepared, IWorkspaceOperationLease lease)
        {
            ArgumentNullException.ThrowIfNull(prepared);
            var directory = GameReviewExport.Write(prepared.Parent, prepared.Files, prepared.ProtectedRoots, backendId: context.Backend.SimId);
            return Task.FromResult(new ProfileCommandReceipt(ProfileCommandOutcome.Succeeded,
                "Review copy with preserved native names and records exported to " + directory + ". Close Squadrons and back up its settings before manually installing this copy.", directory));
        }
        /// <inheritdoc/>
        protected override bool IsSuccessful(ProfileCommandReceipt result) => result?.Succeeded == true;
        /// <inheritdoc/>
        protected override void Accept(ProfileCommandReceipt result, IWorkspaceOperationLease lease) => status(result.Message);
    }

    /// <summary>The reviewed native files and the folders a review copy must never overwrite.</summary>
    internal sealed record SquadronsExportPlan(string Parent, IReadOnlyDictionary<string, string> Files, IReadOnlyList<string> ProtectedRoots);
}

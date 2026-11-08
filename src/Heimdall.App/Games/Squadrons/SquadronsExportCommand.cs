using System.IO;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;

namespace Heimdall.App.Games.Squadrons
{
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
        protected override ProfileCommandEffects ReadEffects(ProfileCommandReceipt result) => ProfileCommandEffects.OutputWrite;
        /// <inheritdoc/>
        protected override bool IsSuccessful(ProfileCommandReceipt result) => result?.Succeeded == true;
        /// <inheritdoc/>
        protected override void Accept(ProfileCommandReceipt result, IWorkspaceOperationLease lease) => status(result.Message);
    }
}

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
}

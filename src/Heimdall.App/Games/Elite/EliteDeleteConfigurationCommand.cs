using System.IO;
using Heimdall.App.Games.Profiles;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Infrastructure;

namespace Heimdall.App.Games.Elite
{
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
        protected override ProfileCommandEffects ReadEffects(ProfileCommandReceipt result) => ProfileCommandEffects.WorkspaceWrite;
        /// <inheritdoc/>
        protected override bool IsSuccessful(ProfileCommandReceipt result) => result?.Succeeded == true;
        /// <inheritdoc/>
        protected override void Accept(ProfileCommandReceipt result, IWorkspaceOperationLease lease) => deleted();
    }
}

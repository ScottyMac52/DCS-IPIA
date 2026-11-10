using System.IO;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;

namespace Heimdall.App.Games.Squadrons
{
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
        protected override ProfileCommandEffects ReadEffects(ProfileCommandReceipt result) => ProfileCommandEffects.WorkspaceWrite;
        /// <inheritdoc/>
        protected override bool IsSuccessful(ProfileCommandReceipt result) => result?.Succeeded == true;
        /// <inheritdoc/>
        protected override void Accept(ProfileCommandReceipt result, IWorkspaceOperationLease lease) => open(Guid.Parse(result.Detail));
    }
}

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
        protected override ProfileCommandEffects ReadEffects(GameSessionInstall result) => ProfileCommandEffects.None;
        /// <inheritdoc/>
        protected override bool IsSuccessful(GameSessionInstall result) => result is not null;
        /// <inheritdoc/>
        protected override void Accept(GameSessionInstall result, IWorkspaceOperationLease lease) => install(result);
    }
}

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
    /// <summary>Installs an imported capture as a new unsaved War Thunder profile that remembers its source directory.</summary>
    internal sealed class WarThunderImportCommand(IGameWorkspaceContext context, IWorkspaceInputLifetime input,
        Func<GameWorkspace, GameProfileSession> createSession, Action<GameSessionInstall> install, Action<string> status)
        : GameProfileCommandBase<WarThunderImportRequest, GameWorkspace, GameSessionInstall>(context, input)
    {
        /// <inheritdoc/>
        protected override bool CanRecoverWorkspace => true;
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
        protected override ProfileCommandEffects ReadEffects(GameSessionInstall result) => ProfileCommandEffects.None;
        /// <inheritdoc/>
        protected override bool IsSuccessful(GameSessionInstall result) => result is not null;
        /// <inheritdoc/>
        protected override void Accept(GameSessionInstall result, IWorkspaceOperationLease lease)
        { install(result); status("Imported captured commands. Save this profile, then export a separate review copy."); }
    }
}

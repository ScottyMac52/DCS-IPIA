using System.IO;
using Heimdall.App.Games.Profiles;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Infrastructure;

namespace Heimdall.App.Games.Elite
{
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
}

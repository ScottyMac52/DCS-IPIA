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
}

using System.IO;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;

namespace Heimdall.App.Games.Squadrons
{
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
}

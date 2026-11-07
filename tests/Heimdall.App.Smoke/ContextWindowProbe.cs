using Heimdall.App.Games;
using Heimdall.Application.Interfaces;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises the real shared window/view notification path with each production context.</summary>
    internal sealed class ContextWindowProbe : GameWorkspaceWindow
    {
        private readonly WorkspaceContextCase _fixture;
        internal ContextWindowProbe(WorkspaceContextCase fixture, ContextAssignmentsProbe view)
        {
            _fixture = fixture;
            BindWorkspaceContext(fixture.Context, view.ReloadNative);
            view.Bind();
            Content = view; Width = 600; Height = 400;
        }
        protected override bool HasWorkspaceChanges => _fixture.Context.State.Dirty;
        protected override bool CanSaveWorkspace => _fixture.Context.State.Capabilities.Save.Enabled;
        protected override Task SaveWorkspaceCoreAsync(IWorkspaceOperationLease lease) => _fixture.Save(lease);
        protected override void DiscardWorkspaceCore(IWorkspaceOperationLease lease) => _fixture.Discard(lease);
        protected override void ReportWorkspaceError(Exception exception) => _fixture.Context.ReportStatus(exception.Message);
        // These fixtures own only temporary data; failures must not leave a modal prompt blocking diagnostics.
        protected override bool CanCloseWorkspace() => true;
    }
}

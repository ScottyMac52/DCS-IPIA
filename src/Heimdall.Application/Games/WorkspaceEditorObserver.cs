using Heimdall.Application.Interfaces;

namespace Heimdall.Application.Games
{
    /// <summary>Tracks the native editor's last completed render independently of browser publication and action gates.</summary>
    public sealed class WorkspaceEditorObserver(IGameWorkspaceContext context)
    {
        private readonly IGameWorkspaceContext _context = context ?? throw new ArgumentNullException(nameof(context));
        private (WorkspaceIdentity Identity, long Content, WorkspaceSelection Selection, int Projection, bool Scanning, bool Open)? _accepted;
        #region BEGIN Public
        /// <summary>Includes native evidence rebuilds and scan completion, but preserves pending input and name edits.</summary>
        public bool NeedsRefresh(int projectionBuilds = 0) => _accepted is not { } accepted || accepted != Capture(projectionBuilds);
        /// <summary>Distinguishes native document replacement from editor-only selection and hardware-evidence changes.</summary>
        public bool NeedsContentRefresh() => _accepted is not { } accepted || accepted.Identity != _context.State.Identity ||
            accepted.Content != _context.ContentVersion || accepted.Open != _context.State.Open;
        /// <summary>Records a completed editor render. A skipped or deferred refresh must never call this method.</summary>
        public void Accept(int projectionBuilds = 0) => _accepted = Capture(projectionBuilds);
        #endregion END Public
        #region BEGIN private helpers
        private (WorkspaceIdentity Identity, long Content, WorkspaceSelection Selection, int Projection, bool Scanning, bool Open) Capture(int projectionBuilds)
        {
            var state = _context.State;
            return (state.Identity, _context.ContentVersion, state.Selection with { InputId = null }, projectionBuilds,
                state.Activity == WorkspaceActivityKind.Scan, state.Open);
        }
        #endregion END private helpers
    }
}

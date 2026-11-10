using Heimdall.Application.Interfaces;

namespace Heimdall.Application.Games
{
    /// <summary>Classifies context notifications per consumer from the authoritative state and content stamp, without
    /// serializing native documents. One policy for every game: selection-only changes never reload native content views.</summary>
    public sealed class WorkspaceChangeObserver(IGameWorkspaceContext context)
    {
        private readonly IGameWorkspaceContext _context = context ?? throw new ArgumentNullException(nameof(context));
        private WorkspaceState? _state;
        private long _content;
        #region BEGIN Public
        /// <summary>Records the current state as observed (for example after a full native reload).</summary>
        public void Accept() { _state = _context.State; _content = _context.ContentVersion; }
        /// <summary>Classifies the change since the last observation and records the current state.</summary>
        public WorkspaceChangeKind Observe()
        {
            var previous = _state; var content = _content;
            Accept();
            var current = _state!;
            if (previous is null || content != _content || previous.Identity != current.Identity || previous.Open != current.Open) return WorkspaceChangeKind.Content;
            if (previous with { Version = 0 } == current with { Version = 0 }) return WorkspaceChangeKind.None;
            return previous with { Version = 0, Selection = current.Selection } == current with { Version = 0 }
                ? WorkspaceChangeKind.Selection : WorkspaceChangeKind.Presentation;
        }
        /// <summary>Creates a notification handler that reloads a native content view for content and gate changes but
        /// skips selection-only and already-observed notifications (views that do not render the shared selection).</summary>
        public static EventHandler ReloadUnlessSelectionOnly(IGameWorkspaceContext context, Action reload)
        {
            ArgumentNullException.ThrowIfNull(reload);
            var observer = new WorkspaceChangeObserver(context); observer.Accept();
            return (_, _) =>
            {
                if (observer.Observe() is not (WorkspaceChangeKind.None or WorkspaceChangeKind.Selection)) reload();
            };
        }
        #endregion END Public
    }
}

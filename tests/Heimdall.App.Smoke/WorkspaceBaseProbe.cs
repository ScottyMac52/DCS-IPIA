using System.Windows.Controls;
using Heimdall.App.Games;
using Heimdall.Application.Games;

namespace Heimdall.App.Smoke
{
    /// <summary>A second consumer with injected actions, demonstrating that the window base does not require DCS.</summary>
    internal sealed class WorkspaceBaseProbe : GameWorkspaceWindow
    {
        internal WorkspaceBaseProbe()
        {
            Content = Surface;
            ConfigureWorkspace(Surface, Actions,
            [
                new("First", () => { FirstCreations++; return new UserControl(); }),
                new("Second", () => new UserControl(), () => SecondAvailable ? null : "Feature unavailable")
            ]);
            ConfigureWorkspaceCommands(Save, Discard, PendingStatus);
            Save.Click += SaveClicked;
            Discard.Click += DiscardClicked;
            RegisterWorkspaceCleanup(() => CleanupCount++);
        }

        #region BEGIN Public
        internal ContentControl Surface { get; } = new();
        internal FeatureActionBar Actions { get; } = new();
        internal Button Save { get; } = new();
        internal Button Discard { get; } = new();
        internal TextBlock PendingStatus { get; } = new();
        internal bool Pending { get; set; }
        internal bool CloseAllowed { get; set; }
        internal bool SecondAvailable { get; set; } = true;
        internal int FirstCreations { get; private set; }
        internal int CleanupCount { get; private set; }
        internal Func<Task> SaveAction { get; set; } = () => Task.CompletedTask;
        internal Action DiscardAction { get; set; } = () => { };
        internal Exception? Error { get; private set; }
        internal void RefreshChrome() => RefreshWorkspaceState();
        internal Task<bool> TryLeaveAsync(GameWorkspaceLeaveDecision decision) => ConfirmWorkspaceLeaveAsync(() => decision);
        #endregion END Public

        /// <inheritdoc/>
        protected override bool HasWorkspaceChanges => Pending;
        /// <inheritdoc/>
        protected override bool CanSaveWorkspace => true;
        /// <inheritdoc/>
        protected override Task SaveWorkspaceCoreAsync() => SaveAction();
        /// <inheritdoc/>
        protected override void DiscardWorkspaceCore() => DiscardAction();
        /// <inheritdoc/>
        protected override bool ConfirmDiscardWorkspaceChanges() => CloseAllowed;
        /// <inheritdoc/>
        protected override void ReportWorkspaceError(Exception exception) => Error = exception;
    }
}

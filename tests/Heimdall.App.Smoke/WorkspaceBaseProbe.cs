using Heimdall.Application.Interfaces;
using System.Windows.Controls;
using Heimdall.App.Games;
using Heimdall.Application.Games;

namespace Heimdall.App.Smoke
{
    /// <summary>A second consumer with injected actions, demonstrating that the window base does not require DCS.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA1001:Types that own disposable fields should be disposable", Justification = "Profile operations are released by the inherited workspace cleanup.")]
    internal sealed class WorkspaceBaseProbe : GameWorkspaceWindow
    {
        private readonly SmokeProfileOperations _profiles;
        internal WorkspaceBaseProbe()
        {
            var context = new WorkspaceSmokeContext(() => Pending, () => Pending.ToString(), () => SaveAvailable);
            context.Open(); BindWorkspaceContext(context, () => NativeRefreshes++);
            _profiles = new SmokeProfileOperations(context, () => context.Working ?? "", () => SaveAction(), () => DiscardAction());
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
            RegisterWorkspaceCleanup(_profiles.Dispose);
        }

        #region BEGIN Public
        internal ContentControl Surface { get; } = new();
        internal FeatureActionBar Actions { get; } = new();
        internal Button Save { get; } = new();
        internal Button Discard { get; } = new();
        internal TextBlock PendingStatus { get; } = new();
        internal bool Pending { get; set; }
        internal bool SaveAvailable { get; set; } = true;
        internal bool CloseAllowed { get; set; }
        internal bool SecondAvailable { get; set; } = true;
        internal int FirstCreations { get; private set; }
        internal int CleanupCount { get; private set; }
        internal int NativeRefreshes { get; private set; }
        internal Func<Task> SaveAction { get; set; } = () => Task.CompletedTask;
        internal Action DiscardAction { get; set; } = () => { };
        internal Exception? Error { get; private set; }
        internal void RefreshChrome() => RefreshWorkspaceState();
        internal Task<bool> TryLeaveAsync(GameWorkspaceLeaveDecision decision) => ConfirmWorkspaceLeaveAsync(() => decision);
        #endregion END Public

        /// <inheritdoc/>
        protected override bool HasWorkspaceChanges => Pending;
        /// <inheritdoc/>
        protected override bool CanSaveWorkspace => SaveAvailable;
        /// <inheritdoc/>
        protected override IGameProfileOperations ProfileOperations => _profiles;
        /// <inheritdoc/>
        protected override bool ConfirmDiscardWorkspaceChanges() => CloseAllowed;
        /// <inheritdoc/>
        protected override void ReportWorkspaceError(Exception exception) => Error = exception;
    }
}

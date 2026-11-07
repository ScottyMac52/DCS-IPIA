using System.Windows.Controls;
using Heimdall.App.Games;
using Heimdall.Application;
using Heimdall.Application.Games;

namespace Heimdall.App.Smoke
{
    /// <summary>Assembles a second game's assignment surface without native simulator types.</summary>
    internal sealed class AssignmentsBaseProbe : GameAssignmentsView, IDisposable
    {
        private readonly WorkspaceSmokeContext _context;
        internal AssignmentsBaseProbe()
        {
            _context = new(() => Value != 0, () => Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _context.Open(); BindAssignmentContext(_context);
            CommandsPanel = new TextBox { Text = "Commands" };
            ControllerPanel = new Button { Content = "Controller" };
            EditorPanel = new GameAssignmentEditorView { NativeEditor = new TextBox { Text = "Editor" } };
        }
        #region BEGIN Public
        /// <summary>Releases the probe-owned context after its host closes.</summary>
        public void Dispose() { _context.Dispose(); GC.SuppressFinalize(this); }
        internal int Value { get; private set; }
        internal int Refreshes { get; private set; }
        internal int DrawingRefreshes { get; private set; }
        internal int Saves { get; private set; }
        internal int Discards { get; private set; }
        internal int ProjectedValue { get; private set; }
        internal bool Busy { get; set; }
        internal string Status { get; private set; } = "";
        internal Func<Task> SaveAction { get; set; } = () => Task.CompletedTask;
        internal Action? RefreshAction { get; set; }
        internal Action DiscardAction { get; set; } = () => { };
        internal void Bind(AssignmentChangeSource source) => BindAssignmentChanges(source.Subscribe, source.Unsubscribe);
        internal DraftResult Apply(string scope, bool change = true) => ExecuteAssignment(new(scope == "probe" ? _context.Context.Scope : scope, "command", "input", "slot"), () =>
        {
            if (change) Value++;
            return new(DraftOutcome.Applied, "staged");
        });
        internal DraftResult ApplyAfterReview(Action review) => ExecuteAssignment(new(_context.Context.Scope, "command", "input", "slot"),
            () => { Value++; return new(DraftOutcome.Applied, "staged"); },
            () => { review(); return new(true, true, ""); });
        internal DraftResult ApplyWithNativeNotification(bool fail = false) => ExecuteAssignment(new(_context.Context.Scope, "command", "input", "slot"), () =>
        {
            Value++;
            // Match a native session: context is notified before the window rebuilds its projection.
            _context.NotifyNativeChanged();
            ProjectedValue = Value;
            if (fail) throw new InvalidOperationException("native assignment failure");
            return new(DraftOutcome.Applied, "staged");
        });
        #endregion END Public
        protected override WorkspaceAssignmentContext CaptureAssignmentContext() => _context.Context with { Busy = Busy || _context.Context.Busy };
        protected override string CaptureAssignmentSnapshot() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        protected override async Task SaveAssignmentsCoreAsync()
        {
            var result = await _context.RunOperationAsync(WorkspaceOperationKind.Save, async _ => { Saves++; await SaveAction(); return new(WorkspaceOperationOutcome.Applied, ""); }, default);
            if (result.Outcome == WorkspaceOperationOutcome.Blocked) throw new InvalidOperationException(result.Message);
        }
        protected override void DiscardAssignmentsCore()
        {
            var result = _context.RunOperation(WorkspaceOperationKind.Discard, _ => { Discards++; DiscardAction(); return new(WorkspaceOperationOutcome.Applied, ""); });
            if (result.Outcome == WorkspaceOperationOutcome.Blocked) throw new InvalidOperationException(result.Message);
        }
        protected override void RefreshAssignmentsCore() { Refreshes++; RefreshAction?.Invoke(); }
        protected override void RefreshAssignmentAvailability() { }
        protected override void RefreshAssignmentDrawing() => DrawingRefreshes++;
        protected override void ShowAssignmentStatus(string message) => Status = message;
    }
}

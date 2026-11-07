using System.Windows.Controls;
using Heimdall.App.Games;
using Heimdall.Application;
using Heimdall.Application.Games;

namespace Heimdall.App.Smoke
{
    /// <summary>Assembles a second game's assignment surface without native simulator types.</summary>
    internal sealed class AssignmentsBaseProbe : GameAssignmentsView
    {
        internal AssignmentsBaseProbe()
        {
            CommandsPanel = new TextBox { Text = "Commands" };
            ControllerPanel = new Button { Content = "Controller" };
            EditorPanel = new GameAssignmentEditorView { NativeEditor = new TextBox { Text = "Editor" } };
        }
        #region BEGIN Public
        internal int Value { get; private set; }
        internal int Refreshes { get; private set; }
        internal int DrawingRefreshes { get; private set; }
        internal int Saves { get; private set; }
        internal int Discards { get; private set; }
        internal bool Busy { get; set; }
        internal string Status { get; private set; } = "";
        internal Func<Task> SaveAction { get; set; } = () => Task.CompletedTask;
        internal Action? RefreshAction { get; set; }
        internal Action DiscardAction { get; set; } = () => { };
        internal void Bind(AssignmentChangeSource source) => BindAssignmentChanges(source.Subscribe, source.Unsubscribe);
        internal DraftResult Apply(string scope, bool change = true) => ExecuteAssignment(new(scope, "command", "input", "slot"), () =>
        {
            if (change) Value++;
            return new(DraftOutcome.Applied, "staged");
        });
        internal DraftResult ApplyAfterReview(Action review) => ExecuteAssignment(new("probe", "command", "input", "slot"),
            () => { Value++; return new(DraftOutcome.Applied, "staged"); },
            () => { review(); return new(true, true, ""); });
        #endregion END Public
        protected override WorkspaceAssignmentContext CaptureAssignmentContext() => new("probe", true, false, Busy, false);
        protected override string CaptureAssignmentSnapshot() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        protected override Task SaveAssignmentsCoreAsync() { Saves++; return SaveAction(); }
        protected override void DiscardAssignmentsCore() { Discards++; DiscardAction(); }
        protected override void RefreshAssignmentsCore() { Refreshes++; RefreshAction?.Invoke(); }
        protected override void RefreshAssignmentDrawing() => DrawingRefreshes++;
        protected override void ShowAssignmentStatus(string message) => Status = message;
    }
}

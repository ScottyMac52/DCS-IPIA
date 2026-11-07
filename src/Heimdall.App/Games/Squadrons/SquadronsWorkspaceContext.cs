using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Squadrons;

namespace Heimdall.App.Games.Squadrons
{
    /// <summary>Shares the native session, verified evidence and selection scope among independently composed presenters.</summary>
    internal sealed class SquadronsWorkspaceContext(GameWorkspaceServices services,
        Func<string?> selectedController, Func<string?> assignmentSource, Func<string> drawingSection,
        Func<string> editedName, Func<bool> busy)
    {
        internal const string CommandDragFormat = "heimdall.squadrons.command";
        internal GameWorkspaceServices Services { get; } = services;
        internal SquadronsProfileBackend Backend { get; } = new();
        internal GameProfileSession? Session { get; set; }
        internal string? Revision { get; set; }
        internal string? SourceDirectory { get; set; }
        internal IReadOnlyList<WorkspaceHardware> Drawings { get; set; } = [];
        internal IReadOnlyList<SquadronsWorkspaceRow> Rows { get; set; } = [];
        internal IReadOnlyList<ControllerObservation> ControllerInputs { get; set; } = [];
        internal GameAssignmentSelection Selection { get; set; } = null!;
        internal string? FocusedConcept { get; set; }
        internal HardwareGraphicControl? InspectedControl { get; set; }
        internal bool Refreshing { get; set; } = true;
        internal bool ScanningInputs { get; set; }
        internal bool Disposed { get; set; }
        internal bool Busy => busy();
        internal string EditedName => editedName();
        internal string? SelectedController => selectedController();
        internal string? AssignmentSource => assignmentSource();
        internal string DrawingSection => drawingSection();
        internal GameWorkspaceChromeState Chrome => GameWorkspaceChromeState.For(Session, Revision, editedName());
        internal Func<bool> Identifying { get; set; } = () => false;
        internal Func<bool> Testing { get; set; } = () => false;
        internal WorkspaceAssignmentContext AssignmentContext => new(
            (Session?.Snapshot.Id.ToString("N") ?? "") + ":" + AssignmentSource + ":" + DrawingSection,
            Session is not null, Session?.AccessMessage.Length > 0 || AssignmentSource?.StartsWith("system.", StringComparison.Ordinal) == true,
            Busy || ScanningInputs || Identifying(), Testing());
        internal event EventHandler? Changed;
        internal void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
    }
}

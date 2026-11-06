using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.WarThunder;

namespace Heimdall.App.Games.WarThunder
{
    /// <summary>Shares native session state, verified input evidence and selection scope without depending on a concrete window.</summary>
    internal sealed class WarThunderWorkspaceContext(GameWorkspaceServices services, Func<string?> selectedDevice,
        Func<string> editedName, Func<bool> busy)
    {
        internal GameWorkspaceServices Services { get; } = services;
        internal WarThunderProfileBackend Backend { get; } = new();
        internal GameProfileSession? Session { get; set; }
        internal string? Revision { get; set; }
        internal IReadOnlyList<WorkspaceHardware> Drawings { get; set; } = [];
        internal IReadOnlyList<DetectedDcsDevice> Observed { get; set; } = [];
        internal IReadOnlyList<ControllerObservation> ControllerInputs { get; set; } = [];
        internal IReadOnlyList<WarThunderWorkspaceRow> Rows { get; set; } = [];
        internal WarThunderPhysicalMap PhysicalMap { get; set; } = new([], "Rescan controller inputs.");
        internal WarThunderPhysicalInput? SelectedPhysicalInput { get; set; }
        internal GameAssignmentSelection Selection { get; set; } = null!;
        internal Dictionary<string, IReadOnlyList<WarThunderWorkspaceRow>> DrawingAssignments { get; } = new(StringComparer.Ordinal);
        internal bool Refreshing { get; set; } = true;
        internal bool Disposed { get; set; }
        internal bool ScanningHardware { get; set; }
        internal bool Busy => busy();
        internal string? SelectedDevice => selectedDevice();
        internal string EditedName => editedName().Trim();
        internal Func<bool> Testing { get; set; } = () => false;
        internal GameWorkspaceChromeState Chrome => GameWorkspaceChromeState.For(Session, Revision, EditedName, Testing());
        internal WorkspaceAssignmentContext AssignmentContext => new(
            (Session?.Snapshot.Id.ToString("N") ?? "") + ":" + (SelectedDevice ?? "*"),
            Session is not null, Session?.AccessMessage.Length > 0, Busy || ScanningHardware, Testing());
        internal event EventHandler? Changed;
        internal void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
    }
}

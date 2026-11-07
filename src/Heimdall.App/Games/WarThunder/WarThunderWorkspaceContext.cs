using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.WarThunder;
using System.Windows.Threading;

namespace Heimdall.App.Games.WarThunder
{
    /// <summary>Supplies typed native projections and policy to the common workspace state and lifetime.</summary>
    internal sealed class WarThunderWorkspaceContext(GameWorkspaceServices services, IWorkspaceScheduler? scheduler = null)
        : GameProfileWorkspaceContextBase(scheduler ?? new DispatcherWorkspaceScheduler(Dispatcher.CurrentDispatcher)),
          IWarThunderWorkspaceContext<WarThunderProfileBackend, WarThunderWorkspaceRow>
    {
        #region BEGIN Public
        internal GameWorkspaceServices Services { get; } = services;
        /// <inheritdoc/>
        public WarThunderProfileBackend Backend { get; } = new();
        internal IReadOnlyList<WorkspaceHardware> Drawings { get; set; } = [];
        /// <inheritdoc/>
        public IReadOnlyList<WarThunderWorkspaceRow> Rows { get; set; } = [];
        internal IReadOnlyList<ControllerObservation> ControllerInputs { get; set; } = [];
        internal GameAssignmentSelection Selection => NativeSelection;
        internal bool Refreshing { get; set; } = true;
        internal bool Disposed => IsDisposed;
        internal bool Busy => State.Operation is not null;
        internal bool Testing => State.Activity == WorkspaceActivityKind.Test;
        internal string EditedName => State.EditedName.Trim();
        internal WorkspaceAssignmentContext AssignmentContext => Context;
        internal void NotifyChanged() => RefreshNativeState();
        internal IReadOnlyList<DetectedDcsDevice> Observed { get; set; } = [];
        internal WarThunderPhysicalMap PhysicalMap { get; set; } = new([], "Rescan controller inputs.");
        internal WarThunderPhysicalInput? SelectedPhysicalInput { get; set; }
        internal Dictionary<string, IReadOnlyList<WarThunderWorkspaceRow>> DrawingAssignments { get; } = new(StringComparer.Ordinal);
        internal string? SelectedDevice => State.Selection.ControllerId;
        internal bool ScanningHardware => State.Activity == WorkspaceActivityKind.Scan;
        #endregion END Public
        protected override NativeWorkspaceFacts ReadNativeFacts() => ProfileFacts(ProfileCapabilities(EditionAccess.Policy.HasPremium, Session?.CanLinkCapturedDevices == true));
    }
}

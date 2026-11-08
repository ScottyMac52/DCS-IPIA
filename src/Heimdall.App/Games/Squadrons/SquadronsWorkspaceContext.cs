using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Squadrons;
using System.Windows.Threading;

namespace Heimdall.App.Games.Squadrons
{
    /// <summary>Supplies typed native projections and policy to the common workspace state and lifetime.</summary>
    internal sealed class SquadronsWorkspaceContext(GameWorkspaceServices services, IWorkspaceScheduler? scheduler = null)
        : GameProfileWorkspaceContextBase(scheduler ?? new DispatcherWorkspaceScheduler(Dispatcher.CurrentDispatcher)),
          ISquadronsWorkspaceContext<SquadronsProfileBackend, SquadronsWorkspaceRow>
    {
        #region BEGIN Public
        internal GameWorkspaceServices Services { get; } = services;
        /// <inheritdoc/>
        public SquadronsProfileBackend Backend { get; } = new();
        internal IReadOnlyList<WorkspaceHardware> Drawings { get; set; } = [];
        /// <inheritdoc/>
        public IReadOnlyList<SquadronsWorkspaceRow> Rows { get; set; } = [];
        internal IReadOnlyList<ControllerObservation> ControllerInputs { get; set; } = [];
        internal GameAssignmentSelection Selection => NativeSelection;
        internal bool Refreshing { get; set; } = true;
        internal bool Disposed => IsDisposed;
        internal bool Busy => State.Operation is not null;
        internal bool Testing => State.Activity == WorkspaceActivityKind.Test;
        internal string EditedName => State.EditedName.Trim();
        internal WorkspaceAssignmentContext AssignmentContext => Context;
        internal void NotifyChanged() => RefreshNativeState();
        internal const string CommandDragFormat = "heimdall.squadrons.command";
        internal string? SourceDirectory { get; set; }
        internal string? FocusedConcept { get; set; }
        internal HardwareGraphicControl? InspectedControl { get; set; }
        internal string? AssignmentSource { get; set; }
        internal string DrawingSection { get; set; } = "All";
        internal string? SelectedController => State.Selection.ControllerId is { } id &&
            id.StartsWith("squadrons.joystick.", StringComparison.Ordinal) &&
            (_controllers ??= new(() => Session, (document, key) => document.Profile.Devices.Any(device => device.LogicalId == key))).Get(id, false) ? id : null;
        private ProfileContentCache<string, bool>? _controllers;
        internal bool ScanningInputs => State.Activity == WorkspaceActivityKind.Scan;
        internal bool Identifying => State.Activity == WorkspaceActivityKind.Identify;
        #endregion END Public
        protected override NativeWorkspaceFacts ReadNativeFacts() => ProfileFacts(ProfileCapabilities(EditionAccess.Policy.HasPremium, Session?.CanLinkCapturedDevices == true));
        protected override bool SelectionReadOnly => AssignmentSource?.StartsWith("system.", StringComparison.Ordinal) == true;
    }
}

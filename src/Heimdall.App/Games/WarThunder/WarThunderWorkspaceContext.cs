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
        /// <summary>Physical input belongs to one navigation address; rendering never repairs or reassigns that address.</summary>
        internal WarThunderPhysicalInput? SelectedPhysicalInput
        {
            get => _physicalScope is { } scope && scope.Identity == State.Identity &&
                scope.Selection == (State.Selection with { InputId = null }) ? _physicalInput : null;
            set
            {
                _physicalInput = value;
                _physicalScope = value is null ? null : (State.Identity, State.Selection with { InputId = null });
            }
        }
        internal Dictionary<string, IReadOnlyList<WarThunderWorkspaceRow>> DrawingAssignments { get; } = new(StringComparer.Ordinal);
        internal string? SelectedDevice => State.Selection.ControllerId;
        /// <summary>Gets the parsed native controller records, re-read only when the native document changes.</summary>
        internal IReadOnlyList<WarThunderDevice> NativeDevices =>
            (_nativeDevices ??= new(() => Session, (document, _) => Array.AsReadOnly(WarThunderProfileBackend.ReadDevices(document.Profile).ToArray()))).Get(0, []);
        /// <summary>Gets the physical-to-native map for one linked slot and observation, rebuilt only for native document changes.</summary>
        internal WarThunderPhysicalMap PhysicalMapFor(string slot, ControllerObservation observation) =>
            (_physicalMaps ??= new(() => Session, (document, key) => WarThunderPhysicalMap.Create(document.Profile, key.Slot, key.Observation)))
                .Get((slot, observation), new([], "Open a War Thunder profile."));
        internal bool ScanningHardware => State.Activity == WorkspaceActivityKind.Scan;
        /// <summary>Gets how many times native controller records were parsed (smoke work counter).</summary>
        internal int NativeDeviceProjections => _nativeDevices?.Projections ?? 0;
        #endregion END Public
        protected override bool SelectionReadOnly => Services.Inventory is not null && State.Selection.ControllerId is { } id &&
            !GameInputReadiness.Ready(Session?.HardwarePin(id), Services.Discover());
        private ProfileContentCache<int, IReadOnlyList<WarThunderDevice>>? _nativeDevices;
        private WarThunderPhysicalInput? _physicalInput;
        private (WorkspaceIdentity Identity, WorkspaceSelection Selection)? _physicalScope;
        private ProfileContentCache<(string Slot, ControllerObservation Observation), WarThunderPhysicalMap>? _physicalMaps;
        protected override NativeWorkspaceFacts ReadNativeFacts() => ProfileFacts(ProfileCapabilities(EditionAccess.Policy.HasPremium, Session?.CanLinkCapturedDevices == true));
    }
}


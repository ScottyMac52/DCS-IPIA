using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;
using Heimdall.EliteDangerous;
using System.Windows.Threading;

namespace Heimdall.App.Games.Elite
{
    /// <summary>Provides ED session facts and typed artwork projections to the inherited workspace context.</summary>
    internal sealed class EliteWorkspaceContext(GameWorkspaceServices services, IReadOnlyList<WorkspaceHardware> hardware,
        IWorkspaceScheduler? scheduler = null, IEliteWorkspaceServices? elite = null) : GameProfileWorkspaceContextBase(scheduler ?? new DispatcherWorkspaceScheduler(Dispatcher.CurrentDispatcher)),
        IEliteWorkspaceContext<EliteDangerousProfileBackend, EliteAssignmentRow>
    {
        #region BEGIN Public
        internal GameWorkspaceServices Services => services;
        /// <inheritdoc/>
        public EliteDangerousProfileBackend Backend => Elite.Backend;
        /// <summary>Gets ED-only native services owned at the ED edge.</summary>
        internal IEliteWorkspaceServices Elite { get; } = elite ?? new EliteWorkspaceServices();
        internal IReadOnlyList<WorkspaceHardware> Hardware => hardware;
        internal GameAssignmentSelection Selection => NativeSelection;
        internal bool Refreshing { get; set; } = true;
        internal IReadOnlyList<SimCommand> Commands { get; set; } = [];
        /// <inheritdoc/>
        public IReadOnlyList<EliteAssignmentRow> Rows { get; set; } = [];
        internal GameDeviceChoice? Device => State.Selection.ControllerId is { } id
            ? (_deviceChoices ??= new(() => Session, (document, key) => document.Profile.Devices.Any(x => x.LogicalId == key)
                ? new GameDeviceChoice(key, ControllerName(document, key)) : null)).Get(id, null) : null;
        internal SimCommand? Command => Commands.FirstOrDefault(command => command.Id == State.Selection.CommandId);
        internal string Section { get; set; } = "All";
        internal int Feature { get; set; }
        internal string EditedName => State.EditedName.Trim();
        internal bool Busy => State.Operation is not null;
        internal bool Testing => State.Activity == WorkspaceActivityKind.Test;
        internal bool Identifying => State.Activity == WorkspaceActivityKind.Identify;
        internal WorkspaceAssignmentContext AssignmentContext => Context;
        internal void NotifyChanged() => RefreshNativeState();
        /// <summary>Gets the drawing for one controller of the open document. The reviewed drawing list is fixed for this
        /// context, so the projection is re-read only when the native document or controller changes.</summary>
        internal WorkspaceHardware? CurrentDrawing(string deviceId) =>
            (_drawings ??= new(() => Session, (document, key) => DrawingFor(document, key))).Get(deviceId, null);
        /// <summary>Gets how many drawing projections were computed (smoke work counter).</summary>
        internal int DrawingProjections => _drawings?.Projections ?? 0;
        internal WorkspaceHardware? DrawingFor(GameWorkspace document, string deviceId)
        {
            var system = EliteSystemInputDrawings.Create(document.Profile, deviceId);
            if (system is not null) return new("elite-system-" + system.Device, system.Device, "", system);
            return document.Hardware.TryGetValue(deviceId, out var pin) && !pin.DrawingHidden
                ? Hardware.FirstOrDefault(x => x.DeviceId == pin.HardwareId) : null;
        }

        internal string? DrawingKey(HardwareGraphicControl control)
        {
            var drawing = Device is { } device ? CurrentDrawing(device.Id) : null;
            return drawing is null ? null : EliteInputKeys.FromDiagram(drawing.Diagram, control);
        }

        private ProfileContentCache<string, GameDeviceChoice?>? _deviceChoices;
        private ProfileContentCache<string, WorkspaceHardware?>? _drawings;
        internal static string ControllerName(GameWorkspace workspace, string id)
        {
            var identity = workspace.Profile.Devices.Single(x => x.LogicalId == id).Identity!;
            var token = identity.SimSpecificIds["ed.device"];
            var suffix = identity.SimSpecificIds.TryGetValue("ed.deviceIndex", out var index) ? " · ED index " + index : "";
            return workspace.Hardware.TryGetValue(id, out var pin) ? pin.Device.Name + " · " + token + suffix : token + suffix;
        }
        #endregion END Public
        protected override NativeWorkspaceFacts ReadNativeFacts() => ProfileFacts(ProfileCapabilities(EditionAccess.Policy.HasPremium, true));
    }
}

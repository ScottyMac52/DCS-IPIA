using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.EliteDangerous;

namespace Heimdall.App.Games.Elite
{
    /// <summary>Current ED session/projection and native selection access, independent of the concrete window.</summary>
    internal sealed class EliteWorkspaceContext(
        GameWorkspaceServices services, IReadOnlyList<WorkspaceHardware> hardware, GameAssignmentSelection selection,
        Func<GameDeviceChoice?> device, Func<string> section, Func<string> editedName, Func<int> feature,
        Func<bool> busy, Func<bool> testing, Func<bool> identifying)
    {
        #region BEGIN Public
        internal GameWorkspaceServices Services => services;
        internal IReadOnlyList<WorkspaceHardware> Hardware => hardware;
        internal GameAssignmentSelection Selection => selection;
        internal GameProfileSession? Session { get; set; }
        internal string? Revision { get; set; }
        internal bool Refreshing { get; set; } = true;
        internal IReadOnlyList<SimCommand> Commands { get; set; } = [];
        internal IReadOnlyList<EliteAssignmentRow> Rows { get; set; } = [];
        internal GameDeviceChoice? Device => device();
        internal SimCommand? Command => Commands.FirstOrDefault(command => command.Id == Selection.CommandId);
        internal string Section => section();
        internal string EditedName => editedName();
        internal int Feature => feature();
        internal bool Busy => busy();
        internal bool Testing => testing();
        internal bool Identifying => identifying();
        internal GameWorkspaceChromeState Chrome => GameWorkspaceChromeState.For(Session, Revision, EditedName, Busy);
        internal WorkspaceAssignmentContext AssignmentContext => new(
            (Session?.Snapshot.Id.ToString("N") ?? "") + ":" + Device?.Id + ":" + Section + ":" + Selection.Slot,
            Session is not null, Session?.AccessMessage.Length > 0, Busy || Identifying, Testing);
        internal event EventHandler? Changed;
        internal void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
        internal WorkspaceHardware? DrawingFor(GameWorkspace document, string deviceId)
        {
            var system = EliteSystemInputDrawings.Create(document.Profile, deviceId);
            if (system is not null) return new("elite-system-" + system.Device, system.Device, "", system);
            return document.Hardware.TryGetValue(deviceId, out var pin) && !pin.DrawingHidden
                ? Hardware.FirstOrDefault(x => x.DeviceId == pin.HardwareId) : null;
        }

        internal string? DrawingKey(HardwareGraphicControl control)
        {
            var drawing = Session is not null && Device is not null ? DrawingFor(Session.Snapshot, Device.Id) : null;
            return drawing is null ? null : EliteInputKeys.FromDiagram(drawing.Diagram, control);
        }

        internal static string ControllerName(GameWorkspace workspace, string id)
        {
            var identity = workspace.Profile.Devices.Single(x => x.LogicalId == id).Identity!;
            var token = identity.SimSpecificIds["ed.device"];
            var suffix = identity.SimSpecificIds.TryGetValue("ed.deviceIndex", out var index) ? " · ED index " + index : "";
            return workspace.Hardware.TryGetValue(id, out var pin) ? pin.Device.Name + " · " + token + suffix : token + suffix;
        }
        #endregion END Public
    }
}

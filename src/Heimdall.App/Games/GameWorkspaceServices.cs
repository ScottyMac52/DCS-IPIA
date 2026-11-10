using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Application.Games;
using System.IO;
using Heimdall.Dcs;

namespace Heimdall.App.Games
{
    /// <summary>Composes the existing hardware, entitlement and native backend boundaries for the game shell.</summary>
    public sealed class GameWorkspaceServices
    {
        private readonly IConnectedDeviceDiscovery _discovery;
        /// <summary>Creates profile-scoped services without global mutable editor state.</summary>
        public GameWorkspaceServices(IGameWorkspaceStore store, IConnectedDeviceDiscovery discovery)
        { Store = store; _discovery = discovery; }
        #region BEGIN Public
        /// <summary>Gets shared device management when the application uses the inventory-backed native adapter.</summary>
        public HardwareInventoryManager? Inventory => (_discovery as InventoryDeviceDiscovery)?.Inventory;
        /// <summary>Gets the user's shared drawing preference, preserving existing game-specific pins.</summary>
        /// <param name="physicalGuid">Exact native physical identity.</param>
        /// <returns>The chosen shared drawing, or null for generic controls.</returns>
        public string? PreferredDrawing(Guid physicalGuid) => Inventory?.Current.Instances
            .FirstOrDefault(x => x.Selected && x.PhysicalGuid == physicalGuid)?.PreferredHardwareId;
        /// <summary>Uses the shared drawing only for an explicitly linked controller that has no game drawing pin.</summary>
        /// <param name="session">Game-owned staged profile.</param>
        /// <param name="logicalId">Native controller reference.</param>
        /// <param name="drawings">The game's available drawing catalog.</param>
        public void ApplyPreferredDrawing(GameProfileSession session, string? logicalId, IReadOnlyList<WorkspaceHardware> drawings)
        {
            if (logicalId is null || session.DrawingId(logicalId) is not null || session.HardwarePin(logicalId) is not { } pin) return;
            var preferred = PreferredDrawing(pin.Device.PhysicalGuid);
            if (preferred is not null && drawings.Any(x => x.DeviceId == preferred)) session.ChangeDrawing(logicalId, preferred);
        }
        /// <summary>Gets profile-owned persistence.</summary>
        public IGameWorkspaceStore Store { get; }
        /// <summary>Gets the last preference error without blocking profile access.</summary>
        public string? SelectionError { get; private set; }
        /// <summary>Reads a best-effort recent choice; malformed preferences never prevent importing a profile.</summary>
        public GameSelectionState Recent()
        {
            try { SelectionError = null; return Store.ReadSelection(); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
            { SelectionError = error.Message; return new(); }
        }
        /// <summary>Remembers an explicit choice independently of profile-save success.</summary>
        public void Remember(GameSelectionState selection)
        {
            try { Store.SaveSelection(selection); SelectionError = null; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
            { SelectionError = error.Message; }
        }
        /// <summary>Gets current native controller evidence, never inferred from a game product token.</summary>
        public IReadOnlyList<DetectedDcsDevice> Discover() => _discovery.Discover()
            .Where(device => Inventory is null || EditionAccess.Policy.AllowsDevice(device)).ToArray();
        /// <summary>Enumerates real device controls without acquisition; missing capability discovery remains unknown.</summary>
        public Task<IReadOnlyList<ControllerObservation>> DiscoverInputsAsync() => _discovery is IControllerCapabilityDiscovery inputs
            ? inputs.DiscoverAsync() : Task.FromResult<IReadOnlyList<ControllerObservation>>([]);
        /// <summary>Loads the same verified bundled drawings used by DCS.</summary>
        public static async Task<IReadOnlyList<WorkspaceHardware>> HardwareAsync()
        {
            var sources = await DcsModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root);
            return JsonSerializer.Deserialize<WorkspaceHardware[]>(sources["config/ipia-hardware.json"]) ?? [];
        }
        #endregion END Public
    }
}

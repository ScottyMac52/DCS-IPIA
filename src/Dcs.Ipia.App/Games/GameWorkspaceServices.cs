using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Application.Games;
using System.IO;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.EliteDangerous;

namespace DcsIpia.Presentation.Games
{
    /// <summary>Composes the existing hardware, entitlement and native backend boundaries for the game shell.</summary>
    public sealed class GameWorkspaceServices
    {
        private readonly IConnectedDeviceDiscovery _discovery;
        /// <summary>Creates profile-scoped services without global mutable editor state.</summary>
        public GameWorkspaceServices(IGameWorkspaceStore store, IConnectedDeviceDiscovery discovery)
        { Store = store; _discovery = discovery; }
        #region BEGIN Public
        /// <summary>Gets profile-owned persistence.</summary>
        public IGameWorkspaceStore Store { get; }
        /// <summary>Gets the implemented game adapter.</summary>
        public EliteDangerousProfileBackend Backend { get; } = new();
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
        public IReadOnlyList<DetectedDcsDevice> Discover() => _discovery.Discover();
        /// <summary>Loads the same verified bundled drawings used by DCS.</summary>
        public static async Task<IReadOnlyList<WorkspaceHardware>> HardwareAsync()
        {
            var sources = await ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root);
            return JsonSerializer.Deserialize<WorkspaceHardware[]>(sources["config/ipia-hardware.json"]) ?? [];
        }
        #endregion END Public
    }
}

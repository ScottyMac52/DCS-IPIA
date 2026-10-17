using System.IO;
using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Infrastructure;

namespace DcsIpia.Presentation
{
    /// <summary>Production edition boundary shared by commands, persistence and native input acquisition.</summary>
    internal static class EditionAccess
    {
        #region BEGIN Public
        /// <summary>Gets the active entitlement; production starts Free and verifies a file before unlocking.</summary>
        internal static EditionPolicy Policy { get; private set; } = new();
        /// <summary>Gets the private per-user license location, outside all profile backup roots.</summary>
        internal static FileOwnerLicenseStore Store { get; } = new(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VyperIndustries", "DCS-IPIA-Licensing", "owner-license.json"));
        /// <summary>Loads the pinned-key authenticated entitlement before startup preferences are applied.</summary>
        internal static void Load() => Policy = Store.Load();
        /// <summary>Permits test fixtures to use their own issuer without a production command-line bypass.</summary>
        internal static void SetForTests(EditionPolicy policy) => Policy = policy;
        /// <summary>Rejects reserved/unknown hardware in all effective module and UI Layer profiles.</summary>
        internal static void RequireWorkspace(EditorWorkspaceSnapshot workspace, IConnectedDeviceDiscovery? discovery = null)
        {
            Policy.RequireHardware(workspace.Configuration.Profiles.Where(profile => !profile.SourceName.Equals("Keyboard.diff.lua", StringComparison.OrdinalIgnoreCase))
                .Select(profile => profile.DeviceId));
            if (Policy.IsOwner || discovery is null) return;
            var physical = workspace.Configuration.Profiles.Select(profile => DcsPhysicalIdentity.FromFileName(profile.SourceName)).ToHashSet();
            if (discovery.Discover().Any(device => device.Connected && physical.Contains(device.PhysicalGuid) && !Policy.AllowsDevice(device)))
                throw new InvalidOperationException("The connected controller is not a verified Thrustmaster HID device. Premium availability TBA.");
        }
        /// <summary>Returns whether a saved workspace can be edited or emitted in this edition.</summary>
        internal static bool AllowsWorkspace(EditorWorkspaceSnapshot? workspace)
        {
            if (workspace is null) return true;
            try { RequireWorkspace(workspace); return true; }
            catch (InvalidOperationException) { return false; }
        }
        /// <summary>Gets reviewed hardware choices without generic/other-brand authoring in Free.</summary>
        internal static IEnumerable<WorkspaceHardware> Hardware(IEnumerable<WorkspaceHardware> hardware) =>
            hardware.Where(item => Policy.AllowsHardware(item.DeviceId));
        /// <summary>Checks shared profiles against reviewed hardware choices, including explicit base aliases.</summary>
        internal static void RequireSharedDiagrams(SharedUiLayerOutputSnapshot snapshot, IEnumerable<WorkspaceHardware> hardware)
        {
            if (!Policy.IsOwner) SharedUiLayerHardwareMapping.Resolve(snapshot.Defaults, Hardware(hardware));
        }
        /// <summary>Checks shared-default saves against the pinned Free catalog before persistence.</summary>
        internal static async Task RequireDefaultsAsync(SharedUiLayerWorkspace defaults)
        {
            if (Policy.IsOwner) return;
            var sources = await ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root);
            var catalog = JsonSerializer.Deserialize<WorkspaceHardware[]>(sources["config/ipia-hardware.json"]) ?? [];
            try { SharedUiLayerHardwareMapping.Resolve(defaults, Hardware(catalog)); }
            catch (InvalidDataException ex)
            { throw new InvalidOperationException("Free shared defaults must use reviewed Thrustmaster hardware only. Existing defaults are unchanged; Premium availability TBA.", ex); }
        }
        #endregion END Public
    }
}

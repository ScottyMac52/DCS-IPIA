using System.Text.Json;

namespace Dcs.Ipia.Application.Games
{
    /// <summary>Validates the generic saved envelope and isolates mutable transfer objects.</summary>
    public static class GameWorkspaceContract
    {
        #region BEGIN Public
        /// <summary>Rejects malformed persistence envelopes before they reach authoring.</summary>
        public static void Validate(GameWorkspace workspace)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            if (workspace.SchemaVersion != 1 || workspace.Id == Guid.Empty || string.IsNullOrWhiteSpace(workspace.Name)
                || workspace.Name.Length > 200 || workspace.Profile is null || workspace.Hardware is null)
                throw new InvalidDataException("Invalid game workspace identity, name or schema.");
            if (workspace.Profile.BackendId != "elite-dangerous" || workspace.Profile.Devices is null
                || workspace.Profile.Devices.Any(x => x is null))
                throw new InvalidDataException("Unsupported game workspace backend or device collection.");
            foreach (var (id, pin) in workspace.Hardware)
            {
                if (!workspace.Profile.Devices.Any(x => x.LogicalId == id) || pin?.Device is null
                    || pin.Device.PhysicalGuid == Guid.Empty || string.IsNullOrWhiteSpace(pin.HardwareId))
                    throw new InvalidDataException("Invalid game hardware association.");
            }
        }
        /// <summary>Creates an independent snapshot, including JSON-valued native extensions.</summary>
        public static GameWorkspace Clone(GameWorkspace workspace) =>
            JsonSerializer.Deserialize<GameWorkspace>(JsonSerializer.Serialize(workspace))!;
        /// <summary>Validates supported recent choices without accepting arbitrary backend metadata.</summary>
        public static void ValidateSelection(GameSelectionState selection)
        {
            ArgumentNullException.ThrowIfNull(selection);
            if (selection.BackendId is not "dcs" and not "elite-dangerous" || selection.WorkspaceId == Guid.Empty
                || selection.DcsModuleId == Guid.Empty || selection.DisplayName is null || selection.DisplayName.Length > 200)
                throw new InvalidDataException("Invalid game selection.");
        }
        #endregion END Public
    }
}

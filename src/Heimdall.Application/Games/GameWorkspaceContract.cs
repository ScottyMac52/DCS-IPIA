using System.Text.Json;

namespace Heimdall.Application.Games
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
                || workspace.Name.Length > 200 || workspace.Profile is null || workspace.Hardware is null || workspace.ControllerOrder is null || workspace.UnlinkedDrawings is null)
                throw new InvalidDataException("Invalid game workspace identity, name or schema.");
            if (workspace.Profile.BackendId is not "elite-dangerous" and not "star-wars-squadrons" and not "war-thunder" || workspace.Profile.Devices is null
                || workspace.Profile.Devices.Any(x => x is null))
                throw new InvalidDataException("Unsupported game workspace backend or device collection.");
            if (workspace.SourceDirectory is { } source && !Path.IsPathFullyQualified(source))
                throw new InvalidDataException("The source directory must be absolute.");
            if (workspace.ControllerOrder.Any(string.IsNullOrWhiteSpace)
                || workspace.ControllerOrder.Distinct(StringComparer.Ordinal).Count() != workspace.ControllerOrder.Count)
                throw new InvalidDataException("Invalid controller display order.");
            if (workspace.UnlinkedDrawings.Any(x => string.IsNullOrWhiteSpace(x.Value) || workspace.Hardware.ContainsKey(x.Key)
                || !workspace.Profile.Devices.Any(d => d.LogicalId == x.Key)))
                throw new InvalidDataException("Invalid independent drawing association.");
            foreach (var (id, pin) in workspace.Hardware)
            {
                if (!workspace.Profile.Devices.Any(x => x.LogicalId == id) || pin?.Device is null
                    || pin.Device.PhysicalGuid == Guid.Empty || (pin.HardwareId is not null && string.IsNullOrWhiteSpace(pin.HardwareId)))
                    throw new InvalidDataException("Invalid game hardware association.");
            }
            if (workspace.Profile.BackendId == "star-wars-squadrons" && (workspace.Hardware.Values.Any(x => x.Device.ProductGuid == Guid.Empty)
                || workspace.Hardware.Values.Select(x => x.Device.PhysicalGuid).Distinct().Count() != workspace.Hardware.Count))
                throw new InvalidDataException("Squadrons links require a native product identity and a different physical instance for each captured slot.");
            if (workspace.Profile.BackendId == "war-thunder" && (workspace.Hardware.Values.Any(x => x.Device.ProductGuid == Guid.Empty)
                || workspace.Hardware.Values.Select(x => x.Device.PhysicalGuid).Distinct().Count() != workspace.Hardware.Count))
                throw new InvalidDataException("War Thunder links require native product identity and distinct physical instances.");
        }
        /// <summary>Creates an independent snapshot, including JSON-valued native extensions.</summary>
        public static GameWorkspace Clone(GameWorkspace workspace) =>
            JsonSerializer.Deserialize<GameWorkspace>(JsonSerializer.Serialize(workspace))!;
        /// <summary>Validates supported recent choices without accepting arbitrary backend metadata.</summary>
        public static void ValidateSelection(GameSelectionState selection)
        {
            ArgumentNullException.ThrowIfNull(selection);
            if (selection.EliteBindingsDirectory is { } directory && (string.IsNullOrWhiteSpace(directory)
                || directory.Length > 32767 || !Path.IsPathFullyQualified(directory)))
                throw new InvalidDataException("An ED bindings override must be an absolute directory.");
            if (selection.BackendId is not "dcs" and not "elite-dangerous" and not "star-wars-squadrons" and not "war-thunder" || selection.WorkspaceId == Guid.Empty
                || selection.DcsModuleId == Guid.Empty || selection.DisplayName is null || selection.DisplayName.Length > 200)
                throw new InvalidDataException("Invalid game selection.");
        }
        #endregion END Public
    }
}

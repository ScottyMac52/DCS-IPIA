using Heimdall.Application;

namespace Heimdall.Dcs
{
    /// <summary>Discovers disconnected controller identities from existing DCS joystick filenames.</summary>
    public static class SavedGamesDeviceDiscovery
    {
        #region BEGIN Public
        /// <summary>Reads profiles below a Saved Games root or a selected DCS installation folder.</summary>
        /// <param name="root">Saved Games or DCS profile root.</param>
        /// <returns>Captured GUID-qualified joystick profiles.</returns>
        public static IReadOnlyList<DetectedDcsDevice> Discover(string root)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(root);
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Saved Games folder not found: " + root);
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.ReparsePoint };
            var devices = new List<DetectedDcsDevice>();
            var directInput = Path.Combine(root, "Config", "Input");
            var inputs = Directory.Exists(directInput) ? new[] { directInput }
                : Directory.EnumerateDirectories(root, "DCS*", SearchOption.TopDirectoryOnly)
                    .Select(x => Path.Combine(x, "Config", "Input")).Where(Directory.Exists).ToArray();
            foreach (var file in inputs.SelectMany(input => Directory.EnumerateFiles(input, "*", options)))
            {
                if (!string.Equals(Path.GetFileName(Path.GetDirectoryName(file)), "joystick", StringComparison.OrdinalIgnoreCase)) continue;
                var name = Path.GetFileName(file);
                if (DcsPhysicalIdentity.FromFileName(name) is { } guid)
                    devices.Add(new(guid, DcsPhysicalIdentity.NameFromFileName(name), false, name));
            }
            return DcsPhysicalIdentity.Merge([], devices);
        }
        #endregion END Public
    }
}

using System.Text.RegularExpressions;

namespace Dcs.Ipia.Infrastructure
{
    /// <summary>Resolves one production or named test profile without accepting arbitrary data paths.</summary>
    public sealed class UserDataProfile
    {
        private readonly string _localData;
        private UserDataProfile(string localData, string? name)
        {
            _localData = Path.TrimEndingDirectorySeparator(Path.GetFullPath(localData));
            if (Path.GetDirectoryName(_localData) is null)
                throw new ArgumentException("A filesystem root cannot contain application profiles.", nameof(localData));
            Name = name ?? "Production";
            IsTest = name is not null;
            DataDirectory = name is null ? Path.Combine(_localData, "DCS-IPIA")
                : Path.Combine(_localData, "DCS-IPIA-TestProfiles", name);
            Validate();
        }
        #region BEGIN Public
        /// <summary>Gets the visible profile name.</summary>
        public string Name { get; }
        /// <summary>Gets whether deployment must be disabled.</summary>
        public bool IsTest { get; }
        /// <summary>Gets the exclusive profile data root.</summary>
        public string DataDirectory { get; }
        /// <summary>Gets a profile-owned temporary directory.</summary>
        public string TemporaryDirectory => Path.Combine(DataDirectory, "Temp");
        /// <summary>Selects production or a portable, case-insensitive named test profile.</summary>
        /// <param name="localData">Operating-system local application data directory.</param>
        /// <param name="name">Null for production; otherwise 1–40 ASCII letters, digits, underscores or hyphens.</param>
        /// <returns>Validated profile.</returns>
        public static UserDataProfile Select(string localData, string? name = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(localData);
            if (name is not null && (!Regex.IsMatch(name, "^[A-Za-z0-9][A-Za-z0-9_-]{0,39}$", RegexOptions.CultureInvariant)
                || Regex.IsMatch(name, "^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)))
                throw new ArgumentException("Use 1–40 letters, digits, underscores or hyphens; device names are reserved.", nameof(name));
            return new UserDataProfile(localData, name?.ToLowerInvariant());
        }
        /// <summary>Rejects symlinks/junctions before any profile operation.</summary>
        public void Validate()
        {
            for (string? path = DataDirectory; path is not null; path = Path.GetDirectoryName(path))
                if (new FileInfo(path).LinkTarget is not null || ((Directory.Exists(path) || File.Exists(path)) &&
                    (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0))
                    throw new IOException("Profile paths cannot contain symbolic links or junctions.");
        }
        /// <summary>Rejects reset archives inside any managed production or test profile.</summary>
        /// <param name="archive">User-selected reset backup destination.</param>
        public void ValidateBackupDestination(string archive)
        {
            var destination = Path.GetFullPath(archive);
            foreach (var name in new[] { "DCS-IPIA", "DCS-IPIA-TestProfiles" })
            {
                var root = Path.Combine(_localData, name);
                if (destination.Equals(root, StringComparison.OrdinalIgnoreCase) ||
                    destination.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Save reset backups outside all IPIA profile folders.", nameof(archive));
            }
        }
        #endregion END Public
    }
}
